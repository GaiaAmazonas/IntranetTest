namespace Gaia.Modules.Training;

public sealed record TrainingAttemptOutcome(Guid EvaluationId,int Status,bool? Passed,decimal? Score,decimal? Maximum,decimal? Percentage);
public sealed record TrainingCompletionOutcome(int Status,bool Completed,decimal? Result);

public static class TrainingCompletionPolicy
{
    public static TrainingCompletionOutcome Evaluate(decimal contentProgress,bool surveyRequired,IReadOnlyList<TrainingEvaluationItem> evaluations,IReadOnlyList<TrainingAttemptOutcome> attempts)
    {
        var needed=evaluations.Where(x=>x.Required||x.AffectsApproval||(surveyRequired&&x.Type==299541052)).ToArray();
        bool Satisfied(TrainingEvaluationItem e)=>attempts.Any(a=>a.EvaluationId==e.Id&&a.Status==299541113&&(!e.AffectsApproval||a.Passed==true));
        var done=contentProgress>=100&&needed.All(Satisfied)&&(!surveyRequired||evaluations.Any(x=>x.Type==299541052));
        var scored=evaluations.Where(x=>x.AffectsApproval).Select(e=>attempts.Where(a=>a.EvaluationId==e.Id&&a.Status==299541113).OrderByDescending(a=>a.Percentage??0).FirstOrDefault()).OfType<TrainingAttemptOutcome>().ToArray();
        var maximum=scored.Sum(a=>a.Maximum??0);
        decimal? result=scored.Length==0?null:maximum==0?100:Math.Round(scored.Sum(a=>a.Score??0)*100/maximum,2);
        var failed=needed.Any(e=>e.AffectsApproval&&!Satisfied(e)&&attempts.Any(a=>a.EvaluationId==e.Id&&a.Status==299541113));
        return new(done?299541093:contentProgress<100?299541091:failed?299541094:299541092,done,result);
    }
}
