using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationUnitUpdater(
    IDataverseDelegatedClientFactory clientFactory) : IOrganizationUnitUpdater
{
    private const string OrganizationTable = "gaia_organizacion";
    private const string OrganizationSet = "gaia_organizacions";
    private const string UnitTypeTable = "gaia_tipounidadorganizacional";
    private const string UnitTypeSet = "gaia_tipounidadorganizacionals";
    private const string SiteTable = "gaia_sede";
    private const string SiteSet = "gaia_sedes";

    public async Task<OrganizationUnitUpdateResult> UpdateAsync(
        Guid id,
        OrganizationUnitUpdateCommand command,
        CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync();
        var relationships = await ReadRelationshipsAsync(client, cancellationToken);
        var typeRelation = FindRelationship(relationships, UnitTypeTable);
        var parentRelation = FindRelationship(
            relationships, OrganizationTable, "gaia_unidadpadre");
        var siteRelation = FindRelationship(relationships, SiteTable, "gaia_sede");

        var units = await ReadHierarchyAsync(
            client, parentRelation.ReferencingAttribute, cancellationToken);
        if (!units.TryGetValue(id, out var current))
            return new(OrganizationUnitUpdateStatus.NotFound);

        var escapedCode = command.Code.Replace("'", "''", StringComparison.Ordinal);
        var duplicates = await DataverseJson.ReadAllAsync(client,
            $"{OrganizationSet}?$select=gaia_organizacionid" +
            $"&$filter=gaia_codigo eq '{escapedCode}' and gaia_organizacionid ne {id:D}&$top=1",
            cancellationToken);
        if (duplicates.Count != 0)
            return new(OrganizationUnitUpdateStatus.DuplicateCode);

        var unitType = await ReadOneAsync(client,
            $"{UnitTypeSet}({command.UnitTypeId:D})?$select=gaia_activo,statecode",
            cancellationToken);
        if (unitType is null || !IsActive(unitType.Value))
            return new(OrganizationUnitUpdateStatus.InvalidUnitType);

        if (command.ParentId == id)
            return new(OrganizationUnitUpdateStatus.SelfParent);
        if (command.ParentId.HasValue && !units.ContainsKey(command.ParentId.Value))
            return new(OrganizationUnitUpdateStatus.ParentNotFound);
        if (command.ParentId.HasValue && CreatesCycle(id, command.ParentId.Value, units))
            return new(OrganizationUnitUpdateStatus.HierarchyCycle);

        if (!command.SiteId.HasValue || !await IsBogotaSiteAsync(
                client, command.SiteId.Value, cancellationToken))
            return new(OrganizationUnitUpdateStatus.SiteNotFound);

        var level = command.ParentId.HasValue
            ? units[command.ParentId.Value].Level + 1
            : 1;
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
            [$"{typeRelation.NavigationProperty}@odata.bind"] = $"/{UnitTypeSet}({command.UnitTypeId:D})",
            [$"{parentRelation.NavigationProperty}@odata.bind"] = command.ParentId.HasValue
                ? $"/{OrganizationSet}({command.ParentId.Value:D})" : null,
            [$"{siteRelation.NavigationProperty}@odata.bind"] = $"/{SiteSet}({command.SiteId.Value:D})"
        };
        await PatchAsync(client, id, payload, cancellationToken);

        if (current.ParentId != command.ParentId)
            await RecalculateDescendantsAsync(client, id, level, units, cancellationToken);

        return new(OrganizationUnitUpdateStatus.Updated, id);
    }

    private static async Task<Dictionary<Guid, UnitNode>> ReadHierarchyAsync(
        HttpClient client,
        string parentAttribute,
        CancellationToken cancellationToken)
    {
        var records = await DataverseJson.ReadAllAsync(client,
            $"{OrganizationSet}?$select=gaia_organizacionid,gaia_nivel,_{parentAttribute}_value",
            cancellationToken);
        return records.ToDictionary(
            item => item.GetProperty("gaia_organizacionid").GetGuid(),
            item => new UnitNode(
                item.GetProperty("gaia_organizacionid").GetGuid(),
                OptionalGuid(item, $"_{parentAttribute}_value"),
                DataverseJson.OptionalInt32(item, "gaia_nivel") ?? 1));
    }

    private static bool CreatesCycle(Guid id, Guid proposedParent, IReadOnlyDictionary<Guid, UnitNode> units)
    {
        var visited = new HashSet<Guid>();
        Guid? cursor = proposedParent;
        while (cursor.HasValue && visited.Add(cursor.Value))
        {
            if (cursor.Value == id) return true;
            cursor = units.TryGetValue(cursor.Value, out var node) ? node.ParentId : null;
        }
        return false;
    }

    private static async Task RecalculateDescendantsAsync(
        HttpClient client,
        Guid rootId,
        int rootLevel,
        IReadOnlyDictionary<Guid, UnitNode> units,
        CancellationToken cancellationToken)
    {
        var children = units.Values
            .Where(item => item.ParentId.HasValue)
            .GroupBy(item => item.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var queue = new Queue<(Guid Id, int Level)>();
        queue.Enqueue((rootId, rootLevel));
        while (queue.TryDequeue(out var parent))
        {
            if (!children.TryGetValue(parent.Id, out var descendants)) continue;
            foreach (var child in descendants)
            {
                var childLevel = parent.Level + 1;
                await PatchAsync(client, child.Id,
                    new Dictionary<string, object?> { ["gaia_nivel"] = childLevel },
                    cancellationToken);
                queue.Enqueue((child.Id, childLevel));
            }
        }
    }

    private static async Task<bool> IsBogotaSiteAsync(
        HttpClient client, Guid id, CancellationToken cancellationToken)
    {
        var site = await ReadOneAsync(client,
            $"{SiteSet}({id:D})?$select=gaia_codigo,statecode", cancellationToken);
        return site is not null
            && site.Value.TryGetProperty("gaia_codigo", out var code)
            && string.Equals(code.GetString(), "BOG", StringComparison.OrdinalIgnoreCase)
            && (DataverseJson.OptionalInt32(site.Value, "statecode") ?? 0) == 0;
    }

    private static bool IsActive(JsonElement item) =>
        (!item.TryGetProperty("gaia_activo", out var active)
            || active.ValueKind == JsonValueKind.Null || active.GetBoolean())
        && (DataverseJson.OptionalInt32(item, "statecode") ?? 0) == 0;

    private static async Task<JsonElement?> ReadOneAsync(
        HttpClient client, string path, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(path, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Dataverse rechazó la validación ({(int)response.StatusCode}): {content}");
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    private static async Task PatchAsync(
        HttpClient client,
        Guid id,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{OrganizationSet}({id:D})")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("If-Match", "*");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Dataverse rechazó la actualización ({(int)response.StatusCode}): {content}");
    }

    private static async Task<Relationship[]> ReadRelationshipsAsync(
        HttpClient client, CancellationToken cancellationToken)
    {
        var records = await DataverseJson.ReadAllAsync(client,
            $"EntityDefinitions(LogicalName='{OrganizationTable}')/ManyToOneRelationships" +
            "?$select=ReferencingAttribute,ReferencingEntityNavigationPropertyName,ReferencedEntity",
            cancellationToken);
        return records.Select(item => new Relationship(
            item.GetProperty("ReferencingAttribute").GetString()!,
            item.GetProperty("ReferencingEntityNavigationPropertyName").GetString()!,
            item.GetProperty("ReferencedEntity").GetString()!)).ToArray();
    }

    private static Relationship FindRelationship(
        IEnumerable<Relationship> relationships, string entity, string? attribute = null)
    {
        var matches = relationships.Where(item =>
            item.ReferencedEntity.Equals(entity, StringComparison.OrdinalIgnoreCase)
            && (attribute is null
                || item.ReferencingAttribute.Equals(attribute, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return matches.Length == 1 && !string.IsNullOrWhiteSpace(matches[0].NavigationProperty)
            ? matches[0]
            : throw new InvalidOperationException($"No se pudo resolver de forma unívoca el lookup hacia {entity}.");
    }

    private static Guid? OptionalGuid(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetGuid() : null;

    private sealed record Relationship(
        string ReferencingAttribute,
        string NavigationProperty,
        string ReferencedEntity);
    private sealed record UnitNode(Guid Id, Guid? ParentId, int Level);
}
