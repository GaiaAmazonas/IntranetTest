using System.Globalization;
using System.Text.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationSiteReader(
    IDataverseDelegatedClientFactory clientFactory) : IOrganizationSiteReader
{
    internal const string EntitySet = "gaia_sedes";
    internal const string PrimaryId = "gaia_sedeid";
    internal const string SelectColumns =
        "gaia_sedeid,gaia_codigo,gaia_name,gaia_ciudad,gaia_direccion,gaia_activo,statecode," +
        "createdon,modifiedon,_createdby_value,_modifiedby_value";

    public async Task<IReadOnlyList<SiteResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync();
        var records = await DataverseJson.ReadAllAsync(
            client, $"{EntitySet}?$select={SelectColumns}&$orderby=gaia_name asc", cancellationToken);
        return records.Select(Map).ToArray();
    }

    internal static SiteResponse Map(JsonElement item) => new(
        RequiredGuid(item, PrimaryId),
        RequiredDate(item, "createdon"),
        OptionalString(item, "_createdby_value@OData.Community.Display.V1.FormattedValue") ?? "Dataverse",
        OptionalDate(item, "modifiedon"),
        OptionalString(item, "_modifiedby_value@OData.Community.Display.V1.FormattedValue"),
        RequiredString(item, "gaia_codigo"),
        RequiredString(item, "gaia_name"),
        OptionalString(item, "gaia_ciudad"),
        OptionalString(item, "gaia_direccion"),
        OptionalBoolean(item, "gaia_activo") ?? (DataverseJson.OptionalInt32(item, "statecode") ?? 0) == 0);

    internal static async Task<SiteResponse?> ReadAsync(
        HttpClient client, Guid id, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"{EntitySet}({id:D})?$select={SelectColumns}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Dataverse rechazó la lectura de la sede ({(int)response.StatusCode}): {content}");
        using var document = JsonDocument.Parse(content);
        return Map(document.RootElement);
    }

    private static string RequiredString(JsonElement item, string property) =>
        OptionalString(item, property)
        ?? throw new InvalidOperationException($"Dataverse no devolvió el campo obligatorio {property}.");

    private static string? OptionalString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() : null;

    private static Guid RequiredGuid(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.TryGetGuid(out var id)
            ? id : throw new InvalidOperationException($"Dataverse no devolvió el GUID obligatorio {property}.");

    private static bool? OptionalBoolean(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : null;

    private static DateTimeOffset RequiredDate(JsonElement item, string property) =>
        OptionalDate(item, property)
        ?? throw new InvalidOperationException($"Dataverse no devolvió la fecha obligatoria {property}.");

    private static DateTimeOffset? OptionalDate(JsonElement item, string property) =>
        OptionalString(item, property) is { } value
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date : null;
}
