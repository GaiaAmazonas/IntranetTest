using System.Globalization;
using System.Text.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationUnitTypeReader(
    IDataverseDelegatedClientFactory clientFactory) : IOrganizationUnitTypeReader
{
    internal const string SelectColumns =
        "gaia_tipounidadorganizacionalid,gaia_codigo,gaia_nombre," +
        "gaia_descripcion,gaia_colortoken,gaia_ordenvisual,gaia_activo,statecode," +
        "createdon,modifiedon,_createdby_value,_modifiedby_value";

    public async Task<IReadOnlyList<UnitTypeResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync();
        var path = $"gaia_tipounidadorganizacionals?$select={SelectColumns}";
        var records = await DataverseJson.ReadAllAsync(client, path, cancellationToken);
        return records.Select(Map).OrderBy(item => item.VisualOrder).ThenBy(item => item.Name).ToArray();
    }

    internal static UnitTypeResponse Map(JsonElement item)
    {
        var code = RequiredString(item, "gaia_codigo");
        var presentation = OrganizationUnitTypePresentation.Get(code);
        return new UnitTypeResponse(
            RequiredGuid(item, "gaia_tipounidadorganizacionalid"),
            RequiredDateTimeOffset(item, "createdon"),
            OptionalString(item, "_createdby_value@OData.Community.Display.V1.FormattedValue") ?? "Dataverse",
            OptionalDateTimeOffset(item, "modifiedon"),
            OptionalString(item, "_modifiedby_value@OData.Community.Display.V1.FormattedValue"),
            code,
            RequiredString(item, "gaia_nombre"),
            OptionalString(item, "gaia_descripcion"),
            OptionalString(item, "gaia_colortoken") ?? presentation.ColorToken,
            DataverseJson.OptionalInt32(item, "gaia_ordenvisual") ?? presentation.VisualOrder,
            OptionalBoolean(item, "gaia_activo") ?? RequiredInt32(item, "statecode") == 0);
    }

    private static string RequiredString(JsonElement item, string property) =>
        OptionalString(item, property)
        ?? throw new InvalidOperationException($"Dataverse no devolvió el campo obligatorio {property}.");

    private static string? OptionalString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static Guid RequiredGuid(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.TryGetGuid(out var id)
            ? id
            : throw new InvalidOperationException($"Dataverse no devolvió el GUID obligatorio {property}.");

    private static int RequiredInt32(JsonElement item, string property) =>
        DataverseJson.OptionalInt32(item, property)
            is { } number ? number
            : throw new InvalidOperationException($"Dataverse no devolvió el entero obligatorio {property}.");

    private static bool? OptionalBoolean(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static DateTimeOffset RequiredDateTimeOffset(JsonElement item, string property) =>
        OptionalDateTimeOffset(item, property)
        ?? throw new InvalidOperationException($"Dataverse no devolvió la fecha obligatoria {property}.");

    private static DateTimeOffset? OptionalDateTimeOffset(JsonElement item, string property) =>
        OptionalString(item, property) is { } value
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;
}
