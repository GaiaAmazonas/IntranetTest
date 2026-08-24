namespace Gaia.Modules.ThirdParties;

public interface IThirdPartyReader
{
    Task<IReadOnlyList<ThirdPartyResponse>> ListAsync(string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<ThirdPartyDirectoryResponse>> ListDirectoryAsync(string? search, CancellationToken cancellationToken);
    Task<ThirdPartyResponse?> GetAsync(Guid id, CancellationToken cancellationToken);
}
public sealed record ThirdPartyDirectoryResponse(Guid Id, string FullName, string DocumentType, string DocumentNumber, bool IsActive);

public interface IThirdPartyWriter
{
    Task<ThirdPartyWriteResult> CreateAsync(ThirdPartyWriteCommand command, CancellationToken cancellationToken);
    Task<ThirdPartyWriteResult> UpdateAsync(Guid id, ThirdPartyWriteCommand command, CancellationToken cancellationToken);
}

public interface IDocumentTypeReader { Task<IReadOnlyList<DocumentTypeResponse>> ListAsync(CancellationToken cancellationToken); }
public interface ICollaboratorEmailStore
{
    Task<IReadOnlyList<CollaboratorEmailResponse>> ListAsync(Guid thirdPartyId, CancellationToken cancellationToken);
    Task<RelatedWriteResult> CreateAsync(Guid thirdPartyId, CollaboratorEmailCommand command, CancellationToken cancellationToken);
    Task<RelatedWriteResult> UpdateAsync(Guid thirdPartyId, Guid id, CollaboratorEmailCommand command, CancellationToken cancellationToken);
}
public interface ICollaboratorPhoneStore
{
    Task<IReadOnlyList<CollaboratorPhoneResponse>> ListAsync(Guid thirdPartyId, CancellationToken cancellationToken);
    Task<RelatedWriteResult> CreateAsync(Guid thirdPartyId, CollaboratorPhoneCommand command, CancellationToken cancellationToken);
    Task<RelatedWriteResult> UpdateAsync(Guid thirdPartyId, Guid id, CollaboratorPhoneCommand command, CancellationToken cancellationToken);
}
public interface IAdministrativePersonnelImporter
{
    Task<PersonnelImportValidation> ValidateAsync(Stream workbook, CancellationToken cancellationToken);
    Task<PersonnelImportResult> ImportAsync(Stream workbook, CancellationToken cancellationToken);
}

public interface IOrganizationalAssignmentStore
{
    Task<IReadOnlyList<OrganizationalAssignmentResponse>> ListAsync(CancellationToken cancellationToken);
    Task<OrganizationalAssignmentWriteResult> CreateAsync(OrganizationalAssignmentCommand command, CancellationToken cancellationToken);
    Task<OrganizationalAssignmentWriteResult> UpdateAsync(Guid id, OrganizationalAssignmentCommand command, CancellationToken cancellationToken);
}
public interface IOrganizationalAssignmentImporter
{
    Task<OrganizationalAssignmentImportValidation> ValidateAsync(Stream workbook, CancellationToken cancellationToken);
    Task<OrganizationalAssignmentImportResult> ImportAsync(Stream workbook, CancellationToken cancellationToken);
}

public sealed record OrganizationalAssignmentResponse(Guid Id, Guid ThirdPartyId, string ThirdPartyName,
    string DocumentNumber, Guid PositionId, string PositionName, Guid OrganizationalUnitId,
    string OrganizationalUnitCode, string OrganizationalUnitName, DateOnly? StartDate, DateOnly? EndDate,
    bool IsPrimary, string? Observations, bool IsActive);
public sealed record OrganizationalAssignmentCommand(Guid ThirdPartyId, Guid PositionId, Guid OrganizationalUnitId,
    DateOnly? StartDate, DateOnly? EndDate, bool IsPrimary, string? Observations, bool IsActive, string Actor);
public enum OrganizationalAssignmentWriteStatus { Created, Updated, NotFound, InvalidThirdParty, InvalidPosition, InvalidUnit, Duplicate }
public sealed record OrganizationalAssignmentWriteResult(OrganizationalAssignmentWriteStatus Status, Guid? Id = null);
public sealed record OrganizationalAssignmentImportValidation(bool Valid, int Rows, int ToCreate, int ToUpdate,
    int Unchanged, IReadOnlyList<AdministrativeImportIssue> Issues);
public sealed record OrganizationalAssignmentImportResult(OrganizationalAssignmentImportValidation Validation,
    int Created, int Updated, int Unchanged, int Errors);

public sealed record DocumentTypeResponse(Guid Id, string Name, bool IsActive);
public sealed record ThirdPartyResponse(Guid Id, string FullName, Guid DocumentTypeId, string DocumentType,
    string DocumentNumber, string FirstName, string? MiddleName, string FirstSurname, string? SecondSurname,
    string Sex, DateOnly? BirthDate, string? Observations, bool IsActive);
public sealed record ThirdPartyWriteCommand(Guid DocumentTypeId, string DocumentNumber, string FirstName,
    string? MiddleName, string FirstSurname, string? SecondSurname, string Sex, DateOnly? BirthDate,
    string? Observations, bool IsActive, string Actor);
public sealed record CollaboratorEmailResponse(Guid Id, string Email, string? Observations, bool IsPrimary, bool IsActive, int ContactType);
public sealed record CollaboratorEmailCommand(string Email, string? Observations, bool IsPrimary, bool IsActive, string Actor, int ContactType = 1);
public sealed record CollaboratorPhoneResponse(Guid Id, string Number, string? Extension, string? Observations,
    bool IsPrimary, string PhoneType, bool IsActive, int ContactType);
public sealed record CollaboratorPhoneCommand(string Number, string? Extension, string? Observations,
    bool IsPrimary, string PhoneType, bool IsActive, string Actor, int ContactType = 1);

public enum ThirdPartyWriteStatus { Created, Updated, NotFound, InvalidDocumentType, InvalidSex, DuplicateDocument }
public sealed record ThirdPartyWriteResult(ThirdPartyWriteStatus Status, Guid? Id = null);
public enum RelatedWriteStatus { Created, Updated, NotFound, ParentNotFound, Duplicate, InvalidOption }
public sealed record RelatedWriteResult(RelatedWriteStatus Status, Guid? Id = null);
public sealed record AdministrativeImportIssue(int Row, string Sheet, string Code, string Detail, string Severity = "error");
public sealed record ImportEntityPreview(int ToCreate, int Existing, int ToUpdate, int Omitted, int Errors);
public sealed record PersonnelImportValidation(bool Valid, IReadOnlyDictionary<string, int> SheetRows,
    int Collaborators, int Jobs, int InstitutionalEmails, int PersonalEmails, int PersonalPhones,
    int CorporatePhones, int WithoutEmail, int WithoutPhone, IReadOnlyList<AdministrativeImportIssue> Issues,
    ImportEntityPreview? CollaboratorPlan = null, ImportEntityPreview? JobPlan = null,
    ImportEntityPreview? EmailPlan = null, ImportEntityPreview? PhonePlan = null);
public sealed record PersonnelImportResult(PersonnelImportValidation Validation, int CollaboratorsProcessed,
    int CollaboratorsCreated, int CollaboratorsExisting, int JobsProcessed, int JobsCreated,
    int JobsExisting, int EmailsCreated, int EmailsExisting, int PhonesCreated, int PhonesExisting,
    int Skipped, IReadOnlyList<AdministrativeImportIssue> Incidents, int CollaboratorsUpdated = 0,
    int CollaboratorsErrors = 0, int JobsUpdated = 0, int JobsErrors = 0, int EmailsUpdated = 0,
    int EmailsOmitted = 0, int EmailsErrors = 0, int PhonesUpdated = 0, int PhonesOmitted = 0,
    int PhonesErrors = 0);
