using System.Text.Json;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed class DataverseHelpdeskAttachmentPolicyStore(IDataverseDelegatedClientFactory clients)
    : IHelpdeskAttachmentPolicyStore
{
    public async Task<HelpdeskAttachmentPolicy> ReadAsync(Guid requestId, Guid actorId, CancellationToken token)
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
        return new(true,
            OptionalGuid(requestRow.Value, "_" + requesterLookup + "_value") == actorId,
            OptionalGuid(requestRow.Value, "_" + managerLookup + "_value") == actorId,
            Bool(serviceRow.Value, service.Attribute("gaia_PermiteAdjuntos")),
            DataverseJson.OptionalInt32(serviceRow.Value, service.Attribute("gaia_MaximoAdjuntos")) ?? 0,
            rows.Count,
            maximumMb <= 0 ? 0 : checked((long)maximumMb * 1024 * 1024));
    }

    private static HelpdeskAttachmentPolicy Missing() => new(false, false, false, false, 0, 0, 0);
    private static bool Bool(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True;
    private static Guid GuidValue(JsonElement row, string name) => OptionalGuid(row, name)
        ?? throw new InvalidOperationException($"Dataverse no devolvió el identificador {name}.");
    private static Guid? OptionalGuid(JsonElement row, string name) => row.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;
}
