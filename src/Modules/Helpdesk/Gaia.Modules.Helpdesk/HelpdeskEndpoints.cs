using System.Security.Claims;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Gaia.Modules.Helpdesk;

public static class HelpdeskEndpoints
{
    public static IEndpointRouteBuilder MapHelpdeskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/helpdesk").WithTags("Helpdesk")
            .RequireAuthorization(AdminCorePermissions.IntranetHelpdeskVer);
        group.MapGet("/portal", Portal);
        group.MapGet("/services/{serviceId:guid}/form", ServiceForm);
        group.MapPost("/requests", CreateRequest);
        group.MapGet("/requests/{requestId:guid}", RequestDetail);
        group.MapPost("/requests/{requestId:guid}/comments", AddComment);
        group.MapPost("/requests/{requestId:guid}/transitions", ApplyTransition);
        group.MapPost("/requests/{requestId:guid}/observation-response", AttendObservation).DisableAntiforgery();
        group.MapPost("/requests/{requestId:guid}/attachments", Upload).DisableAntiforgery();
        group.MapGet("/requests/{requestId:guid}/attachments", List);
        group.MapGet("/attachments/{attachmentId:guid}/content", Download);
        group.MapDelete("/attachments/{attachmentId:guid}", Deactivate);
        group.MapGet("/requests/{requestId:guid}/attachments/reconciliation", Reconcile);
        group.MapGet("/management/queue", ManagementQueue).RequireAuthorization(AdminCorePermissions.HelpdeskSolicitudesVer);
        group.MapGet("/management/catalog", ManagementCatalog).RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosVer);
        group.MapPut("/management/requests/{requestId:guid}/assignment", Reassign)
            .RequireAuthorization(AdminCorePermissions.HelpdeskSolicitudesReasignar);
        group.MapGet("/administration", Administration).RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosVer);
        group.MapPost("/administration/services", CreateService).RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosAdministrar);
        group.MapPut("/administration/services/{id:guid}", UpdateService).RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosAdministrar);
        group.MapPost("/administration/forms", CreateForm).RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosAdministrar);
        group.MapPost("/administration/forms/{id:guid}/publish", PublishForm).RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosAdministrar);
        group.MapGet("/administration/forms/{id:guid}", ReadAdminForm).RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosVer);
        group.MapPost("/administration/forms/{formId:guid}/fields", CreateField).RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosAdministrar);
        group.MapPut("/administration/forms/{formId:guid}/fields/{fieldId:guid}", UpdateField).RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosAdministrar);
        return endpoints;
    }

    private static async Task<IResult> Portal(ClaimsPrincipal principal, ISecurityStore security,
        IHelpdeskPortalReader reader, CancellationToken token)
    {
        try { return Results.Ok(await reader.ReadAsync(await Actor(security, principal, token), token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> ServiceForm(Guid serviceId,IHelpdeskFormReader reader,CancellationToken token)
    {
        try { var form=await reader.ReadForServiceAsync(serviceId,token);return form is null?Results.NoContent():Results.Ok(form); }
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> CreateRequest(CreateHelpdeskRequest request, ClaimsPrincipal principal,
        ISecurityStore security, IHelpdeskRequestApplication application, CancellationToken token)
    {
        try
        {
            var created = await application.CreateAsync(request, await Actor(security, principal, token), token);
            return Results.Created($"/api/helpdesk/requests/{created.Id:D}", created);
        }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> RequestDetail(Guid requestId, ClaimsPrincipal principal, ISecurityStore security,
        IAdminCoreAuthorization authorization, IHelpdeskConversationApplication application, CancellationToken token)
    {
        try { var managementAccess=await authorization.HasPermissionAsync(principal,AdminCorePermissions.HelpdeskSolicitudesVer,token);return Results.Ok(await application.ReadAsync(requestId,await Actor(security,principal,token),managementAccess,token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> AddComment(Guid requestId, AddHelpdeskComment request, ClaimsPrincipal principal,
        ISecurityStore security, IAdminCoreAuthorization authorization, IHelpdeskConversationApplication application, CancellationToken token)
    {
        try { var managementAccess=await authorization.HasPermissionAsync(principal,AdminCorePermissions.HelpdeskSolicitudesVer,token);return Results.Created($"/api/helpdesk/requests/{requestId:D}",await application.AddAsync(requestId,await Actor(security,principal,token),request,managementAccess,token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> ApplyTransition(Guid requestId, ApplyHelpdeskTransition request,
        ClaimsPrincipal principal, ISecurityStore security, IAdminCoreAuthorization authorization, IHelpdeskConversationApplication application, CancellationToken token)
    {
        try { var managementAccess=await authorization.HasPermissionAsync(principal,AdminCorePermissions.HelpdeskSolicitudesVer,token);return Results.Ok(await application.TransitionAsync(requestId,await Actor(security,principal,token),request,managementAccess,token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> AttendObservation(Guid requestId, HttpRequest httpRequest,
        ClaimsPrincipal principal, ISecurityStore security, IHelpdeskObservationApplication application,
        CancellationToken token)
    {
        try
        {
            if (!httpRequest.HasFormContentType) return Invalid("La respuesta debe usar multipart/form-data.");
            var form = await httpRequest.ReadFormAsync(token);
            if (form.Files.Count != 1 || form.Files.GetFile("file") is not { } file)
                return Invalid("Debes adjuntar exactamente un documento.");
            if (!Guid.TryParse(form["transitionId"], out var transitionId) || transitionId == Guid.Empty)
                return Invalid("La transición de la respuesta no es válida.");
            await using var stream = file.OpenReadStream();
            var result = await application.AttendAsync(new(
                requestId,
                transitionId,
                await Actor(security, principal, token),
                form["comment"].ToString(),
                file.FileName,
                file.ContentType,
                file.Length), stream, token);
            return Results.Ok(result);
        }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> Upload(Guid requestId, HttpRequest httpRequest, ClaimsPrincipal principal,
        ISecurityStore security, IHelpdeskAttachmentApplication application, CancellationToken token)
    {
        try
        {
            if (!httpRequest.HasFormContentType) return Invalid("La carga debe usar multipart/form-data.");
            var actor = await Actor(security, principal, token);
            var form = await httpRequest.ReadFormAsync(token);
            var file = form.Files.GetFile("file");
            if (file is null) return Invalid("Debes seleccionar un archivo.");
            var visibility = string.Equals(form["visibility"], "internal", StringComparison.OrdinalIgnoreCase)
                ? AttachmentVisibility.Internal : AttachmentVisibility.Requester;
            if (!OptionalGuid(form["commentId"], out var commentId) || !OptionalGuid(form["fieldResponseId"], out var fieldResponseId))
                return Invalid("La relación opcional del adjunto no es válida.");
            await using var stream = file.OpenReadStream();
            var result = await application.UploadAsync(new(requestId, commentId, fieldResponseId, actor,
                visibility, file.FileName, file.ContentType, file.Length), stream, token);
            return Results.Created($"/api/helpdesk/attachments/{result.Id:D}", result);
        }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> List(Guid requestId, ClaimsPrincipal principal, ISecurityStore security,
        IHelpdeskAttachmentApplication application, CancellationToken token)
    {
        try { return Results.Ok(await application.ListAsync(requestId, await Actor(security, principal, token), token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> Download(Guid attachmentId, HttpContext context, ClaimsPrincipal principal,
        ISecurityStore security, IHelpdeskAttachmentApplication application, CancellationToken token)
    {
        try
        {
            var download = await application.DownloadAsync(attachmentId, await Actor(security, principal, token), token);
            context.Response.OnCompleted(async () => await download.DisposeAsync());
            return Results.Stream(download.Content, download.Metadata.ContentType,
                download.Metadata.OriginalName ?? download.Metadata.StoredName, enableRangeProcessing: false);
        }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> Deactivate(Guid attachmentId, ClaimsPrincipal principal, ISecurityStore security,
        IHelpdeskAttachmentApplication application, CancellationToken token)
    {
        try
        {
            await application.DeactivateAsync(attachmentId, await Actor(security, principal, token), token);
            return Results.NoContent();
        }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> Reconcile(Guid requestId, ClaimsPrincipal principal, ISecurityStore security,
        IHelpdeskAttachmentApplication application, CancellationToken token)
    {
        try { return Results.Ok(await application.ReconcileAsync(requestId, await Actor(security, principal, token), token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> ManagementQueue(string? search,Guid? serviceId,Guid? stateId,
        Guid? responsibleId,bool? overdue,int? page,int? pageSize,string? sort,string? continuationToken,IHelpdeskManagementApplication application,CancellationToken token)
    {
        try{return Results.Ok(await application.ReadQueueAsync(new(search,serviceId,stateId,responsibleId,overdue,page??1,pageSize??25,sort??"submitted-desc",continuationToken),token));}
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> ManagementCatalog(IHelpdeskManagementApplication application,CancellationToken token)
    {
        try{return Results.Ok(await application.ReadCatalogAsync(token));}catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> Reassign(Guid requestId,ReassignHelpdeskRequest request,ClaimsPrincipal principal,
        ISecurityStore security,IHelpdeskManagementApplication application,CancellationToken token)
    {
        try{await application.ReassignAsync(requestId,await Actor(security,principal,token),request,token);return Results.NoContent();}
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> Administration(IHelpdeskManagementApplication application,CancellationToken token)
    {try{return Results.Ok(await application.ReadAdministrationAsync(token));}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateService(SaveHelpdeskService request,IHelpdeskManagementApplication application,CancellationToken token)
    {try{var id=await application.SaveServiceAsync(null,request,token);return Results.Created($"/api/helpdesk/administration/services/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> UpdateService(Guid id,SaveHelpdeskService request,IHelpdeskManagementApplication application,CancellationToken token)
    {try{await application.SaveServiceAsync(id,request,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateForm(CreateHelpdeskFormDraft request,IHelpdeskManagementApplication application,CancellationToken token)
    {try{var id=await application.CreateFormDraftAsync(request,token);return Results.Created($"/api/helpdesk/administration/forms/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> PublishForm(Guid id,ClaimsPrincipal principal,ISecurityStore security,IHelpdeskManagementApplication application,CancellationToken token)
    {try{await application.PublishFormAsync(id,await Actor(security,principal,token),token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> ReadAdminForm(Guid id,IHelpdeskManagementApplication application,CancellationToken token)
    {try{return Results.Ok(await application.ReadFormAsync(id,token));}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateField(Guid formId,SaveHelpdeskFormField request,IHelpdeskManagementApplication application,CancellationToken token)
    {try{var id=await application.SaveFormFieldAsync(formId,null,request,token);return Results.Created($"/api/helpdesk/administration/forms/{formId:D}/fields/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> UpdateField(Guid formId,Guid fieldId,SaveHelpdeskFormField request,IHelpdeskManagementApplication application,CancellationToken token)
    {try{await application.SaveFormFieldAsync(formId,fieldId,request,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}

    private static async Task<Guid> Actor(ISecurityStore security, ClaimsPrincipal principal, CancellationToken token) =>
        (await security.GetOrProvisionAsync(principal, token)).User.ThirdPartyId
        ?? throw new UnauthorizedAccessException("El usuario no está asociado con un tercero activo.");

    private static bool OptionalGuid(string? text, out Guid? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!Guid.TryParse(text, out var parsed) || parsed == Guid.Empty) return false;
        value = parsed; return true;
    }

    private static IResult Invalid(string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { ["attachment"] = [detail] });
    private static IResult Problem(Exception error) => error switch
    {
        FileStorageException storage => Results.Problem(
            statusCode: storage.Code is FileStorageError.InvalidCredentials or FileStorageError.FileUnauthorized
                ? StatusCodes.Status403Forbidden
                : storage.Code is FileStorageError.FileTooLarge or FileStorageError.TypeNotAllowed or FileStorageError.InvalidFileName or FileStorageError.InvalidLength
                    ? StatusCodes.Status422UnprocessableEntity
                    : StatusCodes.Status503ServiceUnavailable,
            title: "No fue posible almacenar el archivo", detail: storage.Message,
            extensions: new Dictionary<string, object?> { ["code"] = storage.Code.ToString() }),
        UnauthorizedAccessException => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Acceso denegado"),
        KeyNotFoundException => Results.NotFound(new { detail = error.Message }),
        ArgumentException => Invalid(error.Message),
        InvalidOperationException => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "No fue posible completar la operación", detail: error.Message),
        _ => Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Servicio temporalmente no disponible")
    };
}
