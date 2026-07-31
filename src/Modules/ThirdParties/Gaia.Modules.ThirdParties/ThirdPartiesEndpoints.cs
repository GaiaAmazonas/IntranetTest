using System.Security.Claims;
using Gaia.Modules.Organization.Infrastructure;
using Gaia.Modules.ThirdParties.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.ThirdParties;

internal static class ThirdPartiesEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/third-parties")
            .WithTags("Third parties")
            .RequireAuthorization(ThirdPartyPermissions.Read);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync).RequireAuthorization(ThirdPartyPermissions.Manage);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(ThirdPartyPermissions.Manage);
        group.MapPost("/import", ImportAsync).RequireAuthorization(ThirdPartyPermissions.Manage);
        group.MapGet("/import-issues", ListImportIssuesAsync)
            .RequireAuthorization(ThirdPartyPermissions.Manage);
        group.MapPost("/{id:guid}/languages", AddLanguageAsync).RequireAuthorization(ThirdPartyPermissions.Manage);
        group.MapPost("/{id:guid}/studies", AddStudyAsync).RequireAuthorization(ThirdPartyPermissions.Manage);
        group.MapPost("/{id:guid}/trainings", AddTrainingAsync).RequireAuthorization(ThirdPartyPermissions.Manage);
        group.MapPost("/{id:guid}/experiences", AddExperienceAsync).RequireAuthorization(ThirdPartyPermissions.Manage);
        group.MapPost("/{id:guid}/emergency-contacts", AddEmergencyAsync).RequireAuthorization(ThirdPartyPermissions.Manage);
    }

    private static async Task<IResult> ListImportIssuesAsync(
        string? batchId,
        ThirdPartiesDbContext context,
        CancellationToken cancellationToken)
    {
        var query = context.ImportIssues.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(batchId))
            query = query.Where(item => item.BatchId == batchId);
        return Results.Ok(await query.OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.SourceRow)
            .ToListAsync(cancellationToken));
    }

    private static async Task<IResult> ListAsync(
        string? search,
        ThirdPartiesDbContext context,
        CancellationToken cancellationToken)
    {
        var query = context.ThirdParties.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.FullName, pattern)
                || EF.Functions.ILike(item.DocumentNumber, pattern));
        }
        return Results.Ok(await query.OrderBy(item => item.FullName)
            .Select(item => new
            {
                item.Id, item.FullName, item.DocumentType, item.DocumentNumber,
                item.PersonType, item.IsActive, item.NeedsNameReview
            }).ToListAsync(cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ThirdPartiesDbContext context,
        CancellationToken cancellationToken)
    {
        var party = await context.ThirdParties.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (party is null) return Results.NotFound();
        return Results.Ok(new
        {
            party,
            engagements = await context.Engagements.AsNoTracking().Where(item => item.ThirdPartyId == id).ToListAsync(cancellationToken),
            assignments = await context.Assignments.AsNoTracking().Where(item => item.ThirdPartyId == id).ToListAsync(cancellationToken),
            studies = await context.Studies.AsNoTracking().Where(item => item.ThirdPartyId == id).ToListAsync(cancellationToken),
            languages = await context.Languages.AsNoTracking().Where(item => item.ThirdPartyId == id).ToListAsync(cancellationToken),
            trainings = await context.Trainings.AsNoTracking().Where(item => item.ThirdPartyId == id).ToListAsync(cancellationToken),
            experiences = await context.Experiences.AsNoTracking().Where(item => item.ThirdPartyId == id).ToListAsync(cancellationToken),
            emergencyContacts = await context.EmergencyContacts.AsNoTracking().Where(item => item.ThirdPartyId == id).ToListAsync(cancellationToken)
        });
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] ThirdPartyRequest request,
        ClaimsPrincipal principal,
        ThirdPartiesDbContext context,
        CancellationToken cancellationToken)
    {
        var normalizedDocumentType = request.DocumentType.Trim().ToUpperInvariant();
        var normalizedDocumentNumber = request.DocumentNumber.Trim();
        if (await context.ThirdParties.AnyAsync(item =>
                item.DocumentType == normalizedDocumentType
                && item.DocumentNumber == normalizedDocumentNumber, cancellationToken))
            return Problem(409, "Ya existe un tercero con ese documento.");
        var party = NewParty(request, Actor(principal));
        context.Add(party);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/third-parties/{party.Id}", new { party.Id });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] ThirdPartyRequest request,
        ClaimsPrincipal principal,
        ThirdPartiesDbContext context,
        CancellationToken cancellationToken)
    {
        var party = await context.ThirdParties.FindAsync([id], cancellationToken);
        if (party is null) return Results.NotFound();
        Apply(party, request);
        party.UpdatedAtUtc = DateTimeOffset.UtcNow;
        party.UpdatedBy = Actor(principal);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { party.Id });
    }

    private static async Task<IResult> ImportAsync(
        [FromBody] IReadOnlyList<ThirdPartyImportRow> rows,
        ClaimsPrincipal principal,
        ThirdPartiesDbContext context,
        OrganizationDbContext organization,
        CancellationToken cancellationToken)
    {
        var batchId = $"TER-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var units = await organization.Units.AsNoTracking()
            .ToDictionaryAsync(item => item.Code, cancellationToken);
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var warnings = 0;

        foreach (var row in rows)
        {
            var fullName = row.FullName.Trim();
            if (fullName.Equals("BODEGA", StringComparison.OrdinalIgnoreCase))
            {
                context.ImportIssues.Add(Issue(batchId, row.SourceRow, "warning", "NOT_A_PERSON",
                    "BODEGA se omitió: debe modelarse como ubicación o custodio institucional."));
                skipped++;
                warnings++;
                continue;
            }
            var document = row.DocumentNumber.Trim();
            if (string.IsNullOrWhiteSpace(document) || string.IsNullOrWhiteSpace(fullName))
            {
                context.ImportIssues.Add(Issue(batchId, row.SourceRow, "error", "MISSING_IDENTITY",
                    "Documento o nombre vacío."));
                skipped++;
                continue;
            }

            var party = await context.ThirdParties.FirstOrDefaultAsync(item =>
                item.DocumentType == "CC" && item.DocumentNumber == document, cancellationToken);
            if (party is null)
            {
                party = new ThirdParty
                {
                    PersonType = "Natural",
                    DocumentType = "CC",
                    DocumentNumber = document,
                    FullName = fullName,
                    NeedsNameReview = true,
                    SourceRow = row.SourceRow,
                    IsActive = row.Status.Equals("Activo", StringComparison.OrdinalIgnoreCase),
                    CreatedBy = Actor(principal)
                };
                context.ThirdParties.Add(party);
                inserted++;
            }
            else
            {
                party.FullName = fullName;
                party.IsActive = row.Status.Equals("Activo", StringComparison.OrdinalIgnoreCase);
                party.UpdatedAtUtc = DateTimeOffset.UtcNow;
                party.UpdatedBy = Actor(principal);
                updated++;
            }
            await context.SaveChangesAsync(cancellationToken);

            var engagement = await context.Engagements.FirstOrDefaultAsync(item =>
                item.ThirdPartyId == party.Id && item.Status == "Activa", cancellationToken);
            if (engagement is null)
            {
                context.Engagements.Add(new Engagement
                {
                    ThirdPartyId = party.Id,
                    Type = "Por clasificar",
                    CorporateEmail = NormalizeEmail(row.CorporateEmail),
                    StartDate = new DateOnly(2021, 1, 1),
                    Status = "Activa"
                });
            }

            units.TryGetValue(row.AreaCode.Trim(), out var unit);
            if (unit is null)
            {
                context.ImportIssues.Add(Issue(batchId, row.SourceRow, "warning", "AREA_NOT_RESOLVED",
                    $"No se resolvió el código de área '{row.AreaCode}'."));
                warnings++;
            }
            var assignment = await context.Assignments.FirstOrDefaultAsync(item =>
                item.ThirdPartyId == party.Id && item.IsPrimary && item.EndDate == null, cancellationToken);
            if (assignment is null)
            {
                context.Assignments.Add(new OrganizationalAssignment
                {
                    ThirdPartyId = party.Id,
                    OrganizationalUnitId = unit?.Id,
                    RoleName = row.Role.Trim(),
                    SourceAreaCode = row.AreaCode.Trim(),
                    StartDate = new DateOnly(2021, 1, 1),
                    IsPrimary = true
                });
            }
        }
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { batchId, processed = rows.Count, inserted, updated, skipped, warnings });
    }

    private static async Task<IResult> AddLanguageAsync(Guid id, LanguageRequest request, ThirdPartiesDbContext context, CancellationToken ct) =>
        await AddChildAsync(id, new LanguageSkill { ThirdPartyId = id, Language = request.Language, OverallLevel = request.OverallLevel, ReadingLevel = request.ReadingLevel, WritingLevel = request.WritingLevel, SpeakingLevel = request.SpeakingLevel, Certification = request.Certification }, context, ct);
    private static async Task<IResult> AddStudyAsync(Guid id, StudyRequest request, ThirdPartiesDbContext context, CancellationToken ct) =>
        await AddChildAsync(id, new Education { ThirdPartyId = id, AcademicLevel = request.AcademicLevel, Title = request.Title, Institution = request.Institution, Graduated = request.Graduated, ValidationStatus = "Pendiente" }, context, ct);
    private static async Task<IResult> AddTrainingAsync(Guid id, TrainingRequest request, ThirdPartiesDbContext context, CancellationToken ct) =>
        await AddChildAsync(id, new Training { ThirdPartyId = id, Type = request.Type, Name = request.Name, Institution = request.Institution, CompletionDate = request.CompletionDate }, context, ct);
    private static async Task<IResult> AddExperienceAsync(Guid id, ExperienceRequest request, ThirdPartiesDbContext context, CancellationToken ct) =>
        await AddChildAsync(id, new Experience { ThirdPartyId = id, Organization = request.Organization, Role = request.Role, StartDate = request.StartDate, EndDate = request.EndDate, Description = request.Description }, context, ct);
    private static async Task<IResult> AddEmergencyAsync(Guid id, EmergencyRequest request, ThirdPartiesDbContext context, CancellationToken ct) =>
        await AddChildAsync(id, new EmergencyContact { ThirdPartyId = id, FullName = request.FullName, Relationship = request.Relationship, Phone = request.Phone, AlternatePhone = request.AlternatePhone, IsPrimary = request.IsPrimary }, context, ct);

    private static async Task<IResult> AddChildAsync<T>(Guid id, T child, ThirdPartiesDbContext context, CancellationToken ct) where T : ThirdPartyChild
    {
        if (!await context.ThirdParties.AnyAsync(item => item.Id == id, ct)) return Results.NotFound();
        context.Add(child);
        await context.SaveChangesAsync(ct);
        return Results.Created($"/api/third-parties/{id}", new { child.Id });
    }

    private static ThirdParty NewParty(ThirdPartyRequest request, string actor) => new()
    {
        PersonType = request.PersonType.Trim(), DocumentType = request.DocumentType.Trim().ToUpperInvariant(),
        DocumentNumber = request.DocumentNumber.Trim(), FullName = request.FullName.Trim(),
        FirstName = request.FirstName, MiddleName = request.MiddleName, FirstSurname = request.FirstSurname,
        SecondSurname = request.SecondSurname, PreferredName = request.PreferredName, BirthDate = request.BirthDate,
        PersonalEmail = request.PersonalEmail, PrimaryPhone = request.PrimaryPhone, AlternatePhone = request.AlternatePhone,
        Address = request.Address, City = request.City, Observations = request.Observations,
        IsActive = request.IsActive, NeedsNameReview = request.NeedsNameReview, CreatedBy = actor
    };
    private static void Apply(ThirdParty party, ThirdPartyRequest request)
    {
        party.PersonType = request.PersonType.Trim(); party.DocumentType = request.DocumentType.Trim().ToUpperInvariant();
        party.DocumentNumber = request.DocumentNumber.Trim(); party.FullName = request.FullName.Trim();
        party.FirstName = request.FirstName; party.MiddleName = request.MiddleName; party.FirstSurname = request.FirstSurname;
        party.SecondSurname = request.SecondSurname; party.PreferredName = request.PreferredName; party.BirthDate = request.BirthDate;
        party.PersonalEmail = request.PersonalEmail; party.PrimaryPhone = request.PrimaryPhone; party.AlternatePhone = request.AlternatePhone;
        party.Address = request.Address; party.City = request.City; party.Observations = request.Observations;
        party.IsActive = request.IsActive; party.NeedsNameReview = request.NeedsNameReview;
    }
    private static ImportIssue Issue(string batch, int row, string severity, string code, string detail) =>
        new() { BatchId = batch, SourceRow = row, Severity = severity, Code = code, Detail = detail };
    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) || email.Equals("No tiene", StringComparison.OrdinalIgnoreCase) ? null : email.Trim();
    private static string Actor(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity?.Name ?? "unknown";
    private static IResult Problem(int status, string detail) => Results.Problem(statusCode: status, detail: detail);
}

public sealed record ThirdPartyRequest(string PersonType, string DocumentType, string DocumentNumber, string FullName,
    string? FirstName, string? MiddleName, string? FirstSurname, string? SecondSurname, string? PreferredName,
    DateOnly? BirthDate, string? PersonalEmail, string? PrimaryPhone, string? AlternatePhone, string? Address,
    string? City, string? Observations, bool IsActive, bool NeedsNameReview);
public sealed record ThirdPartyImportRow(int SourceRow, string DocumentNumber, string FullName, string Role, string AreaCode, string? CorporateEmail, string Status);
public sealed record LanguageRequest(string Language, string OverallLevel, string? ReadingLevel, string? WritingLevel, string? SpeakingLevel, string? Certification);
public sealed record StudyRequest(string AcademicLevel, string Title, string? Institution, bool Graduated);
public sealed record TrainingRequest(string Type, string Name, string? Institution, DateOnly? CompletionDate);
public sealed record ExperienceRequest(string Organization, string Role, DateOnly? StartDate, DateOnly? EndDate, string? Description);
public sealed record EmergencyRequest(string FullName, string Relationship, string Phone, string? AlternatePhone, bool IsPrimary);
