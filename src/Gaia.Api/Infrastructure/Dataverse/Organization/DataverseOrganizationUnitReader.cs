using System.Text.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationUnitReader(
    IDataverseDelegatedClientFactory clientFactory) : IOrganizationUnitReader
{
    private const string OrganizationTable = "gaia_organizacion";
    private const string UnitTypeTable = "gaia_tipounidadorganizacional";
    private const string SiteTable = "gaia_sede";
    private static readonly DateOnly LegacyEffectiveFrom = new(2021, 1, 1);

    public async Task<IReadOnlyList<UnitResponse>> ListAsync(
        UnitFilters filters,
        CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync();
        var relationships = await ReadRelationshipsAsync(client, cancellationToken);
        var parentLookup = FindLookup(relationships, OrganizationTable, "gaia_unidadpadre");
        var unitTypeLookup = FindLookup(relationships, UnitTypeTable);
        var siteLookup = FindLookup(relationships, SiteTable);

        var unitTypes = await ReadUnitTypesAsync(client, cancellationToken);
        var sites = await ReadSitesAsync(client, cancellationToken);
        var filter = BuildFilter(filters, unitTypeLookup);
        var lookupColumns = $"_{parentLookup}_value,_{unitTypeLookup}_value,_{siteLookup}_value";
        var path = "gaia_organizacions" +
            "?$select=gaia_organizacionid,gaia_codigo,gaia_nombre,gaia_nombrecorto,gaia_descripcion,gaia_ordenvisual," +
            "gaia_nivel,gaia_fechainiciovigencia,gaia_fechafinvigencia,statecode," + lookupColumns +
            "&$orderby=gaia_nivel asc,gaia_nombre asc" + filter;

        var records = await DataverseJson.ReadAllAsync(client, path, cancellationToken);
        return records.Select(item => MapUnit(
            item, parentLookup, unitTypeLookup, siteLookup, unitTypes, sites)).ToArray();
    }

    private static async Task<Dictionary<Guid, string>> ReadSitesAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        const string path = "gaia_sedes?$select=gaia_sedeid,gaia_name";
        var records = await DataverseJson.ReadAllAsync(client, path, cancellationToken);
        return records.ToDictionary(
            item => RequiredGuid(item, "gaia_sedeid"),
            item => RequiredString(item, "gaia_name"));
    }

    private static async Task<Relationship[]> ReadRelationshipsAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        var path = $"EntityDefinitions(LogicalName='{OrganizationTable}')" +
            "/ManyToOneRelationships?$select=ReferencingAttribute,ReferencedEntity";
        var records = await DataverseJson.ReadAllAsync(client, path, cancellationToken);
        return records.Select(item => new Relationship(
            RequiredString(item, "ReferencingAttribute"),
            RequiredString(item, "ReferencedEntity"))).ToArray();
    }

    private static string FindLookup(
        IEnumerable<Relationship> relationships,
        string referencedEntity,
        string? requiredAttribute = null)
    {
        var matches = relationships.Where(item =>
            item.ReferencedEntity.Equals(referencedEntity, StringComparison.OrdinalIgnoreCase)
            && (requiredAttribute is null
                || item.ReferencingAttribute.Equals(requiredAttribute, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return matches.Length == 1
            ? matches[0].ReferencingAttribute
            : throw new InvalidOperationException(
                $"No se pudo resolver de forma unívoca el lookup hacia {referencedEntity}.");
    }

    private static async Task<Dictionary<Guid, UnitTypeData>> ReadUnitTypesAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        const string path = "gaia_tipounidadorganizacionals" +
            "?$select=gaia_tipounidadorganizacionalid,gaia_codigo,gaia_nombre," +
            "gaia_colortoken,gaia_ordenvisual";
        var records = await DataverseJson.ReadAllAsync(client, path, cancellationToken);
        return records.ToDictionary(
            item => RequiredGuid(item, "gaia_tipounidadorganizacionalid"),
            item =>
            {
                var code = RequiredString(item, "gaia_codigo");
                var presentation = OrganizationUnitTypePresentation.Get(code);
                return new UnitTypeData(
                    RequiredString(item, "gaia_nombre"),
                    OptionalString(item, "gaia_colortoken") ?? presentation.ColorToken,
                    DataverseJson.OptionalInt32(item, "gaia_ordenvisual") ?? presentation.VisualOrder);
            });
    }

    private static string BuildFilter(UnitFilters filters, string unitTypeLookup)
    {
        var clauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var search = filters.Search.Trim().Replace("'", "''", StringComparison.Ordinal);
            clauses.Add($"(contains(gaia_codigo,'{search}') or contains(gaia_nombre,'{search}'))");
        }
        if (filters.IsActive.HasValue)
        {
            clauses.Add($"statecode eq {(filters.IsActive.Value ? 0 : 1)}");
        }
        if (filters.UnitTypeId.HasValue)
        {
            clauses.Add($"_{unitTypeLookup}_value eq {filters.UnitTypeId.Value:D}");
        }
        return clauses.Count == 0 ? string.Empty : $"&$filter={string.Join(" and ", clauses)}";
    }

    private static UnitResponse MapUnit(
        JsonElement item,
        string parentLookup,
        string unitTypeLookup,
        string siteLookup,
        IReadOnlyDictionary<Guid, UnitTypeData> unitTypes,
        Dictionary<Guid, string> sites)
    {
        var typeId = RequiredGuid(item, $"_{unitTypeLookup}_value");
        if (!unitTypes.TryGetValue(typeId, out var type))
        {
            throw new InvalidOperationException($"La unidad referencia un tipo inexistente: {typeId}.");
        }

        var name = RequiredString(item, "gaia_nombre");
        var siteId = OptionalGuid(item, $"_{siteLookup}_value");
        return new UnitResponse(
            RequiredGuid(item, "gaia_organizacionid"),
            RequiredString(item, "gaia_codigo"),
            OptionalString(item, "gaia_nombrecorto") ?? name,
            name,
            typeId,
            type.Name,
            type.ColorToken,
            OptionalGuid(item, $"_{parentLookup}_value"),
            siteId,
            siteId.HasValue && sites.TryGetValue(siteId.Value, out var siteName) ? siteName : null,
            RequiredInt32(item, "gaia_nivel"),
            OptionalString(item, "gaia_descripcion"),
            DataverseJson.OptionalInt32(item, "gaia_ordenvisual") ?? type.VisualOrder,
            OptionalDate(item, "gaia_fechainiciovigencia") ?? LegacyEffectiveFrom,
            OptionalDate(item, "gaia_fechafinvigencia"),
            RequiredInt32(item, "statecode") == 0);
    }

    private static string RequiredString(JsonElement item, string property) =>
        OptionalString(item, property)
        ?? throw new InvalidOperationException($"Dataverse no devolvió el campo obligatorio {property}.");

    private static string? OptionalString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static Guid RequiredGuid(JsonElement item, string property) =>
        OptionalGuid(item, property)
        ?? throw new InvalidOperationException($"Dataverse no devolvió el GUID obligatorio {property}.");

    private static Guid? OptionalGuid(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetGuid()
            : null;

    private static int RequiredInt32(JsonElement item, string property) =>
        DataverseJson.OptionalInt32(item, property)
            is { } number ? number
            : throw new InvalidOperationException($"Dataverse no devolvió el entero obligatorio {property}.");

    private static DateOnly? OptionalDate(JsonElement item, string property) =>
        OptionalString(item, property) is { } value && DateOnly.TryParse(value, out var date)
            ? date
            : null;

    private sealed record Relationship(string ReferencingAttribute, string ReferencedEntity);
    private sealed record UnitTypeData(string Name, string ColorToken, int VisualOrder);
}
