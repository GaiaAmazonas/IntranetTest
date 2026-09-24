using Gaia.Modules.Helpdesk;

namespace Gaia.ArchitectureTests;

public sealed class HelpdeskWorkflowApplicationTests
{
    [Fact]
    public async Task PublicationStopsBeforeStoreWhenDefinitionIsInvalid()
    {
        var store=new Store{PublicationErrors=["No existe paso final."]};
        var app=new HelpdeskWorkflowApplication(store,TimeProvider.System);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>app.PublishAsync(Guid.NewGuid(),Guid.NewGuid(),default));
        Assert.Contains("paso final",error.Message);Assert.False(store.Published);
    }

    [Fact]
    public async Task CompletionNormalizesAuditableValues()
    {
        var store=new Store();var app=new HelpdeskWorkflowApplication(store,TimeProvider.System);
        await app.CompleteAsync(Guid.NewGuid(),new(Guid.NewGuid(),HelpdeskWorkflowValues.Approved,"  Conforme  ",true," operation-1 "),default);
        Assert.Equal("Conforme",store.Completion!.Observation);Assert.Equal("operation-1",store.Completion.OperationId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public async Task ReassignmentRequiresReason(string reason)
    {
        var store=new Store();var app=new HelpdeskWorkflowApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.ReassignAsync(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),reason),default));
        Assert.Null(store.Reassignment);
    }

    [Fact]
    public async Task RequesterCanRespondWithFileOnly()
    {
        var store=new Store();var app=new HelpdeskWorkflowApplication(store,TimeProvider.System);
        await app.ResumeFromRequesterAsync(Guid.NewGuid(),Guid.NewGuid(),"",true,default);
        Assert.True(store.Resumed);
    }

    [Fact]
    public async Task RequesterCannotRespondWithoutContent()
    {
        var store=new Store();var app=new HelpdeskWorkflowApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.ResumeFromRequesterAsync(Guid.NewGuid(),Guid.NewGuid()," ",false,default));
        Assert.False(store.Resumed);
    }

    [Fact]
    public async Task WorkflowStepRequiresValidCode()
    {
        var app=new HelpdeskWorkflowApplication(new Store(),TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.SaveStepAsync(Guid.NewGuid(),null,
            new("paso con espacios",299540140,10,true,false,false,HelpdeskWorkflowValues.RequestOwner,null,null,
                HelpdeskWorkflowValues.AnyIncoming,false,false,false,false,null),default));
    }

    [Fact]
    public void SingleApprovalCanStartAndFinishWorkflowWithoutRoutes()
    {
        var stepId=Guid.NewGuid();
        var flow=new HelpdeskWorkflowDefinition(Guid.NewGuid(),Guid.NewGuid(),1,HelpdeskWorkflowValues.Draft,
            [new(stepId,"APROBACION_COORDINACION",299540142,10,true,true,false,
                HelpdeskWorkflowValues.UnitQueue,Guid.NewGuid(),null,HelpdeskWorkflowValues.AnyIncoming,
                true,false,false,false,2)],[]);

        Assert.Empty(HelpdeskWorkflowRules.ValidateForPublication(flow));
    }

    [Fact]
    public async Task StageFormFieldUsesSameDynamicFieldValidation()
    {
        var app=new HelpdeskWorkflowApplication(new Store(),TimeProvider.System);
        var invalid=new SaveHelpdeskFormField("DECISION","Decisión",299540046,299540058,null,null,true,10,12,null,null,null,null,false,null,null,true,[]);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.SaveStageFormFieldAsync(Guid.NewGuid(),null,invalid,default));
    }

    [Fact]
    public async Task ManagementFormRejectsDuplicateFieldAnswers()
    {
        var app=new HelpdeskWorkflowApplication(new Store(),TimeProvider.System);var fieldId=Guid.NewGuid();
        var command=new SaveHelpdeskManagementAnswers([new(fieldId,"uno"),new(fieldId,"dos")]);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.SaveManagementAnswersAsync(Guid.NewGuid(),Guid.NewGuid(),command,default));
    }

    private sealed class Store:IHelpdeskWorkflowStore
    {
        public IReadOnlyList<string> PublicationErrors{get;init;}=[];public bool Published{get;private set;}
        public CompleteHelpdeskManagement? Completion{get;private set;}public ReassignHelpdeskManagement? Reassignment{get;private set;}public bool Resumed{get;private set;}
        public Task<HelpdeskWorkflowDefinition?> ReadFlowAsync(Guid id,CancellationToken token)=>Task.FromResult<HelpdeskWorkflowDefinition?>(null);
        public Task<IReadOnlyList<string>> ValidateForPublicationAsync(Guid id,CancellationToken token)=>Task.FromResult(PublicationErrors);
        public Task PublishAsync(Guid id,Guid actor,DateTimeOffset now,CancellationToken token){Published=true;return Task.CompletedTask;}
        public Task StartForRequestAsync(Guid requestId,Guid flowId,Guid actor,DateTimeOffset now,CancellationToken token)=>Task.CompletedTask;
        public Task CompleteAsync(Guid actor,CompleteHelpdeskManagement command,DateTimeOffset now,CancellationToken token){Completion=command;return Task.CompletedTask;}
        public Task ReassignAsync(Guid id,Guid actor,ReassignHelpdeskManagement command,DateTimeOffset now,CancellationToken token){Reassignment=command;return Task.CompletedTask;}
        public Task TakeAsync(Guid id,Guid actor,DateTimeOffset now,CancellationToken token)=>Task.CompletedTask;
        public Task ResumeFromRequesterAsync(Guid id,Guid actor,string comment,bool hasFile,DateTimeOffset now,CancellationToken token){Resumed=true;return Task.CompletedTask;}
        public Task ReopenAsync(Guid requestId,Guid actor,DateTimeOffset now,CancellationToken token)=>Task.CompletedTask;
        public Task<IReadOnlyList<HelpdeskWorkflowSummary>> ListAsync(Guid serviceId,CancellationToken token)=>Task.FromResult<IReadOnlyList<HelpdeskWorkflowSummary>>([]);
        public Task<Guid> CreateDraftAsync(CreateHelpdeskWorkflowDraft command,CancellationToken token)=>Task.FromResult(Guid.NewGuid());
        public Task<Guid> SaveStepAsync(Guid flowId,Guid? stepId,SaveHelpdeskWorkflowStep command,CancellationToken token)=>Task.FromResult(stepId??Guid.NewGuid());
        public Task<Guid> SaveRouteAsync(Guid flowId,Guid? routeId,SaveHelpdeskWorkflowRoute command,CancellationToken token)=>Task.FromResult(routeId??Guid.NewGuid());
        public Task<HelpdeskStageForm?> ReadStageFormAsync(Guid stepId,CancellationToken token)=>Task.FromResult<HelpdeskStageForm?>(null);
        public Task<Guid> SaveStageFormAsync(Guid stepId,SaveHelpdeskStageForm command,CancellationToken token)=>Task.FromResult(Guid.NewGuid());
        public Task<Guid> SaveStageFormFieldAsync(Guid stepId,Guid? fieldId,SaveHelpdeskFormField command,CancellationToken token)=>Task.FromResult(fieldId??Guid.NewGuid());
        public Task<HelpdeskManagementForm> ReadManagementFormAsync(Guid managementId,Guid actorId,CancellationToken token)=>Task.FromResult(new HelpdeskManagementForm(managementId,Guid.NewGuid(),null,[]));
        public Task<IReadOnlyList<SavedHelpdeskManagementAnswer>> SaveManagementAnswersAsync(Guid managementId,Guid actorId,SaveHelpdeskManagementAnswers command,CancellationToken token)=>Task.FromResult<IReadOnlyList<SavedHelpdeskManagementAnswer>>([]);
        public Task<HelpdeskRequestWorkflowState?> ReadRequestStateAsync(Guid requestId,Guid actorId,bool managementAccess,CancellationToken token)=>Task.FromResult<HelpdeskRequestWorkflowState?>(null);
        public Task<IReadOnlyList<HelpdeskWorkflowQueueItem>> ReadWorkQueueAsync(Guid actorId,string queue,CancellationToken token)=>Task.FromResult<IReadOnlyList<HelpdeskWorkflowQueueItem>>([]);
    }
}
