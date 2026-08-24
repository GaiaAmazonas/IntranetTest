using System.Net;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Gaia.Api.Infrastructure.Dataverse;

internal sealed record DataverseRelationship(string ReferencingAttribute, string NavigationProperty, string ReferencedEntity);
internal sealed record DataverseTableMetadata(string LogicalName, string EntitySetName, string PrimaryIdAttribute,
    string PrimaryNameAttribute, IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyDictionary<string, string> AttributeTypes, DataverseRelationship[] Relationships)
{
    public string Attribute(string schemaName)
    {
        var resolved = OptionalAttribute(schemaName);
        return resolved ?? throw new InvalidOperationException($"Dataverse no contiene el campo {schemaName} en {EntitySetName}.");
    }

    public bool UsesNumericLiteral(string schemaName)
    {
        var logicalName = Attribute(schemaName);
        return AttributeTypes.TryGetValue(logicalName, out var type)
            && type is "Picklist" or "Integer" or "BigInt" or "State" or "Status";
    }

    public string EncodedIntegerLiteral(string schemaName, int value) =>
        UsesNumericLiteral(schemaName)
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : $"'{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}'";

    public object EncodedIntegerValue(string schemaName, int value) =>
        UsesNumericLiteral(schemaName)
            ? value
            : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string? OptionalAttribute(string schemaName)
    {
        if (Attributes.TryGetValue(schemaName, out var logical)) return logical;
        var requiredTokens = schemaName switch
        {
            "gaia_Correo" => new[] { "correo" },
            "gaia_Visiblenavegacion" => new[] { "visible", "naveg" },
            _ => []
        };
        if (requiredTokens.Length > 0)
        {
            var candidates = Attributes
                .Where(item => requiredTokens.All(token => item.Key.Contains(token, StringComparison.OrdinalIgnoreCase)))
                .Select(item => item.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (candidates.Length == 1) return candidates[0];
        }
        return null;
    }
    public DataverseRelationship Relationship(string referencingSchema, string referencedEntity)
    {
        var attribute = Attribute(referencingSchema);
        return Relationships.SingleOrDefault(item => item.ReferencingAttribute.Equals(attribute, StringComparison.OrdinalIgnoreCase)
            && item.ReferencedEntity.Equals(referencedEntity, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No se pudo resolver el lookup {LogicalName}.{referencingSchema} mediante metadata.");
    }

    public DataverseRelationship RelationshipTo(string referencedEntity)
    {
        var candidates = Relationships
            .Where(item => item.ReferencedEntity.Equals(referencedEntity, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new InvalidOperationException($"No existe un lookup desde {LogicalName} hacia {referencedEntity}."),
            _ => throw new InvalidOperationException($"Existe más de un lookup desde {LogicalName} hacia {referencedEntity}; se requiere identificar el atributo de referencia.")
        };
    }
}

internal static class DataverseMetadataResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new(StringComparer.OrdinalIgnoreCase);
    public static async Task<IReadOnlyDictionary<string, DataverseAttributeConstraint>> ConstraintsAsync(
        HttpClient client, string logicalName, CancellationToken token)
    {
        var cacheKey = $"constraints:{logicalName}";
        if (CacheEnabled(client) && TryGet(cacheKey, out IReadOnlyDictionary<string, DataverseAttributeConstraint> cached)) return cached;
        var path = $"EntityDefinitions(LogicalName='{logicalName}')/Attributes?$select=LogicalName,SchemaName,AttributeType,RequiredLevel";
        using var response = await client.GetAsync(path, token);
        var content = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Dataverse rechazó la metadata de atributos ({(int)response.StatusCode}).");
        using var document = JsonDocument.Parse(content);
        var attributes = document.RootElement.GetProperty("value").EnumerateArray()
            .Where(x => x.TryGetProperty("SchemaName", out var schema) && schema.ValueKind == JsonValueKind.String)
            .ToDictionary(x => Required(x, "SchemaName"), x => new DataverseAttributeConstraint(
                Required(x, "LogicalName"),
                null,
                x.TryGetProperty("RequiredLevel", out var required) && required.TryGetProperty("Value", out var value)
                    && value.ValueKind == JsonValueKind.String ? value.GetString() : null), StringComparer.OrdinalIgnoreCase);
        var stringsPath = $"EntityDefinitions(LogicalName='{logicalName}')/Attributes/Microsoft.Dynamics.CRM.StringAttributeMetadata?$select=LogicalName,MaxLength";
        using var stringsResponse = await client.GetAsync(stringsPath, token);
        var stringsContent = await stringsResponse.Content.ReadAsStringAsync(token);
        if (!stringsResponse.IsSuccessStatusCode) throw new InvalidOperationException($"Dataverse rechazó la metadata de longitudes ({(int)stringsResponse.StatusCode}).");
        using var stringsDocument = JsonDocument.Parse(stringsContent);
        var lengths = stringsDocument.RootElement.GetProperty("value").EnumerateArray()
            .Where(x => x.TryGetProperty("LogicalName", out var name) && name.ValueKind == JsonValueKind.String)
            .ToDictionary(x => Required(x, "LogicalName"), x => x.TryGetProperty("MaxLength", out var max) && max.TryGetInt32(out var length) ? (int?)length : null, StringComparer.OrdinalIgnoreCase);
        var result = attributes.ToDictionary(x => x.Key, x => x.Value with { MaxLength = lengths.GetValueOrDefault(x.Value.LogicalName) }, StringComparer.OrdinalIgnoreCase);
        if (CacheEnabled(client)) Set(cacheKey, result); return result;
    }
    public static async Task<DataverseTableMetadata> TableAsync(HttpClient client, string logicalName, CancellationToken token)
    {
        var cacheKey = $"table:{logicalName}";
        if (CacheEnabled(client) && TryGet(cacheKey, out DataverseTableMetadata cached)) return cached;
        var path = $"EntityDefinitions(LogicalName='{logicalName}')?$select=LogicalName,EntitySetName,PrimaryIdAttribute,PrimaryNameAttribute" +
            "&$expand=Attributes($select=LogicalName,SchemaName,AttributeType),ManyToOneRelationships($select=ReferencingAttribute,ReferencingEntityNavigationPropertyName,ReferencedEntity)";
        var item = await ReadOneAsync(client, path, token)
            ?? throw new InvalidOperationException($"No existe la tabla Dataverse {logicalName}.");
        var result = new DataverseTableMetadata(logicalName, Required(item, "EntitySetName"), Required(item, "PrimaryIdAttribute"),
            Required(item, "PrimaryNameAttribute"),
            item.GetProperty("Attributes").EnumerateArray()
                .Where(x => x.TryGetProperty("SchemaName", out var s) && s.ValueKind == JsonValueKind.String)
                .ToDictionary(x => Required(x, "SchemaName"), x => Required(x, "LogicalName"), StringComparer.OrdinalIgnoreCase),
            item.GetProperty("Attributes").EnumerateArray()
                .Where(x => x.TryGetProperty("LogicalName", out var logical) && logical.ValueKind == JsonValueKind.String
                    && x.TryGetProperty("AttributeType", out var type) && type.ValueKind == JsonValueKind.String)
                .ToDictionary(x => Required(x, "LogicalName"), x => Required(x, "AttributeType"), StringComparer.OrdinalIgnoreCase),
            item.GetProperty("ManyToOneRelationships").EnumerateArray()
                .Where(x => x.TryGetProperty("ReferencingEntityNavigationPropertyName", out var n) && n.ValueKind == JsonValueKind.String)
                .Select(x => new DataverseRelationship(Required(x, "ReferencingAttribute"), Required(x, "ReferencingEntityNavigationPropertyName"), Required(x, "ReferencedEntity"))).ToArray());
        if (CacheEnabled(client)) Set(cacheKey, result); return result;
    }

    public static async Task<IReadOnlyDictionary<string, int>> ChoicesAsync(HttpClient client, string entityLogicalName,
        string attributeLogicalName, CancellationToken token)
    {
        var cacheKey = $"choices:{entityLogicalName}:{attributeLogicalName}";
        if (CacheEnabled(client) && TryGet(cacheKey, out IReadOnlyDictionary<string, int> cached)) return cached;
        var path = $"EntityDefinitions(LogicalName='{entityLogicalName}')/Attributes(LogicalName='{attributeLogicalName}')/Microsoft.Dynamics.CRM.PicklistAttributeMetadata?$select=LogicalName&$expand=OptionSet($select=Options)";
        var item = await ReadOneAsync(client, path, token)
            ?? throw new InvalidOperationException($"No existe la opción {entityLogicalName}.{attributeLogicalName}.");
        var options = item.GetProperty("OptionSet").GetProperty("Options").EnumerateArray();
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
        {
            if (!option.TryGetProperty("Value", out var value) || !value.TryGetInt32(out var number)) continue;
            var label = option.GetProperty("Label").GetProperty("UserLocalizedLabel").GetProperty("Label").GetString();
            if (!string.IsNullOrWhiteSpace(label)) result[label.Trim()] = number;
        }
        if (CacheEnabled(client)) Set(cacheKey, result); return result;
    }

    public static async Task<JsonElement?> ReadOneAsync(HttpClient client, string path, CancellationToken token)
    {
        using var response = await client.GetAsync(path, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        var content = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Dataverse rechazó la lectura ({(int)response.StatusCode}).");
        using var document = JsonDocument.Parse(content);
        return document.RootElement.Clone();
    }

    public static string Required(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()! : throw new InvalidOperationException($"Dataverse no devolvió {property}.");
    private static bool TryGet<T>(string key, out T value) { if (Cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow && entry.Value is T typed) { value=typed; return true; } Cache.TryRemove(key,out _); value=default!; return false; }
    private static void Set<T>(string key,T value)=>Cache[key]=new(value!,DateTimeOffset.UtcNow.Add(CacheDuration));
    private static bool CacheEnabled(HttpClient client) => client.DefaultRequestHeaders.Authorization is not null;
    private sealed record CacheEntry(object Value,DateTimeOffset ExpiresAt);
}

internal sealed record DataverseAttributeConstraint(string LogicalName, int? MaxLength, string? RequiredLevel);
