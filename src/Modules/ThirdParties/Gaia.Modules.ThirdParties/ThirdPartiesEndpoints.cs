using System.Security.Claims;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Gaia.Modules.ThirdParties;

internal static class ThirdPartiesEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var intranet = endpoints.MapGroup("/api/intranet")
            .WithTags("Intranet")
            .RequireAuthorization();
        intranet.MapGet("/people", ListIntranetPeopleAsync)
            .RequireAuthorization(AdminCorePermissions.IntranetPersonasVer);
        intranet.MapGet("/people/organization-units", ListIntranetOrganizationUnitsAsync)
            .RequireAuthorization(AdminCorePermissions.IntranetPersonasVer);
        intranet.MapGet("/birthdays", ListIntranetBirthdaysAsync)
            .RequireAuthorization("IntranetBirthdays");

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
        group.MapGet("/organizational-assignments", ListOrganizationalAssignmentsAsync).RequireAuthorization(AssignmentAuthorizationPolicies.Read);
        group.MapPost("/organizational-assignments", CreateOrganizationalAssignmentAsync).RequireAuthorization(AssignmentAuthorizationPolicies.Create);
        group.MapPut("/organizational-assignments/{assignmentId:guid}", UpdateOrganizationalAssignmentAsync).RequireAuthorization(AssignmentAuthorizationPolicies.Update);
        group.MapPost("/organizational-assignments/import/validate", ValidateAssignmentImportAsync).DisableAntiforgery().RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapPost("/organizational-assignments/import/execute", ExecuteAssignmentImportAsync).DisableAntiforgery().RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapPost("/administrative-import/validate", ValidateAdministrativeImportAsync)
            .DisableAntiforgery().RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapPost("/administrative-import/execute", ExecuteAdministrativeImportAsync)
            .DisableAntiforgery().RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapPost("/import", LegacyFeatureUnavailable).RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapGet("/import-issues", LegacyFeatureUnavailable).RequireAuthorization(AdminCorePermissions.ThColaboradoresImportar);
        group.MapPost("/{id:guid}/languages", LegacyFeatureUnavailable).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
        group.MapPost("/{id:guid}/studies", LegacyFeatureUnavailable).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
        group.MapPost("/{id:guid}/trainings", LegacyFeatureUnavailable).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
        group.MapPost("/{id:guid}/experiences", LegacyFeatureUnavailable).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
        group.MapPost("/{id:guid}/emergency-contacts", LegacyFeatureUnavailable).RequireAuthorization(AdminCorePermissions.ThInfoActualizar);
    }

    private static IResult LegacyFeatureUnavailable() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Funcionalidad en transición",
        detail: "Esta operación todavía no cuenta con una implementación Dataverse.");

    private static async Task<IResult> ListIntranetPeopleAsync(
        string? search,
        Guid? organizationUnitId,
        bool? includeDescendants,
        int page,
        int pageSize,
        IIntranetDirectoryReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListPeopleAsync(search, organizationUnitId, includeDescendants ?? false,
            page == 0 ? 1 : page, pageSize == 0 ? 24 : pageSize, cancellationToken));

    private static async Task<IResult> ListIntranetOrganizationUnitsAsync(
        IIntranetDirectoryReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListOrganizationUnitsAsync(cancellationToken));

    private static async Task<IResult> ListIntranetBirthdaysAsync(
        int month,
        IIntranetDirectoryReader reader,
        CancellationToken cancellationToken)
    {
        var requestedMonth = month == 0 ? DateTime.Today.Month : month;
        return requestedMonth is < 1 or > 12
            ? Results.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["El mes debe estar entre 1 y 12."] })
            : Results.Ok(await reader.ListBirthdaysAsync(requestedMonth, cancellationToken));
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
        if (request.ContactType is not (1 or 2)) return Validation("El tipo de correo debe ser PERSONAL o CORPORATIVO.");
        return RelatedResult(await store.CreateAsync(id, new(request.Email.Trim().ToLowerInvariant(), Clean(request.Observations), request.IsPrimary, request.IsActive, Actor(principal), request.ContactType), ct), $"/api/third-parties/{id}/emails", "correo");
    }
    private static async Task<IResult> UpdateEmailAsync(Guid id, Guid emailId, CollaboratorEmailRequest request, ClaimsPrincipal principal, ICollaboratorEmailStore store, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !IsEmail(request.Email)) return Validation("El correo electrónico es obligatorio y debe tener un formato válido.");
        if (request.ContactType is not (1 or 2)) return Validation("El tipo de correo debe ser PERSONAL o CORPORATIVO.");
        return RelatedResult(await store.UpdateAsync(id, emailId, new(request.Email.Trim().ToLowerInvariant(), Clean(request.Observations), request.IsPrimary, request.IsActive, Actor(principal), request.ContactType), ct), $"/api/third-parties/{id}/emails", "correo");
    }
    private static async Task<IResult> ListPhonesAsync(Guid id, ICollaboratorPhoneStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(id, ct));
    private static async Task<IResult> ListOrganizationalAssignmentsAsync(IOrganizationalAssignmentStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(ct));
    private static async Task<IResult> CreateOrganizationalAssignmentAsync(OrganizationalAssignmentRequest request, ClaimsPrincipal principal, IOrganizationalAssignmentStore store, CancellationToken ct) =>
        OrganizationalAssignmentResult(await store.CreateAsync(AssignmentCommand(request, principal), ct), true);
    private static async Task<IResult> UpdateOrganizationalAssignmentAsync(Guid assignmentId, OrganizationalAssignmentRequest request, ClaimsPrincipal principal, IOrganizationalAssignmentStore store, CancellationToken ct) =>
        OrganizationalAssignmentResult(await store.UpdateAsync(assignmentId, AssignmentCommand(request, principal), ct), false);
    private static async Task<IResult> ValidateAssignmentImportAsync(IFormFile file,IOrganizationalAssignmentImporter importer,CancellationToken ct){var invalid=ValidateWorkbookFile(file);if(invalid is not null)return invalid;await using var stream=file.OpenReadStream();return Results.Ok(await importer.ValidateAsync(stream,ct));}
    private static async Task<IResult> ExecuteAssignmentImportAsync(IFormFile file,bool confirm,IOrganizationalAssignmentImporter importer,CancellationToken ct){if(!confirm)return Results.Problem(statusCode:400,detail:"La ejecución requiere confirm=true.");var invalid=ValidateWorkbookFile(file);if(invalid is not null)return invalid;await using var stream=file.OpenReadStream();var result=await importer.ImportAsync(stream,ct);return result.Validation.Valid?Results.Ok(result):Results.UnprocessableEntity(result);}
    private static async Task<IResult> CreatePhoneAsync(Guid id, CollaboratorPhoneRequest request, ClaimsPrincipal principal, ICollaboratorPhoneStore store, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Number)) return Validation("El número de teléfono es obligatorio.");
        if (request.ContactType is not (1 or 2)) return Validation("El tipo de teléfono debe ser PERSONAL o CORPORATIVO.");
        return RelatedResult(await store.CreateAsync(id, new(request.Number.Trim(), Clean(request.Extension), Clean(request.Observations), request.IsPrimary, request.PhoneType.Trim().ToUpperInvariant(), request.IsActive, Actor(principal), request.ContactType), ct), $"/api/third-parties/{id}/phones", "teléfono");
    }
    private static async Task<IResult> UpdatePhoneAsync(Guid id, Guid phoneId, CollaboratorPhoneRequest request, ClaimsPrincipal principal, ICollaboratorPhoneStore store, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Number)) return Validation("El número de teléfono es obligatorio.");
        if (request.ContactType is not (1 or 2)) return Validation("El tipo de teléfono debe ser PERSONAL o CORPORATIVO.");
        return RelatedResult(await store.UpdateAsync(id, phoneId, new(request.Number.Trim(), Clean(request.Extension), Clean(request.Observations), request.IsPrimary, request.PhoneType.Trim().ToUpperInvariant(), request.IsActive, Actor(principal), request.ContactType), ct), $"/api/third-parties/{id}/phones", "teléfono");
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

    private static OrganizationalAssignmentCommand AssignmentCommand(OrganizationalAssignmentRequest request, ClaimsPrincipal principal) =>
        new(request.ThirdPartyId, request.PositionId, request.OrganizationalUnitId, request.StartDate,
            request.EndDate, request.IsPrimary, Clean(request.Observations), request.IsActive, Actor(principal));
    private static IResult OrganizationalAssignmentResult(OrganizationalAssignmentWriteResult result, bool created) => result.Status switch
    {
        OrganizationalAssignmentWriteStatus.NotFound => Results.NotFound(),
        OrganizationalAssignmentWriteStatus.InvalidThirdParty => Validation("El colaborador no existe o está inactivo."),
        OrganizationalAssignmentWriteStatus.InvalidPosition => Validation("El cargo no existe o está inactivo."),
        OrganizationalAssignmentWriteStatus.InvalidUnit => Validation("La unidad organizacional no existe o está inactiva."),
        OrganizationalAssignmentWriteStatus.Duplicate => Problem(409, "El colaborador ya tiene una asignación organizacional activa."),
        OrganizationalAssignmentWriteStatus.Created when created && result.Id.HasValue =>
            Results.Created($"/api/third-parties/organizational-assignments/{result.Id.Value}", new { result.Id }),
        OrganizationalAssignmentWriteStatus.Updated when !created && result.Id.HasValue => Results.Ok(new { result.Id }),
        _ => throw new InvalidOperationException("Dataverse no confirmó la asignación organizacional.")
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
public sealed record CollaboratorEmailRequest(string Email, string? Observations, bool IsPrimary, bool IsActive, int ContactType = 1);
public sealed record CollaboratorPhoneRequest(string Number, string? Extension, string? Observations, bool IsPrimary, string PhoneType, bool IsActive, int ContactType = 1);
public sealed record OrganizationalAssignmentRequest(Guid ThirdPartyId, Guid PositionId, Guid OrganizationalUnitId,
    DateOnly? StartDate, DateOnly? EndDate, bool IsPrimary, string? Observations, bool IsActive);
