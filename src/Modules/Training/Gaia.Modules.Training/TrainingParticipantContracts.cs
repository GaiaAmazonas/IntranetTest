using Gaia.BuildingBlocks.Files;

namespace Gaia.Modules.Training;

public sealed record TrainingAnswer(Guid QuestionId, string? Text, decimal? Number, Guid? OptionId);
public sealed record SubmitTrainingAttempt(IReadOnlyList<TrainingAnswer> Answers);
public sealed record StartTrainingAttempt(Guid Token);
public sealed record ParticipantOption(Guid Id, string Name);
public sealed record ParticipantQuestion(Guid Id, string Statement, int Type, bool Required, int? MinimumScale, int? MaximumScale, string? MinimumLabel, string? MaximumLabel, IReadOnlyList<ParticipantOption> Options);
public sealed record ParticipantAnswerResult(Guid QuestionId, decimal? Score, bool? Correct, string? Feedback, IReadOnlyList<Guid>? CorrectOptionIds);
public sealed record ParticipantAttempt(Guid Id, int Number, int Status, DateTimeOffset StartedAt, DateTimeOffset? SubmittedAt, DateTimeOffset? Deadline, decimal? Percentage, bool? Passed, IReadOnlyList<ParticipantAnswerResult> Results);
public sealed record ParticipantEvaluation(Guid Id, string Name, string? Description, int Type, bool Required, bool AffectsApproval, int? MaximumAttempts, bool AllowRetry, int? TimeLimitMinutes, IReadOnlyList<ParticipantQuestion> Questions, IReadOnlyList<ParticipantAttempt> Attempts);
public sealed record TrainingManualAnswer(Guid Id, string Statement, string? Text, decimal? Number, decimal MaximumScore);
public sealed record TrainingPendingReview(Guid AttemptId, Guid AssignmentId, Guid VersionId, string Participant, string Training, string Evaluation, DateTimeOffset? SubmittedAt, IReadOnlyList<TrainingManualAnswer> Answers);
public sealed record TrainingManualGrade(Guid AnswerId, decimal Score, string? Comment);
public sealed record GradeTrainingAttempt(IReadOnlyList<TrainingManualGrade> Answers);

public interface ITrainingParticipantOperations
{
    Task<IReadOnlyList<ParticipantEvaluation>> ReadEvaluationsAsync(Guid assignmentId, Guid actorId, CancellationToken token);
    Task<ParticipantAttempt> StartAttemptAsync(Guid assignmentId, Guid evaluationId, Guid idempotencyToken, Guid actorId, CancellationToken token);
    Task<ParticipantAttempt> SubmitAttemptAsync(Guid assignmentId, Guid attemptId, SubmitTrainingAttempt value, Guid actorId, CancellationToken token);
    Task<IReadOnlyList<TrainingPendingReview>> ReadPendingReviewsAsync(CancellationToken token);
    Task GradeAttemptAsync(Guid attemptId, GradeTrainingAttempt value, Guid actorId, CancellationToken token);
    Task<ExternalFileId> ReadResourceAsync(Guid assignmentId, Guid resourceId, Guid actorId, CancellationToken token);
    Task<ExternalFileId> ReadVersionResourceAsync(Guid versionId, Guid resourceId, CancellationToken token);
}
