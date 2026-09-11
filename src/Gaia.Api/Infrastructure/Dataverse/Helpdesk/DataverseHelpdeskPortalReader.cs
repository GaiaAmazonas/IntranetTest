using System.Text.Json;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed class DataverseHelpdeskPortalReader(IDataverseDelegatedClientFactory clients) : IHelpdeskPortalReader
{
    public async Task<HelpdeskPortalSnapshot> ReadAsync(Guid actorId, CancellationToken token)
    {
        var client = await clients.CreateAsync();
        var service = await DataverseMetadataResolver.TableAsync(client, "gaia_servicio", token);
        var request = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var state = await DataverseMetadataResolver.TableAsync(client, "gaia_estadosolicitud", token);

        var services = await ReadServices(client, service, token);
        var states = await ReadStates(client, state, token);
        var requests = await ReadRequests(client, request, services.ToDictionary(x => x.Id), states, actorId, token);
        var recentThreshold = DateTimeOffset.UtcNow.AddDays(-30);
        return new(
            requests.Count(x => !x.IsFinal),
            requests.Count(x => !x.IsFinal && !string.Equals(x.Status, "Nuevo", StringComparison.OrdinalIgnoreCase)),
            requests.Count(x => x.IsFinal && x.SubmittedAt >= recentThreshold),
            services.OrderBy(x => x.Order).ThenBy(x => x.Name).ToArray(),
            requests.OrderByDescending(x => x.SubmittedAt).ToArray());
    }

    private static async Task<List<HelpdeskPortalService>> ReadServices(HttpClient client, DataverseTableMetadata table,
        CancellationToken token)
    {
        var code = table.Attribute("gaia_Codigo"); var description = table.Attribute("gaia_Descripcion");
        var instructions = table.Attribute("gaia_Instrucciones"); var visible = table.Attribute("gaia_VisibleAlSolicitante");
        var allows = table.Attribute("gaia_PermiteAdjuntos"); var maximum = table.Attribute("gaia_MaximoAdjuntos");
        var maximumMb = table.Attribute("gaia_TamanoMaximoMB"); var order = table.Attribute("gaia_Orden");
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{table.EntitySetName}?$select={table.PrimaryIdAttribute},{table.PrimaryNameAttribute},{code},{description},{instructions},{allows},{maximum},{maximumMb},{order}&$filter=statecode eq 0 and {visible} eq true", token);
        return rows.Select(row => new HelpdeskPortalService(GuidValue(row, table.PrimaryIdAttribute), Text(row, code) ?? "",
            Text(row, table.PrimaryNameAttribute) ?? "Servicio", Text(row, description), Text(row, instructions),
            Bool(row, allows), Int(row, maximum), Int(row, maximumMb), Int(row, order))).ToList();
    }

    private static async Task<Dictionary<Guid, StateInfo>> ReadStates(HttpClient client, DataverseTableMetadata table,
        CancellationToken token)
    {
        var final = table.Attribute("gaia_EsFinal"); var color = table.Attribute("gaia_Color");
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{table.EntitySetName}?$select={table.PrimaryIdAttribute},{table.PrimaryNameAttribute},{final},{color}&$filter=statecode eq 0", token);
        return rows.ToDictionary(row => GuidValue(row, table.PrimaryIdAttribute),
            row => new StateInfo(Text(row, table.PrimaryNameAttribute) ?? "Sin estado", Text(row, color), Bool(row, final)));
    }

    private static async Task<List<HelpdeskPortalRequest>> ReadRequests(HttpClient client, DataverseTableMetadata table,
        Dictionary<Guid, HelpdeskPortalService> services, Dictionary<Guid, StateInfo> states,
        Guid actorId, CancellationToken token)
    {
        var subject = table.Attribute("gaia_Asunto"); var submitted = table.Attribute("gaia_FechaRadicacion");
        var due = table.Attribute("gaia_FechaLimiteActual"); var serviceLookup = table.Attribute("gaia_Servicio");
        var stateLookup = table.Attribute("gaia_EstadoActual"); var requester = table.Attribute("gaia_Solicitante");
        var manager = table.Attribute("gaia_ResponsableInterno");
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{table.EntitySetName}?$select={table.PrimaryIdAttribute},{table.PrimaryNameAttribute},{subject},{submitted},{due},_{serviceLookup}_value,_{stateLookup}_value&$filter=statecode eq 0 and (_{requester}_value eq {actorId:D} or _{manager}_value eq {actorId:D})", token);
        return rows.Select(row =>
        {
            var serviceId = OptionalGuid(row, $"_{serviceLookup}_value"); var stateId = OptionalGuid(row, $"_{stateLookup}_value");
            var status = stateId.HasValue && states.TryGetValue(stateId.Value, out var found) ? found : new("Sin estado", null, false);
            return new HelpdeskPortalRequest(GuidValue(row, table.PrimaryIdAttribute), Text(row, table.PrimaryNameAttribute) ?? "",
                Text(row, subject) ?? "", serviceId.HasValue && services.TryGetValue(serviceId.Value, out var item) ? item.Name : "Servicio",
                status.Name, status.Color, status.IsFinal, DateTime(row, submitted), Date(row, due));
        }).ToList();
    }

    private static string? Text(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool Bool(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static int Int(JsonElement row, string name) => DataverseJson.OptionalInt32(row, name) ?? 0;
    private static Guid GuidValue(JsonElement row, string name) => OptionalGuid(row, name) ?? throw new InvalidOperationException($"Dataverse no devolvió {name}.");
    private static Guid? OptionalGuid(JsonElement row, string name) => Text(row, name) is { } text && Guid.TryParse(text, out var id) ? id : null;
    private static DateTimeOffset? DateTime(JsonElement row, string name) => Text(row, name) is { } text && DateTimeOffset.TryParse(text, out var value) ? value : null;
    private static DateOnly? Date(JsonElement row, string name) => Text(row, name) is { } text && DateOnly.TryParse(text, out var value) ? value : null;
    private sealed record StateInfo(string Name, string? Color, bool IsFinal);
}
