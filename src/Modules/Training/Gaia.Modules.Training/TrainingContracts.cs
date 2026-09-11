namespace Gaia.Modules.Training;

public sealed record TrainingCategoryItem(Guid Id,string Code,string Name,string? Description,string? Color,string? Icon,int Order,bool IsActive);
public sealed record TrainingReferenceItem(Guid Id,string Code,string Name,Guid? ParentId=null,Guid? UnitId=null,int Level=0);
public sealed record TrainingCatalogItem(Guid Id,string Code,string Name,string? Description,Guid CategoryId,string? Category,Guid UnitId,string? Unit,Guid ResponsibleId,string? Responsible,bool IsActive);
public sealed record TrainingVersionItem(Guid Id,Guid TrainingId,string Training,string Number,string PublicTitle,string? Summary,string? Objective,int Status,DateTimeOffset? AvailableFrom,DateTimeOffset? AvailableUntil,int? DaysToComplete,int ExpirationRule,bool DynamicAudience,bool SequentialOrder,decimal? MinimumPercentage,bool RequiresApproval,bool SurveyRequired,bool AllowExpired,string? CompletionMessage,bool IsActive);
public sealed record TrainingAdministrationOverview(int Categories,int Trainings,int Versions,IReadOnlyList<TrainingCategoryItem> CategoryItems,IReadOnlyList<TrainingCatalogItem> Items,IReadOnlyList<TrainingVersionItem> VersionItems,IReadOnlyList<TrainingReferenceItem> Units,IReadOnlyList<TrainingReferenceItem> Responsibles,Guid? CurrentUnitId);
public sealed record SaveTrainingCategory(string Name,string? Description,string? Color,string? Icon,int Order,bool IsActive);
public sealed record SaveTraining(string Name,string? Description,Guid CategoryId,Guid UnitId,Guid ResponsibleId,bool IsActive);
public sealed record SaveTrainingVersion(Guid TrainingId,string PublicTitle,string? Summary,string? Objective,DateTimeOffset? AvailableFrom,DateTimeOffset? AvailableUntil,int? DaysToComplete,int ExpirationRule,bool DynamicAudience,bool SequentialOrder,decimal? MinimumPercentage,bool RequiresApproval,bool SurveyRequired,bool AllowExpired,string? CompletionMessage,Guid? SourceVersionId);
public sealed record TrainingSectionItem(Guid Id,Guid VersionId,string Name,string? Description,int Order,bool Required,bool Active,IReadOnlyList<TrainingBlockItem> Blocks);
public sealed record TrainingBlockItem(Guid Id,Guid SectionId,string Name,int Type,int Order,bool Required,string? Html,string? ExternalUrl,string? ActionText,bool OpenNewTab,int? MinimumViewPercentage,bool RequiresConfirmation,bool Active,Guid? ResourceId);
public sealed record SaveTrainingSection(string Name,string? Description,int Order,bool Required,bool Active);
public sealed record SaveTrainingBlock(string Name,int Type,int Order,bool Required,string? Html,string? ExternalUrl,string? ActionText,bool OpenNewTab,int? MinimumViewPercentage,bool RequiresConfirmation,bool Active,Guid? ResourceId=null);
public sealed record SaveTrainingResource(Guid VersionId,Guid UploadedById,int Type,string OriginalName,string StoredName,string Provider,string RepositoryId,string ContainerId,string FileId,string? LogicalPath,string? WebUrl,string ContentType,long Length,string? Sha256,string? ETag,DateTimeOffset UploadedAt);
public sealed record TrainingResourceItem(Guid Id,string Name,int Type,string ContentType,long Length,string? WebUrl);
public sealed record TrainingEvaluationItem(Guid Id,Guid VersionId,string Name,int Type,string? Description,bool Required,bool AffectsApproval,decimal? MinimumPercentage,int? MaximumAttempts,int? TimeLimitMinutes,bool RandomizeQuestions,bool RandomizeOptions,bool ShowResult,bool ShowCorrectAnswers,bool ShowFeedback,bool AllowRetry,int Order,IReadOnlyList<TrainingQuestionItem> Questions);
public sealed record TrainingQuestionItem(Guid Id,Guid EvaluationId,string Name,string Statement,int Type,bool Required,int Order,decimal? MaximumScore,bool Gradable,bool RequiresManualReview,int? MinimumScale,int? MaximumScale,string? MinimumLabel,string? MaximumLabel,string? CorrectFeedback,string? IncorrectFeedback,IReadOnlyList<TrainingOptionItem> Options);
public sealed record TrainingOptionItem(Guid Id,Guid QuestionId,string Code,string Name,int Order,bool IsCorrect,decimal? Score,string? Feedback);
public sealed record SaveTrainingEvaluation(string Name,int Type,string? Description,bool Required,bool AffectsApproval,decimal? MinimumPercentage,int? MaximumAttempts,int? TimeLimitMinutes,bool RandomizeQuestions,bool RandomizeOptions,bool ShowResult,bool ShowCorrectAnswers,bool ShowFeedback,bool AllowRetry,int Order);
public sealed record SaveTrainingQuestion(string Name,string Statement,int Type,bool Required,int Order,decimal? MaximumScore,bool Gradable,bool RequiresManualReview,int? MinimumScale,int? MaximumScale,string? MinimumLabel,string? MaximumLabel,string? CorrectFeedback,string? IncorrectFeedback);
public sealed record SaveTrainingOption(string Name,int Order,bool IsCorrect,decimal? Score,string? Feedback);
public sealed record TrainingAudienceRuleItem(Guid Id,Guid VersionId,int Type,int Mode,Guid? UnitId,string? Unit,Guid? PersonId,string? Person,bool IncludeSubunits,string? Reason);
public sealed record TrainingAudiencePerson(Guid Id,string Name,Guid? UnitId,string? Unit,bool IsExcluded,string? ExclusionReason);
public sealed record TrainingAudienceOverview(IReadOnlyList<TrainingAudienceRuleItem> Rules,IReadOnlyList<TrainingAudiencePerson> Preview,int IncludedCount,int ExcludedCount);
public sealed record SaveTrainingAudienceRule(int Type,int Mode,Guid? UnitId,Guid? PersonId,bool IncludeSubunits,string? Reason);
public sealed record TrainingVersionTransition(string? Reason,string? CorrelationId);
public sealed record TrainingAssignmentItem(Guid Id,Guid VersionId,Guid ParticipantId,string Training,string Version,string? Summary,string Participant,Guid? UnitId,string? Unit,int Status,DateTimeOffset AssignedAt,DateTimeOffset? DueAt,DateTimeOffset? StartedAt,DateTimeOffset? LastAccessAt,DateTimeOffset? CompletedAt,DateTimeOffset? ApprovedAt,decimal Progress,decimal? Result,int Attempts);
public sealed record TrainingTrackingOverview(int Total,int Assigned,int InProgress,int Overdue,int Completed,IReadOnlyList<TrainingAssignmentItem> Items);
public sealed record TrainingResultItem(Guid AssignmentId,Guid VersionId,string Training,string Version,string Participant,string? Unit,int Status,decimal Progress,decimal? Result,int Attempts,DateTimeOffset? CompletedAt,DateTimeOffset? ApprovedAt);
public sealed record TrainingResultsOverview(int Participants,int Completed,int Approved,int NotApproved,decimal AverageResult,IReadOnlyList<TrainingResultItem> Items);
public sealed record TrainingAssignmentGenerationResult(int Audience,int Created,int Existing);
public sealed record MyTrainingItem(Guid AssignmentId,Guid VersionId,string Training,string Version,string? Summary,int Status,DateTimeOffset AssignedAt,DateTimeOffset? DueAt,decimal Progress,decimal? Result);
public sealed record MyTrainingDetail(MyTrainingItem Assignment,string? Objective,bool SequentialOrder,string? CompletionMessage,IReadOnlyList<TrainingSectionItem> Sections,IReadOnlyList<Guid> CompletedBlockIds);
public sealed record TrainingProgressResult(decimal Progress,int Status,bool Completed);

public interface ITrainingAdministrationReader
{
    Task<TrainingAdministrationOverview> ReadAsync(Guid? actorId,CancellationToken token);
    Task<Guid> SaveCategoryAsync(Guid? id,SaveTrainingCategory value,CancellationToken token);
    Task<Guid> SaveTrainingAsync(Guid? id,SaveTraining value,CancellationToken token);
    Task<Guid> SaveVersionAsync(Guid? id,SaveTrainingVersion value,Guid actorId,CancellationToken token);
    Task<IReadOnlyList<TrainingSectionItem>> ReadContentAsync(Guid versionId,CancellationToken token);
    Task<Guid> SaveSectionAsync(Guid versionId,Guid? id,SaveTrainingSection value,CancellationToken token);
    Task RemoveSectionAsync(Guid versionId,Guid id,CancellationToken token);
    Task<Guid> SaveBlockAsync(Guid sectionId,Guid? id,SaveTrainingBlock value,CancellationToken token);
    Task RemoveBlockAsync(Guid sectionId,Guid id,CancellationToken token);
    Task<TrainingResourceItem> SaveResourceAsync(SaveTrainingResource value,CancellationToken token);
    Task<IReadOnlyList<TrainingEvaluationItem>> ReadEvaluationsAsync(Guid versionId,CancellationToken token);
    Task<Guid> SaveEvaluationAsync(Guid versionId,Guid? id,SaveTrainingEvaluation value,CancellationToken token);
    Task RemoveEvaluationAsync(Guid versionId,Guid id,CancellationToken token);
    Task<Guid> SaveQuestionAsync(Guid evaluationId,Guid? id,SaveTrainingQuestion value,CancellationToken token);
    Task RemoveQuestionAsync(Guid evaluationId,Guid id,CancellationToken token);
    Task<Guid> SaveOptionAsync(Guid questionId,Guid? id,SaveTrainingOption value,CancellationToken token);
    Task RemoveOptionAsync(Guid questionId,Guid id,CancellationToken token);
    Task<TrainingAudienceOverview> ReadAudienceAsync(Guid versionId,CancellationToken token);
    Task<Guid> SaveAudienceRuleAsync(Guid versionId,SaveTrainingAudienceRule value,Guid actorId,CancellationToken token);
    Task RemoveAudienceRuleAsync(Guid versionId,Guid id,CancellationToken token);
    Task TransitionVersionAsync(Guid versionId,int targetStatus,string? reason,string? correlationId,Guid actorId,CancellationToken token);
}

public interface ITrainingOperations
{
    Task<TrainingAssignmentGenerationResult> GenerateAssignmentsAsync(Guid versionId,Guid actorId,CancellationToken token);
    Task<TrainingTrackingOverview> ReadTrackingAsync(CancellationToken token);
    Task<TrainingResultsOverview> ReadResultsAsync(CancellationToken token);
    Task<IReadOnlyList<MyTrainingItem>> ReadMyAssignmentsAsync(Guid actorId,CancellationToken token);
    Task<MyTrainingDetail> ReadMyAssignmentAsync(Guid assignmentId,Guid actorId,CancellationToken token);
    Task<TrainingProgressResult> CompleteBlockAsync(Guid assignmentId,Guid blockId,Guid actorId,CancellationToken token);
}
