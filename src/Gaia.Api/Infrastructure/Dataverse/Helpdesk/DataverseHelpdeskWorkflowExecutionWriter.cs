using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed partial class DataverseHelpdeskWorkflowExecutionWriter(
    IDataverseDelegatedClientFactory clients,
    DataverseHelpdeskWorkflowDefinitionReader definitions) : IHelpdeskWorkflowStore
{
    public Task<HelpdeskWorkflowDefinition?> ReadFlowAsync(Guid flowId,CancellationToken token)=>definitions.ReadAsync(flowId,token);
    public async Task<IReadOnlyList<string>> ValidateForPublicationAsync(Guid flowId,CancellationToken token)
    {
        var errors=(await definitions.ValidateForPublicationAsync(flowId,token)).ToList();
        errors.AddRange(await ValidateStageFormsAsync(flowId,token));
        return errors.Distinct().ToArray();
    }
    public async Task StartForRequestAsync(Guid requestId,Guid flowId,Guid actorId,DateTimeOffset now,CancellationToken token)=>
        _=await StartAsync(requestId,flowId,actorId,now,token);

    public async Task PublishAsync(Guid flowId,Guid actorId,DateTimeOffset now,CancellationToken token)
    {
        var definition=await definitions.ReadAsync(flowId,token)??throw new KeyNotFoundException("El flujo no existe o está inactivo.");
        var errors=HelpdeskWorkflowRules.ValidateForPublication(definition with{Status=HelpdeskWorkflowValues.Published});
        if(errors.Count>0)throw new InvalidOperationException(string.Join(" ",errors));
        var client=await clients.CreateAsync();
        var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);
        var service=await DataverseMetadataResolver.TableAsync(client,"gaia_servicio",token);
        var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);
        var serviceRelation=flow.Relationship("gaia_Servicio","gaia_servicio");
        var status=flow.Attribute("gaia_EstadoFlujo");
        var published=flow.EncodedIntegerValue("gaia_EstadoFlujo",HelpdeskWorkflowValues.Published);
        var previous=await DataverseJson.ReadAllAsync(client,
            $"{flow.EntitySetName}?$select={flow.PrimaryIdAttribute},{status}&$filter=statecode eq 0 and _{serviceRelation.ReferencingAttribute}_value eq {definition.ServiceId:D} and {status} eq {published} and {flow.PrimaryIdAttribute} ne {flowId:D}",token);
        foreach(var row in previous)
            await Patch(client,flow.EntitySetName,RequiredGuid(row,flow.PrimaryIdAttribute),new Dictionary<string,object?>
            {
                [status]=flow.EncodedIntegerValue("gaia_EstadoFlujo",HelpdeskWorkflowValues.Retired)
            },token);
        var publisher=flow.Relationship("gaia_PublicadoPor","gaia_terceros");
        await Patch(client,flow.EntitySetName,flowId,new Dictionary<string,object?>
        {
            [status]=published,
            [flow.Attribute("gaia_FechaPublicacion")]=now,
            [publisher.NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})"
        },token);
        var current=service.Relationship("gaia_FlujoVigente","gaia_flujogestion");
        await Patch(client,service.EntitySetName,definition.ServiceId,new Dictionary<string,object?>
        {
            [current.NavigationProperty+"@odata.bind"]=$"/{flow.EntitySetName}({flowId:D})"
        },token);
    }

    public async Task<IReadOnlyList<HelpdeskWorkflowSummary>> ListAsync(Guid serviceId,CancellationToken token)
    {
        var client=await clients.CreateAsync();var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);var route=await DataverseMetadataResolver.TableAsync(client,"gaia_rutaflujo",token);var relation=flow.Relationship("gaia_Servicio","gaia_servicio");
        var version=flow.Attribute("gaia_Version");var status=flow.Attribute("gaia_EstadoFlujo");var description=flow.Attribute("gaia_Descripcion");var published=flow.Attribute("gaia_FechaPublicacion");
        var rows=await DataverseJson.ReadAllAsync(client,$"{flow.EntitySetName}?$select={flow.PrimaryIdAttribute},{flow.PrimaryNameAttribute},{version},{status},{description},{published}&$filter=statecode eq 0 and _{relation.ReferencingAttribute}_value eq {serviceId:D}&$orderby={version} desc",token);
        var stepFlow=step.Relationship("gaia_FlujoGestion","gaia_flujogestion");var routeFlow=route.Relationship("gaia_FlujoGestion","gaia_flujogestion");
        var stepRows=await DataverseJson.ReadAllAsync(client,$"{step.EntitySetName}?$select={step.PrimaryIdAttribute},_{stepFlow.ReferencingAttribute}_value&$filter=statecode eq 0",token);var routeRows=await DataverseJson.ReadAllAsync(client,$"{route.EntitySetName}?$select={route.PrimaryIdAttribute},_{routeFlow.ReferencingAttribute}_value&$filter=statecode eq 0",token);
        var stepCounts=stepRows.Select(x=>OptionalGuid(x,$"_{stepFlow.ReferencingAttribute}_value")).Where(x=>x.HasValue).GroupBy(x=>x!.Value).ToDictionary(x=>x.Key,x=>x.Count());var routeCounts=routeRows.Select(x=>OptionalGuid(x,$"_{routeFlow.ReferencingAttribute}_value")).Where(x=>x.HasValue).GroupBy(x=>x!.Value).ToDictionary(x=>x.Key,x=>x.Count());
        return rows.Select(x=>{var id=RequiredGuid(x,flow.PrimaryIdAttribute);return new HelpdeskWorkflowSummary(id,serviceId,Int(x,version),Int(x,status),Text(x,flow.PrimaryNameAttribute)??$"Flujo v{Int(x,version)}",Text(x,description),DateTimeValue(x,published),stepCounts.GetValueOrDefault(id),routeCounts.GetValueOrDefault(id));}).ToArray();
    }

    public async Task<Guid> CreateDraftAsync(CreateHelpdeskWorkflowDraft command,CancellationToken token)
    {
        var client=await clients.CreateAsync();var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);var service=await DataverseMetadataResolver.TableAsync(client,"gaia_servicio",token);var existingService=await DataverseMetadataResolver.ReadOneAsync(client,$"{service.EntitySetName}({command.ServiceId:D})?$select={service.PrimaryIdAttribute},statecode",token);if(existingService is null||Int(existingService.Value,"statecode")!=0)throw new ArgumentException("El servicio no existe o está inactivo.");
        var relation=flow.Relationship("gaia_Servicio","gaia_servicio");var version=flow.Attribute("gaia_Version");var status=flow.Attribute("gaia_EstadoFlujo");var rows=await DataverseJson.ReadAllAsync(client,$"{flow.EntitySetName}?$select={flow.PrimaryIdAttribute},{version},{status}&$filter=statecode eq 0 and _{relation.ReferencingAttribute}_value eq {command.ServiceId:D}",token);if(rows.Any(x=>Int(x,status)==HelpdeskWorkflowValues.Draft))throw new InvalidOperationException("Ya existe un flujo borrador para el servicio.");var next=rows.Select(x=>Int(x,version)).DefaultIfEmpty(0).Max()+1;
        var created=await Create(client,flow.EntitySetName,new Dictionary<string,object?>{{flow.PrimaryNameAttribute,command.Name},{version,next},{status,flow.EncodedIntegerValue("gaia_EstadoFlujo",HelpdeskWorkflowValues.Draft)},{flow.Attribute("gaia_Descripcion"),command.Description},{relation.NavigationProperty+"@odata.bind",$"/{service.EntitySetName}({command.ServiceId:D})"},{"statecode",0}},token);
        var source=rows.Where(x=>RequiredGuid(x,flow.PrimaryIdAttribute)!=created&&Int(x,status)!=HelpdeskWorkflowValues.Draft).OrderByDescending(x=>Int(x,version)).FirstOrDefault();
        if(source.ValueKind!=JsonValueKind.Undefined)await CloneWorkflowAsync(RequiredGuid(source,flow.PrimaryIdAttribute),created,token);
        return created;
    }

    public async Task<Guid> SaveStepAsync(Guid flowId,Guid? stepId,SaveHelpdeskWorkflowStep command,CancellationToken token)
    {
        var client=await clients.CreateAsync();await RequireDraft(flowId,client,token);var table=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);var unit=await DataverseMetadataResolver.TableAsync(client,"gaia_organizacion",token);var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);
        if(command.AssignmentStrategy==HelpdeskWorkflowValues.UnitQueue&&!command.UnitId.HasValue)throw new ArgumentException("Selecciona la unidad destino.");if(command.AssignmentStrategy==HelpdeskWorkflowValues.SpecificPerson&&!command.PersonId.HasValue)throw new ArgumentException("Selecciona la persona destino.");
        var payload=new Dictionary<string,object?>{{table.PrimaryNameAttribute,command.Code},{table.Attribute("gaia_Codigo"),command.Code},{table.Attribute("gaia_TipoPaso"),table.EncodedIntegerValue("gaia_TipoPaso",command.Type)},{table.Attribute("gaia_Orden"),command.Order},{table.Attribute("gaia_EsInicial"),command.Initial},{table.Attribute("gaia_EsFinal"),command.Final},{table.Attribute("gaia_EsEntradaReapertura"),command.ReopeningEntry},{table.Attribute("gaia_EstrategiaAsignacion"),table.EncodedIntegerValue("gaia_EstrategiaAsignacion",command.AssignmentStrategy)},{table.Attribute("gaia_ReglaActivacion"),table.EncodedIntegerValue("gaia_ReglaActivacion",command.ActivationRule)},{table.Attribute("gaia_RequiereDecision"),command.RequiresDecision},{table.Attribute("gaia_RequiereObservacion"),command.RequiresObservation},{table.Attribute("gaia_RequiereArchivo"),command.RequiresFile},{table.Attribute("gaia_PermiteDevolverSolicitante"),command.AllowsRequesterReturn},{table.Attribute("gaia_DiasObjetivo"),command.TargetDays},{table.Attribute("gaia_Activo"),command.Active},{table.Relationship("gaia_UnidadDestino","gaia_organizacion").NavigationProperty+"@odata.bind",command.UnitId.HasValue?$"/{unit.EntitySetName}({command.UnitId:D})":null},{table.Relationship("gaia_PersonaDestino","gaia_terceros").NavigationProperty+"@odata.bind",command.PersonId.HasValue?$"/{third.EntitySetName}({command.PersonId:D})":null},{"statecode",command.Active?0:1}};
        if(stepId.HasValue){await Patch(client,table.EntitySetName,stepId.Value,payload,token);return stepId.Value;}payload[table.Relationship("gaia_FlujoGestion","gaia_flujogestion").NavigationProperty+"@odata.bind"]=$"/{flow.EntitySetName}({flowId:D})";return await Create(client,table.EntitySetName,payload,token);
    }

    public async Task<Guid> SaveRouteAsync(Guid flowId,Guid? routeId,SaveHelpdeskWorkflowRoute command,CancellationToken token)
    {
        var client=await clients.CreateAsync();await RequireDraft(flowId,client,token);var table=await DataverseMetadataResolver.TableAsync(client,"gaia_rutaflujo",token);var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);
        var definition=await definitions.ReadAsync(flowId,token)??throw new KeyNotFoundException("El flujo no existe.");if(!definition.Steps.Any(x=>x.Id==command.SourceStepId)||!definition.Steps.Any(x=>x.Id==command.TargetStepId))throw new ArgumentException("Los pasos de la ruta no pertenecen al flujo.");
        var payload=new Dictionary<string,object?>{{table.PrimaryNameAttribute,command.Code},{table.Attribute("gaia_Codigo"),command.Code},{table.Attribute("gaia_ResultadoRequerido"),table.EncodedIntegerValue("gaia_ResultadoRequerido",command.RequiredResult)},{table.Attribute("gaia_Orden"),command.Order},{table.Attribute("gaia_Activa"),command.Active},{table.Relationship("gaia_PasoOrigen","gaia_pasoflujo").NavigationProperty+"@odata.bind",$"/{step.EntitySetName}({command.SourceStepId:D})"},{table.Relationship("gaia_PasoDestino","gaia_pasoflujo").NavigationProperty+"@odata.bind",$"/{step.EntitySetName}({command.TargetStepId:D})"},{"statecode",command.Active?0:1}};
        if(routeId.HasValue){await Patch(client,table.EntitySetName,routeId.Value,payload,token);return routeId.Value;}payload[table.Relationship("gaia_FlujoGestion","gaia_flujogestion").NavigationProperty+"@odata.bind"]=$"/{flow.EntitySetName}({flowId:D})";return await Create(client,table.EntitySetName,payload,token);
    }

    public async Task<HelpdeskRequestWorkflowState?> ReadRequestStateAsync(Guid requestId,Guid actorId,bool managementAccess,CancellationToken token)
    {
        var client=await clients.CreateAsync();var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);var requester=request.Relationship("gaia_Solicitante","gaia_terceros");var requestRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{request.EntitySetName}({requestId:D})?$select=_{requester.ReferencingAttribute}_value,statecode",token)??throw new KeyNotFoundException("La solicitud no existe.");if(!managementAccess&&OptionalGuid(requestRow,$"_{requester.ReferencingAttribute}_value")!=actorId)throw new UnauthorizedAccessException("No puedes consultar el flujo de esta solicitud.");
        var instance=await DataverseMetadataResolver.TableAsync(client,"gaia_instanciaflujo",token);var requestRelation=instance.Relationship("gaia_Solicitud","gaia_solicitud");var flowRelation=instance.Relationship("gaia_FlujoGestion","gaia_flujogestion");var state=instance.Attribute("gaia_Estado");var rows=await DataverseJson.ReadAllAsync(client,$"{instance.EntitySetName}?$select={instance.PrimaryIdAttribute},{state},_{flowRelation.ReferencingAttribute}_value&$filter=statecode eq 0 and _{requestRelation.ReferencingAttribute}_value eq {requestId:D}&$top=2",token);if(rows.Count==0)return null;if(rows.Count>1)throw new InvalidOperationException("La solicitud tiene más de una instancia de flujo.");var instanceRow=rows[0];var instanceId=RequiredGuid(instanceRow,instance.PrimaryIdAttribute);var flowId=RequiredGuid(instanceRow,$"_{flowRelation.ReferencingAttribute}_value");var definition=await definitions.ReadAsync(flowId,token)??throw new InvalidOperationException("No fue posible leer el flujo utilizado.");
        var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);var managementInstance=management.Relationship("gaia_InstanciaFlujo","gaia_instanciaflujo");var managementStep=management.Relationship("gaia_PasoFlujo","gaia_pasoflujo");var unit=management.Relationship("gaia_UnidadResponsable","gaia_organizacion");var responsible=management.Relationship("gaia_Responsable","gaia_terceros");var status=management.Attribute("gaia_Estado");var result=management.Attribute("gaia_Resultado");var number=management.Attribute("gaia_NumeroEjecucion");var observation=management.Attribute("gaia_Observacion");var available=management.Attribute("gaia_FechaDisponibilidad");var completed=management.Attribute("gaia_FechaFinalizacion");
        var managementRows=await DataverseJson.ReadAllAsync(client,$"{management.EntitySetName}?$select={management.PrimaryIdAttribute},{number},{status},{result},{observation},{available},{completed},_{managementStep.ReferencingAttribute}_value,_{unit.ReferencingAttribute}_value,_{responsible.ReferencingAttribute}_value&$filter=statecode eq 0 and _{managementInstance.ReferencingAttribute}_value eq {instanceId:D}&$orderby={number} asc",token);
        var values=managementRows.Select(x=>{var stepId=RequiredGuid(x,$"_{managementStep.ReferencingAttribute}_value");var configured=definition.Steps.Single(s=>s.Id==stepId);return new HelpdeskWorkflowManagementItem(RequiredGuid(x,management.PrimaryIdAttribute),stepId,configured.Code,Int(x,number),Int(x,status),NullableInt(x,result),Text(x,observation),OptionalGuid(x,$"_{unit.ReferencingAttribute}_value"),OptionalGuid(x,$"_{responsible.ReferencingAttribute}_value"),DateTimeValue(x,available),DateTimeValue(x,completed),configured.RequiresDecision,configured.RequiresObservation,configured.RequiresFile,configured.AllowsRequesterReturn,configured.Final);}).ToArray();
        return new(instanceId,flowId,definition.Version,Int(instanceRow,state),values);
    }

    public async Task<IReadOnlyList<HelpdeskWorkflowQueueItem>> ReadWorkQueueAsync(Guid actorId,string queue,CancellationToken token)
    {
        var client=await clients.CreateAsync();
        var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);
        var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);
        var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);
        var requestRelation=management.Relationship("gaia_Solicitud","gaia_solicitud");
        var stepRelation=management.Relationship("gaia_PasoFlujo","gaia_pasoflujo");
        var unitRelation=management.Relationship("gaia_UnidadResponsable","gaia_organizacion");
        var responsibleRelation=management.Relationship("gaia_Responsable","gaia_terceros");
        var status=management.Attribute("gaia_Estado");var execution=management.Attribute("gaia_NumeroEjecucion");var available=management.Attribute("gaia_FechaDisponibilidad");
        var actorUnits=await ActorUnits(client,actorId,token);
        var active=$"({status} eq {management.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.ManagementAvailable)} or {status} eq {management.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.ManagementInProgress)})";
        string access;
        if(queue=="mine")access=$"_{responsibleRelation.ReferencingAttribute}_value eq {actorId:D}";
        else if(queue=="waiting")access=$"{status} eq {management.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.ManagementWaiting)}";
        else
        {
            if(queue=="unit"&&actorUnits.Count==0)return [];
            var units=string.Join(" or ",actorUnits.Select(id=>$"_{unitRelation.ReferencingAttribute}_value eq {id:D}"));
            access=actorUnits.Count==0?$"_{responsibleRelation.ReferencingAttribute}_value eq {actorId:D}":$"(_{responsibleRelation.ReferencingAttribute}_value eq {actorId:D} or (_{responsibleRelation.ReferencingAttribute}_value eq null and ({units})))";
        }
        var filter=queue switch{"mine"=>$"statecode eq 0 and {active} and ({access})","unit"=>$"statecode eq 0 and {active} and _{responsibleRelation.ReferencingAttribute}_value eq null and ({string.Join(" or ",actorUnits.Select(id=>$"_{unitRelation.ReferencingAttribute}_value eq {id:D}"))})","approvals"=>$"statecode eq 0 and {active} and ({access})",_=>$"statecode eq 0 and ({access})"};
        var requestNav=requestRelation.NavigationProperty;var stepNav=stepRelation.NavigationProperty;
        var subject=request.Attribute("gaia_Asunto");var code=step.Attribute("gaia_Codigo");var decision=step.Attribute("gaia_RequiereDecision");
        var path=$"{management.EntitySetName}?$select={management.PrimaryIdAttribute},{execution},{status},{available},_{unitRelation.ReferencingAttribute}_value,_{responsibleRelation.ReferencingAttribute}_value&$expand={requestNav}($select={request.PrimaryIdAttribute},{request.PrimaryNameAttribute},{subject}),{stepNav}($select={step.PrimaryIdAttribute},{code},{decision})&$filter={Uri.EscapeDataString(filter)}&$orderby={available} asc&$top=200";
        var rows=await DataverseJson.ReadAllAsync(client,path,token);
        return rows.Select(row=>
        {
            var requestRow=Nested(row,requestNav);var stepRow=Nested(row,stepNav);
            return new HelpdeskWorkflowQueueItem(RequiredGuid(row,management.PrimaryIdAttribute),RequiredGuid(requestRow,request.PrimaryIdAttribute),Text(requestRow,request.PrimaryNameAttribute)??"",Text(requestRow,subject)??"",Text(stepRow,code)??"Paso",Int(row,execution),Int(row,status),OptionalGuid(row,$"_{unitRelation.ReferencingAttribute}_value"),OptionalGuid(row,$"_{responsibleRelation.ReferencingAttribute}_value"),DateTimeValue(row,available),Bool(stepRow,decision));
        }).Where(item=>queue!="approvals"||item.RequiresDecision).ToArray();
    }

    public async Task<Guid> StartAsync(Guid requestId,Guid flowId,Guid actorId,DateTimeOffset now,CancellationToken token)
    {
        var definition=await definitions.ReadAsync(flowId,token)??throw new KeyNotFoundException("El flujo no existe.");
        if(definition.Status!=HelpdeskWorkflowValues.Published)throw new InvalidOperationException("El flujo debe estar publicado.");
        var validation=HelpdeskWorkflowRules.ValidateForPublication(definition);
        if(validation.Count>0)throw new InvalidOperationException(string.Join(" ",validation));

        var client=await clients.CreateAsync();
        var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);
        var instance=await DataverseMetadataResolver.TableAsync(client,"gaia_instanciaflujo",token);
        var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);
        var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);
        var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);
        var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);
        var unit=await DataverseMetadataResolver.TableAsync(client,"gaia_organizacion",token);

        var requestRelation=instance.Relationship("gaia_Solicitud","gaia_solicitud");
        var existing=await DataverseJson.ReadAllAsync(client,
            $"{instance.EntitySetName}?$select={instance.PrimaryIdAttribute}&$filter=_{requestRelation.ReferencingAttribute}_value eq {requestId:D}&$top=2",token);
        if(existing.Count>1)throw new InvalidOperationException("La solicitud tiene más de una instancia de flujo.");
        if(existing.Count==1)return RequiredGuid(existing[0],instance.PrimaryIdAttribute);

        var requestFlow=request.Relationship("gaia_FlujoUtilizado","gaia_flujogestion");
        var requestOwner=request.Relationship("gaia_ResponsableInterno","gaia_terceros");
        var requestRow=await DataverseMetadataResolver.ReadOneAsync(client,
            $"{request.EntitySetName}({requestId:D})?$select={request.PrimaryIdAttribute},_{requestOwner.ReferencingAttribute}_value,statecode",token)
            ??throw new KeyNotFoundException("La solicitud no existe.");
        if(Int(requestRow,"statecode")!=0)throw new InvalidOperationException("La solicitud está inactiva.");
        var ownerId=OptionalGuid(requestRow,$"_{requestOwner.ReferencingAttribute}_value");

        using(var patch=new HttpRequestMessage(HttpMethod.Patch,$"{request.EntitySetName}({requestId:D})")
        {Content=JsonContent.Create(new Dictionary<string,object?>{
            [requestFlow.NavigationProperty+"@odata.bind"]=$"/{flow.EntitySetName}({flowId:D})"})})
        {patch.Headers.TryAddWithoutValidation("If-Match","*");using var response=await client.SendAsync(patch,token);await Ensure(response,token);}

        var flowRelation=instance.Relationship("gaia_FlujoGestion","gaia_flujogestion");
        var instancePayload=new Dictionary<string,object?>{
            [instance.PrimaryNameAttribute]=$"{requestId:D} · flujo {definition.Version}",
            [instance.Attribute("gaia_Estado")]=instance.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.InstanceRunning),
            [instance.Attribute("gaia_FechaInicio")]=now,
            [requestRelation.NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",
            [flowRelation.NavigationProperty+"@odata.bind"]=$"/{flow.EntitySetName}({flowId:D})",
            ["statecode"]=0};
        var instanceId=await Create(client,instance.EntitySetName,instancePayload,token);

        foreach(var initial in HelpdeskWorkflowRules.ActivateInitial(definition,[]))
        {
            var configured=definition.Steps.Single(x=>x.Id==initial.StepId);
            await EnsureManagement(client,requestId,instanceId,configured,initial.Execution,initial.Status,ownerId,
                management,request,instance,step,third,unit,now,token);
        }
        return instanceId;
    }

    public async Task CompleteAsync(Guid actorId,CompleteHelpdeskManagement command,DateTimeOffset now,CancellationToken token)
    {
        var client=await clients.CreateAsync();
        var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);
        var instance=await DataverseMetadataResolver.TableAsync(client,"gaia_instanciaflujo",token);
        var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);
        var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);
        var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);
        var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);
        var unit=await DataverseMetadataResolver.TableAsync(client,"gaia_organizacion",token);
        var history=await DataverseMetadataResolver.TableAsync(client,"gaia_historialsolicitud",token);
        var operation=history.Attribute("gaia_OperacionId");
        var escaped=command.OperationId.Replace("'","''",StringComparison.Ordinal);
        var prior=await DataverseJson.ReadAllAsync(client,$"{history.EntitySetName}?$select={history.PrimaryIdAttribute}&$filter={operation} eq '{escaped}'&$top=1",token);
        if(prior.Count>0)return;

        var managementInstance=management.Relationship("gaia_InstanciaFlujo","gaia_instanciaflujo");
        var managementStep=management.Relationship("gaia_PasoFlujo","gaia_pasoflujo");
        var managementRequest=management.Relationship("gaia_Solicitud","gaia_solicitud");
        var responsible=management.Relationship("gaia_Responsable","gaia_terceros");
        var responsibleUnit=management.Relationship("gaia_UnidadResponsable","gaia_organizacion");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,
            $"{management.EntitySetName}({command.ManagementId:D})?$select={management.PrimaryIdAttribute},{management.Attribute("gaia_Estado")},_{managementInstance.ReferencingAttribute}_value,_{managementStep.ReferencingAttribute}_value,_{managementRequest.ReferencingAttribute}_value,_{responsible.ReferencingAttribute}_value,_{responsibleUnit.ReferencingAttribute}_value,statecode",token)
            ??throw new KeyNotFoundException("La gestión no existe.");
        if(Int(row,"statecode")!=0||Int(row,management.Attribute("gaia_Estado")) is not (HelpdeskWorkflowValues.ManagementAvailable or HelpdeskWorkflowValues.ManagementInProgress))
            throw new InvalidOperationException("La gestión no está disponible para completar.");
        var instanceId=RequiredGuid(row,$"_{managementInstance.ReferencingAttribute}_value");
        var stepId=RequiredGuid(row,$"_{managementStep.ReferencingAttribute}_value");
        var requestId=RequiredGuid(row,$"_{managementRequest.ReferencingAttribute}_value");
        var personId=OptionalGuid(row,$"_{responsible.ReferencingAttribute}_value");
        var unitId=OptionalGuid(row,$"_{responsibleUnit.ReferencingAttribute}_value");
        var actorUnits=await ActorUnits(client,actorId,token);
        HelpdeskWorkflowAuthorization.Demand(actorId,personId,unitId,actorUnits,false);
        await ValidateManagementStageFormAsync(command.ManagementId,client,token);

        var instanceFlow=instance.Relationship("gaia_FlujoGestion","gaia_flujogestion");
        var instanceRow=await DataverseMetadataResolver.ReadOneAsync(client,
            $"{instance.EntitySetName}({instanceId:D})?$select={instance.Attribute("gaia_Estado")},_{instanceFlow.ReferencingAttribute}_value",token)
            ??throw new InvalidOperationException("La instancia del flujo no existe.");
        if(Int(instanceRow,instance.Attribute("gaia_Estado"))!=HelpdeskWorkflowValues.InstanceRunning)
            throw new InvalidOperationException("La instancia del flujo no está en ejecución.");
        var definition=await definitions.ReadAsync(RequiredGuid(instanceRow,$"_{instanceFlow.ReferencingAttribute}_value"),token)
            ??throw new InvalidOperationException("No fue posible leer la versión del flujo.");
        var configured=definition.Steps.SingleOrDefault(x=>x.Id==stepId&&x.Active)
            ??throw new InvalidOperationException("El paso de la gestión no pertenece al flujo.");
        var attachment=await DataverseMetadataResolver.TableAsync(client,"gaia_adjuntosolicitud",token);
        var attachmentManagement=attachment.Relationship("gaia_GestionSolicitud","gaia_gestionsolicitud");
        var relatedFiles=await DataverseJson.ReadAllAsync(client,$"{attachment.EntitySetName}?$select={attachment.PrimaryIdAttribute}&$filter=statecode eq 0 and _{attachmentManagement.ReferencingAttribute}_value eq {command.ManagementId:D}&$top=1",token);
        command=command with{HasRelatedFile=relatedFiles.Count>0};
        HelpdeskWorkflowRules.ValidateCompletion(configured,command);

        var returned=command.Result==HelpdeskWorkflowValues.Returned;
        if(returned)
        {
            var currentExecutions=await ReadExecutions(client,management,instanceId,token);
            var projected=currentExecutions.Select(x=>x.Id==command.ManagementId?x with{Status=HelpdeskWorkflowValues.ManagementWaiting,Result=command.Result}:x).ToArray();
            await CompleteReturnedAtomic(client,management,request,third,history,command,requestId,actorId,projected,now,token);
            return;
        }
        var beforeCompletion=await ReadExecutions(client,management,instanceId,token);
        var projectedCompletion=beforeCompletion.Select(x=>x.Id==command.ManagementId?x with{Status=HelpdeskWorkflowValues.ManagementCompleted,Result=command.Result}:x).ToArray();
        var beforeDependencies=await ReadDependencies(client,instanceId,token);
        var projectedSource=new HelpdeskManagementExecution(command.ManagementId,stepId,projectedCompletion.Single(x=>x.Id==command.ManagementId).Execution,HelpdeskWorkflowValues.ManagementCompleted,command.Result);
        var projectedActivations=HelpdeskWorkflowRules.ActivateAfter(definition,projectedSource,projectedCompletion,beforeDependencies);
        if(projectedActivations.Count==0&&HelpdeskWorkflowRules.CanCompleteInstance(definition,projectedCompletion))
        {
            await CompleteTerminalAtomic(client,management,request,instance,third,history,command,requestId,instanceId,actorId,now,token);
            return;
        }
        var ownerRelation=request.Relationship("gaia_ResponsableInterno","gaia_terceros");var ownerRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{request.EntitySetName}({requestId:D})?$select=_{ownerRelation.ReferencingAttribute}_value",token);var ownerId=ownerRow is null?null:OptionalGuid(ownerRow.Value,$"_{ownerRelation.ReferencingAttribute}_value");
        await CompleteWithActivationsAtomic(client,management,request,instance,step,third,unit,history,definition,command,requestId,instanceId,actorId,ownerId,beforeCompletion,beforeDependencies,projectedActivations,now,token);
    }

    public async Task ReassignAsync(Guid managementId,Guid actorId,ReassignHelpdeskManagement command,DateTimeOffset now,CancellationToken token)
    {
        var client=await clients.CreateAsync();var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);var history=await DataverseMetadataResolver.TableAsync(client,"gaia_historialsolicitud",token);
        var requestRelation=management.Relationship("gaia_Solicitud","gaia_solicitud");var responsible=management.Relationship("gaia_Responsable","gaia_terceros");var unit=management.Relationship("gaia_UnidadResponsable","gaia_organizacion");var state=management.Attribute("gaia_Estado");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{management.EntitySetName}({managementId:D})?$select={state},_{requestRelation.ReferencingAttribute}_value,_{responsible.ReferencingAttribute}_value,_{unit.ReferencingAttribute}_value,statecode",token)??throw new KeyNotFoundException("La gestión no existe.");
        if(Int(row,"statecode")!=0||Int(row,state) is HelpdeskWorkflowValues.ManagementCompleted or HelpdeskWorkflowValues.ManagementCancelled)throw new InvalidOperationException("La gestión ya no admite reasignación.");
        HelpdeskWorkflowAuthorization.Demand(actorId,OptionalGuid(row,$"_{responsible.ReferencingAttribute}_value"),OptionalGuid(row,$"_{unit.ReferencingAttribute}_value"),await ActorUnits(client,actorId,token),false);
        if(command.ResponsibleId.HasValue)
        {
            var person=await DataverseMetadataResolver.ReadOneAsync(client,$"{third.EntitySetName}({command.ResponsibleId:D})?$select={third.PrimaryIdAttribute},statecode",token);
            if(person is null||Int(person.Value,"statecode")!=0)throw new ArgumentException("El nuevo responsable no existe o está inactivo.");
        }
        await Patch(client,management.EntitySetName,managementId,new Dictionary<string,object?>
        {
            [responsible.NavigationProperty+"@odata.bind"]=command.ResponsibleId.HasValue?$"/{third.EntitySetName}({command.ResponsibleId:D})":null,
            [management.Attribute("gaia_FechaAsignacion")]=now
        },token);
        await AppendHistory(client,history,request,third,RequiredGuid(row,$"_{requestRelation.ReferencingAttribute}_value"),actorId,Guid.NewGuid().ToString("D"),299540206,$"Gestión reasignada: {command.Reason}",managementId,null,now,token);
    }

    public async Task TakeAsync(Guid managementId,Guid actorId,DateTimeOffset now,CancellationToken token)
    {
        var client=await clients.CreateAsync();var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);var history=await DataverseMetadataResolver.TableAsync(client,"gaia_historialsolicitud",token);var requestRelation=management.Relationship("gaia_Solicitud","gaia_solicitud");var responsible=management.Relationship("gaia_Responsable","gaia_terceros");var unit=management.Relationship("gaia_UnidadResponsable","gaia_organizacion");var state=management.Attribute("gaia_Estado");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{management.EntitySetName}({managementId:D})?$select={state},_{requestRelation.ReferencingAttribute}_value,_{responsible.ReferencingAttribute}_value,_{unit.ReferencingAttribute}_value,statecode",token)??throw new KeyNotFoundException("La gestión no existe.");if(Int(row,"statecode")!=0||Int(row,state)!=HelpdeskWorkflowValues.ManagementAvailable)throw new InvalidOperationException("Solo se puede tomar una gestión disponible.");if(OptionalGuid(row,$"_{responsible.ReferencingAttribute}_value").HasValue)throw new InvalidOperationException("La gestión ya tiene responsable.");var unitId=OptionalGuid(row,$"_{unit.ReferencingAttribute}_value")??throw new InvalidOperationException("La gestión no tiene unidad responsable.");if(!(await ActorUnits(client,actorId,token)).Contains(unitId))throw new UnauthorizedAccessException("No perteneces a la unidad responsable de la gestión.");
        await Patch(client,management.EntitySetName,managementId,new Dictionary<string,object?>
        {
            [responsible.NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})",
            [state]=management.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.ManagementInProgress),
            [management.Attribute("gaia_FechaAsignacion")]=now,[management.Attribute("gaia_FechaInicio")]=now
        },token);
        await AppendHistory(client,history,request,third,RequiredGuid(row,$"_{requestRelation.ReferencingAttribute}_value"),actorId,Guid.NewGuid().ToString("D"),299540205,"Gestión tomada por responsable",managementId,null,now,token);
    }

    public async Task ResumeFromRequesterAsync(Guid managementId,Guid actorId,string comment,bool hasFile,DateTimeOffset now,CancellationToken token)
    {
        var client=await clients.CreateAsync();var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);var history=await DataverseMetadataResolver.TableAsync(client,"gaia_historialsolicitud",token);
        var requestRelation=management.Relationship("gaia_Solicitud","gaia_solicitud");var state=management.Attribute("gaia_Estado");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{management.EntitySetName}({managementId:D})?$select={state},_{requestRelation.ReferencingAttribute}_value,statecode",token)??throw new KeyNotFoundException("La gestión no existe.");
        if(Int(row,"statecode")!=0||Int(row,state)!=HelpdeskWorkflowValues.ManagementWaiting)throw new InvalidOperationException("La gestión no está esperando respuesta del solicitante.");
        var requestId=RequiredGuid(row,$"_{requestRelation.ReferencingAttribute}_value");var requester=request.Relationship("gaia_Solicitante","gaia_terceros");
        var requestRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{request.EntitySetName}({requestId:D})?$select=_{requester.ReferencingAttribute}_value",token);
        if(requestRow is null||OptionalGuid(requestRow.Value,$"_{requester.ReferencingAttribute}_value")!=actorId)throw new UnauthorizedAccessException("Solo el solicitante puede reanudar esta gestión.");
        await Patch(client,management.EntitySetName,managementId,new Dictionary<string,object?>
        {
            [state]=management.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.ManagementAvailable),
            [management.Attribute("gaia_FechaDisponibilidad")]=now
        },token);
        await AppendWorkflowComment(client,request,third,requestId,managementId,actorId,299540072,
            string.IsNullOrWhiteSpace(comment)?"El solicitante adjuntó la información requerida.":comment,now,token);
        await AppendHistory(client,history,request,third,requestId,actorId,Guid.NewGuid().ToString("D"),299540213,string.IsNullOrWhiteSpace(comment)?"Gestión reanudada con archivo":comment,managementId,null,now,token);
        var instanceRelation=management.Relationship("gaia_InstanciaFlujo","gaia_instanciaflujo");var resumed=await DataverseMetadataResolver.ReadOneAsync(client,$"{management.EntitySetName}({managementId:D})?$select=_{instanceRelation.ReferencingAttribute}_value",token);if(resumed is not null)await SynchronizeRequestState(client,request,requestId,await ReadExecutions(client,management,RequiredGuid(resumed.Value,$"_{instanceRelation.ReferencingAttribute}_value"),token),false,now,token);
    }

    public async Task ReopenAsync(Guid requestId,Guid actorId,DateTimeOffset now,CancellationToken token)
    {
        var client=await clients.CreateAsync();var instance=await DataverseMetadataResolver.TableAsync(client,"gaia_instanciaflujo",token);var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);var unit=await DataverseMetadataResolver.TableAsync(client,"gaia_organizacion",token);var history=await DataverseMetadataResolver.TableAsync(client,"gaia_historialsolicitud",token);
        var requestRelation=instance.Relationship("gaia_Solicitud","gaia_solicitud");var flowRelation=instance.Relationship("gaia_FlujoGestion","gaia_flujogestion");var state=instance.Attribute("gaia_Estado");
        var rows=await DataverseJson.ReadAllAsync(client,$"{instance.EntitySetName}?$select={instance.PrimaryIdAttribute},{state},_{flowRelation.ReferencingAttribute}_value&$filter=statecode eq 0 and _{requestRelation.ReferencingAttribute}_value eq {requestId:D}&$top=2",token);
        if(rows.Count!=1)throw new InvalidOperationException("La solicitud no tiene una única instancia de flujo.");var row=rows[0];
        if(Int(row,state)!=HelpdeskWorkflowValues.InstanceCompleted)throw new InvalidOperationException("Solo se puede reabrir un flujo completado.");
        var definition=await definitions.ReadAsync(RequiredGuid(row,$"_{flowRelation.ReferencingAttribute}_value"),token)??throw new InvalidOperationException("No fue posible leer el flujo original.");
        var entry=definition.Steps.SingleOrDefault(x=>x.Active&&x.ReopeningEntry)??throw new InvalidOperationException("El flujo no tiene un paso de entrada para reapertura.");var instanceId=RequiredGuid(row,instance.PrimaryIdAttribute);
        var existing=await ReadExecutions(client,management,instanceId,token);var execution=existing.Where(x=>x.StepId==entry.Id).Select(x=>x.Execution).DefaultIfEmpty(0).Max()+1;
        var owner=request.Relationship("gaia_ResponsableInterno","gaia_terceros");var requestRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{request.EntitySetName}({requestId:D})?$select=_{owner.ReferencingAttribute}_value",token);var ownerId=requestRow is null?null:OptionalGuid(requestRow.Value,$"_{owner.ReferencingAttribute}_value");
        await Patch(client,instance.EntitySetName,instanceId,new Dictionary<string,object?>
        {
            [state]=instance.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.InstanceRunning),
            [instance.Attribute("gaia_FechaFinalizacion")]=null
        },token);
        await EnsureManagement(client,requestId,instanceId,entry,execution,HelpdeskWorkflowValues.ManagementAvailable,ownerId,management,request,instance,step,third,unit,now,token);
        await AppendHistory(client,history,request,third,requestId,actorId,Guid.NewGuid().ToString("D"),299540202,"Flujo reactivado",null,null,now,token);
    }

    private static async Task<Guid> EnsureManagement(HttpClient client,Guid requestId,Guid instanceId,
        HelpdeskWorkflowStep configured,int execution,int status,Guid? requestOwner,
        DataverseTableMetadata management,DataverseTableMetadata request,DataverseTableMetadata instance,
        DataverseTableMetadata step,DataverseTableMetadata third,DataverseTableMetadata unit,
        DateTimeOffset now,CancellationToken token)
    {
        var instanceRelation=management.Relationship("gaia_InstanciaFlujo","gaia_instanciaflujo");
        var stepRelation=management.Relationship("gaia_PasoFlujo","gaia_pasoflujo");
        var number=management.Attribute("gaia_NumeroEjecucion");
        var statusField=management.Attribute("gaia_Estado");
        var rows=await DataverseJson.ReadAllAsync(client,
            $"{management.EntitySetName}?$select={management.PrimaryIdAttribute},{statusField}&$filter=_{instanceRelation.ReferencingAttribute}_value eq {instanceId:D} and _{stepRelation.ReferencingAttribute}_value eq {configured.Id:D} and {number} eq {execution}&$top=2",token);
        if(rows.Count>1)throw new InvalidOperationException("La gestión idempotente está duplicada.");
        if(rows.Count==1)
        {
            var id=RequiredGuid(rows[0],management.PrimaryIdAttribute);
            if(Int(rows[0],statusField)==HelpdeskWorkflowValues.ManagementBlocked&&status==HelpdeskWorkflowValues.ManagementAvailable)
                await Patch(client,management.EntitySetName,id,new Dictionary<string,object?>
                {
                    [statusField]=management.EncodedIntegerValue("gaia_Estado",status),
                    [management.Attribute("gaia_FechaDisponibilidad")]=now
                },token);
            return id;
        }
        var requestRelation=management.Relationship("gaia_Solicitud","gaia_solicitud");
        var payload=new Dictionary<string,object?>{
            [management.PrimaryNameAttribute]=$"{configured.Code} · ejecución {execution}",
            [number]=execution,
            [management.Attribute("gaia_Estado")]=management.EncodedIntegerValue("gaia_Estado",status),
            [management.Attribute("gaia_FechaCreacion")]=now,
            [management.Attribute("gaia_FechaDisponibilidad")]=status==HelpdeskWorkflowValues.ManagementAvailable?now:null,
            [requestRelation.NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",
            [instanceRelation.NavigationProperty+"@odata.bind"]=$"/{instance.EntitySetName}({instanceId:D})",
            [stepRelation.NavigationProperty+"@odata.bind"]=$"/{step.EntitySetName}({configured.Id:D})",
            ["statecode"]=0};
        if(configured.UnitId.HasValue)payload[management.Relationship("gaia_UnidadResponsable","gaia_organizacion").NavigationProperty+"@odata.bind"]=$"/{unit.EntitySetName}({configured.UnitId:D})";
        var person=configured.AssignmentStrategy switch{
            HelpdeskWorkflowValues.SpecificPerson=>configured.PersonId,
            HelpdeskWorkflowValues.RequestOwner=>requestOwner,
            _=>null};
        if(person.HasValue)payload[management.Relationship("gaia_Responsable","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({person:D})";
        return await Create(client,management.EntitySetName,payload,token);
    }

    private static async Task<IReadOnlyCollection<Guid>> ActorUnits(HttpClient client,Guid actorId,CancellationToken token)
    {
        var assignment=await DataverseMetadataResolver.TableAsync(client,"gaia_asignacionorganizacional",token);
        var person=assignment.RelationshipTo("gaia_terceros");var unit=assignment.RelationshipTo("gaia_organizacion");
        var rows=await DataverseJson.ReadAllAsync(client,
            $"{assignment.EntitySetName}?$select=_{unit.ReferencingAttribute}_value&$filter=statecode eq 0 and _{person.ReferencingAttribute}_value eq {actorId:D}",token);
        return rows.Select(x=>OptionalGuid(x,$"_{unit.ReferencingAttribute}_value")).Where(x=>x.HasValue).Select(x=>x!.Value).Distinct().ToArray();
    }

    private static async Task<IReadOnlyList<HelpdeskManagementExecution>> ReadExecutions(HttpClient client,
        DataverseTableMetadata management,Guid instanceId,CancellationToken token)
    {
        var instance=management.Relationship("gaia_InstanciaFlujo","gaia_instanciaflujo");
        var step=management.Relationship("gaia_PasoFlujo","gaia_pasoflujo");
        var number=management.Attribute("gaia_NumeroEjecucion");var status=management.Attribute("gaia_Estado");var result=management.Attribute("gaia_Resultado");
        var rows=await DataverseJson.ReadAllAsync(client,
            $"{management.EntitySetName}?$select={management.PrimaryIdAttribute},{number},{status},{result},_{step.ReferencingAttribute}_value&$filter=statecode eq 0 and _{instance.ReferencingAttribute}_value eq {instanceId:D}",token);
        return rows.Select(x=>new HelpdeskManagementExecution(RequiredGuid(x,management.PrimaryIdAttribute),
            RequiredGuid(x,$"_{step.ReferencingAttribute}_value"),Int(x,number),Int(x,status),NullableInt(x,result))).ToArray();
    }

    private static async Task<IReadOnlyList<HelpdeskWorkflowDependency>> ReadDependencies(HttpClient client,Guid instanceId,CancellationToken token)
    {
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_dependenciagestion",token);
        var instance=table.Relationship("gaia_InstanciaFlujo","gaia_instanciaflujo");
        var route=table.Relationship("gaia_RutaFlujo","gaia_rutaflujo");var source=table.Relationship("gaia_GestionOrigen","gaia_gestionsolicitud");var target=table.Relationship("gaia_GestionDestino","gaia_gestionsolicitud");
        var rows=await DataverseJson.ReadAllAsync(client,
            $"{table.EntitySetName}?$select=_{route.ReferencingAttribute}_value,_{source.ReferencingAttribute}_value,_{target.ReferencingAttribute}_value&$filter=statecode eq 0 and _{instance.ReferencingAttribute}_value eq {instanceId:D}",token);
        return rows.Select(x=>new HelpdeskWorkflowDependency(RequiredGuid(x,$"_{source.ReferencingAttribute}_value"),
            RequiredGuid(x,$"_{route.ReferencingAttribute}_value"),RequiredGuid(x,$"_{target.ReferencingAttribute}_value"))).ToArray();
    }

    private static async Task EnsureDependency(HttpClient client,Guid instanceId,Guid sourceId,Guid targetId,Guid routeId,DateTimeOffset now,CancellationToken token)
    {
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_dependenciagestion",token);
        var instance=table.Relationship("gaia_InstanciaFlujo","gaia_instanciaflujo");var route=table.Relationship("gaia_RutaFlujo","gaia_rutaflujo");var source=table.Relationship("gaia_GestionOrigen","gaia_gestionsolicitud");var target=table.Relationship("gaia_GestionDestino","gaia_gestionsolicitud");
        var rows=await DataverseJson.ReadAllAsync(client,
            $"{table.EntitySetName}?$select={table.PrimaryIdAttribute}&$filter=_{instance.ReferencingAttribute}_value eq {instanceId:D} and _{route.ReferencingAttribute}_value eq {routeId:D} and _{source.ReferencingAttribute}_value eq {sourceId:D} and _{target.ReferencingAttribute}_value eq {targetId:D}&$top=1",token);
        if(rows.Count>0)return;
        var instanceTable=await DataverseMetadataResolver.TableAsync(client,"gaia_instanciaflujo",token);var routeTable=await DataverseMetadataResolver.TableAsync(client,"gaia_rutaflujo",token);var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);
        await Create(client,table.EntitySetName,new Dictionary<string,object?>
        {
            [table.PrimaryNameAttribute]=$"{sourceId:D} → {targetId:D}",[table.Attribute("gaia_FechaActivacion")]=now,
            [instance.NavigationProperty+"@odata.bind"]=$"/{instanceTable.EntitySetName}({instanceId:D})",
            [route.NavigationProperty+"@odata.bind"]=$"/{routeTable.EntitySetName}({routeId:D})",
            [source.NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({sourceId:D})",
            [target.NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({targetId:D})",["statecode"]=0
        },token);
    }

    private static async Task AppendHistory(HttpClient client,DataverseTableMetadata history,DataverseTableMetadata request,
        DataverseTableMetadata third,Guid requestId,Guid actorId,string operationId,int movement,string name,Guid? managementId,Guid? routeId,DateTimeOffset now,CancellationToken token)
    {
        var payload=new Dictionary<string,object?>
        {
            [history.PrimaryNameAttribute]=name,[history.Attribute("gaia_OperacionId")]=operationId,
            [history.Attribute("gaia_TipoMovimiento")]=history.EncodedIntegerValue("gaia_TipoMovimiento",movement),
            [history.Attribute("gaia_Origen")]=history.EncodedIntegerValue("gaia_Origen",299540121),
            [history.Attribute("gaia_VisibleAlSolicitante")]=true,[history.Attribute("gaia_FechaEvento")]=now,
            [history.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",
            [history.Relationship("gaia_Actor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})",["statecode"]=0
        };
        if(managementId.HasValue){var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);payload[history.Relationship("gaia_GestionSolicitud","gaia_gestionsolicitud").NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({managementId:D})";}
        if(routeId.HasValue){var route=await DataverseMetadataResolver.TableAsync(client,"gaia_rutaflujo",token);payload[history.Relationship("gaia_RutaFlujo","gaia_rutaflujo").NavigationProperty+"@odata.bind"]=$"/{route.EntitySetName}({routeId:D})";}
        await Create(client,history.EntitySetName,payload,token);
    }

    private static async Task AppendWorkflowComment(HttpClient client,DataverseTableMetadata request,DataverseTableMetadata third,
        Guid requestId,Guid managementId,Guid actorId,int type,string content,DateTimeOffset now,CancellationToken token)
    {
        var comment=await DataverseMetadataResolver.TableAsync(client,"gaia_comentariosolicitud",token);var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);
        await Create(client,comment.EntitySetName,new Dictionary<string,object?>
        {
            [comment.PrimaryNameAttribute]=$"Comentario de gestión {now:yyyy-MM-dd HH:mm}",
            [comment.Attribute("gaia_Tipo")]=comment.EncodedIntegerValue("gaia_Tipo",type),
            [comment.Attribute("gaia_Visibilidad")]=comment.EncodedIntegerValue("gaia_Visibilidad",299540080),
            [comment.Attribute("gaia_Contenido")]=content,[comment.Attribute("gaia_FechaPublicacion")]=now,
            [comment.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",
            [comment.Relationship("gaia_Autor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})",
            [comment.Relationship("gaia_GestionSolicitud","gaia_gestionsolicitud").NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({managementId:D})",
            ["statecode"]=0
        },token);
    }

    private static async Task CompleteReturnedAtomic(HttpClient client,DataverseTableMetadata management,DataverseTableMetadata request,
        DataverseTableMetadata third,DataverseTableMetadata history,CompleteHelpdeskManagement command,Guid requestId,Guid actorId,
        IReadOnlyList<HelpdeskManagementExecution> projected,DateTimeOffset now,CancellationToken token)
    {
        var comment=await DataverseMetadataResolver.TableAsync(client,"gaia_comentariosolicitud",token);var state=await DataverseMetadataResolver.TableAsync(client,"gaia_estadosolicitud",token);var code=state.Attribute("gaia_Codigo");var desired=HelpdeskWorkflowRules.RequestMustWait(projected)?"EN_ESPERA_SOLICITANTE":"EN_GESTION";var states=await DataverseJson.ReadAllAsync(client,$"{state.EntitySetName}?$select={state.PrimaryIdAttribute},{code}&$filter=statecode eq 0",token);var target=states.SingleOrDefault(x=>Normalize(Text(x,code))==Normalize(desired));if(target.ValueKind==JsonValueKind.Undefined)throw new InvalidOperationException($"No existe el estado activo {desired}.");var targetId=RequiredGuid(target,state.PrimaryIdAttribute);var managementId=command.ManagementId;var historyId=Guid.NewGuid();var commentId=Guid.NewGuid();
        var managementPayload=new Dictionary<string,object?>{[management.Attribute("gaia_Estado")]=management.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.ManagementWaiting),[management.Attribute("gaia_Resultado")]=management.EncodedIntegerValue("gaia_Resultado",command.Result),[management.Attribute("gaia_Observacion")]=command.Observation,[management.Attribute("gaia_FechaFinalizacion")]=null,[management.Relationship("gaia_GestionadaPor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})"};
        var historyPayload=new Dictionary<string,object?>{[history.PrimaryIdAttribute]=historyId,[history.PrimaryNameAttribute]="Gestión enviada a espera del solicitante",[history.Attribute("gaia_OperacionId")]=command.OperationId,[history.Attribute("gaia_TipoMovimiento")]=history.EncodedIntegerValue("gaia_TipoMovimiento",299540212),[history.Attribute("gaia_Origen")]=history.EncodedIntegerValue("gaia_Origen",299540121),[history.Attribute("gaia_VisibleAlSolicitante")]=true,[history.Attribute("gaia_FechaEvento")]=now,[history.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",[history.Relationship("gaia_Actor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})",[history.Relationship("gaia_GestionSolicitud","gaia_gestionsolicitud").NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({managementId:D})",["statecode"]=0};
        var commentPayload=new Dictionary<string,object?>{[comment.PrimaryIdAttribute]=commentId,[comment.PrimaryNameAttribute]=$"Comentario de gestión {now:yyyy-MM-dd HH:mm}",[comment.Attribute("gaia_Tipo")]=comment.EncodedIntegerValue("gaia_Tipo",299540071),[comment.Attribute("gaia_Visibilidad")]=comment.EncodedIntegerValue("gaia_Visibilidad",299540080),[comment.Attribute("gaia_Contenido")]=command.Observation??"El equipo solicita información adicional.",[comment.Attribute("gaia_FechaPublicacion")]=now,[comment.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",[comment.Relationship("gaia_Autor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})",[comment.Relationship("gaia_GestionSolicitud","gaia_gestionsolicitud").NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({managementId:D})",["statecode"]=0};
        var requestPayload=new Dictionary<string,object?>{[request.Relationship("gaia_EstadoActual","gaia_estadosolicitud").NavigationProperty+"@odata.bind"]=$"/{state.EntitySetName}({targetId:D})",[request.Attribute("gaia_FechaInicioPausaSLA")]=desired=="EN_ESPERA_SOLICITANTE"?now:null};
        await ExecuteChangeSet(client,[("PATCH",$"{management.EntitySetName}({managementId:D})",managementPayload),("POST",history.EntitySetName,historyPayload),("POST",comment.EntitySetName,commentPayload),("PATCH",$"{request.EntitySetName}({requestId:D})",requestPayload)],token);
    }

    private static async Task CompleteTerminalAtomic(HttpClient client,DataverseTableMetadata management,DataverseTableMetadata request,
        DataverseTableMetadata instance,DataverseTableMetadata third,DataverseTableMetadata history,CompleteHelpdeskManagement command,
        Guid requestId,Guid instanceId,Guid actorId,DateTimeOffset now,CancellationToken token)
    {
        var state=await DataverseMetadataResolver.TableAsync(client,"gaia_estadosolicitud",token);var requestState=request.Relationship("gaia_EstadoActual","gaia_estadosolicitud");var current=await DataverseMetadataResolver.ReadOneAsync(client,$"{request.EntitySetName}({requestId:D})?$select=_{requestState.ReferencingAttribute}_value",token)??throw new KeyNotFoundException("La solicitud no existe.");var currentId=RequiredGuid(current,$"_{requestState.ReferencingAttribute}_value");
        var transition=await DataverseMetadataResolver.TableAsync(client,"gaia_transicionestadosolicitud",token);var origin=transition.Relationship("gaia_EstadoOrigen","gaia_estadosolicitud");var targetRelation=transition.Relationship("gaia_EstadoDestino","gaia_estadosolicitud");var authorized=transition.Attribute("gaia_ActorAutorizado");var action=transition.Attribute("gaia_AccionSLA");var requiresSolution=transition.Attribute("gaia_RequiereSolucion");var transitions=await DataverseJson.ReadAllAsync(client,$"{transition.EntitySetName}?$select={transition.PrimaryIdAttribute},{requiresSolution},_{targetRelation.ReferencingAttribute}_value&$filter=statecode eq 0 and _{origin.ReferencingAttribute}_value eq {currentId:D} and {authorized} eq {transition.EncodedIntegerLiteral("gaia_ActorAutorizado",299540011)} and {action} eq {transition.EncodedIntegerLiteral("gaia_AccionSLA",299540024)}",token);if(transitions.Count!=1)throw new InvalidOperationException("Debe existir una única transición activa de resolución para el estado actual.");var configured=transitions[0];if(Bool(configured,requiresSolution)&&string.IsNullOrWhiteSpace(command.Observation))throw new InvalidOperationException("La transición de resolución requiere el resumen de la solución.");var targetId=RequiredGuid(configured,$"_{targetRelation.ReferencingAttribute}_value");
        var managementPayload=new Dictionary<string,object?>{[management.Attribute("gaia_Estado")]=management.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.ManagementCompleted),[management.Attribute("gaia_Resultado")]=management.EncodedIntegerValue("gaia_Resultado",command.Result),[management.Attribute("gaia_Observacion")]=command.Observation,[management.Attribute("gaia_FechaFinalizacion")]=now,[management.Relationship("gaia_GestionadaPor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})"};
        var managementHistoryId=Guid.NewGuid();var managementHistory=new Dictionary<string,object?>{[history.PrimaryIdAttribute]=managementHistoryId,[history.PrimaryNameAttribute]="Gestión completada",[history.Attribute("gaia_OperacionId")]=command.OperationId,[history.Attribute("gaia_TipoMovimiento")]=history.EncodedIntegerValue("gaia_TipoMovimiento",Movement(command.Result)),[history.Attribute("gaia_Origen")]=history.EncodedIntegerValue("gaia_Origen",299540121),[history.Attribute("gaia_VisibleAlSolicitante")]=true,[history.Attribute("gaia_FechaEvento")]=now,[history.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",[history.Relationship("gaia_Actor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})",[history.Relationship("gaia_GestionSolicitud","gaia_gestionsolicitud").NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({command.ManagementId:D})",["statecode"]=0};
        var instancePayload=new Dictionary<string,object?>{[instance.Attribute("gaia_Estado")]=instance.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.InstanceCompleted),[instance.Attribute("gaia_FechaFinalizacion")]=now};var requestPayload=new Dictionary<string,object?>{[requestState.NavigationProperty+"@odata.bind"]=$"/{state.EntitySetName}({targetId:D})",[request.Attribute("gaia_FechaInicioPausaSLA")]=null,[request.Attribute("gaia_FechaResolucionActual")]=now,[request.Attribute("gaia_ResumenSolucion")]=command.Observation?.Trim()};
        var resolutionHistoryId=Guid.NewGuid();var resolutionHistory=new Dictionary<string,object?>{[history.PrimaryIdAttribute]=resolutionHistoryId,[history.PrimaryNameAttribute]="Solicitud resuelta por flujo",[history.Attribute("gaia_OperacionId")]=$"flow-resolution-{instanceId:D}",[history.Attribute("gaia_TipoMovimiento")]=history.EncodedIntegerValue("gaia_TipoMovimiento",299540103),[history.Attribute("gaia_Origen")]=history.EncodedIntegerValue("gaia_Origen",299540121),[history.Attribute("gaia_CampoModificado")]="EstadoActual",[history.Attribute("gaia_ValorAnterior")]=currentId.ToString("D"),[history.Attribute("gaia_ValorNuevo")]=targetId.ToString("D"),[history.Attribute("gaia_Detalle")]=command.Observation?.Trim(),[history.Attribute("gaia_VisibleAlSolicitante")]=true,[history.Attribute("gaia_FechaEvento")]=now,[history.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",[history.Relationship("gaia_Actor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})",["statecode"]=0};
        await ExecuteChangeSet(client,[("PATCH",$"{management.EntitySetName}({command.ManagementId:D})",managementPayload),("POST",history.EntitySetName,managementHistory),("PATCH",$"{instance.EntitySetName}({instanceId:D})",instancePayload),("PATCH",$"{request.EntitySetName}({requestId:D})",requestPayload),("POST",history.EntitySetName,resolutionHistory)],token);
    }

    private static async Task CompleteWithActivationsAtomic(HttpClient client,DataverseTableMetadata management,DataverseTableMetadata request,
        DataverseTableMetadata instance,DataverseTableMetadata step,DataverseTableMetadata third,DataverseTableMetadata unit,
        DataverseTableMetadata history,HelpdeskWorkflowDefinition definition,CompleteHelpdeskManagement command,Guid requestId,
        Guid instanceId,Guid actorId,Guid? requestOwner,IReadOnlyList<HelpdeskManagementExecution> existing,
        IReadOnlyList<HelpdeskWorkflowDependency> dependencies,IReadOnlyList<HelpdeskWorkflowActivation> activations,
        DateTimeOffset now,CancellationToken token)
    {
        var operations=new List<(string Method,string Path,object Payload)>();
        operations.Add(("PATCH",$"{management.EntitySetName}({command.ManagementId:D})",new Dictionary<string,object?>{[management.Attribute("gaia_Estado")]=management.EncodedIntegerValue("gaia_Estado",HelpdeskWorkflowValues.ManagementCompleted),[management.Attribute("gaia_Resultado")]=management.EncodedIntegerValue("gaia_Resultado",command.Result),[management.Attribute("gaia_Observacion")]=command.Observation,[management.Attribute("gaia_FechaFinalizacion")]=now,[management.Relationship("gaia_GestionadaPor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})"}));
        operations.Add(("POST",history.EntitySetName,new Dictionary<string,object?>{[history.PrimaryIdAttribute]=Guid.NewGuid(),[history.PrimaryNameAttribute]="Gestión completada",[history.Attribute("gaia_OperacionId")]=command.OperationId,[history.Attribute("gaia_TipoMovimiento")]=history.EncodedIntegerValue("gaia_TipoMovimiento",Movement(command.Result)),[history.Attribute("gaia_Origen")]=history.EncodedIntegerValue("gaia_Origen",299540121),[history.Attribute("gaia_VisibleAlSolicitante")]=true,[history.Attribute("gaia_FechaEvento")]=now,[history.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",[history.Relationship("gaia_Actor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})",[history.Relationship("gaia_GestionSolicitud","gaia_gestionsolicitud").NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({command.ManagementId:D})",["statecode"]=0}));
        var targetIds=new Dictionary<(Guid Step,int Execution),Guid>();
        foreach(var activation in activations)
        {
            var configured=definition.Steps.Single(x=>x.Id==activation.StepId);var present=existing.SingleOrDefault(x=>x.StepId==activation.StepId&&x.Execution==activation.Execution);var targetId=present?.Id??Guid.NewGuid();targetIds[(activation.StepId,activation.Execution)]=targetId;
            if(present is not null){if(present.Status==HelpdeskWorkflowValues.ManagementBlocked&&activation.Status==HelpdeskWorkflowValues.ManagementAvailable)operations.Add(("PATCH",$"{management.EntitySetName}({targetId:D})",new Dictionary<string,object?>{[management.Attribute("gaia_Estado")]=management.EncodedIntegerValue("gaia_Estado",activation.Status),[management.Attribute("gaia_FechaDisponibilidad")]=now}));}
            else
            {
                var payload=new Dictionary<string,object?>{[management.PrimaryIdAttribute]=targetId,[management.PrimaryNameAttribute]=$"{configured.Code} · ejecución {activation.Execution}",[management.Attribute("gaia_NumeroEjecucion")]=activation.Execution,[management.Attribute("gaia_Estado")]=management.EncodedIntegerValue("gaia_Estado",activation.Status),[management.Attribute("gaia_FechaCreacion")]=now,[management.Attribute("gaia_FechaDisponibilidad")]=activation.Status==HelpdeskWorkflowValues.ManagementAvailable?now:null,[management.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",[management.Relationship("gaia_InstanciaFlujo","gaia_instanciaflujo").NavigationProperty+"@odata.bind"]=$"/{instance.EntitySetName}({instanceId:D})",[management.Relationship("gaia_PasoFlujo","gaia_pasoflujo").NavigationProperty+"@odata.bind"]=$"/{step.EntitySetName}({configured.Id:D})",["statecode"]=0};if(configured.UnitId.HasValue)payload[management.Relationship("gaia_UnidadResponsable","gaia_organizacion").NavigationProperty+"@odata.bind"]=$"/{unit.EntitySetName}({configured.UnitId:D})";var person=configured.AssignmentStrategy switch{HelpdeskWorkflowValues.SpecificPerson=>configured.PersonId,HelpdeskWorkflowValues.RequestOwner=>requestOwner,_=>null};if(person.HasValue)payload[management.Relationship("gaia_Responsable","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({person:D})";operations.Add(("POST",management.EntitySetName,payload));
            }
            foreach(var routeId in activation.SatisfiedRouteIds.Where(routeId=>!dependencies.Any(x=>x.SourceManagementId==command.ManagementId&&x.RouteId==routeId&&x.TargetManagementId==targetId)))
            {
                var dependency=await DataverseMetadataResolver.TableAsync(client,"gaia_dependenciagestion",token);var route=await DataverseMetadataResolver.TableAsync(client,"gaia_rutaflujo",token);operations.Add(("POST",dependency.EntitySetName,new Dictionary<string,object?>{[dependency.PrimaryIdAttribute]=Guid.NewGuid(),[dependency.PrimaryNameAttribute]=$"{command.ManagementId:D} → {targetId:D}",[dependency.Attribute("gaia_FechaActivacion")]=now,[dependency.Relationship("gaia_InstanciaFlujo","gaia_instanciaflujo").NavigationProperty+"@odata.bind"]=$"/{instance.EntitySetName}({instanceId:D})",[dependency.Relationship("gaia_RutaFlujo","gaia_rutaflujo").NavigationProperty+"@odata.bind"]=$"/{route.EntitySetName}({routeId:D})",[dependency.Relationship("gaia_GestionOrigen","gaia_gestionsolicitud").NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({command.ManagementId:D})",[dependency.Relationship("gaia_GestionDestino","gaia_gestionsolicitud").NavigationProperty+"@odata.bind"]=$"/{management.EntitySetName}({targetId:D})",["statecode"]=0}));
            }
        }
        var projected=existing.Select(x=>x.Id==command.ManagementId?x with{Status=HelpdeskWorkflowValues.ManagementCompleted,Result=command.Result}:x).ToList();foreach(var activation in activations){var present=projected.FindIndex(x=>x.StepId==activation.StepId&&x.Execution==activation.Execution);var value=new HelpdeskManagementExecution(targetIds[(activation.StepId,activation.Execution)],activation.StepId,activation.Execution,activation.Status);if(present>=0)projected[present]=value;else projected.Add(value);}var desired=HelpdeskWorkflowRules.RequestMustWait(projected)?"ENESPERASOLICITANTE":"ENGESTION";var state=await DataverseMetadataResolver.TableAsync(client,"gaia_estadosolicitud",token);var code=state.Attribute("gaia_Codigo");var states=await DataverseJson.ReadAllAsync(client,$"{state.EntitySetName}?$select={state.PrimaryIdAttribute},{code}&$filter=statecode eq 0",token);var targetState=states.Single(x=>Normalize(Text(x,code))==desired);operations.Add(("PATCH",$"{request.EntitySetName}({requestId:D})",new Dictionary<string,object?>{[request.Relationship("gaia_EstadoActual","gaia_estadosolicitud").NavigationProperty+"@odata.bind"]=$"/{state.EntitySetName}({RequiredGuid(targetState,state.PrimaryIdAttribute):D})",[request.Attribute("gaia_FechaInicioPausaSLA")]=desired=="ENESPERASOLICITANTE"?now:null}));
        await ExecuteChangeSet(client,operations,token);
    }

    private static async Task ExecuteChangeSet(HttpClient client,IReadOnlyList<(string Method,string Path,object Payload)> operations,CancellationToken token)
        =>await DataverseChangeSet.ExecuteAsync(client,operations,token);

    private static async Task SynchronizeRequestState(HttpClient client,DataverseTableMetadata request,Guid requestId,
        IReadOnlyList<HelpdeskManagementExecution> executions,bool completed,DateTimeOffset now,CancellationToken token,
        Guid? actorId=null,Guid? instanceId=null,string? solution=null)
    {
        var desired=completed?"RESUELTA":HelpdeskWorkflowRules.RequestMustWait(executions)?"EN_ESPERA_SOLICITANTE":"EN_GESTION";
        var state=await DataverseMetadataResolver.TableAsync(client,"gaia_estadosolicitud",token);var relation=request.Relationship("gaia_EstadoActual","gaia_estadosolicitud");var current=await DataverseMetadataResolver.ReadOneAsync(client,$"{request.EntitySetName}({requestId:D})?$select=_{relation.ReferencingAttribute}_value",token);var currentId=current is null?null:OptionalGuid(current.Value,$"_{relation.ReferencingAttribute}_value");
        Guid targetId;
        if(completed)
        {
            if(!actorId.HasValue||!instanceId.HasValue)throw new InvalidOperationException("No se recibió el contexto para resolver la solicitud.");
            var transition=await DataverseMetadataResolver.TableAsync(client,"gaia_transicionestadosolicitud",token);var origin=transition.Relationship("gaia_EstadoOrigen","gaia_estadosolicitud");var targetRelation=transition.Relationship("gaia_EstadoDestino","gaia_estadosolicitud");var authorized=transition.Attribute("gaia_ActorAutorizado");var action=transition.Attribute("gaia_AccionSLA");var requiresSolution=transition.Attribute("gaia_RequiereSolucion");
            var transitions=await DataverseJson.ReadAllAsync(client,$"{transition.EntitySetName}?$select={transition.PrimaryIdAttribute},{action},{requiresSolution},_{targetRelation.ReferencingAttribute}_value&$filter=statecode eq 0 and _{origin.ReferencingAttribute}_value eq {currentId:D} and {authorized} eq {transition.EncodedIntegerLiteral("gaia_ActorAutorizado",299540011)} and {action} eq {transition.EncodedIntegerLiteral("gaia_AccionSLA",299540024)}",token);
            if(transitions.Count!=1)throw new InvalidOperationException("Debe existir una única transición activa de resolución para el estado actual.");
            var configured=transitions[0];if(Bool(configured,requiresSolution)&&string.IsNullOrWhiteSpace(solution))throw new InvalidOperationException("La transición de resolución requiere el resumen de la solución.");targetId=RequiredGuid(configured,$"_{targetRelation.ReferencingAttribute}_value");
            var resolutionPayload=new Dictionary<string,object?>{[relation.NavigationProperty+"@odata.bind"]=$"/{state.EntitySetName}({targetId:D})",[request.Attribute("gaia_FechaInicioPausaSLA")]=null,[request.Attribute("gaia_FechaResolucionActual")]=now,[request.Attribute("gaia_ResumenSolucion")]=solution?.Trim()};await Patch(client,request.EntitySetName,requestId,resolutionPayload,token);
            var history=await DataverseMetadataResolver.TableAsync(client,"gaia_historialsolicitud",token);var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);await Create(client,history.EntitySetName,new Dictionary<string,object?>{[history.PrimaryNameAttribute]="Solicitud resuelta por flujo",[history.Attribute("gaia_OperacionId")]=$"flow-resolution-{instanceId:D}",[history.Attribute("gaia_TipoMovimiento")]=history.EncodedIntegerValue("gaia_TipoMovimiento",299540103),[history.Attribute("gaia_Origen")]=history.EncodedIntegerValue("gaia_Origen",299540121),[history.Attribute("gaia_CampoModificado")]="EstadoActual",[history.Attribute("gaia_ValorAnterior")]=currentId?.ToString("D"),[history.Attribute("gaia_ValorNuevo")]=targetId.ToString("D"),[history.Attribute("gaia_Detalle")]=solution?.Trim(),[history.Attribute("gaia_VisibleAlSolicitante")]=true,[history.Attribute("gaia_FechaEvento")]=now,[history.Relationship("gaia_Solicitud","gaia_solicitud").NavigationProperty+"@odata.bind"]=$"/{request.EntitySetName}({requestId:D})",[history.Relationship("gaia_Actor","gaia_terceros").NavigationProperty+"@odata.bind"]=$"/{third.EntitySetName}({actorId:D})",["statecode"]=0},token);return;
        }
        var code=state.Attribute("gaia_Codigo");var states=await DataverseJson.ReadAllAsync(client,$"{state.EntitySetName}?$select={state.PrimaryIdAttribute},{code}&$filter=statecode eq 0",token);var target=states.SingleOrDefault(x=>Normalize(Text(x,code))==Normalize(desired));if(target.ValueKind==JsonValueKind.Undefined)throw new InvalidOperationException($"No existe el estado activo {desired}.");targetId=RequiredGuid(target,state.PrimaryIdAttribute);if(currentId==targetId)return;
        var payload=new Dictionary<string,object?>{{relation.NavigationProperty+"@odata.bind",$"/{state.EntitySetName}({targetId:D})"}};
        if(desired=="EN_ESPERA_SOLICITANTE")payload[request.Attribute("gaia_FechaInicioPausaSLA")]=now;else payload[request.Attribute("gaia_FechaInicioPausaSLA")]=null;
        await Patch(client,request.EntitySetName,requestId,payload,token);
    }

    private static int Movement(int result)=>result switch
    {
        HelpdeskWorkflowValues.Approved=>299540209,HelpdeskWorkflowValues.Rejected=>299540210,
        HelpdeskWorkflowValues.Returned=>299540211,_=>299540208
    };

    private static async Task<Guid> Create(HttpClient client,string set,object payload,CancellationToken token)
    {using var response=await client.PostAsJsonAsync(set,payload,token);await Ensure(response,token);var uri=response.Headers.TryGetValues("OData-EntityId",out var values)?values.SingleOrDefault():null;var match=Regex.Match(uri??"",@"\(([0-9a-f-]{36})\)$");return match.Success?Guid.Parse(match.Groups[1].Value):throw new InvalidOperationException("Dataverse no devolvió el identificador creado.");}
    private static async Task Patch(HttpClient client,string set,Guid id,object payload,CancellationToken token)
    {using var request=new HttpRequestMessage(HttpMethod.Patch,$"{set}({id:D})"){Content=JsonContent.Create(payload)};request.Headers.TryAddWithoutValidation("If-Match","*");using var response=await client.SendAsync(request,token);await Ensure(response,token);}
    private static async Task RequireDraft(Guid flowId,HttpClient client,CancellationToken token)
    {
        var flow=await DataverseMetadataResolver.TableAsync(client,"gaia_flujogestion",token);
        var status=flow.Attribute("gaia_EstadoFlujo");
        var serviceRelation=flow.Relationship("gaia_Servicio","gaia_servicio");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{flow.EntitySetName}({flowId:D})?$select={status},_{serviceRelation.ReferencingAttribute}_value,statecode",token);
        if(row is null||Int(row.Value,"statecode")!=0)throw new KeyNotFoundException("El flujo no existe.");
        if(Int(row.Value,status)==HelpdeskWorkflowValues.Draft)return;
        if(Int(row.Value,status)!=HelpdeskWorkflowValues.Published)throw new InvalidOperationException("No puede modificarse un flujo retirado.");

        var serviceId=RequiredGuid(row.Value,$"_{serviceRelation.ReferencingAttribute}_value");
        var service=await DataverseMetadataResolver.TableAsync(client,"gaia_servicio",token);
        var visible=service.Attribute("gaia_VisibleAlSolicitante");
        var serviceRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{service.EntitySetName}({serviceId:D})?$select={visible},statecode",token);
        if(serviceRow is null||Int(serviceRow.Value,"statecode")!=0)throw new InvalidOperationException("El servicio está inactivo.");
        if(Bool(serviceRow.Value,visible))throw new InvalidOperationException("Despublica el servicio antes de modificar su flujo publicado.");

        var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);
        var requestService=request.Relationship("gaia_Servicio","gaia_servicio");
        var used=await DataverseJson.ReadAllAsync(client,$"{request.EntitySetName}?$select={request.PrimaryIdAttribute}&$filter=_{requestService.ReferencingAttribute}_value eq {serviceId:D}&$top=1",token);
        if(used.Count>0)throw new InvalidOperationException("El flujo ya fue utilizado por solicitudes y debe conservarse como historial. Crea una nueva versión.");
    }
    private static async Task Ensure(HttpResponseMessage response,CancellationToken token){if(response.IsSuccessStatusCode)return;var body=await response.Content.ReadAsStringAsync(token);throw new InvalidOperationException($"Dataverse rechazó el inicio del flujo ({(int)response.StatusCode}): {body}");}
    private static string? Text(JsonElement row,string name)=>row.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static int Int(JsonElement row,string name)=>DataverseJson.OptionalInt32(row,name)??0;
    private static int? NullableInt(JsonElement row,string name)=>DataverseJson.OptionalInt32(row,name);
    private static Guid RequiredGuid(JsonElement row,string name)=>OptionalGuid(row,name)??throw new InvalidOperationException($"Dataverse no devolvió {name}.");
    private static Guid? OptionalGuid(JsonElement row,string name)=>Guid.TryParse(Text(row,name),out var id)?id:null;
    private static DateTimeOffset? DateTimeValue(JsonElement row,string name)=>DateTimeOffset.TryParse(Text(row,name),out var value)?value:null;
    private static bool Bool(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind is JsonValueKind.True;
    private static JsonElement Nested(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.Object?value:JsonSerializer.SerializeToElement(new{});
    private static string Normalize(string? value)=>string.Concat((value??string.Empty).Where(char.IsLetterOrDigit)).ToUpperInvariant();
}
