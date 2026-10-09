using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Gaia.Modules.Solicitudes;

namespace Gaia.Api.Infrastructure.Dataverse.Solicitudes;

internal sealed partial class DataverseSolicitudesManagementStore
{
    public async Task<SolicitudesQueuePage> ReadQueueAsync(Guid actorId, SolicitudesQueueFilter filter, CancellationToken token)
    {
        var client = await clients.CreateAsync();
        var request = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var service = await DataverseMetadataResolver.TableAsync(client, "gaia_servicio", token);
        var state = await DataverseMetadataResolver.TableAsync(client, "gaia_estadosolicitud", token);
        var third = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        var unit = await DataverseMetadataResolver.TableAsync(client, "gaia_organizacion", token);
        var accessibleRequests = filter.View == "all"
            ? null
            : await ReadAccessibleRequestIds(client, actorId, filter.View, token);
        if (accessibleRequests is { Count: 0 }) return new(0, filter.Page, filter.PageSize, [], false, 0);
        var path = BuildQueueQuery(filter, request, service, state, third, unit, DateOnly.FromDateTime(DateTime.UtcNow), accessibleRequests);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{client.BaseAddress}|{filter.PageSize}|{path}")));
        var protector = protection.CreateProtector("Gaia.Solicitudes.Queue.v1");
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
                var statusCode = Text(status, state.Attribute("gaia_Codigo"));
                var isFinal = Bool(status, state.Attribute("gaia_EsFinal"));
                var isClosed = string.Equals(statusCode,"RESUELTA",StringComparison.OrdinalIgnoreCase)
                    || string.Equals(statusCode,"CERRADA",StringComparison.OrdinalIgnoreCase);
                var due = Date(row, request.Attribute("gaia_FechaLimiteActual"));
                return new SolicitudesQueueItem(RequiredGuid(row, request.PrimaryIdAttribute), Text(row, request.PrimaryNameAttribute) ?? "",
                    DisplaySubject(Text(row, request.Attribute("gaia_Asunto"))), Text(Nested(row, serviceNav), service.PrimaryNameAttribute) ?? "Servicio",
                    isClosed ? "Cerrada" : Text(status, state.PrimaryNameAttribute) ?? "Sin estado", Text(status, state.Attribute("gaia_Color")),
                    Text(Nested(row, requesterNav), third.PrimaryNameAttribute) ?? "Sin solicitante",
                    Text(Nested(row, responsibleNav), third.PrimaryNameAttribute), Text(Nested(row, unitNav), unit.PrimaryNameAttribute),
                    DateTimeValue(row, request.Attribute("gaia_FechaRadicacion")), due,
                    !isFinal && due.HasValue && due < today,
                    isClosed);
            }).ToArray();
            var activeAssignments = await ReadActiveAssignments(client, items.Select(item => item.Id).ToArray(), token);
            items = items.Select(item => activeAssignments.TryGetValue(item.Id, out var assignment)
                ? item with { Responsible = assignment.Responsible, Unit = assignment.Unit }
                : item with { Responsible = null, Unit = null }).ToArray();
            var continuation = next is null ? null : protector.Protect(JsonSerializer.Serialize(new QueueCursor(fingerprint, filter.Page + 1, next, total)));
            return new(total ?? -1, filter.Page, filter.PageSize, items, next is not null, total, continuation);
        }
    }

    private static async Task<IReadOnlyDictionary<Guid, ActiveAssignment>> ReadActiveAssignments(
        HttpClient client, Guid[] requestIds, CancellationToken token)
    {
        if (requestIds.Length == 0) return new Dictionary<Guid, ActiveAssignment>();
        var management = await DataverseMetadataResolver.TableAsync(client, "gaia_gestionsolicitud", token);
        var request = management.Relationship("gaia_Solicitud", "gaia_solicitud");
        var responsible = management.Relationship("gaia_Responsable", "gaia_terceros");
        var unit = management.Relationship("gaia_UnidadResponsable", "gaia_organizacion");
        var third = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        var organization = await DataverseMetadataResolver.TableAsync(client, "gaia_organizacion", token);
        var status = management.Attribute("gaia_Estado");
        var activeStates = new[]
        {
            SolicitudesWorkflowValues.ManagementAvailable,
            SolicitudesWorkflowValues.ManagementInProgress,
            SolicitudesWorkflowValues.ManagementWaiting
        }.Select(value => management.EncodedIntegerValue("gaia_Estado", value)).ToArray();
        var requestFilter = string.Join(" or ", requestIds.Select(id => $"_{request.ReferencingAttribute}_value eq {id:D}"));
        var stateFilter = string.Join(" or ", activeStates.Select(value => $"{status} eq {value}"));
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{management.EntitySetName}?$select={management.PrimaryIdAttribute},{status},_{request.ReferencingAttribute}_value,_{responsible.ReferencingAttribute}_value,_{unit.ReferencingAttribute}_value&$expand={responsible.NavigationProperty}($select={third.PrimaryNameAttribute}),{unit.NavigationProperty}($select={organization.PrimaryNameAttribute})&$filter=statecode eq 0 and ({requestFilter}) and ({stateFilter})", token);
        return rows.GroupBy(row => RequiredGuid(row, $"_{request.ReferencingAttribute}_value")).ToDictionary(
            group => group.Key,
            group => new ActiveAssignment(
                JoinDistinct(group.Select(row => Text(Nested(row, responsible.NavigationProperty), third.PrimaryNameAttribute))),
                JoinDistinct(group.Select(row => Text(Nested(row, unit.NavigationProperty), organization.PrimaryNameAttribute)))));
    }

    private static string? JoinDistinct(IEnumerable<string?> values)
    {
        var result = values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return result.Length == 0 ? null : string.Join(" · ", result);
    }

    private static string DisplaySubject(string? value) =>
        string.IsNullOrWhiteSpace(value) || Guid.TryParse(value, out _) ? string.Empty : value.Trim();

    internal static int? ReliableQueueTotal(JsonElement root)
    {
        var count = DataverseJson.OptionalInt32(root, "@odata.count");
        if (count is null or < 0 || Bool(root, "@Microsoft.Dynamics.CRM.totalrecordcountlimitexceeded")) return null;
        // Dataverse caps counts. Without an explicit assurance, never present the cap as an exact total.
        return count < 5000 || root.TryGetProperty("@Microsoft.Dynamics.CRM.totalrecordcountlimitexceeded", out var exceeded)
            && exceeded.ValueKind == JsonValueKind.False ? count : null;
    }

    internal static string BuildQueueQuery(SolicitudesQueueFilter filter, DataverseTableMetadata request,
        DataverseTableMetadata service, DataverseTableMetadata state, DataverseTableMetadata third,
        DataverseTableMetadata unit, DateOnly today, IReadOnlyCollection<Guid>? accessibleRequests=null)
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
        var stateCode = $"{stateNav}/{state.Attribute("gaia_Codigo")}";
        var terminal = $"({final} eq true or {stateCode} eq 'CERRADA' or {stateCode} eq 'RESUELTA')";
        var nonTerminal = $"(({final} eq false or {final} eq null) and ({stateCode} eq null or ({stateCode} ne 'CERRADA' and {stateCode} ne 'RESUELTA')))";
        var clauses = new List<string> { "statecode eq 0" };
        if (accessibleRequests is { Count: > 0 })
            clauses.Add($"({string.Join(" or ", accessibleRequests.Select(id => $"{request.PrimaryIdAttribute} eq {id:D}"))})");
        foreach (var (schema, id) in new[] { ("gaia_Servicio", filter.ServiceId), ("gaia_EstadoActual", filter.StateId), ("gaia_ResponsableInterno", filter.ResponsibleId) })
            if (id.HasValue) clauses.Add($"_{request.Attribute(schema)}_value eq {id.Value:D}");
        var date = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (filter.Overdue == true) clauses.Add($"({due} ne null and {due} lt {date} and {nonTerminal})");
        if (filter.Overdue == false) clauses.Add($"({due} eq null or {due} ge {date} or {terminal})");
        if (filter.View == "resolved") clauses.Add(terminal);
        if (filter.View == "tracking") clauses.Add(nonTerminal);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().Replace("'", "''");
            clauses.Add($"(contains({request.PrimaryNameAttribute},'{term}') or contains({subject},'{term}') or contains({requesterNav}/{third.PrimaryNameAttribute},'{term}') or contains({serviceNav}/{service.PrimaryNameAttribute},'{term}'))");
        }
        var expand = $"{serviceNav}($select={service.PrimaryNameAttribute}),{stateNav}($select={state.Attribute("gaia_Codigo")},{state.PrimaryNameAttribute},{state.Attribute("gaia_Color")},{state.Attribute("gaia_EsFinal")}),{requesterNav}($select={third.PrimaryNameAttribute}),{responsibleNav}($select={third.PrimaryNameAttribute}),{unitNav}($select={unit.PrimaryNameAttribute})";
        return $"{request.EntitySetName}?$select={request.PrimaryIdAttribute},{request.PrimaryNameAttribute},{subject},{submitted},{due}&$expand={expand}&$filter={Uri.EscapeDataString(string.Join(" and ", clauses))}&$orderby={submitted} desc,{request.PrimaryIdAttribute} desc&$count=true";
    }

    private static JsonElement Nested(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : EmptyObject;
    private static readonly JsonElement EmptyObject = JsonSerializer.SerializeToElement(new { });
    private sealed record ActiveAssignment(string? Responsible, string? Unit);
    private sealed record QueueCursor(string Query, int Page, string Path, int? TotalCount);

    private async Task<IReadOnlyCollection<Guid>> ReadAccessibleRequestIds(HttpClient client, Guid actorId, string view, CancellationToken token)
    {
        var management = await DataverseMetadataResolver.TableAsync(client, "gaia_gestionsolicitud", token);
        var request = management.Relationship("gaia_Solicitud", "gaia_solicitud");
        var responsible = management.Relationship("gaia_Responsable", "gaia_terceros");
        var managedBy = management.Relationship("gaia_GestionadaPor", "gaia_terceros");
        var unit = management.Relationship("gaia_UnidadResponsable", "gaia_organizacion");
        var step = management.Relationship("gaia_PasoFlujo", "gaia_pasoflujo");
        var stepTable = await DataverseMetadataResolver.TableAsync(client, "gaia_pasoflujo", token);
        var status = management.Attribute("gaia_Estado");
        var active = $"({status} eq {management.EncodedIntegerValue("gaia_Estado",SolicitudesWorkflowValues.ManagementAvailable)} or {status} eq {management.EncodedIntegerValue("gaia_Estado",SolicitudesWorkflowValues.ManagementInProgress)})";
        var waiting = $"{status} eq {management.EncodedIntegerValue("gaia_Estado",SolicitudesWorkflowValues.ManagementWaiting)}";
        var completed = $"{status} eq {management.EncodedIntegerValue("gaia_Estado",SolicitudesWorkflowValues.ManagementCompleted)}";
        async Task<HashSet<Guid>> RequestIds(string criteria)
        {
            var rows = await DataverseJson.ReadAllAsync(client,
                $"{management.EntitySetName}?$select=_{request.ReferencingAttribute}_value&$filter={Uri.EscapeDataString($"statecode eq 0 and ({criteria})")}", token);
            return rows.Select(row => OptionalGuid(row, $"_{request.ReferencingAttribute}_value"))
                .Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        }
        var currentDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var actorUnits = (await assignmentStore.ListAsync(token))
            .Where(item => item.IsActive && item.ThirdPartyId == actorId
                && (!item.StartDate.HasValue || item.StartDate <= currentDate)
                && (!item.EndDate.HasValue || item.EndDate >= currentDate))
            .Select(item => item.OrganizationalUnitId).Distinct().ToArray();
        var units = string.Join(" or ", actorUnits.Select(id => $"_{unit.ReferencingAttribute}_value eq {id:D}"));
        var generalAccess = actorUnits.Length == 0
            ? $"_{responsible.ReferencingAttribute}_value eq {actorId:D}"
            : $"(_{responsible.ReferencingAttribute}_value eq {actorId:D} or (_{responsible.ReferencingAttribute}_value eq null and ({units})))";
        var mineCriteria = $"{active} and ({generalAccess})";
        var waitingCriteria = $"{waiting} and _{managedBy.ReferencingAttribute}_value eq {actorId:D}";
        if (view == "mine") return await RequestIds(mineCriteria);
        if (view == "waiting")
        {
            var values = await RequestIds(waitingCriteria);
            values.ExceptWith(await RequestIds(mineCriteria));
            return values;
        }
        if (view is "tracking" or "resolved")
        {
            var values = await RequestIds($"{completed} and _{managedBy.ReferencingAttribute}_value eq {actorId:D}");
            if (view == "tracking")
            {
                values.ExceptWith(await RequestIds(mineCriteria));
                values.ExceptWith(await RequestIds(waitingCriteria));
            }
            return values;
        }
        if (view == "unit" && actorUnits.Length == 0) return [];
        var access = view switch
        {
            "unit" => $"{active} and _{responsible.ReferencingAttribute}_value eq null and ({units})",
            "approvals" => $"{active} and ({generalAccess}) and {step.NavigationProperty}/{stepTable.Attribute("gaia_RequiereDecision")} eq true",
            _ => generalAccess
        };
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{management.EntitySetName}?$select=_{request.ReferencingAttribute}_value&$expand={step.NavigationProperty}($select={stepTable.Attribute("gaia_RequiereDecision")})&$filter={Uri.EscapeDataString($"statecode eq 0 and ({access})")}", token);
        return rows.Select(row => OptionalGuid(row, $"_{request.ReferencingAttribute}_value"))
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
    }

}
