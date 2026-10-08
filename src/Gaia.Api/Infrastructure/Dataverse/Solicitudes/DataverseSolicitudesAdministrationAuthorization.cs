using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Gaia.Modules.Security;
using Gaia.Modules.Solicitudes;

namespace Gaia.Api.Infrastructure.Dataverse.Solicitudes;

internal sealed class DataverseSolicitudesAdministrationAuthorization(
    IDataverseDelegatedClientFactory clients,ISecurityStore security):ISolicitudesAdministrationAuthorization
{
    public async Task<SolicitudesAdministrationScope> ResolveAsync(ClaimsPrincipal principal,CancellationToken token)
    {
        var context=await security.GetOrProvisionAsync(principal,token);
        if(!context.User.IsActive)throw new UnauthorizedAccessException("El usuario no tiene acceso activo.");
        if(context.IsGlobalAdministrator)return new(true,new HashSet<Guid>());
        var thirdPartyId=context.User.ThirdPartyId??throw new UnauthorizedAccessException("El usuario no está asociado con un tercero activo.");
        var client=await clients.CreateAsync();
        var assignment=await DataverseMetadataResolver.TableAsync(client,"gaia_asignacionorganizacional",token);
        var third=assignment.RelationshipTo("gaia_terceros");
        var unit=assignment.RelationshipTo("gaia_organizacion");
        var start=Optional(assignment,"gaia_FechaInicio","gaia_VigenteDesde");
        var end=Optional(assignment,"gaia_FechaFin","gaia_VigenteHasta");
        var today=DateOnly.FromDateTime(DateTime.UtcNow);
        var select=$"_{unit.ReferencingAttribute}_value"+(start is null?"":$",{start}")+(end is null?"":$",{end}");
        var rows=await DataverseJson.ReadAllAsync(client,$"{assignment.EntitySetName}?$select={select}&$filter=statecode eq 0 and _{third.ReferencingAttribute}_value eq {thirdPartyId:D}",token);
        var units=rows.Where(row=>InForce(row,start,end,today)).Select(row=>GuidValue(row,$"_{unit.ReferencingAttribute}_value")).Where(id=>id!=Guid.Empty).ToHashSet();
        return new(false,units);
    }

    public Task DemandUnitAsync(SolicitudesAdministrationScope scope,Guid unitId,CancellationToken token)
    {
        Demand(scope,unitId);return Task.CompletedTask;
    }

    public async Task DemandServiceAsync(SolicitudesAdministrationScope scope,Guid serviceId,CancellationToken token)
    {
        if(scope.IsGlobalAdministrator)return;
        var client=await clients.CreateAsync();var service=await DataverseMetadataResolver.TableAsync(client,"gaia_servicio",token);
        var unit=service.Relationship("gaia_UnidadResponsable","gaia_organizacion");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{service.EntitySetName}({serviceId:D})?$select={service.PrimaryIdAttribute},_{unit.ReferencingAttribute}_value",token)
            ??throw new KeyNotFoundException("El servicio no existe.");
        Demand(scope,GuidValue(row,$"_{unit.ReferencingAttribute}_value"));
    }

    public async Task DemandFormAsync(SolicitudesAdministrationScope scope,Guid formId,CancellationToken token)
    {
        if(scope.IsGlobalAdministrator)return;
        var client=await clients.CreateAsync();var form=await DataverseMetadataResolver.TableAsync(client,"gaia_formularioservicio",token);
        var service=form.Relationship("gaia_Servicio","gaia_servicio");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{form.EntitySetName}({formId:D})?$select={form.PrimaryIdAttribute},_{service.ReferencingAttribute}_value",token)
            ??throw new KeyNotFoundException("El formulario no existe.");
        await DemandServiceAsync(scope,GuidValue(row,$"_{service.ReferencingAttribute}_value"),token);
    }

    public async Task DemandWorkflowAsync(SolicitudesAdministrationScope scope,Guid flowId,CancellationToken token)
    {
        if(scope.IsGlobalAdministrator)return;
        var client=await clients.CreateAsync();var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);
        var service=flow.Relationship("gaia_Servicio","gaia_servicio");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{flow.EntitySetName}({flowId:D})?$select={flow.PrimaryIdAttribute},_{service.ReferencingAttribute}_value",token)
            ??throw new KeyNotFoundException("El flujo no existe.");
        await DemandServiceAsync(scope,GuidValue(row,$"_{service.ReferencingAttribute}_value"),token);
    }

    public async Task DemandStepAsync(SolicitudesAdministrationScope scope,Guid stepId,CancellationToken token)
    {
        if(scope.IsGlobalAdministrator)return;
        var client=await clients.CreateAsync();var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);
        var flow=step.Relationship("gaia_Flujo","gaia_flujogestion");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{step.EntitySetName}({stepId:D})?$select={step.PrimaryIdAttribute},_{flow.ReferencingAttribute}_value",token)
            ??throw new KeyNotFoundException("La etapa no existe.");
        await DemandWorkflowAsync(scope,GuidValue(row,$"_{flow.ReferencingAttribute}_value"),token);
    }

    private static void Demand(SolicitudesAdministrationScope scope,Guid unitId)
    {
        if(!scope.IsGlobalAdministrator&&(unitId==Guid.Empty||!scope.UnitIds.Contains(unitId)))
            throw new UnauthorizedAccessException("El recurso pertenece a otra unidad organizacional.");
    }
    private static bool InForce(JsonElement row,string? start,string? end,DateOnly today)=>(Date(row,start) is not { } from||from<=today)&&(Date(row,end) is not { } until||until>=today);
    private static DateOnly? Date(JsonElement row,string? field)=>field is not null&&row.TryGetProperty(field,out var value)&&DateOnly.TryParse(value.GetString(),CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date:null;
    private static Guid GuidValue(JsonElement row,string field)=>row.TryGetProperty(field,out var value)&&Guid.TryParse(value.GetString(),out var id)?id:Guid.Empty;
    private static string? Optional(DataverseTableMetadata table,params string[] names)=>names.Select(table.OptionalAttribute).FirstOrDefault(value=>value is not null);
}
