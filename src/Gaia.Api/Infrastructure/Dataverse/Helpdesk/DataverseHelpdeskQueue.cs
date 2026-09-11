using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed partial class DataverseHelpdeskManagementStore
{
    public async Task<HelpdeskQueuePage> ReadQueueAsync(HelpdeskQueueFilter filter, CancellationToken token)
    {
        var client = await clients.CreateAsync();
        var request = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var service = await DataverseMetadataResolver.TableAsync(client, "gaia_servicio", token);
        var state = await DataverseMetadataResolver.TableAsync(client, "gaia_estadosolicitud", token);
        var third = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        var unit = await DataverseMetadataResolver.TableAsync(client, "gaia_organizacion", token);
        var path = BuildQueueQuery(filter, request, service, state, third, unit, DateOnly.FromDateTime(DateTime.UtcNow));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{client.BaseAddress}|{filter.PageSize}|{path}")));
        var protector = protection.CreateProtector("Gaia.Helpdesk.Queue.v1");
        var currentPage = 1;
        int? cursorTotal = null;
        if (!string.IsNullOrEmpty(filter.ContinuationToken))
        {
            try
            {
                var cursor = JsonSerializer.Deserialize<QueueCursor>(protector.Unprotect(filter.ContinuationToken));
                if (cursor is null || cursor.Query != fingerprint || cursor.Page != filter.Page)
                    throw new ArgumentException("La continuación no corresponde a estos filtros o página.");
                path = cursor.Path;
                currentPage = cursor.Page;
                cursorTotal = cursor.TotalCount;
            }
            catch (Exception error) when (error is CryptographicException or JsonException)
            { throw new ArgumentException("La continuación no es válida.", error); }
        }

        // A direct URL/reload may lack a cursor. Walk bounded server pages, never read the whole queue.
        while (true)
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, path);
            message.Headers.TryAddWithoutValidation("Prefer", $"odata.maxpagesize={filter.PageSize},odata.include-annotations=\"Microsoft.Dynamics.CRM.totalrecordcount,Microsoft.Dynamics.CRM.totalrecordcountlimitexceeded\"");
            using var response = await client.SendAsync(message, token);
            await Ensure(response, token);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var root = document.RootElement;
            var next = Text(root, "@odata.nextLink");
            var total = ReliableQueueTotal(root) ?? cursorTotal;
            if (currentPage < filter.Page)
            {
                if (next is null) return new(total ?? -1, filter.Page, filter.PageSize, [], false, total);
                path = next;
                currentPage++;
                continue;
            }
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var serviceNav = request.Relationship("gaia_Servicio", service.LogicalName).NavigationProperty;
            var stateNav = request.Relationship("gaia_EstadoActual", state.LogicalName).NavigationProperty;
            var requesterNav = request.Relationship("gaia_Solicitante", third.LogicalName).NavigationProperty;
            var responsibleNav = request.Relationship("gaia_ResponsableInterno", third.LogicalName).NavigationProperty;
            var unitNav = request.Relationship("gaia_UnidadResponsable", unit.LogicalName).NavigationProperty;
            var items = root.GetProperty("value").EnumerateArray().Select(row =>
            {
                var status = Nested(row, stateNav);
                var due = Date(row, request.Attribute("gaia_FechaLimiteActual"));
                return new HelpdeskQueueItem(RequiredGuid(row, request.PrimaryIdAttribute), Text(row, request.PrimaryNameAttribute) ?? "",
                    Text(row, request.Attribute("gaia_Asunto")) ?? "", Text(Nested(row, serviceNav), service.PrimaryNameAttribute) ?? "Servicio",
                    Text(status, state.PrimaryNameAttribute) ?? "Sin estado", Text(status, state.Attribute("gaia_Color")),
                    Text(Nested(row, requesterNav), third.PrimaryNameAttribute) ?? "Sin solicitante",
                    Text(Nested(row, responsibleNav), third.PrimaryNameAttribute), Text(Nested(row, unitNav), unit.PrimaryNameAttribute),
                    DateTimeValue(row, request.Attribute("gaia_FechaRadicacion")), due,
                    !Bool(status, state.Attribute("gaia_EsFinal")) && due.HasValue && due < today);
            }).ToArray();
            var continuation = next is null ? null : protector.Protect(JsonSerializer.Serialize(new QueueCursor(fingerprint, filter.Page + 1, next, total)));
            return new(total ?? -1, filter.Page, filter.PageSize, items, next is not null, total, continuation);
        }
    }

    internal static int? ReliableQueueTotal(JsonElement root)
    {
        var count = DataverseJson.OptionalInt32(root, "@odata.count");
        if (count is null or < 0 || Bool(root, "@Microsoft.Dynamics.CRM.totalrecordcountlimitexceeded")) return null;
        // Dataverse caps counts. Without an explicit assurance, never present the cap as an exact total.
        return count < 5000 || root.TryGetProperty("@Microsoft.Dynamics.CRM.totalrecordcountlimitexceeded", out var exceeded)
            && exceeded.ValueKind == JsonValueKind.False ? count : null;
    }

    internal static string BuildQueueQuery(HelpdeskQueueFilter filter, DataverseTableMetadata request,
        DataverseTableMetadata service, DataverseTableMetadata state, DataverseTableMetadata third,
        DataverseTableMetadata unit, DateOnly today)
    {
        var serviceNav = request.Relationship("gaia_Servicio", service.LogicalName).NavigationProperty;
        var stateNav = request.Relationship("gaia_EstadoActual", state.LogicalName).NavigationProperty;
        var requesterNav = request.Relationship("gaia_Solicitante", third.LogicalName).NavigationProperty;
        var responsibleNav = request.Relationship("gaia_ResponsableInterno", third.LogicalName).NavigationProperty;
        var unitNav = request.Relationship("gaia_UnidadResponsable", unit.LogicalName).NavigationProperty;
        var subject = request.Attribute("gaia_Asunto");
        var submitted = request.Attribute("gaia_FechaRadicacion");
        var due = request.Attribute("gaia_FechaLimiteActual");
        var final = $"{stateNav}/{state.Attribute("gaia_EsFinal")}";
        var clauses = new List<string> { "statecode eq 0" };
        foreach (var (schema, id) in new[] { ("gaia_Servicio", filter.ServiceId), ("gaia_EstadoActual", filter.StateId), ("gaia_ResponsableInterno", filter.ResponsibleId) })
            if (id.HasValue) clauses.Add($"_{request.Attribute(schema)}_value eq {id.Value:D}");
        var date = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (filter.Overdue == true) clauses.Add($"({due} ne null and {due} lt {date} and ({final} eq false or {final} eq null))");
        if (filter.Overdue == false) clauses.Add($"({due} eq null or {due} ge {date} or {final} eq true)");
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().Replace("'", "''");
            clauses.Add($"(contains({request.PrimaryNameAttribute},'{term}') or contains({subject},'{term}') or contains({requesterNav}/{third.PrimaryNameAttribute},'{term}') or contains({serviceNav}/{service.PrimaryNameAttribute},'{term}'))");
        }
        var expand = $"{serviceNav}($select={service.PrimaryNameAttribute}),{stateNav}($select={state.PrimaryNameAttribute},{state.Attribute("gaia_Color")},{state.Attribute("gaia_EsFinal")}),{requesterNav}($select={third.PrimaryNameAttribute}),{responsibleNav}($select={third.PrimaryNameAttribute}),{unitNav}($select={unit.PrimaryNameAttribute})";
        return $"{request.EntitySetName}?$select={request.PrimaryIdAttribute},{request.PrimaryNameAttribute},{subject},{submitted},{due}&$expand={expand}&$filter={Uri.EscapeDataString(string.Join(" and ", clauses))}&$orderby={submitted} desc,{request.PrimaryIdAttribute} desc&$count=true";
    }

    private static JsonElement Nested(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : EmptyObject;
    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new { });
    private sealed record QueueCursor(string Query, int Page, string Path, int? TotalCount);
}
