using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using Gaia.Modules.ThirdParties;

namespace Gaia.Api.Infrastructure.Dataverse.ThirdParties;

internal sealed class DataverseCollaboratorEmailStore(IDataverseDelegatedClientFactory factory, ILogger<DataverseCollaboratorEmailStore>? logger = null) : ICollaboratorEmailStore
{
    private const string Table = "gaia_correocolaborador";
    private const string ParentTable = "gaia_terceros";
    public async Task<IReadOnlyList<CollaboratorEmailResponse>> ListAsync(Guid parent, CancellationToken token)
    {
        var (client, table, fields) = await Context(factory, token);
        var rows = await DataverseJson.ReadAllAsync(client, $"{table.EntitySetName}?$select={fields.Select}&$filter=_{fields.Parent}_value eq {parent:D}&$orderby={fields.Email}", token);
        return rows.Select(x => new CollaboratorEmailResponse(GuidValue(x, fields.Id), StringValue(x, fields.Email) ?? "", StringValue(x, fields.Notes),
            BoolValue(x, fields.Primary), (DataverseJson.OptionalInt32(x, "statecode") ?? 0) == 0, DataverseJson.OptionalEncodedInt32(x, fields.ContactType) ?? 1)).ToArray();
    }
    public Task<RelatedWriteResult> CreateAsync(Guid parent, CollaboratorEmailCommand command, CancellationToken token) => Write(parent, null, command, token);
    public Task<RelatedWriteResult> UpdateAsync(Guid parent, Guid id, CollaboratorEmailCommand command, CancellationToken token) => Write(parent, id, command, token);
    private async Task<RelatedWriteResult> Write(Guid parent, Guid? id, CollaboratorEmailCommand command, CancellationToken token)
    {
        var gate = CollaboratorContactWriteLocks.For(Table, parent);
        await gate.WaitAsync(token);
        try { return await WriteCore(parent, id, command, token); }
        finally { gate.Release(); }
    }
    private async Task<RelatedWriteResult> WriteCore(Guid parent, Guid? id, CollaboratorEmailCommand command, CancellationToken token)
    {
        var (client, table, f) = await Context(factory, token); var parentMeta = await DataverseMetadataResolver.TableAsync(client, ParentTable, token);
        if (await DataverseMetadataResolver.ReadOneAsync(client, $"{parentMeta.EntitySetName}({parent:D})?$select={parentMeta.PrimaryIdAttribute}", token) is null) return new(RelatedWriteStatus.ParentNotFound);
        if (id.HasValue)
        {
            var current = await DataverseMetadataResolver.ReadOneAsync(client, $"{table.EntitySetName}({id:D})?$select={f.Id},_{f.Parent}_value", token);
            if (current is null || GuidValue(current.Value, $"_{f.Parent}_value") != parent) return new(RelatedWriteStatus.NotFound);
        }
        var own = id.HasValue ? $" and {f.Id} ne {id:D}" : "";
        var duplicate = await DataverseJson.ReadAllAsync(client, $"{table.EntitySetName}?$select={f.Id}&$filter=_{f.Parent}_value eq {parent:D} and {f.Email} eq '{Escape(command.Email)}'{own}&$top=1", token);
        if (duplicate.Count > 0) return new(RelatedWriteStatus.Duplicate);
        if (command.IsPrimary) await ClearPrincipals(client, table, f, parent, id, token);
        var relation = table.Relationship("gaia_Tercero", ParentTable);
        var payload = new Dictionary<string, object?> { [f.Email] = command.Email, [f.Notes] = command.Observations,
            [f.Primary] = command.IsPrimary, [f.ContactType] = table.EncodedIntegerValue("gaia_Tipocorreo", command.ContactType), ["statecode"] = command.IsActive ? 0 : 1,
            [$"{relation.NavigationProperty}@odata.bind"] = $"/{parentMeta.EntitySetName}({parent:D})" };
        LogPayload(logger, "email", payload);
        return await Send(client, table.EntitySetName, id, payload, token);
    }
    private static async Task ClearPrincipals(HttpClient client, DataverseTableMetadata table, EmailFields f, Guid parent, Guid? except, CancellationToken token)
    {
        var own = except.HasValue ? $" and {f.Id} ne {except:D}" : "";
        var rows = await DataverseJson.ReadAllAsync(client, $"{table.EntitySetName}?$select={f.Id}&$filter=_{f.Parent}_value eq {parent:D} and {f.Primary} eq true and statecode eq 0{own}", token);
        foreach (var row in rows) await Patch(client, $"{table.EntitySetName}({GuidValue(row, f.Id):D})", new Dictionary<string, object?> { [f.Primary] = false }, token);
    }
    private static async Task<(HttpClient, DataverseTableMetadata, EmailFields)> Context(IDataverseDelegatedClientFactory factory, CancellationToken token)
    { var client = await factory.CreateAsync(); var table = await DataverseMetadataResolver.TableAsync(client, Table, token); return (client, table, EmailFields.From(table)); }
    private sealed record EmailFields(string Id, string Email, string Notes, string Primary, string Parent, string ContactType)
    { public string Select => string.Join(',', Id, Email, Notes, Primary, $"_{Parent}_value", ContactType, "statecode"); public static EmailFields From(DataverseTableMetadata m) => new(m.PrimaryIdAttribute, m.Attribute("gaia_Correoelectronico"), m.Attribute("gaia_Observaciones"), m.Attribute("gaia_Principal"), m.Attribute("gaia_Tercero"), m.Attribute("gaia_Tipocorreo")); }

    internal static async Task<RelatedWriteResult> Send(HttpClient client, string set, Guid? id, Dictionary<string, object?> payload, CancellationToken token)
    { if (!id.HasValue) { using var response = await client.PostAsJsonAsync(set, payload, token); await Ensure(response, token); return new(RelatedWriteStatus.Created, CreatedId(response)); } await Patch(client, $"{set}({id:D})", payload, token); return new(RelatedWriteStatus.Updated, id); }
    internal static async Task Patch(HttpClient client, string path, Dictionary<string, object?> payload, CancellationToken token)
    { using var request = new HttpRequestMessage(HttpMethod.Patch, path) { Content = JsonContent.Create(payload) }; request.Headers.TryAddWithoutValidation("If-Match", "*"); using var response = await client.SendAsync(request, token); await Ensure(response, token); }
    internal static async Task Ensure(HttpResponseMessage response, CancellationToken token) { if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Dataverse rechazó la operación ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(token)}"); }
    internal static Guid? CreatedId(HttpResponseMessage response) { var text = response.Headers.TryGetValues("OData-EntityId", out var values) ? values.SingleOrDefault() : null; var match = text is null ? null : System.Text.RegularExpressions.Regex.Match(text, @"\(([0-9a-f-]{36})\)$"); return match?.Success == true ? Guid.Parse(match.Groups[1].Value) : null; }
    internal static Guid GuidValue(JsonElement x, string p) => x.TryGetProperty(p, out var v) && Guid.TryParse(v.GetString(), out var id) ? id : throw new InvalidOperationException($"Dataverse no devolvió {p}.");
    internal static string? StringValue(JsonElement x, string p) => x.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    internal static bool BoolValue(JsonElement x, string p) => x.TryGetProperty(p, out var v) && v.ValueKind is JsonValueKind.True;
    internal static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    internal static string PayloadShape(IReadOnlyDictionary<string, object?> payload) => string.Join(", ", payload.OrderBy(x => x.Key).Select(x => $"{x.Key}:{JsonType(x.Value)}"));
    internal static void LogPayload(ILogger? logger, string entity, IReadOnlyDictionary<string, object?> payload)
    { if (logger?.IsEnabled(LogLevel.Information) == true) PayloadShapeLog(logger, entity, PayloadShape(payload), null); }
    private static readonly Action<ILogger, string, string, Exception?> PayloadShapeLog = LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(4301, "DataverseContactPayloadShape"), "Dataverse {Entity} payload shape: {PayloadShape}");
    private static string JsonType(object? value) => value switch { null => "null", string => "string", bool => "boolean", int => "integer", _ => value.GetType().Name };
}

internal sealed class DataverseCollaboratorPhoneStore(IDataverseDelegatedClientFactory factory, ILogger<DataverseCollaboratorPhoneStore>? logger = null) : ICollaboratorPhoneStore
{
    private const string Table = "gaia_telefonocolaborador"; private const string ParentTable = "gaia_terceros";
    public async Task<IReadOnlyList<CollaboratorPhoneResponse>> ListAsync(Guid parent, CancellationToken token)
    { var (client, table, f) = await Context(token); var rows = await DataverseJson.ReadAllAsync(client, $"{table.EntitySetName}?$select={f.Select}&$filter=_{f.Parent}_value eq {parent:D}&$orderby={f.Number}", token); var types = f.Type is null ? new Dictionary<int,string>() : (await DataverseMetadataResolver.ChoicesAsync(client, Table, f.Type, token)).ToDictionary(x => x.Value, x => x.Key); return rows.Select(x => { var value = f.Type is null ? null : DataverseJson.OptionalInt32(x, f.Type); var label = f.Type is null ? "CELULAR" : DataverseCollaboratorEmailStore.StringValue(x, $"{f.Type}@OData.Community.Display.V1.FormattedValue") ?? (value.HasValue && types.TryGetValue(value.Value, out var mapped) ? mapped : "CELULAR"); return new CollaboratorPhoneResponse(DataverseCollaboratorEmailStore.GuidValue(x, f.Id), DataverseCollaboratorEmailStore.StringValue(x, f.Number) ?? "", DataverseCollaboratorEmailStore.StringValue(x, f.Extension), DataverseCollaboratorEmailStore.StringValue(x, f.Notes), DataverseCollaboratorEmailStore.BoolValue(x, f.Primary), label, (DataverseJson.OptionalInt32(x, "statecode") ?? 0) == 0, DataverseJson.OptionalEncodedInt32(x, f.ContactType) ?? 1); }).ToArray(); }
    public Task<RelatedWriteResult> CreateAsync(Guid parent, CollaboratorPhoneCommand command, CancellationToken token) => Write(parent, null, command, token);
    public Task<RelatedWriteResult> UpdateAsync(Guid parent, Guid id, CollaboratorPhoneCommand command, CancellationToken token) => Write(parent, id, command, token);
    private async Task<RelatedWriteResult> Write(Guid parent, Guid? id, CollaboratorPhoneCommand command, CancellationToken token)
    {
        var gate = CollaboratorContactWriteLocks.For(Table, parent);
        await gate.WaitAsync(token);
        try { return await WriteCore(parent, id, command, token); }
        finally { gate.Release(); }
    }
    private async Task<RelatedWriteResult> WriteCore(Guid parent, Guid? id, CollaboratorPhoneCommand command, CancellationToken token)
    {
        var (client, table, f) = await Context(token); var parentMeta = await DataverseMetadataResolver.TableAsync(client, ParentTable, token);
        if (await DataverseMetadataResolver.ReadOneAsync(client, $"{parentMeta.EntitySetName}({parent:D})?$select={parentMeta.PrimaryIdAttribute}", token) is null) return new(RelatedWriteStatus.ParentNotFound);
        if (id.HasValue)
        {
            var current = await DataverseMetadataResolver.ReadOneAsync(client, $"{table.EntitySetName}({id:D})?$select={f.Id},_{f.Parent}_value", token);
            if (current is null || DataverseCollaboratorEmailStore.GuidValue(current.Value, $"_{f.Parent}_value") != parent) return new(RelatedWriteStatus.NotFound);
        }
        var own = id.HasValue ? $" and {f.Id} ne {id:D}" : "";
        if ((await DataverseJson.ReadAllAsync(client, $"{table.EntitySetName}?$select={f.Id}&$filter=_{f.Parent}_value eq {parent:D} and {f.Number} eq '{DataverseCollaboratorEmailStore.Escape(command.Number)}'{own}&$top=1", token)).Count > 0) return new(RelatedWriteStatus.Duplicate);
        int? typeValue = null;
        if (f.Type is not null)
        {
            var choices = await DataverseMetadataResolver.ChoicesAsync(client, Table, f.Type, token);
            if (!choices.TryGetValue(command.PhoneType, out var resolvedType)) return new(RelatedWriteStatus.InvalidOption);
            typeValue = resolvedType;
        }
        if (command.IsPrimary) { var rows = await DataverseJson.ReadAllAsync(client, $"{table.EntitySetName}?$select={f.Id}&$filter=_{f.Parent}_value eq {parent:D} and {f.Primary} eq true and statecode eq 0{own}", token); foreach (var row in rows) await DataverseCollaboratorEmailStore.Patch(client, $"{table.EntitySetName}({DataverseCollaboratorEmailStore.GuidValue(row, f.Id):D})", new() { [f.Primary] = false }, token); }
        var relation = table.Relationship("gaia_Tercero", ParentTable); var payload = new Dictionary<string, object?> { [f.Number] = command.Number, [f.Extension] = command.Extension, [f.Notes] = command.Observations, [f.Primary] = command.IsPrimary, [f.ContactType] = table.EncodedIntegerValue("gaia_TipoTelefono", command.ContactType), ["statecode"] = command.IsActive ? 0 : 1, [$"{relation.NavigationProperty}@odata.bind"] = $"/{parentMeta.EntitySetName}({parent:D})" };
        if (f.Type is not null) payload[f.Type] = typeValue;
        DataverseCollaboratorEmailStore.LogPayload(logger, "phone", payload);
        return await DataverseCollaboratorEmailStore.Send(client, table.EntitySetName, id, payload, token);
    }
    private async Task<(HttpClient, DataverseTableMetadata, PhoneFields)> Context(CancellationToken token) { var client = await factory.CreateAsync(); var table = await DataverseMetadataResolver.TableAsync(client, Table, token); return (client, table, PhoneFields.From(table)); }
    private sealed record PhoneFields(string Id, string Number, string Extension, string Notes, string Primary, string Parent, string? Type, string ContactType)
    { public string Select => string.Join(',', new[] { Id, Number, Extension, Notes, Primary, $"_{Parent}_value", Type, ContactType, "statecode" }.Where(x => !string.IsNullOrWhiteSpace(x))); public static PhoneFields From(DataverseTableMetadata m) => new(m.PrimaryIdAttribute, m.Attribute("gaia_Numero"), m.Attribute("gaia_Extension"), m.Attribute("gaia_Observaciones"), m.Attribute("gaia_Principal"), m.Attribute("gaia_Tercero"), m.OptionalAttribute("gaia_Tipodetelefono"), m.Attribute("gaia_TipoTelefono")); }
}

internal static class CollaboratorContactWriteLocks
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.Ordinal);
    internal static SemaphoreSlim For(string table, Guid parent) => Locks.GetOrAdd($"{table}:{parent:D}", static _ => new SemaphoreSlim(1, 1));
}
