using System.Net;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationUnitDeleter(IDataverseDelegatedClientFactory clients):IOrganizationUnitDeleter
{
    public async Task<bool> DeleteAsync(Guid id,CancellationToken token)
    {
        var client=await clients.CreateAsync();
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_organizacion",token);
        using var response=await client.DeleteAsync($"{table.EntitySetName}({id:D})",token);
        if(response.StatusCode==HttpStatusCode.NotFound)return false;
        if(!response.IsSuccessStatusCode)
        {
            var detail=await response.Content.ReadAsStringAsync(token);
            throw new InvalidOperationException($"La unidad no puede eliminarse porque tiene registros relacionados, por ejemplo unidades hijas, asignaciones de personas, servicios, solicitudes o pasos de flujo. Retira primero esas relaciones o inactiva la unidad. Detalle técnico: {detail[..Math.Min(detail.Length,180)]}");
        }
        return true;
    }
}
