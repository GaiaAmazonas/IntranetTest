using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Modules.Training;
using Gaia.Modules.ThirdParties;

namespace Gaia.Api.Infrastructure.Dataverse.Training;

internal sealed partial class DataverseTrainingOperations(
    IDataverseDelegatedClientFactory clients,
    ITrainingAdministrationReader administration,
    IIntranetDirectoryReader directory) : ITrainingOperations, ITrainingParticipantOperations
{
    public async Task<TrainingAssignmentGenerationResult> GenerateAssignmentsAsync(Guid versionId,Guid actorId,CancellationToken token)
    {
        var client=await clients.CreateAsync();
        var version=await DataverseMetadataResolver.TableAsync(client,"gaia_versioncapacitacion",token);
        var status=version.Attribute("gaia_Estado");
        var expirationRule=version.Attribute("gaia_ReglaVencimiento");
        var days=version.Attribute("gaia_DiasParaCompletar");
        var availableUntil=version.Attribute("gaia_FinDisponibilidad");
        var survey=version.Attribute("gaia_EncuestaObligatoria");
        var versionRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{version.EntitySetName}({versionId:D})?$select={version.PrimaryNameAttribute},{status},{expirationRule},{days},{availableUntil},{survey},statecode",token)
            ?? throw new KeyNotFoundException("La versión no existe.");
        if((Number(versionRow,status)??0)!=299540002||(Number(versionRow,"statecode")??0)!=0)throw new InvalidOperationException("Solo una versión publicada y activa puede generar asignaciones.");

        var audience=await administration.ReadAudienceAsync(versionId,token);
        if(audience.IncludedCount==0)throw new InvalidOperationException("La audiencia publicada no contiene participantes efectivos.");
        var assignment=await DataverseMetadataResolver.TableAsync(client,"gaia_asignacioncapacitacion",token);
        var versionRelation=assignment.Relationship("gaia_VersionCapacitacion","gaia_versioncapacitacion");
        var participantRelation=assignment.Relationship("gaia_Participante","gaia_terceros");
        var unitRelation=assignment.Relationship("gaia_UnidadAlAsignar","gaia_organizacion");
        var assignedByRelation=assignment.Relationship("gaia_AsignadaPor","gaia_terceros");
        var thirdParty=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);
        var organization=await DataverseMetadataResolver.TableAsync(client,"gaia_organizacion",token);
        var origin=assignment.Attribute("gaia_OrigenAsignacion");
        var assignmentStatus=assignment.Attribute("gaia_Estado");
        var assignedAt=assignment.Attribute("gaia_FechaAsignacion");
        var dueAt=assignment.Attribute("gaia_FechaVencimiento");
        var progress=assignment.Attribute("gaia_PorcentajeAvance");
        var seconds=assignment.Attribute("gaia_TiempoTotalSegundos");
        var requiresSurvey=assignment.Attribute("gaia_RequiereEncuesta");
        var existingRows=await DataverseJson.ReadAllAsync(client,$"{assignment.EntitySetName}?$select=_{participantRelation.ReferencingAttribute}_value&$filter=_{versionRelation.ReferencingAttribute}_value eq {versionId:D}",token);
        var existing=existingRows.Select(row=>OptionalGuid(row,$"_{participantRelation.ReferencingAttribute}_value")).Where(id=>id.HasValue).Select(id=>id!.Value).ToHashSet();
        var now=DateTimeOffset.UtcNow;
        var rule=Number(versionRow,expirationRule)??299540000;
        DateTimeOffset? due=rule switch{299540001=>Date(versionRow,availableUntil),299540002 when Number(versionRow,days) is int count=>now.AddDays(count),_=>null};
        var created=0;
        foreach(var person in audience.Preview.Where(person=>!person.IsExcluded&&!existing.Contains(person.Id)))
        {
            var payload=new Dictionary<string,object?>
            {
                [assignment.PrimaryNameAttribute]=Limit($"{Text(versionRow,version.PrimaryNameAttribute)??"Capacitación"} · {person.Name}",200),
                [versionRelation.NavigationProperty+"@odata.bind"]=$"/{version.EntitySetName}({versionId:D})",
                [participantRelation.NavigationProperty+"@odata.bind"]=$"/{thirdParty.EntitySetName}({person.Id:D})",
                [assignedByRelation.NavigationProperty+"@odata.bind"]=$"/{thirdParty.EntitySetName}({actorId:D})",
                [origin]=assignment.EncodedIntegerValue("gaia_OrigenAsignacion",OriginFor(person,audience.Rules)),
                [assignmentStatus]=assignment.EncodedIntegerValue("gaia_Estado",299541090),
                [assignedAt]=now,
                [dueAt]=due,
                [progress]=0m,
                [seconds]=0L,
                [requiresSurvey]=Boolean(versionRow,survey)??false,
                ["statecode"]=0
            };
            if(person.UnitId.HasValue)payload[unitRelation.NavigationProperty+"@odata.bind"]=$"/{organization.EntitySetName}({person.UnitId:D})";
            using var response=await client.PostAsJsonAsync(assignment.EntitySetName,payload,token);
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Dataverse rechazó la asignación de {person.Name} ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(token)}");
            created++;
        }
        return new(audience.Preview.Count,created,existing.Count);
    }

    public async Task<TrainingTrackingOverview> ReadTrackingAsync(CancellationToken token)
    {
        var items=await ReadAssignments(token);
        var now=DateTimeOffset.UtcNow;
        return new(items.Count,items.Count(x=>x.Status==299541090),items.Count(x=>x.Status is 299541091 or 299541092),items.Count(x=>x.DueAt<now&&x.Status is not (299541093 or 299541096 or 299541097)),items.Count(x=>x.Status is 299541093 or 299541094),items);
    }

    public async Task<TrainingResultsOverview> ReadResultsAsync(CancellationToken token)
    {
        var items=await ReadAssignments(token);
        var results=items.Where(x=>x.Status is 299541093 or 299541094||x.Result.HasValue).Select(x=>new TrainingResultItem(x.Id,x.VersionId,x.Training,x.Version,x.Participant,x.Unit,x.Status,x.Progress,x.Result,x.Attempts,x.CompletedAt,x.ApprovedAt)).ToArray();
        var scored=results.Where(x=>x.Result.HasValue).Select(x=>x.Result!.Value).ToArray();
        return new(items.Select(x=>x.Participant).Distinct(StringComparer.OrdinalIgnoreCase).Count(),results.Count(x=>x.CompletedAt.HasValue),results.Count(x=>x.Status==299541093),results.Count(x=>x.Status==299541094),scored.Length==0?0:Math.Round(scored.Average(),1),results);
    }

    public async Task<IReadOnlyList<MyTrainingItem>> ReadMyAssignmentsAsync(Guid actorId,CancellationToken token)
    {
        var items=await ReadAssignments(token,actorId);
        return items.Where(x=>x.Status is not (299541096 or 299541097)).Select(ToMyTraining).ToArray();
    }

    public async Task<MyTrainingDetail> ReadMyAssignmentAsync(Guid assignmentId,Guid actorId,CancellationToken token)
    {
        var assigned=await ReadAssignments(token,actorId,assignmentId);
        var assignment=assigned.Count>0?assigned[0]:throw new KeyNotFoundException("La capacitación asignada no existe o no pertenece al usuario autenticado.");
        var client=await clients.CreateAsync();var version=await DataverseMetadataResolver.TableAsync(client,"gaia_versioncapacitacion",token);var objective=version.Attribute("gaia_Objetivo");var sequential=version.Attribute("gaia_RequiereOrdenSecuencial");var completion=version.Attribute("gaia_MensajeFinalizacion");var summary=version.Attribute("gaia_Resumen");
        var versionRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{version.EntitySetName}({assignment.VersionId:D})",token)??throw new KeyNotFoundException("La versión asignada no existe.");
        var sections=await administration.ReadContentAsync(assignment.VersionId,token);var completedIds=await CompletedBlocks(client,assignmentId,token);
        await Owned(client,assignmentId,actorId,token);
        if(completedIds.Count>0||(await Attempts(client,assignmentId,token)).Count>0)
        {
            var refreshed=await RefreshCompletion(client,assignmentId,assignment.VersionId,token);
            var refreshedItems=await ReadAssignments(token,actorId,assignmentId);
            assignment=refreshedItems[0] with{Progress=refreshed.Progress,Status=refreshed.Status};
        }
        var item=ToMyTraining(assignment) with { Summary=Text(versionRow,summary) };
        var now=DateTimeOffset.UtcNow;string? unavailable=null;
        if((Number(versionRow,version.Attribute("gaia_Estado"))??0)!=299540002||(Number(versionRow,"statecode")??0)!=0)unavailable="Esta versión está cerrada para nuevas actividades; puedes consultar su historial.";
        else if(Date(versionRow,version.Attribute("gaia_InicioDisponibilidad")) is DateTimeOffset from&&from>now)unavailable=$"Disponible a partir del {from:yyyy-MM-dd HH:mm} UTC.";
        else if(Date(versionRow,version.Attribute("gaia_FinDisponibilidad"))<now)unavailable="Terminó el período de disponibilidad. Consulta al responsable de la capacitación.";
        else if(assignment.DueAt<now&&!(Boolean(versionRow,version.Attribute("gaia_PermitirContinuarVencida"))??false))unavailable="Tu fecha límite venció y esta versión no permite continuar después del vencimiento.";
        return new(item,Text(versionRow,objective),Boolean(versionRow,sequential)??true,Text(versionRow,completion),sections,completedIds.ToArray(),unavailable is null,unavailable);
    }

    public async Task<TrainingProgressResult> CompleteBlockAsync(Guid assignmentId,Guid blockId,Guid actorId,CancellationToken token,CompleteTrainingBlock? observation=null)
    {
        await Owned(await clients.CreateAsync(),assignmentId,actorId,token,true);
        var client=await clients.CreateAsync();var assignment=await DataverseMetadataResolver.TableAsync(client,"gaia_asignacioncapacitacion",token);var participantRelation=assignment.Relationship("gaia_Participante","gaia_terceros");var versionRelation=assignment.Relationship("gaia_VersionCapacitacion","gaia_versioncapacitacion");var status=assignment.Attribute("gaia_Estado");var started=assignment.Attribute("gaia_FechaInicio");
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{assignment.EntitySetName}({assignmentId:D})?$select=_{participantRelation.ReferencingAttribute}_value,_{versionRelation.ReferencingAttribute}_value,{status},{started},statecode",token)??throw new KeyNotFoundException("La asignación no existe.");
        if(GuidValue(row,$"_{participantRelation.ReferencingAttribute}_value")!=actorId)throw new UnauthorizedAccessException("La asignación no pertenece al usuario autenticado.");
        if((Number(row,"statecode")??0)!=0||(Number(row,status)??0) is 299541096 or 299541097)throw new InvalidOperationException("La asignación no está disponible para continuar.");
        var versionId=GuidValue(row,$"_{versionRelation.ReferencingAttribute}_value");var sections=await administration.ReadContentAsync(versionId,token);var ordered=sections.Where(x=>x.Active).OrderBy(x=>x.Order).SelectMany(x=>x.Blocks.Where(b=>b.Active).OrderBy(b=>b.Order)).ToArray();var selected=ordered.FirstOrDefault(x=>x.Id==blockId)??throw new ArgumentException("El bloque no pertenece a la versión asignada.");
        var completed=await CompletedBlocks(client,assignmentId,token);
        var version=await DataverseMetadataResolver.TableAsync(client,"gaia_versioncapacitacion",token);var sequentialField=version.Attribute("gaia_RequiereOrdenSecuencial");var versionRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{version.EntitySetName}({versionId:D})?$select={sequentialField}",token)??throw new KeyNotFoundException("La versión no existe.");
        if((Boolean(versionRow,sequentialField)??true)&&ordered.TakeWhile(x=>x.Id!=selected.Id).Any(x=>x.Required&&!completed.Contains(x.Id)))throw new InvalidOperationException("Completa primero los bloques anteriores de la capacitación.");
        if(observation is {ViewedPercentage:<0 or >100} or {ViewedSeconds:<0})throw new ArgumentException("El avance del video no es válido.");
        if(!completed.Contains(blockId)&&selected.Type==299541074&&selected.MinimumViewPercentage is int minimum&&(observation?.ViewedPercentage??0)<minimum)throw new InvalidOperationException($"Visualiza al menos el {minimum}% del video antes de confirmar.");
        await SaveCompletedBlock(client,assignmentId,blockId,token,selected.Type==299541074?observation:null);
        return await RefreshCompletion(client,assignmentId,versionId,token);
    }

    static MyTrainingItem ToMyTraining(TrainingAssignmentItem x)=>new(x.Id,x.VersionId,x.Training,x.Version,x.Summary,x.Status,x.AssignedAt,x.DueAt,x.Progress,x.Result);

    static async Task<HashSet<Guid>> CompletedBlocks(HttpClient client,Guid assignmentId,CancellationToken token)
    {
        var progress=await DataverseMetadataResolver.TableAsync(client,"gaia_progresobloque",token);var assignment=progress.Relationship("gaia_AsignacionCapacitacion","gaia_asignacioncapacitacion");var block=progress.Relationship("gaia_BloqueCapacitacion","gaia_bloquecapacitacion");var status=progress.Attribute("gaia_Estado");var rows=await DataverseJson.ReadAllAsync(client,$"{progress.EntitySetName}?$select=_{block.ReferencingAttribute}_value&$filter=_{assignment.ReferencingAttribute}_value eq {assignmentId:D} and {status} eq 299541102 and statecode eq 0",token);return rows.Select(x=>GuidValue(x,$"_{block.ReferencingAttribute}_value")).ToHashSet();
    }

    static async Task SaveCompletedBlock(HttpClient client,Guid assignmentId,Guid blockId,CancellationToken token,CompleteTrainingBlock? observation=null)
    {
        var progress=await DataverseMetadataResolver.TableAsync(client,"gaia_progresobloque",token);var assignment=progress.Relationship("gaia_AsignacionCapacitacion","gaia_asignacioncapacitacion");var block=progress.Relationship("gaia_BloqueCapacitacion","gaia_bloquecapacitacion");var state=progress.Attribute("gaia_Estado");var rows=await DataverseJson.ReadAllAsync(client,$"{progress.EntitySetName}?$select={progress.PrimaryIdAttribute}&$filter=_{assignment.ReferencingAttribute}_value eq {assignmentId:D} and _{block.ReferencingAttribute}_value eq {blockId:D}&$top=1",token);var now=DateTimeOffset.UtcNow;var payload=new Dictionary<string,object?>{{state,progress.EncodedIntegerValue("gaia_Estado",299541102)},{progress.Attribute("gaia_PorcentajeVisualizado"),observation?.ViewedPercentage??100m},{progress.Attribute("gaia_TiempoAcumuladoSegundos"),observation?.ViewedSeconds??0},{progress.Attribute("gaia_ConfirmacionRealizada"),true},{progress.Attribute("gaia_FechaConfirmacion"),now},{progress.Attribute("gaia_FechaFinalizacion"),now},{"statecode",0}};
        if(rows.Count>0){await Patch(client,$"{progress.EntitySetName}({GuidValue(rows[0],progress.PrimaryIdAttribute):D})",payload,token);return;}var assignmentTable=await DataverseMetadataResolver.TableAsync(client,"gaia_asignacioncapacitacion",token);var blockTable=await DataverseMetadataResolver.TableAsync(client,"gaia_bloquecapacitacion",token);payload[progress.PrimaryNameAttribute]=$"Progreso {assignmentId:N} {blockId:N}";payload[assignment.NavigationProperty+"@odata.bind"]=$"/{assignmentTable.EntitySetName}({assignmentId:D})";payload[block.NavigationProperty+"@odata.bind"]=$"/{blockTable.EntitySetName}({blockId:D})";using var response=await client.PostAsJsonAsync(progress.EntitySetName,payload,token);if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Dataverse rechazó el progreso ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(token)}");
    }

    static async Task Patch(HttpClient client,string uri,Dictionary<string,object?> payload,CancellationToken token){using var request=new HttpRequestMessage(HttpMethod.Patch,uri){Content=JsonContent.Create(payload)};request.Headers.TryAddWithoutValidation("If-Match","*");using var response=await client.SendAsync(request,token);if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Dataverse rechazó el progreso ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(token)}");}

    async Task<IReadOnlyList<TrainingAssignmentItem>> ReadAssignments(CancellationToken token,Guid? actorId=null,Guid? assignmentId=null)
    {
        var client=await clients.CreateAsync();
        var assignment=await DataverseMetadataResolver.TableAsync(client,"gaia_asignacioncapacitacion",token);
        var versionRelation=assignment.Relationship("gaia_VersionCapacitacion","gaia_versioncapacitacion");
        var participantRelation=assignment.Relationship("gaia_Participante","gaia_terceros");
        var unitRelation=assignment.Relationship("gaia_UnidadAlAsignar","gaia_organizacion");
        var state=assignment.Attribute("gaia_Estado");var assigned=assignment.Attribute("gaia_FechaAsignacion");var due=assignment.Attribute("gaia_FechaVencimiento");var started=assignment.Attribute("gaia_FechaInicio");var access=assignment.Attribute("gaia_UltimoAcceso");var completed=assignment.Attribute("gaia_FechaFinalizacion");var approved=assignment.Attribute("gaia_FechaAprobacion");var progress=assignment.Attribute("gaia_PorcentajeAvance");var result=assignment.Attribute("gaia_ResultadoPorcentaje");
        var select=string.Join(',',assignment.PrimaryIdAttribute,$"_{versionRelation.ReferencingAttribute}_value",$"_{participantRelation.ReferencingAttribute}_value",$"_{unitRelation.ReferencingAttribute}_value",state,assigned,due,started,access,completed,approved,progress,result,"statecode");
        var filter="statecode eq 0";if(actorId.HasValue)filter+=$" and _{participantRelation.ReferencingAttribute}_value eq {actorId:D}";if(assignmentId.HasValue)filter+=$" and {assignment.PrimaryIdAttribute} eq {assignmentId:D}";
        var rows=await DataverseJson.ReadAllAsync(client,$"{assignment.EntitySetName}?$select={select}&$filter={filter}&$orderby={assigned} desc",token);
        if(rows.Count==0)return [];
        var version=await DataverseMetadataResolver.TableAsync(client,"gaia_versioncapacitacion",token);var trainingRelation=version.Relationship("gaia_Capacitacion","gaia_capacitacion");var number=version.Attribute("gaia_NumeroVersion");var summary=version.Attribute("gaia_Resumen");
        var versions=await DataverseJson.ReadAllAsync(client,$"{version.EntitySetName}?$select={version.PrimaryIdAttribute},_{trainingRelation.ReferencingAttribute}_value,{number},{summary}",token);
        var training=await DataverseMetadataResolver.TableAsync(client,"gaia_capacitacion",token);var trainings=(await DataverseJson.ReadAllAsync(client,$"{training.EntitySetName}?$select={training.PrimaryIdAttribute},{training.PrimaryNameAttribute}",token)).ToDictionary(x=>GuidValue(x,training.PrimaryIdAttribute),x=>Text(x,training.PrimaryNameAttribute)??"Capacitación");
        var versionMap=versions.ToDictionary(x=>GuidValue(x,version.PrimaryIdAttribute),x=>(Training:trainings.GetValueOrDefault(GuidValue(x,$"_{trainingRelation.ReferencingAttribute}_value"))??"Capacitación",Version:Text(x,number)??"",Summary:Text(x,summary)));
        var people=await directory.ListPeopleAsync(null,null,false,1,5000,token);var personMap=people.Items.ToDictionary(x=>x.Id,x=>x.FullName);var unitMap=(await directory.ListOrganizationUnitsAsync(token)).ToDictionary(x=>x.Id,x=>x.Name);
        var attempt=await DataverseMetadataResolver.TableAsync(client,"gaia_intentoevaluacion",token);var attemptAssignment=attempt.Relationship("gaia_AsignacionCapacitacion","gaia_asignacioncapacitacion");var attempts=(await DataverseJson.ReadAllAsync(client,$"{attempt.EntitySetName}?$select={attempt.PrimaryIdAttribute},_{attemptAssignment.ReferencingAttribute}_value&$filter=statecode eq 0",token)).GroupBy(x=>GuidValue(x,$"_{attemptAssignment.ReferencingAttribute}_value")).ToDictionary(x=>x.Key,x=>x.Count());
        return rows.Select(row=>{var versionId=GuidValue(row,$"_{versionRelation.ReferencingAttribute}_value");var participantId=GuidValue(row,$"_{participantRelation.ReferencingAttribute}_value");var unitId=OptionalGuid(row,$"_{unitRelation.ReferencingAttribute}_value");var info=versionMap.GetValueOrDefault(versionId);var id=GuidValue(row,assignment.PrimaryIdAttribute);return new TrainingAssignmentItem(id,versionId,participantId,info.Training??"Capacitación",info.Version??"",info.Summary,personMap.GetValueOrDefault(participantId)??"Participante",unitId,unitId.HasValue?unitMap.GetValueOrDefault(unitId.Value):null,Number(row,state)??299541090,Date(row,assigned)??DateTimeOffset.MinValue,Date(row,due),Date(row,started),Date(row,access),Date(row,completed),Date(row,approved),Decimal(row,progress)??0,Decimal(row,result),attempts.GetValueOrDefault(id));}).ToArray();
    }

    static int OriginFor(TrainingAudiencePerson person,IReadOnlyList<TrainingAudienceRuleItem> rules)=>rules.Any(x=>x.Mode==299541010&&x.Type==299541002&&x.PersonId==person.Id)?299541082:rules.Any(x=>x.Mode==299541010&&x.Type==299541001)?299541081:299541080;
    static string Limit(string value,int maximum)=>value.Length<=maximum?value:value[..maximum];
    static string? Text(JsonElement row,string property)=>row.TryGetProperty(property,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    static int? Number(JsonElement row,string property)=>DataverseJson.OptionalEncodedInt32(row,property);
    static decimal? Decimal(JsonElement row,string property)=>row.TryGetProperty(property,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var number)?number:null;
    static bool? Boolean(JsonElement row,string property)=>row.TryGetProperty(property,out var value)&&value.ValueKind is JsonValueKind.True or JsonValueKind.False?value.GetBoolean():null;
    static DateTimeOffset? Date(JsonElement row,string property)=>DateTimeOffset.TryParse(Text(row,property),out var value)?value:null;
    static Guid? OptionalGuid(JsonElement row,string property)=>Guid.TryParse(Text(row,property),out var value)?value:null;
    static Guid GuidValue(JsonElement row,string property)=>OptionalGuid(row,property)??throw new InvalidOperationException($"Dataverse no devolvió {property}.");
}
