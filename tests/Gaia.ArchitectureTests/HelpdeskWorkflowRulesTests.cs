using Gaia.Modules.Helpdesk;

namespace Gaia.ArchitectureTests;

public sealed class HelpdeskWorkflowRulesTests
{
    [Fact]
    public void CompletionRejectsRequesterReturnWhenStepDoesNotAllowIt()
    {
        var step=Step("REVISION",initial:true) with{AllowsRequesterReturn=false};
        var command=new CompleteHelpdeskManagement(Guid.NewGuid(),HelpdeskWorkflowValues.Returned,"Falta información",false,"op-return");
        Assert.Throws<ArgumentException>(()=>HelpdeskWorkflowRules.ValidateCompletion(step,command));
    }
    [Fact]
    public void PublicationRejectsUnreachableAndInvalidAssignments()
    {
        var a=Step("A",initial:true);var b=Step("B",final:true);var orphan=Step("X",strategy:HelpdeskWorkflowValues.UnitQueue);
        var flow=Flow([a,b,orphan],[Route(a,b,HelpdeskWorkflowValues.Completed)]);
        var errors=HelpdeskWorkflowRules.ValidateForPublication(flow);
        Assert.Contains(errors,x=>x.Contains("X requiere unidad",StringComparison.Ordinal));
        Assert.Contains(errors,x=>x.Contains("X no es alcanzable",StringComparison.Ordinal));
    }

    [Fact]
    public void ParallelConvergenceWaitsForAllIncomingRoutes()
    {
        var start=Step("START",initial:true);var left=Step("LEFT");var right=Step("RIGHT");var join=Step("JOIN",final:true,activation:HelpdeskWorkflowValues.AllIncoming);
        var l=Route(start,left,HelpdeskWorkflowValues.RequiresApproval);
        var r=Route(start,right,HelpdeskWorkflowValues.RequiresApproval);
        var lj=Route(left,join,HelpdeskWorkflowValues.Approved);
        var rj=Route(right,join,HelpdeskWorkflowValues.Approved);
        var flow=Flow([start,left,right,join],[l,r,lj,rj]);
        var leftRun=new HelpdeskManagementExecution(Guid.NewGuid(),left.Id,1,HelpdeskWorkflowValues.ManagementCompleted,HelpdeskWorkflowValues.Approved);
        var rightRun=new HelpdeskManagementExecution(Guid.NewGuid(),right.Id,1,HelpdeskWorkflowValues.ManagementCompleted,HelpdeskWorkflowValues.Approved);
        var target=new HelpdeskManagementExecution(Guid.NewGuid(),join.Id,1,HelpdeskWorkflowValues.ManagementBlocked);
        var first=HelpdeskWorkflowRules.ActivateAfter(flow,leftRun,[leftRun,rightRun,target],[]);
        Assert.Equal(HelpdeskWorkflowValues.ManagementBlocked,Assert.Single(first).Status);
        var dependency=new HelpdeskWorkflowDependency(leftRun.Id,lj.Id,target.Id);
        var second=HelpdeskWorkflowRules.ActivateAfter(flow,rightRun,[leftRun,rightRun,target],[dependency]);
        Assert.Equal(HelpdeskWorkflowValues.ManagementAvailable,Assert.Single(second).Status);
        Assert.True(second[0].Reused);
    }

    [Fact]
    public void AnyIncomingEnablesOnFirstCompatibleRoute()
    {
        var a=Step("A",initial:true);var b=Step("B",initial:true);var target=Step("T",final:true,activation:HelpdeskWorkflowValues.AnyIncoming);
        var route=Route(a,target,HelpdeskWorkflowValues.Approved);
        var flow=Flow([a,b,target],[route,Route(b,target,HelpdeskWorkflowValues.Approved)]);
        var run=new HelpdeskManagementExecution(Guid.NewGuid(),a.Id,1,HelpdeskWorkflowValues.ManagementCompleted,HelpdeskWorkflowValues.Approved);
        Assert.Equal(HelpdeskWorkflowValues.ManagementAvailable,Assert.Single(HelpdeskWorkflowRules.ActivateAfter(flow,run,[run],[])).Status);
    }

    [Fact]
    public void RepeatedStepUsesNextExecutionWithoutMutatingHistory()
    {
        var a=Step("A",initial:true);var b=Step("B",final:true);var back=Route(a,b,HelpdeskWorkflowValues.Returned);
        var flow=Flow([a,b],[back]);
        var previous=new HelpdeskManagementExecution(Guid.NewGuid(),b.Id,1,HelpdeskWorkflowValues.ManagementCompleted,HelpdeskWorkflowValues.Returned);
        var source=new HelpdeskManagementExecution(Guid.NewGuid(),a.Id,2,HelpdeskWorkflowValues.ManagementCompleted,HelpdeskWorkflowValues.Returned);
        Assert.Equal(2,Assert.Single(HelpdeskWorkflowRules.ActivateAfter(flow,source,[previous,source],[])).Execution);
    }

    [Fact]
    public void RequestWaitsOnlyWhenNoOtherManagementCanContinue()
    {
        Assert.False(HelpdeskWorkflowRules.RequestMustWait([Run(HelpdeskWorkflowValues.ManagementWaiting),Run(HelpdeskWorkflowValues.ManagementInProgress)]));
        Assert.True(HelpdeskWorkflowRules.RequestMustWait([Run(HelpdeskWorkflowValues.ManagementWaiting),Run(HelpdeskWorkflowValues.ManagementCompleted)]));
    }

    [Fact]
    public void RequiredFileAndObservationAreEnforced()
    {
        var step=Step("A") with{RequiresFile=true,RequiresObservation=true};
        Assert.Throws<ArgumentException>(()=>HelpdeskWorkflowRules.ValidateCompletion(step,new(Guid.NewGuid(),HelpdeskWorkflowValues.Approved,null,false,"op")));
        HelpdeskWorkflowRules.ValidateCompletion(step,new(Guid.NewGuid(),HelpdeskWorkflowValues.Approved,"Aprobado",true,"op"));
    }

    [Fact]
    public void FinalStepRequiresSolutionSummary()
    {
        var step=Step("FINAL",final:true);
        Assert.Throws<ArgumentException>(()=>HelpdeskWorkflowRules.ValidateCompletion(step,new(Guid.NewGuid(),HelpdeskWorkflowValues.Completed,null,false,"op")));
        HelpdeskWorkflowRules.ValidateCompletion(step,new(Guid.NewGuid(),HelpdeskWorkflowValues.Completed,"Solución aplicada",false,"op"));
    }

    [Fact]
    public void UnitQueueUsesExistingOrganizationalMembership()
    {
        var actor=Guid.NewGuid();var unit=Guid.NewGuid();
        Assert.True(HelpdeskWorkflowAuthorization.CanWork(actor,null,unit,[unit],false));
        Assert.False(HelpdeskWorkflowAuthorization.CanWork(actor,null,unit,[],false));
        Assert.True(HelpdeskWorkflowAuthorization.CanWork(actor,null,unit,[],true));
    }

    [Fact]
    public void AssignedManagementIsRestrictedToItsResponsible()
    {
        var actor=Guid.NewGuid();var other=Guid.NewGuid();
        Assert.True(HelpdeskWorkflowAuthorization.CanWork(actor,actor,Guid.NewGuid(),[],false));
        Assert.False(HelpdeskWorkflowAuthorization.CanWork(actor,other,Guid.NewGuid(),[Guid.NewGuid()],false));
    }

    [Fact]
    public void LinearFlowActivatesOneStepAtATime()
    {
        var a=Step("A",initial:true);var b=Step("B");var c=Step("C",final:true);var ab=Route(a,b,HelpdeskWorkflowValues.Completed);var bc=Route(b,c,HelpdeskWorkflowValues.Completed);var flow=Flow([a,b,c],[ab,bc]);
        var first=Assert.Single(HelpdeskWorkflowRules.ActivateInitial(flow,[]));var aRun=new HelpdeskManagementExecution(Guid.NewGuid(),a.Id,first.Execution,HelpdeskWorkflowValues.ManagementCompleted,HelpdeskWorkflowValues.Completed);
        Assert.Equal(b.Id,Assert.Single(HelpdeskWorkflowRules.ActivateAfter(flow,aRun,[aRun],[])).StepId);
    }

    [Fact]
    public void TwoInitialStepsAreActivatedInParallel()
    {
        var left=Step("LEFT",initial:true);var right=Step("RIGHT",initial:true);var final=Step("FINAL",final:true,activation:HelpdeskWorkflowValues.AllIncoming);var flow=Flow([left,right,final],[Route(left,final,HelpdeskWorkflowValues.Approved),Route(right,final,HelpdeskWorkflowValues.Approved)]);
        var active=HelpdeskWorkflowRules.ActivateInitial(flow,[]);
        Assert.Equal(2,active.Count);Assert.All(active,x=>Assert.Equal(HelpdeskWorkflowValues.ManagementAvailable,x.Status));
    }

    [Fact]
    public void RejectedResultOnlyExecutesCompatibleRoute()
    {
        var review=Step("REVIEW",initial:true);var approved=Step("APPROVED",final:true);var rejected=Step("REWORK",final:true);var flow=Flow([review,approved,rejected],[Route(review,approved,HelpdeskWorkflowValues.Approved),Route(review,rejected,HelpdeskWorkflowValues.Rejected)]);var run=new HelpdeskManagementExecution(Guid.NewGuid(),review.Id,1,HelpdeskWorkflowValues.ManagementCompleted,HelpdeskWorkflowValues.Rejected);
        Assert.Equal(rejected.Id,Assert.Single(HelpdeskWorkflowRules.ActivateAfter(flow,run,[run],[])).StepId);
    }

    [Fact]
    public void WaitingBranchDoesNotPauseRequestWhileAnotherBranchCanWork()
    {
        Assert.False(HelpdeskWorkflowRules.RequestMustWait([Run(HelpdeskWorkflowValues.ManagementWaiting),Run(HelpdeskWorkflowValues.ManagementAvailable)]));
        Assert.True(HelpdeskWorkflowRules.RequestMustWait([Run(HelpdeskWorkflowValues.ManagementWaiting),Run(HelpdeskWorkflowValues.ManagementBlocked)]));
    }

    [Fact]
    public void InstanceCompletesOnlyAfterFinalStepAndNoPendingWork()
    {
        var start=Step("START",initial:true);var final=Step("FINAL",final:true);var flow=Flow([start,final],[Route(start,final,HelpdeskWorkflowValues.Completed)]);var completed=new HelpdeskManagementExecution(Guid.NewGuid(),final.Id,1,HelpdeskWorkflowValues.ManagementCompleted,HelpdeskWorkflowValues.Completed);
        Assert.True(HelpdeskWorkflowRules.CanCompleteInstance(flow,[completed]));Assert.False(HelpdeskWorkflowRules.CanCompleteInstance(flow,[completed,Run(HelpdeskWorkflowValues.ManagementAvailable)]));
    }

    private static HelpdeskWorkflowDefinition Flow(IReadOnlyList<HelpdeskWorkflowStep> steps,IReadOnlyList<HelpdeskWorkflowRoute> routes)=>
        new(Guid.NewGuid(),Guid.NewGuid(),1,HelpdeskWorkflowValues.Published,steps,routes);
    private static HelpdeskWorkflowStep Step(string code,bool initial=false,bool final=false,int strategy=HelpdeskWorkflowValues.RequestOwner,int activation=HelpdeskWorkflowValues.AnyIncoming)=>
        new(Guid.NewGuid(),code,299540140,1,initial,final,false,strategy,null,null,activation,false,false,false,false,null);
    private static HelpdeskWorkflowRoute Route(HelpdeskWorkflowStep source,HelpdeskWorkflowStep target,int result)=>
        new(Guid.NewGuid(),source.Code+"-"+target.Code,source.Id,target.Id,result,1);
    private static HelpdeskManagementExecution Run(int status)=>new(Guid.NewGuid(),Guid.NewGuid(),1,status);
}
