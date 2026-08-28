namespace Gaia.Modules.ThirdParties;

public interface IIntranetDirectoryReader
{
    Task<IntranetPeoplePage> ListPeopleAsync(string? search, Guid? organizationUnitId, bool includeDescendants,
        int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<IntranetOrganizationUnit>> ListOrganizationUnitsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<IntranetBirthday>> ListBirthdaysAsync(int month, CancellationToken cancellationToken);
}

public sealed record IntranetPerson(
    Guid Id,
    string FullName,
    string? JobTitle,
    Guid? OrganizationUnitId,
    string? OrganizationUnit,
    string? OrganizationUnitCode,
    string? Site,
    string? InstitutionalEmail,
    string? VisiblePhone,
    string? PhotoUrl);

public sealed record IntranetPeoplePage(IReadOnlyList<IntranetPerson> Items, int Page, int PageSize, int Total);

public sealed record IntranetOrganizationUnit(Guid Id, string Code, string Name, Guid? ParentId, int Level, int VisualOrder);

public sealed record IntranetBirthday(Guid Id, string FullName, int Day, int Month, string? PhotoUrl);
