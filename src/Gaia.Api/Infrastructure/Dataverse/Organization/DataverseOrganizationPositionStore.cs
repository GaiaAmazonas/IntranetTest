using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationPositionStore(IDataverseDelegatedClientFactory clientFactory)
    : IOrganizationPositionStore
{
    private const string Table = "gaia_cargo";

    public async Task<IReadOnlyList<PositionResponse>> ListAsync(CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token);
        var fields = Fields.From(metadata);
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{metadata.EntitySetName}?$select={fields.Select}&$orderby={fields.Name} asc", token);
        return rows.Select(item => Map(item, fields)).ToArray();
    }

    public Task<PositionWriteResult> CreateAsync(PositionWriteCommand command, CancellationToken token) => WriteAsync(null, command, token);
    public Task<PositionWriteResult> UpdateAsync(Guid id, PositionWriteCommand command, CancellationToken token) => WriteAsync(id, command, token);

    private async Task<PositionWriteResult> WriteAsync(Guid? id, PositionWriteCommand command, CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token);
        var fields = Fields.From(metadata);
        if (id.HasValue && await DataverseMetadataResolver.ReadOneAsync(client,
            $"{metadata.EntitySetName}({id:D})?$select={metadata.PrimaryIdAttribute}", token) is null)
            return new(PositionWriteStatus.NotFound);
        if (command.Code is not null)
        {
            var own = id.HasValue ? $" and {metadata.PrimaryIdAttribute} ne {id:D}" : "";
            var duplicates = await DataverseJson.ReadAllAsync(client,
                $"{metadata.EntitySetName}?$select={metadata.PrimaryIdAttribute}&$filter={fields.Code} eq '{Escape(command.Code)}'{own}&$top=1", token);
            if (duplicates.Count > 0) return new(PositionWriteStatus.DuplicateCode);
        }
        var payload = new Dictionary<string, object?> {
            [fields.Code] = command.Code, [fields.Name] = command.Name,
            [fields.Description] = command.Description, ["statecode"] = command.IsActive ? 0 : 1 };
        if (!id.HasValue)
        {
            using var response = await client.PostAsJsonAsync(metadata.EntitySetName, payload, token);
            await EnsureAsync(response, token);
            var createdId = CreatedId(response);
            return new(PositionWriteStatus.Created, await ReadAsync(client, metadata, fields, createdId, token));
        }
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{metadata.EntitySetName}({id:D})") { Content = JsonContent.Create(payload) };
        request.Headers.TryAddWithoutValidation("If-Match", "*");
        using var updated = await client.SendAsync(request, token);
        await EnsureAsync(updated, token);
        return new(PositionWriteStatus.Updated, await ReadAsync(client, metadata, fields, id.Value, token));
    }

    private static async Task<PositionResponse> ReadAsync(HttpClient client, DataverseTableMetadata metadata, Fields fields, Guid id, CancellationToken token)
    {
        var item = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{metadata.EntitySetName}({id:D})?$select={fields.Select}", token)
            ?? throw new InvalidOperationException("Dataverse no devolvió el cargo.");
        return Map(item, fields);
    }

    private static PositionResponse Map(JsonElement item, Fields fields) => new(
        Guid.Parse(String(item, fields.Id)!), Date(item, "createdon") ?? DateTimeOffset.MinValue,
        String(item, "_createdby_value@OData.Community.Display.V1.FormattedValue") ?? "Dataverse",
        Date(item, "modifiedon"), String(item, "_modifiedby_value@OData.Community.Display.V1.FormattedValue"),
        String(item, fields.Code), String(item, fields.Name) ?? "", String(item, fields.Description),
        (DataverseJson.OptionalInt32(item, "statecode") ?? 0) == 0);
    private static string? String(JsonElement item, string field) => item.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static DateTimeOffset? Date(JsonElement item, string field) => DateTimeOffset.TryParse(String(item, field), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;
    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static Guid CreatedId(HttpResponseMessage response) { var uri=response.Headers.TryGetValues("OData-EntityId",out var values)?values.SingleOrDefault():null; var match=System.Text.RegularExpressions.Regex.Match(uri??"",@"\(([0-9a-f-]{36})\)$"); return match.Success?Guid.Parse(match.Groups[1].Value):throw new InvalidOperationException("Dataverse no devolvió el GUID del cargo."); }
    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken token) { if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Dataverse rechazó el cargo ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(token)}"); }
    private sealed record Fields(string Id,string Code,string Name,string Description)
    {
        public string Select => string.Join(',',Id,Code,Name,Description,"statecode","createdon","modifiedon","_createdby_value","_modifiedby_value");
        public static Fields From(DataverseTableMetadata m) => new(m.PrimaryIdAttribute,m.Attribute("gaia_Codigo"),m.Attribute("gaia_Nombre"),m.Attribute("gaia_Descripcion"));
    }
}
