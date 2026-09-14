namespace Gaia.Modules.Training;

public sealed record ScoredTrainingAnswer(TrainingQuestionItem Question, TrainingAnswer Answer, decimal Maximum, decimal Score, bool? Correct, bool Manual);
public sealed record ScoredTrainingEvaluation(IReadOnlyList<ScoredTrainingAnswer> Answers, decimal Maximum, decimal Score, decimal Percentage, bool PendingReview);

/// <summary>Scores trusted configuration, never points or correctness supplied by the browser.</summary>
public static class TrainingEvaluationScoring
{
    public static decimal Maximum(TrainingQuestionItem question) => !question.Gradable ? 0 : Math.Max(0, question.MaximumScore ?? Math.Max(1,question.Options.Select(x => x.Score ?? 0).DefaultIfEmpty(0).Max()));

    public static ScoredTrainingEvaluation Score(TrainingEvaluationItem evaluation, IReadOnlyList<TrainingAnswer> answers, bool timedOut = false)
    {
        if (answers.Count > evaluation.Questions.Count || answers.Select(x => x.QuestionId).Distinct().Count() != answers.Count || answers.Any(x => evaluation.Questions.All(q => q.Id != x.QuestionId)))
            throw new ArgumentException("Las respuestas contienen preguntas duplicadas o ajenas a la evaluación.");
        var map = answers.ToDictionary(x => x.QuestionId);
        var scored = new List<ScoredTrainingAnswer>();
        foreach (var question in evaluation.Questions)
        {
            var answer = map.GetValueOrDefault(question.Id) ?? new(question.Id, null, null, null);
            var choice = question.Type is 299541060 or 299541061 or 299541062;
            if (answer.OptionId.HasValue && (!choice || question.Options.All(x => x.Id != answer.OptionId))) throw new ArgumentException("Selecciona una respuesta válida para cada pregunta.");
            var answered = choice ? answer.OptionId.HasValue : question.Type == 299541065 ? answer.Number.HasValue : !string.IsNullOrWhiteSpace(answer.Text);
            if (!timedOut && question.Required && !answered) throw new ArgumentException($"Responde la pregunta obligatoria: {question.Name}.");
            if (answer.Text?.Length > 4000) throw new ArgumentException("Una respuesta supera los 4000 caracteres.");
            if (question.Type == 299541065 && answer.Number is decimal number && (number != Math.Truncate(number) || number < (question.MinimumScale ?? 1) || number > (question.MaximumScale ?? 5))) throw new ArgumentException("El valor de la escala está fuera del rango permitido.");
            answer = answer with { Text = choice || question.Type == 299541065 ? null : answer.Text?.Trim(), Number = question.Type == 299541065 ? answer.Number : null, OptionId = choice ? answer.OptionId : null };
            var max = Maximum(question);
            var manual = !timedOut && answered && question.Gradable && (question.RequiresManualReview || !choice);
            var option = question.Options.FirstOrDefault(x => x.Id == answer.OptionId);
            var points = timedOut || manual || !question.Gradable ? 0 : Math.Clamp(option?.Score ?? (option?.IsCorrect == true ? max : 0), 0, max);
            scored.Add(new(question, answer, max, points, question.Gradable && !manual ? option?.IsCorrect == true && !timedOut : null, manual));
        }
        var maximum = scored.Sum(x => x.Maximum); var score = scored.Sum(x => x.Score);
        return new(scored, maximum, score, timedOut ? 0 : maximum == 0 ? 100 : Math.Round(score * 100 / maximum, 2), scored.Any(x => x.Manual));
    }
}
