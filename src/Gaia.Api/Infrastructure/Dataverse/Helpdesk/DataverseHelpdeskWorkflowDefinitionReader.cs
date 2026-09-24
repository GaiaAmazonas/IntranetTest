using System.Text.Json;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed class DataverseHelpdeskWorkflowDefinitionReader(IDataverseDelegatedClientFactory clients)
{
    public async Task<HelpdeskWorkflowDefinition?> ReadAsync(Guid flowId,CancellationToken token)
    {
        if(flowId==Guid.Empty)return null;
        var client=await clients.CreateAsync();
        var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);
        var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);
        var route=await DataverseMetadataResolver.TableAsync(client,"gaia_rutaflujo",token);
        var serviceRelation=flow.Relationship("gaia_Servicio","gaia_servicio");
        var flowRow=await DataverseMetadataResolver.ReadOneAsync(client,
            $"{flow.EntitySetName}({flowId:D})?$select={flow.PrimaryIdAttribute},{flow.Attribute("gaia_Version")},{flow.Attribute("gaia_EstadoFlujo")},_{serviceRelation.ReferencingAttribute}_value,statecode",token);
        if(flowRow is null||Int(flowRow.Value,"statecode")!=0)return null;

        var stepFlow=step.Relationship("gaia_FlujoGestion","gaia_flujogestion");
        var unit=step.Relationship("gaia_UnidadDestino","gaia_organizacion");
        var person=step.Relationship("gaia_PersonaDestino","gaia_terceros");
        var stepRows=await DataverseJson.ReadAllAsync(client,
            $"{step.EntitySetName}?$select={step.PrimaryIdAttribute},{step.Attribute("gaia_Codigo")},{step.Attribute("gaia_TipoPaso")},{step.Attribute("gaia_Orden")},{step.Attribute("gaia_EsInicial")},{step.Attribute("gaia_EsFinal")},{step.Attribute("gaia_EsEntradaReapertura")},{step.Attribute("gaia_EstrategiaAsignacion")},{step.Attribute("gaia_ReglaActivacion")},{step.Attribute("gaia_RequiereDecision")},{step.Attribute("gaia_RequiereObservacion")},{step.Attribute("gaia_RequiereArchivo")},{step.Attribute("gaia_PermiteDevolverSolicitante")},{step.Attribute("gaia_DiasObjetivo")},{step.Attribute("gaia_Activo")},_{unit.ReferencingAttribute}_value,_{person.ReferencingAttribute}_value&$filter=statecode eq 0 and _{stepFlow.ReferencingAttribute}_value eq {flowId:D}",token);
        var steps=stepRows.Select(x=>new HelpdeskWorkflowStep(
            RequiredGuid(x,step.PrimaryIdAttribute),Text(x,step.Attribute("gaia_Codigo"))??"",
            Int(x,step.Attribute("gaia_TipoPaso")),Int(x,step.Attribute("gaia_Orden")),
            Bool(x,step.Attribute("gaia_EsInicial")),Bool(x,step.Attribute("gaia_EsFinal")),
            Bool(x,step.Attribute("gaia_EsEntradaReapertura")),Int(x,step.Attribute("gaia_EstrategiaAsignacion")),
            OptionalGuid(x,$"_{unit.ReferencingAttribute}_value"),OptionalGuid(x,$"_{person.ReferencingAttribute}_value"),
            Int(x,step.Attribute("gaia_ReglaActivacion")),Bool(x,step.Attribute("gaia_RequiereDecision")),
            Bool(x,step.Attribute("gaia_RequiereObservacion")),Bool(x,step.Attribute("gaia_RequiereArchivo")),
            Bool(x,step.Attribute("gaia_PermiteDevolverSolicitante")),NullableInt(x,step.Attribute("gaia_DiasObjetivo")),
            Bool(x,step.Attribute("gaia_Activo")))).OrderBy(x=>x.Order).ToArray();

        var routeFlow=route.Relationship("gaia_FlujoGestion","gaia_flujogestion");
        var source=route.Relationship("gaia_PasoOrigen","gaia_pasoflujo");
        var target=route.Relationship("gaia_PasoDestino","gaia_pasoflujo");
        var routeRows=await DataverseJson.ReadAllAsync(client,
            $"{route.EntitySetName}?$select={route.PrimaryIdAttribute},{route.Attribute("gaia_Codigo")},{route.Attribute("gaia_ResultadoRequerido")},{route.Attribute("gaia_Orden")},{route.Attribute("gaia_Activa")},_{source.ReferencingAttribute}_value,_{target.ReferencingAttribute}_value&$filter=statecode eq 0 and _{routeFlow.ReferencingAttribute}_value eq {flowId:D}",token);
        var routes=routeRows.Select(x=>new HelpdeskWorkflowRoute(
            RequiredGuid(x,route.PrimaryIdAttribute),Text(x,route.Attribute("gaia_Codigo"))??"",
            RequiredGuid(x,$"_{source.ReferencingAttribute}_value"),RequiredGuid(x,$"_{target.ReferencingAttribute}_value"),
            Int(x,route.Attribute("gaia_ResultadoRequerido")),Int(x,route.Attribute("gaia_Orden")),
            Bool(x,route.Attribute("gaia_Activa")))).OrderBy(x=>x.Order).ToArray();
        var row=flowRow.Value;
        return new(flowId,RequiredGuid(row,$"_{serviceRelation.ReferencingAttribute}_value"),
            Int(row,flow.Attribute("gaia_Version")),Int(row,flow.Attribute("gaia_EstadoFlujo")),steps,routes);
    }

    public async Task<IReadOnlyList<string>> ValidateForPublicationAsync(Guid flowId,CancellationToken token)
    {
        var flow=await ReadAsync(flowId,token)??throw new KeyNotFoundException("El flujo no existe o está inactivo.");
        return HelpdeskWorkflowRules.ValidateForPublication(flow with{Status=HelpdeskWorkflowValues.Published});
    }

    private static string? Text(JsonElement row,string name)=>row.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static int Int(JsonElement row,string name)=>DataverseJson.OptionalInt32(row,name)??0;
    private static int? NullableInt(JsonElement row,string name)=>DataverseJson.OptionalInt32(row,name);
    private static bool Bool(JsonElement row,string name)=>row.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.True;
    private static Guid RequiredGuid(JsonElement row,string name)=>OptionalGuid(row,name)??throw new InvalidOperationException($"Dataverse no devolvió {name}.");
    private static Guid? OptionalGuid(JsonElement row,string name)=>Guid.TryParse(Text(row,name),out var id)?id:null;
}
