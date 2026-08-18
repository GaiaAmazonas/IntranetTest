using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationUnitCreator(
    IDataverseDelegatedClientFactory clientFactory) : IOrganizationUnitCreator
{
    private const string OrganizationTable = "gaia_organizacion";
    private const string OrganizationSet = "gaia_organizacions";
    private const string UnitTypeTable = "gaia_tipounidadorganizacional";
    private const string UnitTypeSet = "gaia_tipounidadorganizacionals";
    private const string SiteTable = "gaia_sede";

    public async Task<OrganizationUnitCreateResult> CreateAsync(
        OrganizationUnitCreateCommand command,
        CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync();
        var escapedCode = command.Code.Replace("'", "''", StringComparison.Ordinal);
        if (await ExistsAsync(client,
            $"{OrganizationSet}?$select=gaia_organizacionid&$filter=gaia_codigo eq '{escapedCode}'&$top=1",
            cancellationToken))
        {
            return new(OrganizationUnitCreateStatus.DuplicateCode);
        }

        var unitType = await ReadOneAsync(client,
            $"{UnitTypeSet}({command.UnitTypeId:D})?$select=gaia_activo,statecode",
            cancellationToken);
        if (unitType is null || !IsActiveUnitType(unitType.Value))
        {
            return new(OrganizationUnitCreateStatus.InvalidUnitType);
        }

        var relationships = await ReadRelationshipsAsync(client, cancellationToken);
        var unitTypeNavigation = FindNavigationProperty(relationships, UnitTypeTable);
        var parentNavigation = FindNavigationProperty(
            relationships, OrganizationTable, "gaia_unidadpadre");

        var level = 1;
        if (command.ParentId.HasValue)
        {
            var parent = await ReadOneAsync(client,
                $"{OrganizationSet}({command.ParentId.Value:D})?$select=gaia_nivel",
                cancellationToken);
            if (parent is null)
            {
                return new(OrganizationUnitCreateStatus.ParentNotFound);
            }
            level = (DataverseJson.OptionalInt32(parent.Value, "gaia_nivel") ?? 0) + 1;
        }

        if (!command.SiteId.HasValue)
            return new(OrganizationUnitCreateStatus.SiteNotFound);

        var siteNavigation = FindNavigationProperty(relationships, SiteTable, "gaia_sede");
        var siteSet = await ReadEntitySetAsync(client, SiteTable, cancellationToken);
        var site = await ReadOneAsync(client,
            $"{siteSet}({command.SiteId.Value:D})?$select=gaia_codigo,statecode",
            cancellationToken);
        if (site is null
            || !site.Value.TryGetProperty("gaia_codigo", out var siteCode)
            || !string.Equals(siteCode.GetString(), "BOG", StringComparison.OrdinalIgnoreCase)
            || (DataverseJson.OptionalInt32(site.Value, "statecode") ?? 0) != 0)
        {
            return new(OrganizationUnitCreateStatus.SiteNotFound);
        }

        var payload = new Dictionary<string, object?>
        {
            ["gaia_codigo"] = command.Code,
            ["gaia_nombre"] = command.Name,
            ["gaia_nombrecorto"] = command.ShortName,
            ["gaia_descripcion"] = command.Description,
            ["gaia_ordenvisual"] = command.VisualOrder,
            ["gaia_nivel"] = level,
            ["gaia_fechainiciovigencia"] = command.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["gaia_fechafinvigencia"] = command.EffectiveTo?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["statecode"] = command.IsActive ? 0 : 1,
            [$"{unitTypeNavigation}@odata.bind"] = $"/{UnitTypeSet}({command.UnitTypeId:D})"
        };
        if (command.ParentId.HasValue)
        {
            payload[$"{parentNavigation}@odata.bind"] = $"/{OrganizationSet}({command.ParentId.Value:D})";
        }
        payload[$"{siteNavigation}@odata.bind"] = $"/{siteSet}({command.SiteId.Value:D})";

        using var response = await client.PostAsJsonAsync(OrganizationSet, payload, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Dataverse rechazó la creación de la unidad ({(int)response.StatusCode}): {content}");
        }
        var id = ReadCreatedId(response)
            ?? throw new InvalidOperationException("Dataverse no devolvió OData-EntityId al crear la unidad.");
        return new(OrganizationUnitCreateStatus.Created, id);
    }

    private static bool IsActiveUnitType(JsonElement item) =>
        (!item.TryGetProperty("gaia_activo", out var active) || active.ValueKind == JsonValueKind.Null || active.GetBoolean())
        && (DataverseJson.OptionalInt32(item, "statecode") ?? 0) == 0;

    private static async Task<bool> ExistsAsync(HttpClient client, string path, CancellationToken token) =>
        (await DataverseJson.ReadAllAsync(client, path, token)).Count != 0;

    private static async Task<JsonElement?> ReadOneAsync(HttpClient client, string path, CancellationToken token)
    {
        using var response = await client.GetAsync(path, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        var content = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Dataverse rechazó la validación ({(int)response.StatusCode}): {content}");
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private static async Task<string> ReadEntitySetAsync(HttpClient client, string logicalName, CancellationToken token)
    {
        var record = await ReadOneAsync(client,
            $"EntityDefinitions(LogicalName='{logicalName}')?$select=EntitySetName", token)
            ?? throw new InvalidOperationException($"No existe la tabla Dataverse {logicalName}.");
        return record.GetProperty("EntitySetName").GetString()
            ?? throw new InvalidOperationException($"Dataverse no devolvió EntitySetName para {logicalName}.");
    }

    private static async Task<Relationship[]> ReadRelationshipsAsync(HttpClient client, CancellationToken token)
    {
        var records = await DataverseJson.ReadAllAsync(client,
            $"EntityDefinitions(LogicalName='{OrganizationTable}')/ManyToOneRelationships" +
            "?$select=ReferencingAttribute,ReferencingEntityNavigationPropertyName,ReferencedEntity",
            token);
        return records.Select(item => new Relationship(
            item.GetProperty("ReferencingAttribute").GetString()!,
            item.GetProperty("ReferencingEntityNavigationPropertyName").GetString()!,
            item.GetProperty("ReferencedEntity").GetString()!)).ToArray();
    }

    private static string FindNavigationProperty(
        IEnumerable<Relationship> relationships,
        string entity,
        string? attribute = null)
    {
        var matches = relationships.Where(item =>
            item.ReferencedEntity.Equals(entity, StringComparison.OrdinalIgnoreCase)
            && (attribute is null || item.ReferencingAttribute.Equals(attribute, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return matches.Length == 1 && !string.IsNullOrWhiteSpace(matches[0].NavigationProperty)
            ? matches[0].NavigationProperty
            : throw new InvalidOperationException($"No se pudo resolver de forma unívoca el lookup hacia {entity}.");
    }

    private static Guid? ReadCreatedId(HttpResponseMessage response)
    {
        var value = response.Headers.TryGetValues("OData-EntityId", out var values)
            ? values.SingleOrDefault()
            : null;
        if (value is null) return null;
        var start = value.LastIndexOf('(') + 1;
        var end = value.LastIndexOf(')');
        return start > 0 && end > start && Guid.TryParse(value[start..end], out var id) ? id : null;
    }

    private sealed record Relationship(
        string ReferencingAttribute,
        string NavigationProperty,
        string ReferencedEntity);
}
