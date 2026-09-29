using Gaia.Modules.Solicitudes;

namespace Gaia.ArchitectureTests;

public sealed class SolicitudesWorkflowRulesTests
{
    [Fact]
    public void CompletionRejectsRequesterReturnWhenStepDoesNotAllowIt()
    {
        var step=Step("REVISION",initial:true) with{AllowsRequesterReturn=false};
        var command=new CompleteSolicitudesManagement(Guid.NewGuid(),SolicitudesWorkflowValues.Returned,"Falta información",false,"op-return");
        Assert.Throws<ArgumentException>(()=>SolicitudesWorkflowRules.ValidateCompletion(step,command));
    }
    [Fact]
    public void PublicationRejectsUnreachableAndInvalidAssignments()
    {
        var a=Step("A",initial:true);var b=Step("B",final:true);var orphan=Step("X",strategy:SolicitudesWorkflowValues.UnitQueue);
        var flow=Flow([a,b,orphan],[Route(a,b,SolicitudesWorkflowValues.Completed)]);
        var errors=SolicitudesWorkflowRules.ValidateForPublication(flow);
        Assert.Contains(errors,x=>x.Contains("X requiere unidad",StringComparison.Ordinal));
        Assert.Contains(errors,x=>x.Contains("X no es alcanzable",StringComparison.Ordinal));
    }

    [Fact]
    public void ParallelConvergenceWaitsForAllIncomingRoutes()
    {
        var start=Step("START",initial:true);var left=Step("LEFT");var right=Step("RIGHT");var join=Step("JOIN",final:true,activation:SolicitudesWorkflowValues.AllIncoming);
        var l=Route(start,left,SolicitudesWorkflowValues.RequiresApproval);
        var r=Route(start,right,SolicitudesWorkflowValues.RequiresApproval);
        var lj=Route(left,join,SolicitudesWorkflowValues.Approved);
        var rj=Route(right,join,SolicitudesWorkflowValues.Approved);
        var flow=Flow([start,left,right,join],[l,r,lj,rj]);
        var leftRun=new SolicitudesManagementExecution(Guid.NewGuid(),left.Id,1,SolicitudesWorkflowValues.ManagementCompleted,SolicitudesWorkflowValues.Approved);
        var rightRun=new SolicitudesManagementExecution(Guid.NewGuid(),right.Id,1,SolicitudesWorkflowValues.ManagementCompleted,SolicitudesWorkflowValues.Approved);
        var target=new SolicitudesManagementExecution(Guid.NewGuid(),join.Id,1,SolicitudesWorkflowValues.ManagementBlocked);
        var first=SolicitudesWorkflowRules.ActivateAfter(flow,leftRun,[leftRun,rightRun,target],[]);
        Assert.Equal(SolicitudesWorkflowValues.ManagementBlocked,Assert.Single(first).Status);
        var dependency=new SolicitudesWorkflowDependency(leftRun.Id,lj.Id,target.Id);
        var second=SolicitudesWorkflowRules.ActivateAfter(flow,rightRun,[leftRun,rightRun,target],[dependency]);
        Assert.Equal(SolicitudesWorkflowValues.ManagementAvailable,Assert.Single(second).Status);
        Assert.True(second[0].Reused);
    }

    [Fact]
    public void AnyIncomingEnablesOnFirstCompatibleRoute()
    {
        var a=Step("A",initial:true);var b=Step("B",initial:true);var target=Step("T",final:true,activation:SolicitudesWorkflowValues.AnyIncoming);
        var route=Route(a,target,SolicitudesWorkflowValues.Approved);
        var flow=Flow([a,b,target],[route,Route(b,target,SolicitudesWorkflowValues.Approved)]);
        var run=new SolicitudesManagementExecution(Guid.NewGuid(),a.Id,1,SolicitudesWorkflowValues.ManagementCompleted,SolicitudesWorkflowValues.Approved);
        Assert.Equal(SolicitudesWorkflowValues.ManagementAvailable,Assert.Single(SolicitudesWorkflowRules.ActivateAfter(flow,run,[run],[])).Status);
    }

    [Fact]
    public void RepeatedStepUsesNextExecutionWithoutMutatingHistory()
    {
        var a=Step("A",initial:true);var b=Step("B",final:true);var back=Route(a,b,SolicitudesWorkflowValues.Returned);
        var flow=Flow([a,b],[back]);
        var previous=new SolicitudesManagementExecution(Guid.NewGuid(),b.Id,1,SolicitudesWorkflowValues.ManagementCompleted,SolicitudesWorkflowValues.Returned);
        var source=new SolicitudesManagementExecution(Guid.NewGuid(),a.Id,2,SolicitudesWorkflowValues.ManagementCompleted,SolicitudesWorkflowValues.Returned);
        Assert.Equal(2,Assert.Single(SolicitudesWorkflowRules.ActivateAfter(flow,source,[previous,source],[])).Execution);
    }

    [Fact]
    public void RequestWaitsOnlyWhenNoOtherManagementCanContinue()
    {
        Assert.False(SolicitudesWorkflowRules.RequestMustWait([Run(SolicitudesWorkflowValues.ManagementWaiting),Run(SolicitudesWorkflowValues.ManagementInProgress)]));
        Assert.True(SolicitudesWorkflowRules.RequestMustWait([Run(SolicitudesWorkflowValues.ManagementWaiting),Run(SolicitudesWorkflowValues.ManagementCompleted)]));
    }

    [Fact]
    public void RequiredFileAndObservationAreEnforced()
    {
        var step=Step("A") with{RequiresFile=true,RequiresObservation=true};
        Assert.Throws<ArgumentException>(()=>SolicitudesWorkflowRules.ValidateCompletion(step,new(Guid.NewGuid(),SolicitudesWorkflowValues.Approved,null,false,"op")));
        SolicitudesWorkflowRules.ValidateCompletion(step,new(Guid.NewGuid(),SolicitudesWorkflowValues.Approved,"Aprobado",true,"op"));
    }

    [Fact]
    public void FinalStepRequiresSolutionSummary()
    {
        var step=Step("FINAL",final:true);
        Assert.Throws<ArgumentException>(()=>SolicitudesWorkflowRules.ValidateCompletion(step,new(Guid.NewGuid(),SolicitudesWorkflowValues.Completed,null,false,"op")));
        SolicitudesWorkflowRules.ValidateCompletion(step,new(Guid.NewGuid(),SolicitudesWorkflowValues.Completed,"Solución aplicada",false,"op"));
    }

    [Fact]
    public void UnitQueueUsesExistingOrganizationalMembership()
    {
        var actor=Guid.NewGuid();var unit=Guid.NewGuid();
        Assert.True(SolicitudesWorkflowAuthorization.CanWork(actor,null,unit,[unit],false));
        Assert.False(SolicitudesWorkflowAuthorization.CanWork(actor,null,unit,[],false));
        Assert.True(SolicitudesWorkflowAuthorization.CanWork(actor,null,unit,[],true));
    }

    [Fact]
    public void AssignedManagementIsRestrictedToItsResponsible()
    {
        var actor=Guid.NewGuid();var other=Guid.NewGuid();
        Assert.True(SolicitudesWorkflowAuthorization.CanWork(actor,actor,Guid.NewGuid(),[],false));
        Assert.False(SolicitudesWorkflowAuthorization.CanWork(actor,other,Guid.NewGuid(),[Guid.NewGuid()],false));
    }

    [Fact]
    public void LinearFlowActivatesOneStepAtATime()
    {
        var a=Step("A",initial:true);var b=Step("B");var c=Step("C",final:true);var ab=Route(a,b,SolicitudesWorkflowValues.Completed);var bc=Route(b,c,SolicitudesWorkflowValues.Completed);var flow=Flow([a,b,c],[ab,bc]);
        var first=Assert.Single(SolicitudesWorkflowRules.ActivateInitial(flow,[]));var aRun=new SolicitudesManagementExecution(Guid.NewGuid(),a.Id,first.Execution,SolicitudesWorkflowValues.ManagementCompleted,SolicitudesWorkflowValues.Completed);
        Assert.Equal(b.Id,Assert.Single(SolicitudesWorkflowRules.ActivateAfter(flow,aRun,[aRun],[])).StepId);
    }

    [Fact]
    public void TwoInitialStepsAreActivatedInParallel()
    {
        var left=Step("LEFT",initial:true);var right=Step("RIGHT",initial:true);var final=Step("FINAL",final:true,activation:SolicitudesWorkflowValues.AllIncoming);var flow=Flow([left,right,final],[Route(left,final,SolicitudesWorkflowValues.Approved),Route(right,final,SolicitudesWorkflowValues.Approved)]);
        var active=SolicitudesWorkflowRules.ActivateInitial(flow,[]);
        Assert.Equal(2,active.Count);Assert.All(active,x=>Assert.Equal(SolicitudesWorkflowValues.ManagementAvailable,x.Status));
    }

    [Fact]
    public void RejectedResultOnlyExecutesCompatibleRoute()
    {
        var review=Step("REVIEW",initial:true);var approved=Step("APPROVED",final:true);var rejected=Step("REWORK",final:true);var flow=Flow([review,approved,rejected],[Route(review,approved,SolicitudesWorkflowValues.Approved),Route(review,rejected,SolicitudesWorkflowValues.Rejected)]);var run=new SolicitudesManagementExecution(Guid.NewGuid(),review.Id,1,SolicitudesWorkflowValues.ManagementCompleted,SolicitudesWorkflowValues.Rejected);
        Assert.Equal(rejected.Id,Assert.Single(SolicitudesWorkflowRules.ActivateAfter(flow,run,[run],[])).StepId);
    }

    [Fact]
    public void WaitingBranchDoesNotPauseRequestWhileAnotherBranchCanWork()
    {
        Assert.False(SolicitudesWorkflowRules.RequestMustWait([Run(SolicitudesWorkflowValues.ManagementWaiting),Run(SolicitudesWorkflowValues.ManagementAvailable)]));
        Assert.True(SolicitudesWorkflowRules.RequestMustWait([Run(SolicitudesWorkflowValues.ManagementWaiting),Run(SolicitudesWorkflowValues.ManagementBlocked)]));
    }

    [Fact]
    public void InstanceCompletesOnlyAfterFinalStepAndNoPendingWork()
    {
        var start=Step("START",initial:true);var final=Step("FINAL",final:true);var flow=Flow([start,final],[Route(start,final,SolicitudesWorkflowValues.Completed)]);var completed=new SolicitudesManagementExecution(Guid.NewGuid(),final.Id,1,SolicitudesWorkflowValues.ManagementCompleted,SolicitudesWorkflowValues.Completed);
        Assert.True(SolicitudesWorkflowRules.CanCompleteInstance(flow,[completed]));Assert.False(SolicitudesWorkflowRules.CanCompleteInstance(flow,[completed,Run(SolicitudesWorkflowValues.ManagementAvailable)]));
    }

    private static SolicitudesWorkflowDefinition Flow(IReadOnlyList<SolicitudesWorkflowStep> steps,IReadOnlyList<SolicitudesWorkflowRoute> routes)=>
        new(Guid.NewGuid(),Guid.NewGuid(),1,SolicitudesWorkflowValues.Published,steps,routes);
    private static SolicitudesWorkflowStep Step(string code,bool initial=false,bool final=false,int strategy=SolicitudesWorkflowValues.RequestOwner,int activation=SolicitudesWorkflowValues.AnyIncoming)=>
        new(Guid.NewGuid(),code,299540140,1,initial,final,false,strategy,null,null,activation,false,false,false,false,null);
    private static SolicitudesWorkflowRoute Route(SolicitudesWorkflowStep source,SolicitudesWorkflowStep target,int result)=>
        new(Guid.NewGuid(),source.Code+"-"+target.Code,source.Id,target.Id,result,1);
    private static SolicitudesManagementExecution Run(int status)=>new(Guid.NewGuid(),Guid.NewGuid(),1,status);
}
