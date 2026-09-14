using Gaia.Modules.Training;

namespace Gaia.ArchitectureTests;

public sealed class TrainingEvaluationScoringTests
{
    static TrainingQuestionItem Question(int type=299541061,bool gradable=true,bool manual=false,bool required=true)=>new(Guid.NewGuid(),Guid.NewGuid(),"Pregunta","¿Cuál es la respuesta?",type,required,1,20,gradable,manual,1,5,"Bajo","Alto","Bien","Revisa",[]);
    static TrainingEvaluationItem Evaluation(params TrainingQuestionItem[] questions)=>new(Guid.NewGuid(),Guid.NewGuid(),"Prueba",299541050,null,true,true,70,3,10,false,false,true,true,true,true,1,questions);
    static TrainingQuestionItem Choices()=>Question() with {Options=[new(Guid.NewGuid(),Guid.Empty,"A","Correcta",1,true,20,null),new(Guid.NewGuid(),Guid.Empty,"B","Incorrecta",2,false,0,null)]};

    [Fact] public void DropdownGradesOnlySelectedOption(){var q=Choices();var result=TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,null,null,q.Options[0].Id)]);Assert.Equal(100,result.Percentage);Assert.Equal(20,result.Score);Assert.False(result.PendingReview);}
    [Fact] public void CorrectChoiceWithoutExplicitPointsUsesOnePoint(){var q=Choices();q=q with{MaximumScore=null,Options=q.Options.Select(o=>o with{Score=null}).ToArray()};var result=TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,null,null,q.Options[0].Id)]);Assert.Equal(1,result.Maximum);Assert.Equal(100,result.Percentage);}
    [Fact] public void IncorrectChoiceDoesNotPass(){var q=Choices();var result=TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,null,null,q.Options[1].Id)]);Assert.Equal(0,result.Percentage);Assert.False(result.Answers[0].Correct);}
    [Fact] public void MissingRequiredResponseIsRejected(){var q=Choices();Assert.Throws<ArgumentException>(()=>TrainingEvaluationScoring.Score(Evaluation(q),[]));}
    [Fact] public void ForeignOptionIsRejected(){var q=Choices();Assert.Throws<ArgumentException>(()=>TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,null,null,Guid.NewGuid())]));}
    [Fact] public void ForeignQuestionIsRejected(){var q=Choices();Assert.Throws<ArgumentException>(()=>TrainingEvaluationScoring.Score(Evaluation(q),[new(Guid.NewGuid(),null,null,q.Options[0].Id)]));}
    [Fact] public void DuplicateAnswersAreRejected(){var q=Choices();var a=new TrainingAnswer(q.Id,null,null,q.Options[0].Id);Assert.Throws<ArgumentException>(()=>TrainingEvaluationScoring.Score(Evaluation(q),[a,a]));}
    [Fact] public void OpenGradableAnswerRequiresManualReview(){var q=Question(299541064);var result=TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,"Explicación",null,null)]);Assert.True(result.PendingReview);Assert.Null(result.Answers[0].Correct);Assert.Equal(20,result.Maximum);}
    [Fact] public void UngradedSurveyCompletesWithoutManualReview(){var q=Question(299541064,false);var result=TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,"Me gustó",null,null)]);Assert.False(result.PendingReview);Assert.Equal(100,result.Percentage);Assert.Equal(0,result.Maximum);}
    [Theory] [InlineData(0)] [InlineData(6)] [InlineData(2.5)] public void ScaleRejectsInvalidValues(decimal value){var q=Question(299541065,false);Assert.Throws<ArgumentException>(()=>TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,null,value,null)]));}
    [Fact] public void ScaleAcceptsEndpointValues(){var q=Question(299541065,false);Assert.Equal(100,TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,null,5,null)]).Percentage);}
    [Fact] public void TooLongTextIsRejected(){var q=Question(299541063);Assert.Throws<ArgumentException>(()=>TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,new string('a',4001),null,null)]));}
    [Fact] public void ExpiredAttemptCannotPassWithCorrectResponses(){var q=Choices();var result=TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,null,null,q.Options[0].Id)],true);Assert.Equal(0,result.Percentage);Assert.Equal(0,result.Score);}
    [Fact] public void ExpiredAttemptCanPersistUnansweredQuestions(){var q=Choices();Assert.Equal(0,TrainingEvaluationScoring.Score(Evaluation(q),[],true).Percentage);}
    [Fact] public void OptionalBlankQuestionDoesNotRequireReview(){var q=Question(299541064,true,true,false);Assert.False(TrainingEvaluationScoring.Score(Evaluation(q),[]).PendingReview);}
    [Fact] public void BrowserTextAndNumbersCannotOverrideChoiceGrade(){var q=Choices();var result=TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,"Correcta",100000,q.Options[1].Id)]);Assert.Equal(0,result.Score);Assert.Null(result.Answers[0].Answer.Text);Assert.Null(result.Answers[0].Answer.Number);}
    [Fact] public void ConfiguredPointsAreBoundedByQuestionMaximum(){var q=Choices();q=q with{Options=[q.Options[0] with{Score=900},q.Options[1]]};Assert.Equal(20,TrainingEvaluationScoring.Score(Evaluation(q),[new(q.Id,null,null,q.Options[0].Id)]).Score);}
    [Fact] public void AggregateIsWeightedByQuestionPoints(){var first=Choices();var second=Choices() with{MaximumScore=80};var result=TrainingEvaluationScoring.Score(Evaluation(first,second),[new(first.Id,null,null,first.Options[0].Id),new(second.Id,null,null,second.Options[1].Id)]);Assert.Equal(20,result.Percentage);}
}
