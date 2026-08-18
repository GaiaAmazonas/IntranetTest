using System.Security.Claims;
using Gaia.Modules.Organization.Infrastructure;
using Gaia.Modules.Security;
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
            .RequireAuthorization();
        group.MapGet("/", ListAsync).RequireAuthorization(AdminCorePermissions.ThColaboradoresVer);
        group.MapGet("/{id:guid}", GetAsync).RequireAuthorization(AdminCorePermissions.ThInfoVer);
        group.MapGet("/document-types", ListDocumentTypesAsync).RequireAuthorization(AdminCorePermissions.ThColaboradoresVer);
        group.MapPost("/", CreateAsync).RequireAuthorization(AdminCorePermissions.ThColaboradoresCrear);
        group.MapPut("/{id:guid}", UpdateAsync).RequireAuthorization(AdminCorePermissions.ThColaboradoresActualizar);
        group.MapGet("/{id:guid}/emails", ListEmailsAsync).RequireAuthorization(AdminCorePermissions.ThCorreosVer);
        group.MapPost("/{id:guid}/emails", CreateEmailAsync).RequireAuthorization(AdminCorePermissions.ThCorreosCrear);
        group.MapPut("/{id:guid}/emails/{emailId:guid}", UpdateEmailAsync).RequireAuthorization(AdminCorePermissions.ThCorreosActualizar);
        group.MapGet("/{id:guid}/phones", ListPhonesAsync).RequireAuthorization(AdminCorePermissions.ThTelefonosVer);
        group.MapPost("/{id:guid}/phones", CreatePhoneAsync).RequireAuthorization(AdminCorePermissions.ThTelefonosCrear);
        group.MapPut("/{id:guid}/phones/{phoneId:guid}", UpdatePhoneAsync).RequireAuthorization(AdminCorePermissions.ThTelefonosActualizar);
        group.MapPost("/administrative-import/validate", ValidateAdministrativeImportAsync)
            .DisableAntiforgery().RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapPost("/administrative-import/execute", ExecuteAdministrativeImportAsync)
            .DisableAntiforgery().RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapPost("/import", ImportAsync).RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapGet("/import-issues", ListImportIssuesAsync)
            .RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapPost("/{id:guid}/languages", AddLanguageAsync).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
        group.MapPost("/{id:guid}/studies", AddStudyAsync).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
        group.MapPost("/{id:guid}/trainings", AddTrainingAsync).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
        group.MapPost("/{id:guid}/experiences", AddExperienceAsync).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
        group.MapPost("/{id:guid}/emergency-contacts", AddEmergencyAsync).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
    }

    private static async Task<IResult> ValidateAdministrativeImportAsync(IFormFile file,
        IAdministrativePersonnelImporter importer, CancellationToken ct)
    {
        var fileValidation = ValidateWorkbookFile(file); if (fileValidation is not null) return fileValidation;
        await using var stream = file.OpenReadStream();
        return Results.Ok(await importer.ValidateAsync(stream, ct));
    }

    private static async Task<IResult> ExecuteAdministrativeImportAsync(IFormFile file, bool confirm,
        IAdministrativePersonnelImporter importer, CancellationToken ct)
    {
        if (!confirm) return Results.Problem(statusCode: 400, detail: "La ejecución requiere confirm=true después de revisar el dry-run.");
        var fileValidation = ValidateWorkbookFile(file); if (fileValidation is not null) return fileValidation;
        await using var stream = file.OpenReadStream();
        var result = await importer.ImportAsync(stream, ct);
        return result.Validation.Valid ? Results.Ok(result) : Results.UnprocessableEntity(result);
    }

    private static IResult? ValidateWorkbookFile(IFormFile file)
    {
        if (file.Length == 0) return Validation("Debe adjuntar el archivo Excel.");
        if (file.Length > 10 * 1024 * 1024) return Validation("El archivo supera el máximo permitido de 10 MB.");
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Validation("El archivo debe tener formato .xlsx.");
        return null;
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
        bool directory,
        IThirdPartyReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(directory ? await reader.ListDirectoryAsync(search, cancellationToken) : await reader.ListAsync(search, cancellationToken));

    private static async Task<IResult> ListDocumentTypesAsync(
        IDocumentTypeReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListAsync(cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid id,
        IThirdPartyReader reader,
        CancellationToken cancellationToken)
    {
        var party = await reader.GetAsync(id, cancellationToken);
        if (party is null) return Results.NotFound();
        return Results.Ok(new
        {
            party,
            engagements = Array.Empty<object>(),
            assignments = Array.Empty<object>(),
            studies = Array.Empty<object>(),
            languages = Array.Empty<object>(),
            trainings = Array.Empty<object>(),
            experiences = Array.Empty<object>(),
            emergencyContacts = Array.Empty<object>()
        });
    }

    private static async Task<IResult> CreateAsync(
        [FromBody] ThirdPartyRequest request,
        ClaimsPrincipal principal,
        IThirdPartyWriter writer,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        var result = await writer.CreateAsync(Command(request, principal), cancellationToken);
        return WriteResult(result, created: true);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] ThirdPartyRequest request,
        ClaimsPrincipal principal,
        IThirdPartyWriter writer,
        CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        var result = await writer.UpdateAsync(id, Command(request, principal), cancellationToken);
        return WriteResult(result, created: false);
    }

    private static async Task<IResult> ListEmailsAsync(Guid id, ICollaboratorEmailStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(id, ct));
    private static async Task<IResult> CreateEmailAsync(Guid id, CollaboratorEmailRequest request, ClaimsPrincipal principal, ICollaboratorEmailStore store, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !IsEmail(request.Email)) return Validation("El correo electrónico es obligatorio y debe tener un formato válido.");
        return RelatedResult(await store.CreateAsync(id, new(request.Email.Trim().ToLowerInvariant(), Clean(request.Observations), request.IsPrimary, request.IsActive, Actor(principal)), ct), $"/api/third-parties/{id}/emails", "correo");
    }
    private static async Task<IResult> UpdateEmailAsync(Guid id, Guid emailId, CollaboratorEmailRequest request, ClaimsPrincipal principal, ICollaboratorEmailStore store, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !IsEmail(request.Email)) return Validation("El correo electrónico es obligatorio y debe tener un formato válido.");
        return RelatedResult(await store.UpdateAsync(id, emailId, new(request.Email.Trim().ToLowerInvariant(), Clean(request.Observations), request.IsPrimary, request.IsActive, Actor(principal)), ct), $"/api/third-parties/{id}/emails", "correo");
    }
    private static async Task<IResult> ListPhonesAsync(Guid id, ICollaboratorPhoneStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(id, ct));
    private static async Task<IResult> CreatePhoneAsync(Guid id, CollaboratorPhoneRequest request, ClaimsPrincipal principal, ICollaboratorPhoneStore store, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Number)) return Validation("El número de teléfono es obligatorio.");
        return RelatedResult(await store.CreateAsync(id, new(request.Number.Trim(), Clean(request.Extension), Clean(request.Observations), request.IsPrimary, request.PhoneType.Trim().ToUpperInvariant(), request.IsActive, Actor(principal)), ct), $"/api/third-parties/{id}/phones", "teléfono");
    }
    private static async Task<IResult> UpdatePhoneAsync(Guid id, Guid phoneId, CollaboratorPhoneRequest request, ClaimsPrincipal principal, ICollaboratorPhoneStore store, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Number)) return Validation("El número de teléfono es obligatorio.");
        return RelatedResult(await store.UpdateAsync(id, phoneId, new(request.Number.Trim(), Clean(request.Extension), Clean(request.Observations), request.IsPrimary, request.PhoneType.Trim().ToUpperInvariant(), request.IsActive, Actor(principal)), ct), $"/api/third-parties/{id}/phones", "teléfono");
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

    private static IResult? Validate(ThirdPartyRequest request)
    {
        if (request.DocumentTypeId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.DocumentNumber)
            || string.IsNullOrWhiteSpace(request.FirstName)
            || string.IsNullOrWhiteSpace(request.FirstSurname)
            || string.IsNullOrWhiteSpace(request.Sex))
            return Validation("Tipo y número de documento, primer nombre, primer apellido y sexo son obligatorios.");
        if (request.Sex is not ("MASCULINO" or "FEMENINO")) return Validation("Sexo debe ser MASCULINO o FEMENINO.");
        return null;
    }

    private static ThirdPartyWriteCommand Command(ThirdPartyRequest request, ClaimsPrincipal principal) => new(
        request.DocumentTypeId,
        request.DocumentNumber.Trim(),
        request.FirstName.Trim(),
        Clean(request.MiddleName),
        request.FirstSurname.Trim(),
        Clean(request.SecondSurname),
        request.Sex,
        request.BirthDate,
        Clean(request.Observations),
        request.IsActive,
        Actor(principal));

    private static IResult WriteResult(ThirdPartyWriteResult result, bool created) => result.Status switch
    {
        ThirdPartyWriteStatus.NotFound => Results.NotFound(),
        ThirdPartyWriteStatus.InvalidDocumentType => Validation("El tipo de documento no existe o está inactivo."),
        ThirdPartyWriteStatus.DuplicateDocument => Problem(409, "Ya existe un tercero con ese tipo y número de documento."),
        ThirdPartyWriteStatus.InvalidSex => Validation("Sexo no corresponde a una opción válida en Dataverse."),
        ThirdPartyWriteStatus.Created when created && result.Id.HasValue =>
            Results.Created($"/api/third-parties/{result.Id.Value}", new { Id = result.Id.Value }),
        ThirdPartyWriteStatus.Updated when !created && result.Id.HasValue => Results.Ok(new { Id = result.Id.Value }),
        _ => throw new InvalidOperationException("Dataverse no confirmó la escritura del tercero.")
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult Validation(string detail) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["thirdParty"] = [detail] });
    private static ImportIssue Issue(string batch, int row, string severity, string code, string detail) =>
        new() { BatchId = batch, SourceRow = row, Severity = severity, Code = code, Detail = detail };
    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) || email.Equals("No tiene", StringComparison.OrdinalIgnoreCase) ? null : email.Trim();
    private static string Actor(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity?.Name ?? "unknown";
    private static IResult Problem(int status, string detail) => Results.Problem(statusCode: status, detail: detail);
    private static bool IsEmail(string value) => System.Net.Mail.MailAddress.TryCreate(value.Trim(), out var address) && address.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
    private static IResult RelatedResult(RelatedWriteResult result, string path, string entity) => result.Status switch
    {
        RelatedWriteStatus.NotFound => Results.NotFound(), RelatedWriteStatus.ParentNotFound => Results.NotFound(),
        RelatedWriteStatus.Duplicate => Problem(409, $"El {entity} ya está registrado para este colaborador."),
        RelatedWriteStatus.InvalidOption => Validation("La opción indicada no existe en Dataverse."),
        RelatedWriteStatus.Created when result.Id.HasValue => Results.Created($"{path}/{result.Id}", new { result.Id }),
        RelatedWriteStatus.Updated when result.Id.HasValue => Results.Ok(new { result.Id }),
        _ => throw new InvalidOperationException($"Dataverse no confirmó la escritura del {entity}.")
    };
}

public sealed record ThirdPartyRequest(
    Guid DocumentTypeId,
    string DocumentNumber,
    string FirstName,
    string? MiddleName,
    string FirstSurname,
    string? SecondSurname,
    string Sex,
    DateOnly? BirthDate,
    string? Observations,
    bool IsActive);
public sealed record CollaboratorEmailRequest(string Email, string? Observations, bool IsPrimary, bool IsActive);
public sealed record CollaboratorPhoneRequest(string Number, string? Extension, string? Observations, bool IsPrimary, string PhoneType, bool IsActive);
public sealed record ThirdPartyImportRow(int SourceRow, string DocumentNumber, string FullName, string Role, string AreaCode, string? CorporateEmail, string Status);
public sealed record LanguageRequest(string Language, string OverallLevel, string? ReadingLevel, string? WritingLevel, string? SpeakingLevel, string? Certification);
public sealed record StudyRequest(string AcademicLevel, string Title, string? Institution, bool Graduated);
public sealed record TrainingRequest(string Type, string Name, string? Institution, DateOnly? CompletionDate);
public sealed record ExperienceRequest(string Organization, string Role, DateOnly? StartDate, DateOnly? EndDate, string? Description);
public sealed record EmergencyRequest(string FullName, string Relationship, string Phone, string? AlternatePhone, bool IsPrimary);
