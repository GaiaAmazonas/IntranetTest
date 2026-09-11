using System.Text.Json;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

internal sealed record SharePointRepositorySettings(Guid Id, string SiteId, string DriveId, string RootFolder,
    string TenantId, string ClientId, int AuthenticationMethod, string? CredentialReference,
    string LibraryName, string SiteUrl, string TransferHosts, int OperationalState);

internal interface ISharePointRepositoryReader
{
    Task<SharePointRepositorySettings> ReadAsync(ExternalFileId? file, CancellationToken token);
}

internal sealed class DataverseSharePointConfigurationReader(IDataverseDelegatedClientFactory clients,
    IConfiguration configuration, IHostEnvironment environment,
    ILogger<DataverseSharePointConfigurationReader>? logger = null) : ISharePointRepositoryReader
{
    internal const string Table = "gaia_configuracionsharepoint";
    private static readonly string[] Fields = ["gaia_siteid", "gaia_driveid", "gaia_carpetaraiz", "gaia_tenantid",
        "gaia_clientid", "gaia_metodoautenticacio", "gaia_referenciacredencial", "gaia_nombrebiblioteca",
        "gaia_urlsitio", "gaia_hoststransferencia", "gaia_estadooperativo"];
    private static readonly string[] DiagnosticFields = ["gaia_entorno", "gaia_proveedor", "gaia_predeterminado"];
    private static readonly Action<ILogger, Guid, string, string, string, string, Exception?> LogCandidate =
        LoggerMessage.Define<Guid, string, string, string, string>(LogLevel.Warning, new EventId(4301, "SharePointConfigurationCandidate"),
            "SharePoint configuration candidate {Id}: state={State}, environment={Environment}, provider={Provider}, default={Default}");

    internal static int EnvironmentValue(string name) => name switch
    {
        "Development" => 1, "Staging" or "Testing" => 2, "Production" => 3,
        _ => throw new FileStorageException(FileStorageError.MissingConfiguration)
    };

    public async Task<SharePointRepositorySettings> ReadAsync(ExternalFileId? file, CancellationToken token)
    {
        var targetEnvironment = configuration.GetValue<int?>("FileStorage:SharePoint:RepositoryEnvironment")
            ?? EnvironmentValue(environment.EnvironmentName);
        if (targetEnvironment is < 1 or > 3)
            throw new FileStorageException(FileStorageError.MissingConfiguration);
        if (file is not null && file.Provider != "SharePoint") throw new FileStorageException(FileStorageError.FileUnauthorized);
        using var client = await clients.CreateAsync();
        // Table logical name inferred from the supplied primary-ID screenshot; metadata confirms it at runtime.
        var table = configuration["FileStorage:SharePoint:ConfigurationTable"] ?? Table;
        if (!table.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            throw new FileStorageException(FileStorageError.MissingConfiguration);
        var metadata = await DataverseMetadataResolver.TableAsync(client, table, token);
        var columns = Fields.ToDictionary(field => field, metadata.Attribute, StringComparer.Ordinal);
        var filter = $"statecode eq 0 and {metadata.Attribute("gaia_entorno")} eq {targetEnvironment} and {metadata.Attribute("gaia_proveedor")} eq 1";
        filter += file is null ? $" and {metadata.Attribute("gaia_predeterminado")} eq true"
            : $" and {columns["gaia_siteid"]} eq '{Escape(file.RepositoryId)}' and {columns["gaia_driveid"]} eq '{Escape(file.ContainerId)}'";
        var path = $"{metadata.EntitySetName}?$select={metadata.PrimaryIdAttribute},{string.Join(',', columns.Values)}&$filter={Uri.EscapeDataString(filter)}&$top=2";
        var rows = new List<JsonElement>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? next = path;
        while (next is not null && rows.Count < 2)
        {
            if (!seen.Add(next)) throw new FileStorageException(FileStorageError.MissingConfiguration);
            // Never send the delegated Dataverse token outside the configured API, even on nextLink.
            if (Uri.TryCreate(next, UriKind.Absolute, out var uri)
                && (client.BaseAddress is null || !client.BaseAddress.IsBaseOf(uri)))
                throw new FileStorageException(FileStorageError.FileUnauthorized);
            using var response = await client.GetAsync(next, token);
            if (!response.IsSuccessStatusCode) throw new FileStorageException(FileStorageError.MissingConfiguration);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            rows.AddRange(document.RootElement.GetProperty("value").EnumerateArray().Take(2 - rows.Count).Select(row => row.Clone()));
            next = document.RootElement.TryGetProperty("@odata.nextLink", out var link) ? link.GetString() : null;
        }
        if (rows.Count != 1)
        {
            var diagnosticFields = DiagnosticFields
                .ToDictionary(field => field, metadata.Attribute, StringComparer.Ordinal);
            var diagnosticPath = $"{metadata.EntitySetName}?$select={metadata.PrimaryIdAttribute},statecode,{string.Join(',', diagnosticFields.Values)}&$top=10";
            using var diagnosticResponse = await client.GetAsync(diagnosticPath, token);
            if (diagnosticResponse.IsSuccessStatusCode)
            {
                using var diagnosticDocument = JsonDocument.Parse(await diagnosticResponse.Content.ReadAsStringAsync(token));
                foreach (var candidate in diagnosticDocument.RootElement.GetProperty("value").EnumerateArray())
                {
                    LogCandidate(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DataverseSharePointConfigurationReader>.Instance,
                        candidate.GetProperty(metadata.PrimaryIdAttribute).GetGuid(),
                        candidate.TryGetProperty("statecode", out var state) ? state.GetRawText() : "missing",
                        candidate.TryGetProperty(diagnosticFields["gaia_entorno"], out var repositoryEnvironment) ? repositoryEnvironment.GetRawText() : "missing",
                        candidate.TryGetProperty(diagnosticFields["gaia_proveedor"], out var provider) ? provider.GetRawText() : "missing",
                        candidate.TryGetProperty(diagnosticFields["gaia_predeterminado"], out var isDefault) ? isDefault.GetRawText() : "missing", null);
                }
            }
            throw new FileStorageException(FileStorageError.MissingConfiguration);
        }
        return Map(rows[0], metadata.PrimaryIdAttribute, columns);
    }

    internal static SharePointRepositorySettings Map(JsonElement row, string primaryId, IReadOnlyDictionary<string, string> columns)
    {
        string Text(string field) => row.TryGetProperty(columns[field], out var value) && value.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new FileStorageException(FileStorageError.MissingConfiguration);
        int Number(string field) => row.TryGetProperty(columns[field], out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number : throw new FileStorageException(FileStorageError.MissingConfiguration);
        var method = Number("gaia_metodoautenticacio");
        // The screenshot's option 5, 'solo desarrollo', is not an authentication method.
        if (method is < 1 or > 4) throw new FileStorageException(FileStorageError.MissingConfiguration);
        var state = Number("gaia_estadooperativo");
        if (state is < 1 or > 5) throw new FileStorageException(FileStorageError.MissingConfiguration);
        var alias = row.TryGetProperty(columns["gaia_referenciacredencial"], out var reference) && reference.ValueKind == JsonValueKind.String
            ? reference.GetString() : null;
        return new(row.GetProperty(primaryId).GetGuid(), Text("gaia_siteid"), Text("gaia_driveid"), Text("gaia_carpetaraiz"),
            Text("gaia_tenantid"), Text("gaia_clientid"), method, alias, Text("gaia_nombrebiblioteca"),
            Text("gaia_urlsitio"), Text("gaia_hoststransferencia"), state);
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
