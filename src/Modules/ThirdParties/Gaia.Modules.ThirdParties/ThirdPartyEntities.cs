namespace Gaia.Modules.ThirdParties;

public abstract class ThirdPartyChild
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ThirdPartyId { get; init; }
    public ThirdParty? ThirdParty { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ThirdParty
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string PersonType { get; set; }
    public required string DocumentType { get; set; }
    public required string DocumentNumber { get; set; }
    public required string FullName { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? FirstSurname { get; set; }
    public string? SecondSurname { get; set; }
    public string? PreferredName { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string? PersonalEmail { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? AlternatePhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Observations { get; set; }
    public bool IsActive { get; set; } = true;
    public bool NeedsNameReview { get; set; }
    public int? SourceRow { get; set; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string CreatedBy { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class Engagement : ThirdPartyChild
{
    public required string Type { get; init; }
    public string? CorporateEmail { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public required string Status { get; init; }
}

public sealed class OrganizationalAssignment : ThirdPartyChild
{
    public Guid? OrganizationalUnitId { get; init; }
    public Guid? PositionId { get; init; }
    public required string RoleName { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool IsPrimary { get; init; } = true;
    public string? SourceAreaCode { get; init; }
}

public sealed class Education : ThirdPartyChild
{
    public required string AcademicLevel { get; init; }
    public required string Title { get; init; }
    public string? Institution { get; init; }
    public bool Graduated { get; init; }
    public required string ValidationStatus { get; init; }
}

public sealed class LanguageSkill : ThirdPartyChild
{
    public required string Language { get; init; }
    public required string OverallLevel { get; init; }
    public string? ReadingLevel { get; init; }
    public string? WritingLevel { get; init; }
    public string? SpeakingLevel { get; init; }
    public string? Certification { get; init; }
}

public sealed class Training : ThirdPartyChild
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public string? Institution { get; init; }
    public DateOnly? CompletionDate { get; init; }
}

public sealed class Experience : ThirdPartyChild
{
    public required string Organization { get; init; }
    public required string Role { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public string? Description { get; init; }
}

public sealed class EmergencyContact : ThirdPartyChild
{
    public required string FullName { get; init; }
    public required string Relationship { get; init; }
    public required string Phone { get; init; }
    public string? AlternatePhone { get; init; }
    public bool IsPrimary { get; init; } = true;
}

public sealed class ImportIssue
{
    public long Id { get; init; }
    public required string BatchId { get; init; }
    public int SourceRow { get; init; }
    public required string Severity { get; init; }
    public required string Code { get; init; }
    public required string Detail { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
