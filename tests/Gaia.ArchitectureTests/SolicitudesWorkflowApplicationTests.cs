using Gaia.Modules.Solicitudes;

namespace Gaia.ArchitectureTests;

public sealed class SolicitudesWorkflowApplicationTests
{
    [Fact]
    public async Task PublicationStopsBeforeStoreWhenDefinitionIsInvalid()
    {
        var store=new Store{PublicationErrors=["No existe paso final."]};
        var app=new SolicitudesWorkflowApplication(store,TimeProvider.System);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>app.PublishAsync(Guid.NewGuid(),Guid.NewGuid(),default));
        Assert.Contains("paso final",error.Message);Assert.False(store.Published);
    }

    [Fact]
    public async Task CompletionNormalizesAuditableValues()
    {
        var store=new Store();var app=new SolicitudesWorkflowApplication(store,TimeProvider.System);
        await app.CompleteAsync(Guid.NewGuid(),new(Guid.NewGuid(),SolicitudesWorkflowValues.Approved,"  Conforme  ",true," operation-1 "),default);
        Assert.Equal("Conforme",store.Completion!.Observation);Assert.Equal("operation-1",store.Completion.OperationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public async Task ReassignmentRequiresReason(string reason)
    {
        var store=new Store();var app=new SolicitudesWorkflowApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.ReassignAsync(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),reason),default));
        Assert.Null(store.Reassignment);
    }

    [Fact]
    public async Task RequesterCanRespondWithFileOnly()
    {
        var store=new Store();var app=new SolicitudesWorkflowApplication(store,TimeProvider.System);
        await app.ResumeFromRequesterAsync(Guid.NewGuid(),Guid.NewGuid(),"",true,default);
        Assert.True(store.Resumed);
    }

    [Fact]
    public async Task RequesterCannotRespondWithoutContent()
    {
        var store=new Store();var app=new SolicitudesWorkflowApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.ResumeFromRequesterAsync(Guid.NewGuid(),Guid.NewGuid()," ",false,default));
        Assert.False(store.Resumed);
    }

    [Fact]
    public async Task AdministratorCanResumeWaitingManagement()
    {
        var store=new Store();var app=new SolicitudesWorkflowApplication(store,TimeProvider.System);
        await app.ResumeFromManagementAsync(Guid.NewGuid(),Guid.NewGuid(),default);
        Assert.True(store.Resumed);
    }

    [Fact]
    public async Task WorkflowStepRequiresValidCode()
    {
        var app=new SolicitudesWorkflowApplication(new Store(),TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.SaveStepAsync(Guid.NewGuid(),null,
            new("paso con espacios",299540140,10,true,false,false,SolicitudesWorkflowValues.RequestOwner,null,null,
                SolicitudesWorkflowValues.AnyIncoming,false,false,false,false,null),default));
    }

    [Fact]
    public void SingleApprovalCanStartAndFinishWorkflowWithoutRoutes()
    {
        var stepId=Guid.NewGuid();
        var flow=new SolicitudesWorkflowDefinition(Guid.NewGuid(),Guid.NewGuid(),1,SolicitudesWorkflowValues.Draft,
            [new(stepId,"APROBACION_COORDINACION",299540142,10,true,true,false,
                SolicitudesWorkflowValues.UnitQueue,Guid.NewGuid(),null,SolicitudesWorkflowValues.AnyIncoming,
                true,false,false,false,2)],[]);

        Assert.Empty(SolicitudesWorkflowRules.ValidateForPublication(flow));
    }

    [Fact]
    public async Task StageFormFieldUsesSameDynamicFieldValidation()
    {
        var app=new SolicitudesWorkflowApplication(new Store(),TimeProvider.System);
        var invalid=new SaveSolicitudesFormField("DECISION","Decisión",299540046,299540058,null,null,true,10,12,null,null,null,null,false,null,null,true,[]);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.SaveStageFormFieldAsync(Guid.NewGuid(),null,invalid,default));
    }

    [Fact]
    public void StageFormOptionCodesAreGeneratedFromVisibleLabels()
    {
        var field=new SaveSolicitudesFormField("TIPO_CONTRATO","Tipo de contrato",299540046,299540058,null,null,true,10,12,null,null,null,null,false,null,null,true,
            [new(null,"","Laboral",0,true),new(null,"","Consultoría",1,false),new(null,"","Otro",2,false)]);

        var normalized=SolicitudesFormFieldValidation.Normalize(field);

        Assert.Equal(["LABORAL","CONSULTORIA","OTRO"],normalized.Options.Select(option=>option.Code));
    }

    [Fact]
    public void StageFormOptionsRejectRepeatedVisibleLabels()
    {
        var field=new SaveSolicitudesFormField("TIPO_CONTRATO","Tipo de contrato",299540046,299540058,null,null,true,10,12,null,null,null,null,false,null,null,true,
            [new(null,"","Laboral",0,true),new(null,""," laboral ",1,false)]);

        var error=Assert.Throws<ArgumentException>(()=>SolicitudesFormFieldValidation.Normalize(field));

        Assert.Contains("No repitas opciones",error.Message);
    }

    [Fact]
    public async Task ManagementFormRejectsDuplicateFieldAnswers()
    {
        var app=new SolicitudesWorkflowApplication(new Store(),TimeProvider.System);var fieldId=Guid.NewGuid();
        var command=new SaveSolicitudesManagementAnswers([new(fieldId,"uno"),new(fieldId,"dos")]);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.SaveManagementAnswersAsync(Guid.NewGuid(),Guid.NewGuid(),command,default));
    }

    [Fact]
    public async Task DiagramPositionRejectsNegativeCoordinates()
    {
        var app=new SolicitudesWorkflowApplication(new Store(),TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.UpdateStepPositionAsync(Guid.NewGuid(),Guid.NewGuid(),new(-1,20),default));
    }

    [Fact]
    public async Task RouteDeletionReachesStore()
    {
        var store=new Store();var app=new SolicitudesWorkflowApplication(store,TimeProvider.System);
        await app.DeleteRouteAsync(Guid.NewGuid(),Guid.NewGuid(),default);
        Assert.True(store.RouteDeleted);
    }

    [Fact]
    public async Task StepDeletionReachesStore()
    {
        var store=new Store();var app=new SolicitudesWorkflowApplication(store,TimeProvider.System);
        await app.DeleteStepAsync(Guid.NewGuid(),Guid.NewGuid(),default);
        Assert.True(store.StepDeleted);
    }

    private sealed class Store:ISolicitudesWorkflowStore
    {
        public IReadOnlyList<string> PublicationErrors{get;init;}=[];public bool Published{get;private set;}
        public CompleteSolicitudesManagement? Completion{get;private set;}public ReassignSolicitudesManagement? Reassignment{get;private set;}public bool Resumed{get;private set;}public bool RouteDeleted{get;private set;}public bool StepDeleted{get;private set;}
        public Task<SolicitudesWorkflowDefinition?> ReadFlowAsync(Guid id,CancellationToken token)=>Task.FromResult<SolicitudesWorkflowDefinition?>(null);
        public Task<IReadOnlyList<string>> ValidateForPublicationAsync(Guid id,CancellationToken token)=>Task.FromResult(PublicationErrors);
        public Task PublishAsync(Guid id,Guid actor,DateTimeOffset now,CancellationToken token){Published=true;return Task.CompletedTask;}
        public Task StartForRequestAsync(Guid requestId,Guid flowId,Guid actor,DateTimeOffset now,CancellationToken token)=>Task.CompletedTask;
        public Task CompleteAsync(Guid actor,CompleteSolicitudesManagement command,DateTimeOffset now,CancellationToken token){Completion=command;return Task.CompletedTask;}
        public Task ReassignAsync(Guid id,Guid actor,ReassignSolicitudesManagement command,DateTimeOffset now,CancellationToken token){Reassignment=command;return Task.CompletedTask;}
        public Task TakeAsync(Guid id,Guid actor,DateTimeOffset now,CancellationToken token)=>Task.CompletedTask;
        public Task ResumeFromRequesterAsync(Guid id,Guid actor,string comment,bool hasFile,DateTimeOffset now,CancellationToken token){Resumed=true;return Task.CompletedTask;}
        public Task ResumeFromManagementAsync(Guid id,Guid actor,DateTimeOffset now,CancellationToken token){Resumed=true;return Task.CompletedTask;}
        public Task ReopenAsync(Guid requestId,Guid actor,DateTimeOffset now,CancellationToken token)=>Task.CompletedTask;
        public Task<IReadOnlyList<SolicitudesWorkflowSummary>> ListAsync(Guid serviceId,CancellationToken token)=>Task.FromResult<IReadOnlyList<SolicitudesWorkflowSummary>>([]);
        public Task<Guid> CreateDraftAsync(CreateSolicitudesWorkflowDraft command,CancellationToken token)=>Task.FromResult(Guid.NewGuid());
        public Task UpdateDraftAsync(Guid flowId,UpdateSolicitudesWorkflowDraft command,CancellationToken token)=>Task.CompletedTask;
        public Task DeleteDraftAsync(Guid flowId,CancellationToken token)=>Task.CompletedTask;
        public Task<Guid> SaveStepAsync(Guid flowId,Guid? stepId,SaveSolicitudesWorkflowStep command,CancellationToken token)=>Task.FromResult(stepId??Guid.NewGuid());
        public Task<Guid> DuplicateStepAsync(Guid flowId,Guid stepId,CancellationToken token)=>Task.FromResult(Guid.NewGuid());
        public Task DeleteStepAsync(Guid flowId,Guid stepId,CancellationToken token){StepDeleted=true;return Task.CompletedTask;}
        public Task UpdateStepPositionAsync(Guid flowId,Guid stepId,UpdateSolicitudesWorkflowStepPosition command,CancellationToken token)=>Task.CompletedTask;
        public Task<Guid> SaveRouteAsync(Guid flowId,Guid? routeId,SaveSolicitudesWorkflowRoute command,CancellationToken token)=>Task.FromResult(routeId??Guid.NewGuid());
        public Task DeleteRouteAsync(Guid flowId,Guid routeId,CancellationToken token){RouteDeleted=true;return Task.CompletedTask;}
        public Task UpdateStepAssignmentAsync(Guid flowId,Guid stepId,UpdateSolicitudesWorkflowAssignment command,DateTimeOffset now,CancellationToken token)=>Task.CompletedTask;
        public Task<SolicitudesStageForm?> ReadStageFormAsync(Guid stepId,CancellationToken token)=>Task.FromResult<SolicitudesStageForm?>(null);
        public Task<Guid> SaveStageFormAsync(Guid stepId,SaveSolicitudesStageForm command,CancellationToken token)=>Task.FromResult(Guid.NewGuid());
        public Task<Guid> SaveStageFormFieldAsync(Guid stepId,Guid? fieldId,SaveSolicitudesFormField command,CancellationToken token)=>Task.FromResult(fieldId??Guid.NewGuid());
        public Task DeleteStageFormFieldAsync(Guid stepId,Guid fieldId,CancellationToken token)=>Task.CompletedTask;
        public Task<SolicitudesManagementForm> ReadManagementFormAsync(Guid managementId,Guid actorId,CancellationToken token)=>Task.FromResult(new SolicitudesManagementForm(managementId,Guid.NewGuid(),null,[]));
        public Task<IReadOnlyList<SavedSolicitudesManagementAnswer>> SaveManagementAnswersAsync(Guid managementId,Guid actorId,SaveSolicitudesManagementAnswers command,CancellationToken token)=>Task.FromResult<IReadOnlyList<SavedSolicitudesManagementAnswer>>([]);
        public Task<SolicitudesRequestWorkflowState?> ReadRequestStateAsync(Guid requestId,Guid actorId,bool managementAccess,CancellationToken token)=>Task.FromResult<SolicitudesRequestWorkflowState?>(null);
        public Task<IReadOnlyList<SolicitudesWorkflowQueueItem>> ReadWorkQueueAsync(Guid actorId,string queue,CancellationToken token)=>Task.FromResult<IReadOnlyList<SolicitudesWorkflowQueueItem>>([]);
    }
}
