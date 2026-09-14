using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Training;

namespace Gaia.Api.Infrastructure.Dataverse.Training;

internal sealed partial class DataverseTrainingOperations
{
    static string Lookup(DataverseTableMetadata table,string field,string target) => "_"+table.Relationship(field,target).ReferencingAttribute+"_value";
    static void Bind(Dictionary<string,object?> payload,DataverseTableMetadata table,string field,DataverseTableMetadata target,Guid id) => payload[table.Relationship(field,target.LogicalName).NavigationProperty+"@odata.bind"]=$"/{target.EntitySetName}({id:D})";

    static async Task<(Guid VersionId,JsonElement Row,DataverseTableMetadata Table)> Owned(HttpClient client,Guid id,Guid actor,CancellationToken token,bool mutate=false)
    {
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_asignacioncapacitacion",token);
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{table.EntitySetName}({id:D})",token)??throw new KeyNotFoundException("La capacitación asignada no existe.");
        if(OptionalGuid(row,Lookup(table,"gaia_Participante","gaia_terceros"))!=actor)throw new KeyNotFoundException("La capacitación no pertenece a tu cuenta.");
        if((Number(row,"statecode")??0)!=0||(Number(row,table.Attribute("gaia_Estado"))??0) is 299541096 or 299541097)throw new InvalidOperationException("Esta asignación no está disponible.");
        var versionId=GuidValue(row,Lookup(table,"gaia_VersionCapacitacion","gaia_versioncapacitacion"));
        if(mutate)
        {
            var version=await DataverseMetadataResolver.TableAsync(client,"gaia_versioncapacitacion",token);
            var v=await DataverseMetadataResolver.ReadOneAsync(client,$"{version.EntitySetName}({versionId:D})",token)??throw new KeyNotFoundException("La versión no existe.");
            var now=DateTimeOffset.UtcNow;var allow=Boolean(v,version.Attribute("gaia_PermitirContinuarVencida"))??false;
            if((Number(v,version.Attribute("gaia_Estado"))??0)!=299540002||(Number(v,"statecode")??0)!=0)throw new InvalidOperationException("Esta versión no está abierta para realizar actividades.");
            if(Date(v,version.Attribute("gaia_InicioDisponibilidad"))>now)throw new InvalidOperationException("La capacitación aún no está disponible.");
            if(Date(v,version.Attribute("gaia_FinDisponibilidad"))<now)throw new InvalidOperationException("Terminó el período de disponibilidad de la capacitación.");
            if(!allow&&Date(row,table.Attribute("gaia_FechaVencimiento"))<now)throw new InvalidOperationException("Tu fecha límite venció. Solicita al responsable que revise tu asignación.");
        }
        return(versionId,row,table);
    }

    static async Task<IReadOnlyList<JsonElement>> Attempts(HttpClient client,Guid assignmentId,CancellationToken token,bool includeInactive=false)
    {
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_intentoevaluacion",token);
        return await DataverseJson.ReadAllAsync(client,$"{table.EntitySetName}?$filter={Lookup(table,"gaia_AsignacionCapacitacion","gaia_asignacioncapacitacion")} eq {assignmentId:D} {(includeInactive?"":"and statecode eq 0")}&$orderby={table.Attribute("gaia_NumeroIntento")} asc",token);
    }

    public async Task<IReadOnlyList<ParticipantEvaluation>> ReadEvaluationsAsync(Guid assignmentId,Guid actorId,CancellationToken token)
    {
        var client=await clients.CreateAsync();var owned=await Owned(client,assignmentId,actorId,token);
        var evaluations=await administration.ReadEvaluationsAsync(owned.VersionId,token);var rows=await Attempts(client,assignmentId,token);
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_intentoevaluacion",token);
        var items=new List<ParticipantEvaluation>();
        foreach(var e in evaluations.OrderBy(x=>x.Order))
        {
            var attempts=new List<ParticipantAttempt>();
            foreach(var row in rows.Where(x=>OptionalGuid(x,Lookup(table,"gaia_Evaluacion","gaia_evaluacion"))==e.Id))attempts.Add(await PublicAttempt(client,row,e,token));
            var active=attempts.LastOrDefault(x=>x.Status==299541110);var seed=active?.Id??e.Id;
            var questions=e.Questions.OrderBy(x=>x.Order).ToArray();if(e.RandomizeQuestions)questions=Shuffle(questions,seed);
            items.Add(new(e.Id,e.Name,e.Description,e.Type,e.Required,e.AffectsApproval,e.MaximumAttempts,e.AllowRetry,e.TimeLimitMinutes,questions.Select(q=>new ParticipantQuestion(q.Id,q.Statement,q.Type,q.Required,q.MinimumScale,q.MaximumScale,q.MinimumLabel,q.MaximumLabel,(e.RandomizeOptions?Shuffle(q.Options.ToArray(),seed):q.Options.OrderBy(x=>x.Order).ToArray()).Select(o=>new ParticipantOption(o.Id,o.Name)).ToArray())).ToArray(),attempts));
        }
        return items;
    }

    static T[] Shuffle<T>(T[] values,Guid seed){var copy=values.ToArray();var random=new Random(BitConverter.ToInt32(seed.ToByteArray(),0));random.Shuffle(copy);return copy;}

    public async Task<ParticipantAttempt> StartAttemptAsync(Guid assignmentId,Guid evaluationId,Guid idempotencyToken,Guid actorId,CancellationToken token)
    {
        if(idempotencyToken==Guid.Empty)throw new ArgumentException("Falta el identificador del intento.");
        var client=await clients.CreateAsync();var owned=await Owned(client,assignmentId,actorId,token,true);
        var evaluation=(await administration.ReadEvaluationsAsync(owned.VersionId,token)).FirstOrDefault(x=>x.Id==evaluationId)??throw new KeyNotFoundException("La evaluación no pertenece a esta capacitación.");
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_intentoevaluacion",token);
        var attempts=(await Attempts(client,assignmentId,token)).Where(x=>OptionalGuid(x,Lookup(table,"gaia_Evaluacion","gaia_evaluacion"))==evaluationId).ToArray();
        var repeated=attempts.FirstOrDefault(x=>Text(x,table.Attribute("gaia_TokenIdempotencia"))==idempotencyToken.ToString("D"));
        if(repeated.ValueKind!=JsonValueKind.Undefined)return await PublicAttempt(client,repeated,evaluation,token);
        var active=attempts.FirstOrDefault(x=>Number(x,table.Attribute("gaia_Estado"))==299541110);
        if(active.ValueKind!=JsonValueKind.Undefined)return await PublicAttempt(client,active,evaluation,token);
        if(attempts.Any(x=>Boolean(x,table.Attribute("gaia_Aprobado"))==true||Number(x,table.Attribute("gaia_Estado"))==299541112))throw new InvalidOperationException("La evaluación ya fue aprobada o está pendiente de revisión.");
        if((evaluation.MaximumAttempts.HasValue&&attempts.Length>=evaluation.MaximumAttempts.Value)||(attempts.Length>0&&!evaluation.AllowRetry))throw new InvalidOperationException("Ya utilizaste los intentos permitidos para esta evaluación.");
        var sections=await administration.ReadContentAsync(owned.VersionId,token);var completed=await CompletedBlocks(client,assignmentId,token);
        if(evaluation.Type!=299541051&&sections.Where(x=>x.Active).SelectMany(x=>x.Blocks).Any(x=>x.Active&&x.Required&&!completed.Contains(x.Id)))throw new InvalidOperationException("Completa el material obligatorio antes de iniciar esta evaluación.");
        if(evaluation.Questions.Count==0)throw new InvalidOperationException("La evaluación no tiene preguntas configuradas.");
        var previous=await Attempts(client,assignmentId,token,true);var nextNumber=previous.Where(x=>OptionalGuid(x,Lookup(table,"gaia_Evaluacion","gaia_evaluacion"))==evaluationId).Select(x=>Number(x,table.Attribute("gaia_NumeroIntento"))??0).DefaultIfEmpty(0).Max()+1;var assignment=owned.Table;var eTable=await DataverseMetadataResolver.TableAsync(client,"gaia_evaluacion",token);var id=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        var payload=new Dictionary<string,object?>{[table.PrimaryIdAttribute]=id,[table.PrimaryNameAttribute]=$"Intento {nextNumber} · {assignmentId:N}",[table.Attribute("gaia_NumeroIntento")]=nextNumber,[table.Attribute("gaia_Estado")]=table.EncodedIntegerValue("gaia_Estado",299541110),[table.Attribute("gaia_FechaInicio")]=now,[table.Attribute("gaia_TokenIdempotencia")]=idempotencyToken.ToString("D")};
        Bind(payload,table,"gaia_AsignacionCapacitacion",assignment,assignmentId);Bind(payload,table,"gaia_Evaluacion",eTable,evaluationId);
        using var response=await client.PostAsJsonAsync(table.EntitySetName,payload,token);
        if(!response.IsSuccessStatusCode)
        {
            var existing=(await Attempts(client,assignmentId,token)).FirstOrDefault(x=>OptionalGuid(x,Lookup(table,"gaia_Evaluacion","gaia_evaluacion"))==evaluationId&&Number(x,table.Attribute("gaia_Estado"))==299541110);
            if(existing.ValueKind!=JsonValueKind.Undefined)return await PublicAttempt(client,existing,evaluation,token);
            throw new InvalidOperationException("No fue posible iniciar la evaluación. Actualiza la página e inténtalo nuevamente.");
        }
        var saved=await DataverseMetadataResolver.ReadOneAsync(client,$"{table.EntitySetName}({id:D})",token)??throw new InvalidOperationException("No fue posible recuperar el intento creado.");
        return await PublicAttempt(client,saved,evaluation,token);
    }

    public async Task<ParticipantAttempt> SubmitAttemptAsync(Guid assignmentId,Guid attemptId,SubmitTrainingAttempt value,Guid actorId,CancellationToken token)
    {
        var client=await clients.CreateAsync();var owned=await Owned(client,assignmentId,actorId,token);
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_intentoevaluacion",token);var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{table.EntitySetName}({attemptId:D})",token)??throw new KeyNotFoundException("El intento no existe.");
        if(OptionalGuid(row,Lookup(table,"gaia_AsignacionCapacitacion","gaia_asignacioncapacitacion"))!=assignmentId)throw new KeyNotFoundException("El intento no pertenece a esta asignación.");
        var evaluation=(await administration.ReadEvaluationsAsync(owned.VersionId,token)).FirstOrDefault(x=>x.Id==OptionalGuid(row,Lookup(table,"gaia_Evaluacion","gaia_evaluacion")))??throw new KeyNotFoundException("La evaluación no existe.");
        if(Number(row,table.Attribute("gaia_Estado"))!=299541110){await RefreshCompletion(client,assignmentId,owned.VersionId,token);return await PublicAttempt(client,row,evaluation,token);}
        await Owned(client,assignmentId,actorId,token,true);
        var now=DateTimeOffset.UtcNow;var started=Date(row,table.Attribute("gaia_FechaInicio"))??now;var timeout=evaluation.TimeLimitMinutes.HasValue&&now>started.AddMinutes(evaluation.TimeLimitMinutes.Value);
        var scored=TrainingEvaluationScoring.Score(evaluation,value.Answers??[],timeout);
        var answers=await DataverseMetadataResolver.TableAsync(client,"gaia_respuestapregunta",token);var questions=await DataverseMetadataResolver.TableAsync(client,"gaia_preguntaevaluacion",token);
        var junction=await DataverseMetadataResolver.TableAsync(client,"gaia_respuestaopcion",token);var options=await DataverseMetadataResolver.TableAsync(client,"gaia_opcionpregunta",token);
        var writes=new List<AssessmentWrite>();
        foreach(var result in scored.Answers)
        {
            var id=Guid.NewGuid();var payload=new Dictionary<string,object?>{[answers.PrimaryIdAttribute]=id,[answers.PrimaryNameAttribute]=$"Respuesta {attemptId:N} {result.Question.Id:N}",[answers.Attribute("gaia_ValorTexto")]=result.Answer.Text,[answers.Attribute("gaia_ValorNumerico")]=result.Answer.Number,[answers.Attribute("gaia_PuntajeOtorgado")]=result.Score,[answers.Attribute("gaia_EsCorrecta")]=result.Correct,[answers.Attribute("gaia_RequiereRevision")]=result.Manual};
            if(result.Question.Type==299541062&&result.Answer.OptionId.HasValue){var name=result.Question.Options.First(x=>x.Id==result.Answer.OptionId).Name.Trim();payload[answers.Attribute("gaia_ValorSiNo")]=name.Equals("Sí",StringComparison.OrdinalIgnoreCase)||name.Equals("Si",StringComparison.OrdinalIgnoreCase);}
            Bind(payload,answers,"gaia_IntentoEvaluacion",table,attemptId);Bind(payload,answers,"gaia_PreguntaEvaluacion",questions,result.Question.Id);writes.Add(new("POST",answers.EntitySetName,payload));
            if(result.Answer.OptionId is Guid optionId)
            {
                var link=new Dictionary<string,object?>{[junction.PrimaryNameAttribute]=$"Opción {id:N}",[junction.Attribute("gaia_PuntajeAplicado")]=result.Score};Bind(link,junction,"gaia_RespuestaPregunta",answers,id);Bind(link,junction,"gaia_OpcionPregunta",options,optionId);writes.Add(new("POST",junction.EntitySetName,link));
            }
        }
        var threshold=await Threshold(client,owned.VersionId,evaluation,token);
        var update=AttemptGrade(table,scored.Score,scored.Maximum,scored.Percentage,scored.PendingReview,threshold,now);
        update[table.Attribute("gaia_FechaEnvio")]=now;update[table.Attribute("gaia_TiempoEmpleadoSegundos")]=(int)Math.Clamp((now-started).TotalSeconds,0,int.MaxValue);
        if(timeout)update[table.Attribute("gaia_Observacion")]="El intento fue enviado después de terminar el tiempo permitido.";
        if(timeout)update[table.Attribute("gaia_Aprobado")]=false;
        writes.Add(new("PATCH",$"{table.EntitySetName}({attemptId:D})",update,Text(row,"@odata.etag")??"*"));
        await Atomic(client,writes,token);
        await RefreshCompletion(client,assignmentId,owned.VersionId,token);
        var saved=await DataverseMetadataResolver.ReadOneAsync(client,$"{table.EntitySetName}({attemptId:D})",token)??throw new KeyNotFoundException("El intento no existe.");return await PublicAttempt(client,saved,evaluation,token);
    }

    static Dictionary<string,object?> AttemptGrade(DataverseTableMetadata table,decimal score,decimal max,decimal percent,bool pending,decimal threshold,DateTimeOffset now)=>new(){[table.Attribute("gaia_Estado")]=table.EncodedIntegerValue("gaia_Estado",pending?299541112:299541113),[table.Attribute("gaia_PuntajeObtenido")]=score,[table.Attribute("gaia_PuntajeMaximo")]=max,[table.Attribute("gaia_PorcentajeObtenido")]=pending?null:percent,[table.Attribute("gaia_Aprobado")]=pending?null:percent>=threshold,[table.Attribute("gaia_FechaCalificacion")]=pending?null:now};

    static async Task<decimal> Threshold(HttpClient client,Guid versionId,TrainingEvaluationItem e,CancellationToken token)
    {
        if(e.MinimumPercentage.HasValue)return e.MinimumPercentage.Value;var table=await DataverseMetadataResolver.TableAsync(client,"gaia_versioncapacitacion",token);var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{table.EntitySetName}({versionId:D})",token);return row.HasValue?Decimal(row.Value,table.Attribute("gaia_PorcentajeMinimoGeneral"))??70:70;
    }

    static async Task<ParticipantAttempt> PublicAttempt(HttpClient client,JsonElement row,TrainingEvaluationItem e,CancellationToken token)
    {
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_intentoevaluacion",token);var id=GuidValue(row,table.PrimaryIdAttribute);var status=Number(row,table.Attribute("gaia_Estado"))??299541110;var started=Date(row,table.Attribute("gaia_FechaInicio"))??DateTimeOffset.MinValue;
        var results=new List<ParticipantAnswerResult>();
        if(status==299541113&&(e.ShowResult||e.ShowCorrectAnswers||e.ShowFeedback))
        {
            var answerTable=await DataverseMetadataResolver.TableAsync(client,"gaia_respuestapregunta",token);var answers=await DataverseJson.ReadAllAsync(client,$"{answerTable.EntitySetName}?$filter={Lookup(answerTable,"gaia_IntentoEvaluacion","gaia_intentoevaluacion")} eq {id:D}",token);
            foreach(var answer in answers)
            {
                var q=e.Questions.FirstOrDefault(x=>x.Id==OptionalGuid(answer,Lookup(answerTable,"gaia_PreguntaEvaluacion","gaia_preguntaevaluacion")));if(q is null)continue;var correct=Boolean(answer,answerTable.Attribute("gaia_EsCorrecta"));
                results.Add(new(q.Id,e.ShowResult?Decimal(answer,answerTable.Attribute("gaia_PuntajeOtorgado")):null,e.ShowResult?correct:null,e.ShowFeedback?(Text(answer,answerTable.Attribute("gaia_ComentarioRevisor"))??(correct==true?q.CorrectFeedback:q.IncorrectFeedback)):null,e.ShowCorrectAnswers?q.Options.Where(x=>x.IsCorrect).Select(x=>x.Id).ToArray():null));
            }
        }
        return new(id,Number(row,table.Attribute("gaia_NumeroIntento"))??1,status,started,Date(row,table.Attribute("gaia_FechaEnvio")),e.TimeLimitMinutes.HasValue?started.AddMinutes(e.TimeLimitMinutes.Value):null,e.ShowResult?Decimal(row,table.Attribute("gaia_PorcentajeObtenido")):null,Boolean(row,table.Attribute("gaia_Aprobado")),results);
    }

    async Task<TrainingProgressResult> RefreshCompletion(HttpClient client,Guid assignmentId,Guid versionId,CancellationToken token,int retry=0)
    {
        var assignment=await DataverseMetadataResolver.TableAsync(client,"gaia_asignacioncapacitacion",token);var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{assignment.EntitySetName}({assignmentId:D})",token)??throw new KeyNotFoundException("La asignación no existe.");
        var sections=await administration.ReadContentAsync(versionId,token);var required=sections.Where(x=>x.Active).SelectMany(x=>x.Blocks).Where(x=>x.Active&&x.Required).ToArray();var completed=await CompletedBlocks(client,assignmentId,token);var progress=required.Length==0?100:Math.Round(required.Count(x=>completed.Contains(x.Id))*100m/required.Length,2);
        var evaluations=await administration.ReadEvaluationsAsync(versionId,token);var attempts=await Attempts(client,assignmentId,token);var table=await DataverseMetadataResolver.TableAsync(client,"gaia_intentoevaluacion",token);
        var survey=Boolean(row,assignment.Attribute("gaia_RequiereEncuesta"))??false;
        var outcome=TrainingCompletionPolicy.Evaluate(progress,survey,evaluations,attempts.Select(a=>new TrainingAttemptOutcome(GuidValue(a,Lookup(table,"gaia_Evaluacion","gaia_evaluacion")),Number(a,table.Attribute("gaia_Estado"))??299541110,Boolean(a,table.Attribute("gaia_Aprobado")),Decimal(a,table.Attribute("gaia_PuntajeObtenido")),Decimal(a,table.Attribute("gaia_PuntajeMaximo")),Decimal(a,table.Attribute("gaia_PorcentajeObtenido")))).ToArray());
        var done=outcome.Completed;var result=outcome.Result;var status=outcome.Status;var now=DateTimeOffset.UtcNow;
        var payload=new Dictionary<string,object?>{[assignment.Attribute("gaia_PorcentajeAvance")]=progress,[assignment.Attribute("gaia_Estado")]=assignment.EncodedIntegerValue("gaia_Estado",status),[assignment.Attribute("gaia_UltimoAcceso")]=now,[assignment.Attribute("gaia_ResultadoPorcentaje")]=result,[assignment.Attribute("gaia_FechaFinalizacion")]=done?(Date(row,assignment.Attribute("gaia_FechaFinalizacion"))??now):null,[assignment.Attribute("gaia_FechaAprobacion")]=done?(Date(row,assignment.Attribute("gaia_FechaAprobacion"))??now):null};
        if(Date(row,assignment.Attribute("gaia_FechaInicio")) is null)payload[assignment.Attribute("gaia_FechaInicio")]=now;
        using var request=new HttpRequestMessage(HttpMethod.Patch,$"{assignment.EntitySetName}({assignmentId:D})"){Content=JsonContent.Create(payload)};request.Headers.TryAddWithoutValidation("If-Match",Text(row,"@odata.etag")??"*");using var response=await client.SendAsync(request,token);
        if(response.StatusCode==System.Net.HttpStatusCode.PreconditionFailed&&retry<3)return await RefreshCompletion(client,assignmentId,versionId,token,retry+1);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Tus respuestas se guardaron, pero no fue posible actualizar el avance. Vuelve a abrir la capacitación para actualizar el resultado.");
        return new(progress,status,done);
    }

    public async Task<IReadOnlyList<TrainingPendingReview>> ReadPendingReviewsAsync(CancellationToken token)
    {
        var client=await clients.CreateAsync();var table=await DataverseMetadataResolver.TableAsync(client,"gaia_intentoevaluacion",token);var rows=await DataverseJson.ReadAllAsync(client,$"{table.EntitySetName}?$filter={table.Attribute("gaia_Estado")} eq {table.EncodedIntegerLiteral("gaia_Estado",299541112)} and statecode eq 0",token);
        var assignments=await ReadAssignments(token);var answerTable=await DataverseMetadataResolver.TableAsync(client,"gaia_respuestapregunta",token);var items=new List<TrainingPendingReview>();
        foreach(var row in rows)
        {
            var id=GuidValue(row,table.PrimaryIdAttribute);var assignment=assignments.FirstOrDefault(x=>x.Id==OptionalGuid(row,Lookup(table,"gaia_AsignacionCapacitacion","gaia_asignacioncapacitacion")));if(assignment is null)continue;
            var evaluation=(await administration.ReadEvaluationsAsync(assignment.VersionId,token)).FirstOrDefault(x=>x.Id==OptionalGuid(row,Lookup(table,"gaia_Evaluacion","gaia_evaluacion")));if(evaluation is null)continue;
            var answers=await DataverseJson.ReadAllAsync(client,$"{answerTable.EntitySetName}?$filter={Lookup(answerTable,"gaia_IntentoEvaluacion","gaia_intentoevaluacion")} eq {id:D} and {answerTable.Attribute("gaia_RequiereRevision")} eq true",token);
            items.Add(new(id,assignment.Id,assignment.VersionId,assignment.Participant,assignment.Training,evaluation.Name,Date(row,table.Attribute("gaia_FechaEnvio")),answers.Select(a=>{var q=evaluation.Questions.First(x=>x.Id==GuidValue(a,Lookup(answerTable,"gaia_PreguntaEvaluacion","gaia_preguntaevaluacion")));return new TrainingManualAnswer(GuidValue(a,answerTable.PrimaryIdAttribute),q.Statement,Text(a,answerTable.Attribute("gaia_ValorTexto")),Decimal(a,answerTable.Attribute("gaia_ValorNumerico")),TrainingEvaluationScoring.Maximum(q));}).ToArray()));
        }
        return items;
    }

    public async Task GradeAttemptAsync(Guid attemptId,GradeTrainingAttempt value,Guid actorId,CancellationToken token)
    {
        var review=(await ReadPendingReviewsAsync(token)).FirstOrDefault(x=>x.AttemptId==attemptId)??throw new InvalidOperationException("El intento ya fue revisado o no requiere revisión.");
        if(value.Answers is null||value.Answers.Count!=review.Answers.Count||value.Answers.Select(x=>x.AnswerId).Distinct().Count()!=value.Answers.Count||value.Answers.Any(x=>review.Answers.All(a=>a.Id!=x.AnswerId)))throw new ArgumentException("Califica todas las respuestas pendientes una sola vez.");
        var client=await clients.CreateAsync();var table=await DataverseMetadataResolver.TableAsync(client,"gaia_intentoevaluacion",token);var answerTable=await DataverseMetadataResolver.TableAsync(client,"gaia_respuestapregunta",token);var people=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);
        var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{table.EntitySetName}({attemptId:D})",token)??throw new KeyNotFoundException("El intento no existe.");if(Number(row,table.Attribute("gaia_Estado"))!=299541112)throw new InvalidOperationException("Otro usuario ya calificó este intento.");
        var writes=new List<AssessmentWrite>();var now=DateTimeOffset.UtcNow;
        foreach(var grade in value.Answers)
        {
            var answer=review.Answers.First(x=>x.Id==grade.AnswerId);if(grade.Score<0||grade.Score>answer.MaximumScore||grade.Comment?.Length>1000)throw new ArgumentException("El puntaje debe estar entre cero y el máximo de la pregunta; el comentario admite hasta 1000 caracteres.");
            var payload=new Dictionary<string,object?>{[answerTable.Attribute("gaia_PuntajeOtorgado")]=grade.Score,[answerTable.Attribute("gaia_EsCorrecta")]=grade.Score==answer.MaximumScore,[answerTable.Attribute("gaia_RequiereRevision")]=false,[answerTable.Attribute("gaia_FechaRevision")]=now,[answerTable.Attribute("gaia_ComentarioRevisor")]=grade.Comment};Bind(payload,answerTable,"gaia_RevisadaPor",people,actorId);writes.Add(new("PATCH",$"{answerTable.EntitySetName}({grade.AnswerId:D})",payload));
        }
        var evaluation=(await administration.ReadEvaluationsAsync(review.VersionId,token)).First(x=>x.Id==GuidValue(row,Lookup(table,"gaia_Evaluacion","gaia_evaluacion")));var max=Decimal(row,table.Attribute("gaia_PuntajeMaximo"))??0;var score=(Decimal(row,table.Attribute("gaia_PuntajeObtenido"))??0)+value.Answers.Sum(x=>x.Score);var percent=max==0?100:Math.Round(score*100/max,2);
        var update=AttemptGrade(table,score,max,percent,false,await Threshold(client,review.VersionId,evaluation,token),now);Bind(update,table,"gaia_CalificadoPor",people,actorId);writes.Add(new("PATCH",$"{table.EntitySetName}({attemptId:D})",update,Text(row,"@odata.etag")??"*"));await Atomic(client,writes,token);await RefreshCompletion(client,review.AssignmentId,review.VersionId,token);
    }

    public async Task<ExternalFileId> ReadResourceAsync(Guid assignmentId,Guid resourceId,Guid actorId,CancellationToken token)
    {
        var client=await clients.CreateAsync();var owned=await Owned(client,assignmentId,actorId,token);
        return await ReadVersionResourceAsync(owned.VersionId,resourceId,token);
    }

    public async Task<ExternalFileId> ReadVersionResourceAsync(Guid versionId,Guid resourceId,CancellationToken token)
    {
        var client=await clients.CreateAsync();var content=await administration.ReadContentAsync(versionId,token);
        if(!content.Where(x=>x.Active).SelectMany(x=>x.Blocks).Any(x=>x.Active&&x.ResourceId==resourceId))throw new KeyNotFoundException("El archivo no pertenece al contenido de esta capacitación.");
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_recursocapacitacion",token);var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{table.EntitySetName}({resourceId:D})",token)??throw new KeyNotFoundException("El archivo no existe.");
        if(OptionalGuid(row,Lookup(table,"gaia_VersionCapacitacion","gaia_versioncapacitacion"))!=versionId||(Number(row,"statecode")??0)!=0||Number(row,table.Attribute("gaia_EstadoArchivo"))!=299541041)throw new InvalidOperationException("El archivo no está disponible.");
        return new("SharePoint",Text(row,table.Attribute("gaia_RepositorioExternoId"))??"",Text(row,table.Attribute("gaia_ContenedorExternoId"))??"",Text(row,table.Attribute("gaia_ArchivoExternoId"))??"");
    }

    sealed record AssessmentWrite(string Method,string Path,Dictionary<string,object?> Body,string? ETag=null);
    // A changeset is transactional: answers cannot be stored without closing their attempt.
    static async Task Atomic(HttpClient client,IReadOnlyList<AssessmentWrite> writes,CancellationToken token)
    {
        var batch="batch_"+Guid.NewGuid().ToString("N");var change="changeset_"+Guid.NewGuid().ToString("N");var body=new StringBuilder($"--{batch}\r\nContent-Type: multipart/mixed; boundary={change}\r\n\r\n");var index=0;
        foreach(var write in writes)
        {
            body.Append(System.Globalization.CultureInfo.InvariantCulture,$"--{change}\r\nContent-Type: application/http\r\nContent-Transfer-Encoding: binary\r\nContent-ID: {++index}\r\n\r\n{write.Method} {new Uri(client.BaseAddress!,write.Path)} HTTP/1.1\r\nContent-Type: application/json\r\n");if(write.ETag is not null)body.Append(System.Globalization.CultureInfo.InvariantCulture,$"If-Match: {write.ETag}\r\n");body.Append(System.Globalization.CultureInfo.InvariantCulture,$"\r\n{JsonSerializer.Serialize(write.Body)}\r\n");
        }
        body.Append(System.Globalization.CultureInfo.InvariantCulture,$"--{change}--\r\n--{batch}--\r\n");using var request=new HttpRequestMessage(HttpMethod.Post,"$batch"){Content=new StringContent(body.ToString(),Encoding.UTF8)};request.Content.Headers.ContentType=System.Net.Http.Headers.MediaTypeHeaderValue.Parse($"multipart/mixed; boundary={batch}");using var response=await client.SendAsync(request,token);var text=await response.Content.ReadAsStringAsync(token);
        if(!response.IsSuccessStatusCode||System.Text.RegularExpressions.Regex.IsMatch(text,@"HTTP/1\.[01] [45]\d\d"))throw new InvalidOperationException("No fue posible guardar las respuestas. Otro envío pudo actualizar el intento; actualiza la página antes de reintentar.");
    }
}
