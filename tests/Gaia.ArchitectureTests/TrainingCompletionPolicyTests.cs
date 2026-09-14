using Gaia.Modules.Training;

namespace Gaia.ArchitectureTests;

public sealed class TrainingCompletionPolicyTests
{
    static TrainingEvaluationItem Evaluation(bool required=true,bool affects=true,int type=299541050)=>new(Guid.NewGuid(),Guid.NewGuid(),"Prueba",type,null,required,affects,70,3,null,false,false,true,false,false,true,1,[]);
    static TrainingAttemptOutcome Passed(TrainingEvaluationItem e)=>new(e.Id,299541113,true,80,100,80);
    [Fact] public void ContentWithoutEvaluationsCompletes(){Assert.True(TrainingCompletionPolicy.Evaluate(100,false,[],[]).Completed);}
    [Fact] public void IncompleteContentCannotBeApproved(){var e=Evaluation();Assert.Equal(299541091,TrainingCompletionPolicy.Evaluate(50,false,[e],[Passed(e)]).Status);}
    [Fact] public void RequiredEvaluationMustBeSubmitted(){var e=Evaluation();Assert.Equal(299541092,TrainingCompletionPolicy.Evaluate(100,false,[e],[]).Status);}
    [Fact] public void FailedEvaluationCannotApprove(){var e=Evaluation();Assert.Equal(299541094,TrainingCompletionPolicy.Evaluate(100,false,[e],[new(e.Id,299541113,false,20,100,20)]).Status);}
    [Fact] public void PendingManualReviewDoesNotComplete(){var e=Evaluation();Assert.False(TrainingCompletionPolicy.Evaluate(100,false,[e],[new(e.Id,299541112,null,0,100,null)]).Completed);}
    [Fact] public void SurveyOnlyNeedsSubmissionNotPassingScore(){var e=Evaluation(true,false,299541052);Assert.True(TrainingCompletionPolicy.Evaluate(100,true,[e],[new(e.Id,299541113,false,0,0,0)]).Completed);}
    [Fact] public void MissingMandatorySurveyPreventsCompletion(){Assert.False(TrainingCompletionPolicy.Evaluate(100,true,[],[]).Completed);}
    [Fact] public void BestPassedAttemptIsPreserved(){var e=Evaluation();Assert.True(TrainingCompletionPolicy.Evaluate(100,false,[e],[Passed(e),new(e.Id,299541113,false,20,100,20)]).Completed);}
    [Fact] public void OptionalEvaluationDoesNotBlockCompletion(){var e=Evaluation(false,false);Assert.True(TrainingCompletionPolicy.Evaluate(100,false,[e],[]).Completed);}
    [Fact] public void AffectsApprovalStillBlocksWhenNotMarkedRequired(){var e=Evaluation(false,true);Assert.False(TrainingCompletionPolicy.Evaluate(100,false,[e],[]).Completed);}
    [Fact] public void ResultIsWeightedAcrossEvaluations(){var first=Evaluation();var second=Evaluation();var outcome=TrainingCompletionPolicy.Evaluate(100,false,[first,second],[new(first.Id,299541113,true,10,10,100),new(second.Id,299541113,true,45,90,50)]);Assert.Equal(55,outcome.Result);}
}
