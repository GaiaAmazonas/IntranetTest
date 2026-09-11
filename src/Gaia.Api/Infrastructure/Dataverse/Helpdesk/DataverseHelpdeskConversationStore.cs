using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed class DataverseHelpdeskConversationStore(IDataverseDelegatedClientFactory clients,
    IHelpdeskHistoryStore history) : IHelpdeskConversationStore
{
    public async Task<HelpdeskRequestDetail?> ReadAsync(Guid requestId, Guid actorId, bool managementAccess, CancellationToken token)
    {
        var client=await clients.CreateAsync();
        var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);
        var service=await DataverseMetadataResolver.TableAsync(client,"gaia_servicio",token);
        var state=await DataverseMetadataResolver.TableAsync(client,"gaia_estadosolicitud",token);
        var comment=await DataverseMetadataResolver.TableAsync(client,"gaia_comentariosolicitud",token);
        var fields=RequestFields.From(request);
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{request.EntitySetName}({requestId:D})?$select={fields.Select}",token);
        if(row is null||Int(row.Value,"statecode")!=0)return null;
        var requester=GuidValue(row.Value,fields.Requester);var manager=GuidValue(row.Value,fields.Manager);
        var isRequester=requester==actorId;var isManager=manager==actorId||managementAccess;if(!isRequester&&!isManager)return null;
        var serviceId=GuidValue(row.Value,fields.Service);var stateId=GuidValue(row.Value,fields.State);
        var serviceRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{service.EntitySetName}({serviceId:D})?$select={service.PrimaryNameAttribute}",token);
        var allow=state.Attribute("gaia_PermiteComentarioSolicitante");var color=state.Attribute("gaia_Color");
        var stateRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{state.EntitySetName}({stateId:D})?$select={state.PrimaryNameAttribute},{allow},{color}",token);
        if(serviceRow is null||stateRow is null)return null;
        var requestLookup=comment.Attribute("gaia_Solicitud");var visibility=comment.Attribute("gaia_Visibilidad");var author=comment.Attribute("gaia_Autor");
        var content=comment.Attribute("gaia_Contenido");var published=comment.Attribute("gaia_FechaPublicacion");
        var filter=$"_{requestLookup}_value eq {requestId:D} and statecode eq 0"+(isManager?"":$" and {visibility} eq {comment.EncodedIntegerLiteral("gaia_Visibilidad",299540080)}");
        var rows=await DataverseJson.ReadAllAsync(client,$"{comment.EntitySetName}?$select={comment.PrimaryIdAttribute},{content},{published},{visibility},_{author}_value&$filter={filter}&$orderby={published} asc",token);
        var comments=rows.Select(item=>new HelpdeskComment(GuidValue(item,comment.PrimaryIdAttribute),Text(item,content)??"",Date(item,published)??DateTimeOffset.MinValue,Int(item,visibility)==299540081,GuidValue(item,author)==actorId,GuidValue(item,author)==requester?"Solicitante":"Equipo Gaia")).ToArray();
        var stateName=Text(stateRow.Value,state.PrimaryNameAttribute)??"Sin estado";
        var requesterCanReply=isRequester&&(stateName.Contains("devuelt",StringComparison.OrdinalIgnoreCase)||stateName.Contains("espera del solicitante",StringComparison.OrdinalIgnoreCase));
        var transitions=isManager
            ? await ReadTransitions(client,state,stateId,299540011,token)
            : requesterCanReply
                ? await ReadTransitions(client,state,stateId,299540010,token)
                : [];
        return new(requestId,Text(row.Value,request.PrimaryNameAttribute)??"",Text(row.Value,fields.Subject)??"",Text(row.Value,fields.Description)??"",Text(serviceRow.Value,service.PrimaryNameAttribute)??"Servicio",stateName,Text(stateRow.Value,color),Date(row.Value,fields.Submitted),DateOnlyValue(row.Value,fields.Due),isManager||requesterCanReply,isManager,comments,transitions);
    }

    public async Task<HelpdeskComment> AddAsync(Guid requestId,Guid actorId,string content,bool internalOnly,bool managementAccess,DateTimeOffset now,CancellationToken token)
    {
        var detail=await ReadAsync(requestId,actorId,managementAccess,token)??throw new KeyNotFoundException("La solicitud no existe o no tienes acceso.");
        if(!detail.AllowsRequesterComments)throw new InvalidOperationException("El estado actual no permite comentarios del solicitante.");
        if(internalOnly&&!detail.IsManager)throw new UnauthorizedAccessException("Solo el responsable puede publicar notas internas.");
        var client=await clients.CreateAsync();var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);var comment=await DataverseMetadataResolver.TableAsync(client,"gaia_comentariosolicitud",token);var actor=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);
        var policyRequest=await DataverseMetadataResolver.ReadOneAsync(client,$"{request.EntitySetName}({requestId:D})?$select=_{request.Attribute("gaia_Solicitante")}_value",token);
        var isRequester=policyRequest is not null&&GuidValue(policyRequest.Value,request.Attribute("gaia_Solicitante"))==actorId;
        var payload=new Dictionary<string,object?>{{comment.PrimaryNameAttribute,$"Comentario {now:yyyy-MM-dd HH:mm}"},{comment.Attribute("gaia_Tipo"),comment.EncodedIntegerValue("gaia_Tipo",internalOnly?299540074:isRequester?299540072:299540073)},{comment.Attribute("gaia_Visibilidad"),comment.EncodedIntegerValue("gaia_Visibilidad",internalOnly?299540081:299540080)},{comment.Attribute("gaia_Contenido"),content},{comment.Attribute("gaia_FechaPublicacion"),now},{comment.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind",$"/{request.EntitySetName}({requestId:D})"},{comment.Relationship("gaia_Autor","gaia_terceros").NavigationProperty+"@odata.bind",$"/{actor.EntitySetName}({actorId:D})"},{"statecode",0}};
        using var response=await client.PostAsJsonAsync(comment.EntitySetName,payload,token);if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Dataverse no pudo registrar el comentario.");var id=CreatedId(response);
        try
        {
            await history.AppendAsync(new(requestId,actorId,id.ToString("D"),HelpdeskHistoryMovement.CommentAdded,isRequester?HelpdeskHistoryOrigin.RequesterPortal:HelpdeskHistoryOrigin.HelpdeskAdministration,true,now),token);
        }
        catch
        {
            using var deactivate=new HttpRequestMessage(HttpMethod.Patch,$"{comment.EntitySetName}({id:D})"){Content=JsonContent.Create(new Dictionary<string,object?>{{"statecode",1}})};
            deactivate.Headers.TryAddWithoutValidation("If-Match","*");
            using var ignored=await client.SendAsync(deactivate,CancellationToken.None);
            throw;
        }
        return new(id,content,now,internalOnly,true,isRequester?"Solicitante":"Equipo Gaia");
    }

    public async Task<HelpdeskRequestDetail> TransitionAsync(Guid requestId,Guid actorId,ApplyHelpdeskTransition command,bool managementAccess,DateTimeOffset now,CancellationToken token)
    {
        var detail=await ReadAsync(requestId,actorId,managementAccess,token)??throw new KeyNotFoundException("La solicitud no existe o no tienes acceso.");
        var allowed=detail.Transitions.SingleOrDefault(item=>item.Id==command.TransitionId)??throw new InvalidOperationException("La transición no está permitida.");
        if(allowed.RequiresComment&&string.IsNullOrWhiteSpace(command.Comment))throw new ArgumentException("La transición requiere un comentario.");
        if(allowed.RequiresReason&&string.IsNullOrWhiteSpace(command.Reason))throw new ArgumentException("La transición requiere un motivo.");
        if(allowed.RequiresSolution&&string.IsNullOrWhiteSpace(command.Solution))throw new ArgumentException("La transición requiere una solución.");
        if(allowed.RequestsRating&&command.Rating is not (>=1 and <=5))throw new ArgumentException("Selecciona una calificación entre 1 y 5.");
        var client=await clients.CreateAsync();var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);var state=await DataverseMetadataResolver.TableAsync(client,"gaia_estadosolicitud",token);var transition=await DataverseMetadataResolver.TableAsync(client,"gaia_transicionestadosolicitud",token);
        var action=transition.Attribute("gaia_AccionSLA");var directReturn=allowed.Id==allowed.TargetStateId;var row=directReturn?null:await DataverseMetadataResolver.ReadOneAsync(client,$"{transition.EntitySetName}({command.TransitionId:D})?$select={action}",token);if(!directReturn&&row is null)throw new InvalidOperationException("La transición ya no está disponible.");
        var stateRelationship=request.Relationship("gaia_EstadoActual","gaia_estadosolicitud");var payload=new Dictionary<string,object?>{{stateRelationship.NavigationProperty+"@odata.bind",$"/{state.EntitySetName}({allowed.TargetStateId:D})"}};
        switch(directReturn?299540022:Int(row!.Value,action)){case 299540021:payload[request.Attribute("gaia_FechaInicioSLA")]=now;break;case 299540022:payload[request.Attribute("gaia_FechaInicioPausaSLA")]=now;break;case 299540023:payload[request.Attribute("gaia_FechaInicioPausaSLA")]=null;break;case 299540024:payload[request.Attribute("gaia_FechaResolucionActual")]=now;if(!string.IsNullOrWhiteSpace(command.Solution))payload[request.Attribute("gaia_ResumenSolucion")]=command.Solution.Trim();break;}
        // El solicitante solo puede comentar mientras el caso está esperando su respuesta.
        // Registra la atención antes de reanudar el flujo para no invalidar esa autorización.
        if(!managementAccess&&!string.IsNullOrWhiteSpace(command.Comment))
            await AddAsync(requestId,actorId,command.Comment.Trim(),false,false,now,token);
        using var patch=new HttpRequestMessage(HttpMethod.Patch,$"{request.EntitySetName}({requestId:D})"){Content=JsonContent.Create(payload)};patch.Headers.TryAddWithoutValidation("If-Match","*");using var response=await client.SendAsync(patch,token);if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Dataverse no pudo cambiar el estado.");
        if(allowed.RequestsRating)await AddRating(client,requestId,actorId,command.Rating!.Value,command.RatingComment?.Trim(),now,token);
        if(managementAccess&&!string.IsNullOrWhiteSpace(command.Comment))await AddAsync(requestId,actorId,command.Comment.Trim(),false,true,now,token);
        await history.AppendAsync(new(requestId,actorId,command.TransitionId.ToString("D"),HelpdeskHistoryMovement.StateChanged,detail.IsManager?HelpdeskHistoryOrigin.HelpdeskAdministration:HelpdeskHistoryOrigin.RequesterPortal,true,now),token);
        return await ReadAsync(requestId,actorId,managementAccess,token)??throw new InvalidOperationException("No fue posible confirmar el nuevo estado.");
    }

    static async Task<IReadOnlyList<HelpdeskTransition>> ReadTransitions(HttpClient client,DataverseTableMetadata state,Guid stateId,int actor,CancellationToken token)
    {
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_transicionestadosolicitud",token);var origin=table.Attribute("gaia_EstadoOrigen");var target=table.Attribute("gaia_EstadoDestino");var actorField=table.Attribute("gaia_ActorAutorizado");var comment=table.Attribute("gaia_RequiereComentario");var reason=table.Attribute("gaia_RequiereMotivo");var solution=table.Attribute("gaia_RequiereSolucion");var rating=table.Attribute("gaia_SolicitaCalificacion");
        var rows=await DataverseJson.ReadAllAsync(client,$"{table.EntitySetName}?$select={table.PrimaryIdAttribute},_{target}_value,{comment},{reason},{solution},{rating}&$filter=statecode eq 0 and _{origin}_value eq {stateId:D} and {actorField} eq {table.EncodedIntegerLiteral("gaia_ActorAutorizado",actor)}",token);var result=new List<HelpdeskTransition>();
        foreach(var row in rows){var targetId=GuidValue(row,target);var targetRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{state.EntitySetName}({targetId:D})?$select={state.PrimaryNameAttribute}",token);if(targetRow is not null)result.Add(new(GuidValue(row,table.PrimaryIdAttribute),targetId,Text(targetRow.Value,state.PrimaryNameAttribute)??"Estado",Bool(row,comment),Bool(row,reason),Bool(row,solution),Bool(row,rating)));}
        var current=await DataverseMetadataResolver.ReadOneAsync(client,$"{state.EntitySetName}({stateId:D})?$select={state.PrimaryNameAttribute}",token);var currentName=current is null?"":Text(current.Value,state.PrimaryNameAttribute)??"";if(currentName.Contains("radicad",StringComparison.OrdinalIgnoreCase)&&!result.Any(item=>item.TargetState.Contains("solicitante",StringComparison.OrdinalIgnoreCase)||item.TargetState.Contains("devuelt",StringComparison.OrdinalIgnoreCase))){var stateRows=await DataverseJson.ReadAllAsync(client,$"{state.EntitySetName}?$select={state.PrimaryIdAttribute},{state.PrimaryNameAttribute}&$filter=statecode eq 0",token);var returned=stateRows.FirstOrDefault(item=>{var name=Text(item,state.PrimaryNameAttribute)??"";return name.Contains("espera del solicitante",StringComparison.OrdinalIgnoreCase)||name.Contains("devuelt",StringComparison.OrdinalIgnoreCase);});if(returned.ValueKind!=JsonValueKind.Undefined){var returnedId=GuidValue(returned,state.PrimaryIdAttribute);result.Add(new(returnedId,returnedId,"Devolver al solicitante",true,false,false,false));}}return result;
    }

    static async Task AddRating(HttpClient client,Guid requestId,Guid actorId,int value,string? comment,DateTimeOffset now,CancellationToken token){var rating=await DataverseMetadataResolver.TableAsync(client,"gaia_calificacionsolicitud",token);var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);var actor=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);var payload=new Dictionary<string,object?>{{rating.PrimaryNameAttribute,$"Calificación {requestId:D}"},{rating.Attribute("gaia_Calificacion"),value},{rating.Attribute("gaia_Comentario"),comment},{rating.Attribute("gaia_FechaCalificacion"),now},{rating.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind",$"/{request.EntitySetName}({requestId:D})"},{rating.Relationship("gaia_CalificadoPor","gaia_terceros").NavigationProperty+"@odata.bind",$"/{actor.EntitySetName}({actorId:D})"},{"statecode",0}};using var response=await client.PostAsJsonAsync(rating.EntitySetName,payload,token);if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Dataverse no pudo registrar la calificación.");}

    static string? Text(JsonElement x,string p)=>x.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    static int Int(JsonElement x,string p)=>DataverseJson.OptionalEncodedInt32(x,p)??0;
    static bool Bool(JsonElement x,string p)=>x.TryGetProperty(p,out var v)&&v.ValueKind==JsonValueKind.True;
    static Guid GuidValue(JsonElement x,string lookup)=>Guid.TryParse(Text(x,lookup)??Text(x,lookup.StartsWith('_')?lookup:$"_{lookup}_value"),out var id)?id:Guid.Empty;
    static DateTimeOffset? Date(JsonElement x,string p)=>DateTimeOffset.TryParse(Text(x,p),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var d)?d:null;
    static DateOnly? DateOnlyValue(JsonElement x,string p)=>DateOnly.TryParse(Text(x,p),out var d)?d:null;
    static Guid CreatedId(HttpResponseMessage r){var uri=r.Headers.TryGetValues("OData-EntityId",out var values)?values.SingleOrDefault():null;var m=Regex.Match(uri??"",@"\(([0-9a-f-]{36})\)$");return m.Success?Guid.Parse(m.Groups[1].Value):throw new InvalidOperationException("Dataverse no devolvió el comentario creado.");}
    sealed record RequestFields(string Subject,string Description,string Submitted,string Due,string Service,string State,string Requester,string Manager){public string Select=>string.Join(',',"statecode",Subject,Description,Submitted,Due,$"_{Service}_value",$"_{State}_value",$"_{Requester}_value",$"_{Manager}_value");public static RequestFields From(DataverseTableMetadata m)=>new(m.Attribute("gaia_Asunto"),m.Attribute("gaia_DescripcionInicial"),m.Attribute("gaia_FechaRadicacion"),m.Attribute("gaia_FechaLimiteActual"),m.Attribute("gaia_Servicio"),m.Attribute("gaia_EstadoActual"),m.Attribute("gaia_Solicitante"),m.Attribute("gaia_ResponsableInterno"));}
}
