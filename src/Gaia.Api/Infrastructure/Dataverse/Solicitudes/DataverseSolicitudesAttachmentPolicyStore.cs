using System.Text.Json;
using Gaia.Modules.Solicitudes;

namespace Gaia.Api.Infrastructure.Dataverse.Solicitudes;

internal sealed class DataverseSolicitudesAttachmentPolicyStore(IDataverseDelegatedClientFactory clients)
    : ISolicitudesAttachmentPolicyStore
{
    public async Task<SolicitudesAttachmentPolicy> ReadAsync(Guid requestId, Guid actorId, Guid? managementId, CancellationToken token)
    {
        var client = await clients.CreateAsync();
        var request = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var service = await DataverseMetadataResolver.TableAsync(client, "gaia_servicio", token);
        var attachment = await DataverseMetadataResolver.TableAsync(client, "gaia_adjuntosolicitud", token);
        var serviceLookup = request.Attribute("gaia_Servicio");
        var requesterLookup = request.Attribute("gaia_Solicitante");
        var managerLookup = request.Attribute("gaia_ResponsableInterno");
        var requestRow = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{request.EntitySetName}({requestId:D})?$select=statecode,_{serviceLookup}_value,_{requesterLookup}_value,_{managerLookup}_value", token);
        if (requestRow is null || (DataverseJson.OptionalInt32(requestRow.Value, "statecode") ?? 0) != 0)
            return Missing();
        var serviceId = GuidValue(requestRow.Value, "_" + serviceLookup + "_value");
        var serviceRow = await DataverseMetadataResolver.ReadOneAsync(client,
            $"{service.EntitySetName}({serviceId:D})?$select=statecode,{service.Attribute("gaia_PermiteAdjuntos")},{service.Attribute("gaia_MaximoAdjuntos")},{service.Attribute("gaia_TamanoMaximoMB")}", token);
        if (serviceRow is null || (DataverseJson.OptionalInt32(serviceRow.Value, "statecode") ?? 0) != 0)
            return Missing();
        var requestAttachment = attachment.Attribute("gaia_Solicitud");
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{attachment.EntitySetName}?$select={attachment.PrimaryIdAttribute}&$filter=_{requestAttachment}_value eq {requestId:D} and statecode eq 0&$count=true", token);
        var maximumMb = DataverseJson.OptionalInt32(serviceRow.Value, service.Attribute("gaia_TamanoMaximoMB")) ?? 0;
        var requestManager = OptionalGuid(requestRow.Value, "_" + managerLookup + "_value") == actorId;
        var stageManager = await CanManageStage(client, requestId, actorId, managementId, token);
        return new(true,
            OptionalGuid(requestRow.Value, "_" + requesterLookup + "_value") == actorId,
            requestManager || stageManager,
            Bool(serviceRow.Value, service.Attribute("gaia_PermiteAdjuntos")),
            DataverseJson.OptionalInt32(serviceRow.Value, service.Attribute("gaia_MaximoAdjuntos")) ?? 0,
            rows.Count,
            maximumMb <= 0 ? 0 : checked((long)maximumMb * 1024 * 1024));
    }

    private static async Task<bool> CanManageStage(HttpClient client, Guid requestId, Guid actorId, Guid? managementId, CancellationToken token)
    {
        var management = await DataverseMetadataResolver.TableAsync(client, "gaia_gestionsolicitud", token);
        var request = management.Relationship("gaia_Solicitud", "gaia_solicitud");
        var responsible = management.Relationship("gaia_Responsable", "gaia_terceros");
        var unit = management.Relationship("gaia_UnidadResponsable", "gaia_organizacion");
        var status = management.Attribute("gaia_Estado");
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{management.EntitySetName}?$select={management.PrimaryIdAttribute},{status},_{responsible.ReferencingAttribute}_value,_{unit.ReferencingAttribute}_value&$filter=statecode eq 0 and _{request.ReferencingAttribute}_value eq {requestId:D}", token);
        var relevantRows = managementId.HasValue
            ? rows.Where(row => OptionalGuid(row, management.PrimaryIdAttribute) == managementId).ToArray()
            : rows.ToArray();
        var actionable = relevantRows.Where(row => (DataverseJson.OptionalInt32(row, status) ?? 0) is SolicitudesWorkflowValues.ManagementAvailable or SolicitudesWorkflowValues.ManagementInProgress).ToArray();
        if (actionable.Any(row => OptionalGuid(row, "_" + responsible.ReferencingAttribute + "_value") == actorId)) return true;
        var assignment = await DataverseMetadataResolver.TableAsync(client, "gaia_asignacionorganizacional", token);
        var person = assignment.RelationshipTo("gaia_terceros");
        var assignedUnit = assignment.RelationshipTo("gaia_organizacion");
        var assignments = await DataverseJson.ReadAllAsync(client,
            $"{assignment.EntitySetName}?$select=_{assignedUnit.ReferencingAttribute}_value&$filter=statecode eq 0 and _{person.ReferencingAttribute}_value eq {actorId:D}", token);
        var actorUnits = assignments.Select(row => OptionalGuid(row, "_" + assignedUnit.ReferencingAttribute + "_value")).Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
        return actionable.Any(row => !OptionalGuid(row, "_" + responsible.ReferencingAttribute + "_value").HasValue
            && OptionalGuid(row, "_" + unit.ReferencingAttribute + "_value") is { } unitId && actorUnits.Contains(unitId));
    }

    private static SolicitudesAttachmentPolicy Missing() => new(false, false, false, false, 0, 0, 0);
    private static bool Bool(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True;
    private static Guid GuidValue(JsonElement row, string name) => OptionalGuid(row, name)
        ?? throw new InvalidOperationException($"Dataverse no devolvió el identificador {name}.");
    private static Guid? OptionalGuid(JsonElement row, string name) => row.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;
}
