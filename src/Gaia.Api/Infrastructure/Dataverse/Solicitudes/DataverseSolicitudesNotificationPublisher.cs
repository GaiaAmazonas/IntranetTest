using System.Text.Json;
using Gaia.BuildingBlocks.Notifications;
using Gaia.Modules.Solicitudes;

namespace Gaia.Api.Infrastructure.Dataverse.Solicitudes;

internal sealed class DataverseSolicitudesNotificationPublisher(
    IDataverseDelegatedClientFactory clients,
    IEmailNotificationOutbox queue,
    ILogger<DataverseSolicitudesNotificationPublisher> logger) : ISolicitudesNotificationPublisher
{
    private static readonly Action<ILogger, string, Guid, Exception?> LogQueueFailure =
        LoggerMessage.Define<string, Guid>(LogLevel.Error, new EventId(4701, "SolicitudesNotificationQueueFailed"),
            "No fue posible encolar la notificación de {EventName} para {SourceId}.");

    public Task RequestSubmittedAsync(CreatedSolicitudesRequest created, Guid requesterId, CancellationToken token) =>
        Safely("radicación", created.Id, async () =>
        {
            var client = await clients.CreateAsync();
            var request = await ReadRequest(client, created.Id, token);
            var requester = await ReadRecipient(client, requesterId, token);
            if (requester is not null)
                await queue.QueueAsync(new(
                    $"SOLICITUD_RADICADA:{created.Id:D}:SOLICITANTE", created.Id.ToString("D"), "SOLICITUDES", "RADICACION",
                    created.Id.ToString("D"), "SOLICITUD_RADICADA", [requester], Variables(request, created),
                    created.Number, request.ServiceName), token);

            if (request.ResponsibleId.HasValue)
            {
                var responsible = await ReadRecipient(client, request.ResponsibleId.Value, token);
                if (responsible is not null)
                    await QueueAssignment(created.Id, created.Number, request, responsible,
                        AssignmentOperation(created.Id, request.ResponsibleId.Value, request.ModifiedOn), "ASIGNACION_INICIAL", token);
            }

            foreach (var management in await ReadAssignedManagements(client, created.Id, token))
                await ManagementReassignedAsync(management.Id, management.ResponsibleId, token);
        }, token);

    public Task RequestReassignedAsync(Guid requestId, Guid responsibleId, CancellationToken token) =>
        Safely("reasignación de solicitud", requestId, async () =>
        {
            var client = await clients.CreateAsync();
            var request = await ReadRequest(client, requestId, token);
            var responsible = await ReadRecipient(client, responsibleId, token);
            if (responsible is null) return;
            await QueueAssignment(requestId, request.Number, request, responsible,
                AssignmentOperation(requestId, responsibleId, request.ModifiedOn), "REASIGNACION", token);
        }, token);

    public Task ManagementReassignedAsync(Guid managementId, Guid responsibleId, CancellationToken token) =>
        Safely("reasignación de etapa", managementId, async () =>
        {
            var client = await clients.CreateAsync();
            var managementTable = await DataverseMetadataResolver.TableAsync(client, "gaia_gestionsolicitud", token);
            var requestTable = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
            var stepTable = await DataverseMetadataResolver.TableAsync(client, "gaia_pasoflujo", token);
            var requestRelation = managementTable.Relationship("gaia_Solicitud", requestTable.LogicalName);
            var stepRelation = managementTable.Relationship("gaia_PasoFlujo", stepTable.LogicalName);
            var assignedAtField = managementTable.Attribute("gaia_FechaAsignacion");
            var row = await DataverseMetadataResolver.ReadOneAsync(client,
                $"{managementTable.EntitySetName}({managementId:D})?$select={assignedAtField},_{requestRelation.ReferencingAttribute}_value,_{stepRelation.ReferencingAttribute}_value", token)
                ?? throw new KeyNotFoundException("La gestión reasignada no existe.");
            var requestId = RequiredGuid(row, $"_{requestRelation.ReferencingAttribute}_value");
            var stepId = RequiredGuid(row, $"_{stepRelation.ReferencingAttribute}_value");
            var request = await ReadRequest(client, requestId, token);
            var responsible = await ReadRecipient(client, responsibleId, token);
            if (responsible is null) return;
            var step = await DataverseMetadataResolver.ReadOneAsync(client,
                $"{stepTable.EntitySetName}({stepId:D})?$select={stepTable.PrimaryNameAttribute}", token);
            var stepName = step is null ? "Etapa de solicitud" : Text(step.Value, stepTable.PrimaryNameAttribute) ?? "Etapa de solicitud";
            var variables = CommonVariables(request).Concat(new Dictionary<string, string?>
            {
                ["responsable"] = responsible.Name,
                ["etapa"] = stepName
            }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            await queue.QueueAsync(new(
                $"SOL_ETAPA:{managementId:N}:{(DateTimeValue(row, assignedAtField) ?? DateTimeOffset.UtcNow).UtcTicks}", managementId.ToString("D"), "SOLICITUDES", "ETAPA_ASIGNADA",
                managementId.ToString("D"), "SOLICITUD_ETAPA_ASIGNADA", [responsible], variables,
                request.Number, $"{request.ServiceName} · {stepName}"), token);
        }, token);

    private async Task QueueAssignment(Guid requestId, string number, RequestContext request, EmailRecipient responsible,
        string operationId, string eventCode, CancellationToken token)
    {
        var variables = CommonVariables(request).Concat(new Dictionary<string, string?>
        {
            ["responsable"] = responsible.Name
        }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        await queue.QueueAsync(new(operationId, requestId.ToString("D"), "SOLICITUDES", eventCode,
            requestId.ToString("D"), "SOLICITUD_ASIGNADA", [responsible], variables, number, request.ServiceName), token);
    }

    private static async Task<RequestContext> ReadRequest(HttpClient client, Guid requestId, CancellationToken token)
    {
        var request = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var service = await DataverseMetadataResolver.TableAsync(client, "gaia_servicio", token);
        var thirdParty = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        var serviceRelation = request.Relationship("gaia_Servicio", service.LogicalName);
        var responsibleRelation = request.Relationship("gaia_ResponsableInterno", thirdParty.LogicalName);
        var subject = request.Attribute("gaia_Asunto");
        var due = request.Attribute("gaia_FechaLimiteActual");
        var row = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{request.EntitySetName}({requestId:D})?$select={request.PrimaryNameAttribute},{subject},{due},modifiedon,_{serviceRelation.ReferencingAttribute}_value,_{responsibleRelation.ReferencingAttribute}_value", token)
            ?? throw new KeyNotFoundException("La solicitud no existe.");
        var serviceId = RequiredGuid(row, $"_{serviceRelation.ReferencingAttribute}_value");
        var serviceRow = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{service.EntitySetName}({serviceId:D})?$select={service.PrimaryNameAttribute}", token)
            ?? throw new InvalidOperationException("No fue posible leer el servicio de la solicitud.");
        return new(Text(row, request.PrimaryNameAttribute) ?? requestId.ToString("D"),
            Text(row, subject) ?? "Solicitud", Text(serviceRow, service.PrimaryNameAttribute) ?? "Servicio",
            Text(row, due), OptionalGuid(row, $"_{responsibleRelation.ReferencingAttribute}_value"), DateTimeValue(row, "modifiedon") ?? DateTimeOffset.UtcNow);
    }

    private static async Task<EmailRecipient?> ReadRecipient(HttpClient client, Guid personId, CancellationToken token)
    {
        var person = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        var email = await DataverseMetadataResolver.TableAsync(client, "gaia_correocolaborador", token);
        var personRow = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{person.EntitySetName}({personId:D})?$select={person.PrimaryNameAttribute},statecode", token);
        if (personRow is null || (DataverseJson.OptionalInt32(personRow.Value, "statecode") ?? 1) != 0) return null;
        var parent = email.Attribute("gaia_Tercero");
        var address = email.Attribute("gaia_Correoelectronico");
        var primary = email.Attribute("gaia_Principal");
        var type = email.Attribute("gaia_Tipocorreo");
        var corporate = email.EncodedIntegerLiteral("gaia_Tipocorreo", 2);
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{email.EntitySetName}?$select={address},{primary}&$filter=statecode eq 0 and _{parent}_value eq {personId:D} and {type} eq {corporate}&$top=10", token);
        var selected = rows.OrderByDescending(row => Bool(row, primary)).Select(row => Text(row, address))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return selected is null ? null : new(selected, Text(personRow.Value, person.PrimaryNameAttribute));
    }

    private static async Task<AssignedManagement[]> ReadAssignedManagements(HttpClient client, Guid requestId, CancellationToken token)
    {
        var management = await DataverseMetadataResolver.TableAsync(client, "gaia_gestionsolicitud", token);
        var request = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var thirdParty = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        var requestRelation = management.Relationship("gaia_Solicitud", request.LogicalName);
        var responsibleRelation = management.Relationship("gaia_Responsable", thirdParty.LogicalName);
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{management.EntitySetName}?$select={management.PrimaryIdAttribute},_{responsibleRelation.ReferencingAttribute}_value&$filter=statecode eq 0 and _{requestRelation.ReferencingAttribute}_value eq {requestId:D}", token);
        return rows.Select(row => new { Id = RequiredGuid(row, management.PrimaryIdAttribute),
                ResponsibleId = OptionalGuid(row, $"_{responsibleRelation.ReferencingAttribute}_value") })
            .Where(item => item.ResponsibleId.HasValue)
            .Select(item => new AssignedManagement(item.Id, item.ResponsibleId!.Value)).ToArray();
    }

    private static Dictionary<string, string?> Variables(RequestContext request, CreatedSolicitudesRequest created) =>
        CommonVariables(request).Concat(new Dictionary<string, string?>
        {
            ["fechaRadicacion"] = created.SubmittedAt.ToString("dd/MM/yyyy h:mm tt", System.Globalization.CultureInfo.GetCultureInfo("es-CO")),
            ["fechaLimite"] = created.DueDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.GetCultureInfo("es-CO"))
        }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string?> CommonVariables(RequestContext request) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["numeroSolicitud"] = request.Number,
        ["servicio"] = request.ServiceName,
        ["asunto"] = request.Subject,
        ["fechaLimite"] = request.DueDate
    };

    private static string AssignmentOperation(Guid requestId, Guid responsibleId, DateTimeOffset modifiedOn) =>
        $"SOL_ASIG:{requestId:N}:{responsibleId:N}:{modifiedOn.UtcTicks}";

    private async Task Safely(string eventName, Guid sourceId, Func<Task> operation, CancellationToken token)
    {
        try { await operation(); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            LogQueueFailure(logger, eventName, sourceId, exception);
        }
    }

    private static string? Text(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool Bool(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static Guid RequiredGuid(JsonElement row, string name) => OptionalGuid(row, name) ?? throw new InvalidOperationException($"Dataverse no devolvió {name}.");
    private static Guid? OptionalGuid(JsonElement row, string name) => Guid.TryParse(Text(row, name), out var id) ? id : null;
    private static DateTimeOffset? DateTimeValue(JsonElement row, string name) => DateTimeOffset.TryParse(Text(row, name), out var value) ? value : null;

    private sealed record RequestContext(string Number, string Subject, string ServiceName, string? DueDate, Guid? ResponsibleId, DateTimeOffset ModifiedOn);
    private sealed record AssignedManagement(Guid Id, Guid ResponsibleId);
}
