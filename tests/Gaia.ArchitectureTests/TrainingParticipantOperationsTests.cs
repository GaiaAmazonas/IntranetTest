using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.Training;
using Gaia.Modules.Training;

namespace Gaia.ArchitectureTests;

public sealed class TrainingParticipantOperationsTests
{
    [Fact] public async Task AnotherUserCannotReadEvaluationOrStartAttempt()
    {
        var handler=new Handler();var operations=Create(handler);
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>operations.ReadEvaluationsAsync(handler.Assignment,Guid.NewGuid(),CancellationToken.None));
        Assert.Equal(0,handler.Writes);
    }
    [Fact] public async Task PublicQuestionsNeverContainCorrectnessOrPoints()
    {
        var handler=new Handler();var questions=await Create(handler).ReadEvaluationsAsync(handler.Assignment,handler.Actor,CancellationToken.None);
        var json=JsonSerializer.Serialize(questions);Assert.DoesNotContain("IsCorrect",json);Assert.DoesNotContain("CorrectFeedback",json);Assert.DoesNotContain("MaximumScore",json);Assert.Single(questions[0].Questions);
    }
    [Fact] public async Task FutureAvailabilityPreventsStartingAttempt()
    {
        var handler=new Handler{Future=true};var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>Create(handler).StartAttemptAsync(handler.Assignment,handler.Evaluation.Id,Guid.NewGuid(),handler.Actor,CancellationToken.None));
        Assert.Contains("aún no está disponible",error.Message);Assert.Equal(0,handler.Writes);
    }
    [Fact] public async Task ExpiredDueDatePreventsStartingAttempt()
    {
        var handler=new Handler{Overdue=true};var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>Create(handler).StartAttemptAsync(handler.Assignment,handler.Evaluation.Id,Guid.NewGuid(),handler.Actor,CancellationToken.None));
        Assert.Contains("fecha límite venció",error.Message);Assert.Equal(0,handler.Writes);
    }
    [Fact] public async Task ForeignResourceIsRejectedBeforeReadingStorage()
    {
        var handler=new Handler();await Assert.ThrowsAsync<KeyNotFoundException>(()=>Create(handler).ReadResourceAsync(handler.Assignment,Guid.NewGuid(),handler.Actor,CancellationToken.None));Assert.Equal(0,handler.Writes);
    }
    static DataverseTrainingOperations Create(Handler handler)
    {
        var admin=DispatchProxy.Create<ITrainingAdministrationReader,AdminProxy>();((AdminProxy)(object)admin).Evaluation=handler.Evaluation;
        return new(new Factory(handler),admin,null!);
    }
    public class AdminProxy:DispatchProxy
    {
        public TrainingEvaluationItem Evaluation {get;set;}=null!;
        protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)=>targetMethod?.Name switch
        {
            "ReadEvaluationsAsync"=>Task.FromResult<IReadOnlyList<TrainingEvaluationItem>>([Evaluation]),
            "ReadContentAsync"=>Task.FromResult<IReadOnlyList<TrainingSectionItem>>([]),
            _=>throw new NotSupportedException()
        };
    }
    sealed class Factory(Handler handler):IDataverseDelegatedClientFactory
    {
        public Task<HttpClient> CreateAsync()=>Task.FromResult(new HttpClient(handler){BaseAddress=new Uri("https://example/api/data/v9.2/")});
    }
    sealed class Handler:HttpMessageHandler
    {
        public Guid Assignment {get;}=Guid.NewGuid();public Guid Actor {get;}=Guid.NewGuid();public Guid Version {get;}=Guid.NewGuid();public bool Future {get;init;}public bool Overdue {get;init;}public int Writes {get;private set;}
        public TrainingEvaluationItem Evaluation {get;}
        public Handler()
        {
            var id=Guid.NewGuid();var questionId=Guid.NewGuid();Evaluation=new(id,Version,"Prueba",299541050,null,true,true,70,3,null,false,false,true,false,true,true,1,[new(questionId,id,"Pregunta","¿Cuál?",299541061,true,1,20,true,false,null,null,null,null,"secreto correcto","secreto incorrecto",[new(Guid.NewGuid(),questionId,"A","Opción",1,true,20,"secreto opción")])]);
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            if(request.Method!=HttpMethod.Get){Writes++;throw new InvalidOperationException("Esta prueba no debe escribir.");}
            var path=request.RequestUri!.PathAndQuery;object body;
            if(path.Contains("EntityDefinitions",StringComparison.Ordinal))
            {
                var logical=path.Contains("gaia_asignacioncapacitacion",StringComparison.Ordinal)?"gaia_asignacioncapacitacion":path.Contains("gaia_versioncapacitacion",StringComparison.Ordinal)?"gaia_versioncapacitacion":"gaia_intentoevaluacion";
                var schemas=new[]{"gaia_Participante","gaia_VersionCapacitacion","gaia_Estado","gaia_FechaVencimiento","gaia_InicioDisponibilidad","gaia_FinDisponibilidad","gaia_PermitirContinuarVencida","gaia_AsignacionCapacitacion","gaia_Evaluacion","gaia_NumeroIntento"};
                body=new{LogicalName=logical,EntitySetName=logical+"s",PrimaryIdAttribute=logical+"id",PrimaryNameAttribute="gaia_nombre",Attributes=schemas.Select(s=>new{SchemaName=s,LogicalName=s.ToLowerInvariant(),AttributeType="Integer"}),ManyToOneRelationships=new[]{new{ReferencingAttribute="gaia_participante",ReferencingEntityNavigationPropertyName="participant",ReferencedEntity="gaia_terceros"},new{ReferencingAttribute="gaia_versioncapacitacion",ReferencingEntityNavigationPropertyName="version",ReferencedEntity="gaia_versioncapacitacion"},new{ReferencingAttribute="gaia_asignacioncapacitacion",ReferencingEntityNavigationPropertyName="assignment",ReferencedEntity="gaia_asignacioncapacitacion"},new{ReferencingAttribute="gaia_evaluacion",ReferencingEntityNavigationPropertyName="evaluation",ReferencedEntity="gaia_evaluacion"}}};
            }
            else if(path.Contains("gaia_asignacioncapacitacions(",StringComparison.Ordinal))body=new Dictionary<string,object?>{{"_gaia_participante_value",Actor},{"_gaia_versioncapacitacion_value",Version},{"statecode",0},{"gaia_estado",299541090},{"gaia_fechavencimiento",Overdue?DateTimeOffset.UtcNow.AddDays(-1):null}};
            else if(path.Contains("gaia_versioncapacitacions(",StringComparison.Ordinal))body=new Dictionary<string,object?>{{"gaia_estado",299540002},{"statecode",0},{"gaia_iniciodisponibilidad",Future?DateTimeOffset.UtcNow.AddDays(1):null},{"gaia_findisponibilidad",null},{"gaia_permitircontinuarvencida",false}};
            else body=new{value=Array.Empty<object>()};
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json")});
        }
    }
}
