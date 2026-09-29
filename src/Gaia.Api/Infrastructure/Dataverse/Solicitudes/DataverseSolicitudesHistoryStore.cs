using System.Net.Http.Json;
using Gaia.Modules.Solicitudes;

namespace Gaia.Api.Infrastructure.Dataverse.Solicitudes;

internal sealed class DataverseSolicitudesHistoryStore(IDataverseDelegatedClientFactory clients) : ISolicitudesHistoryStore
{
    public async Task AppendAsync(AppendSolicitudesHistory history, CancellationToken token)
    {
        var client = await clients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, "gaia_historialsolicitud", token);
        var request = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var payload = new Dictionary<string, object?>
        {
            [metadata.PrimaryNameAttribute] = history.Movement switch { SolicitudesHistoryMovement.StateChanged => "Estado cambiado", SolicitudesHistoryMovement.CommentAdded => "Comentario agregado", SolicitudesHistoryMovement.FileAdded => "Archivo agregado", _ => "Archivo inactivado" },
            [metadata.Attribute("gaia_OperacionId")] = history.OperationId,
            [metadata.Attribute("gaia_TipoMovimiento")] = metadata.EncodedIntegerValue("gaia_TipoMovimiento", (int)history.Movement),
            [metadata.Attribute("gaia_Origen")] = metadata.EncodedIntegerValue("gaia_Origen", (int)history.Origin),
            [metadata.Attribute("gaia_VisibleAlSolicitante")] = history.VisibleToRequester,
            [metadata.Attribute("gaia_FechaEvento")] = history.OccurredAt,
            [metadata.Relationship("gaia_Solicitud", "gaia_solicitud").NavigationProperty + "@odata.bind"] = $"/{request.EntitySetName}({history.RequestId:D})",
            ["statecode"] = 0
        };
        if (history.PreviousStateId.HasValue) {
            payload[metadata.Attribute("gaia_CampoModificado")] = "EstadoActual";
            payload[metadata.Attribute("gaia_ValorAnterior")] = history.PreviousStateId.Value.ToString("D");
            payload[metadata.Attribute("gaia_ValorNuevo")] = history.NewStateId?.ToString("D");
        }
        if (history.ActorThirdPartyId.HasValue)
        {
            var actor = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
            payload[metadata.Relationship("gaia_Actor", "gaia_terceros").NavigationProperty + "@odata.bind"] =
                $"/{actor.EntitySetName}({history.ActorThirdPartyId:D})";
        }
        using var response = await client.PostAsJsonAsync(metadata.EntitySetName, payload, token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Dataverse no pudo registrar la trazabilidad de la solicitud.");
    }
}
