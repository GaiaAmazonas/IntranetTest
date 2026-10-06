using System.Security.Claims;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Gaia.Modules.Solicitudes;

public static class SolicitudesEndpoints
{
    public static IEndpointRouteBuilder MapSolicitudesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/solicitudes").WithTags("Solicitudes")
            .RequireAuthorization();
        group.MapGet("/portal/catalog", PortalCatalog)
            .RequireAuthorization(AdminCorePermissions.IntranetSolicitudesVer);
        group.MapGet("/portal", Portal)
            .RequireAuthorization(AdminCorePermissions.IntranetSolicitudesVer);
        group.MapGet("/services/{serviceId:guid}/form", ServiceForm)
            .RequireAuthorization(AdminCorePermissions.IntranetSolicitudesVer);
        group.MapPost("/requests", CreateRequest)
            .RequireAuthorization(AdminCorePermissions.IntranetSolicitudesVer);
        group.MapGet("/requests/{requestId:guid}", RequestDetail);
        group.MapGet("/requests/{requestId:guid}/workflow",RequestWorkflow);
        group.MapPost("/requests/{requestId:guid}/comments", AddComment);
        group.MapPost("/requests/{requestId:guid}/transitions", ApplyTransition);
        group.MapPost("/requests/{requestId:guid}/observation-response", AttendObservation).DisableAntiforgery();
        group.MapPost("/requests/{requestId:guid}/attachments", Upload).DisableAntiforgery();
        group.MapGet("/requests/{requestId:guid}/attachments", List);
        group.MapGet("/attachments/{attachmentId:guid}/content", Download);
        group.MapDelete("/attachments/{attachmentId:guid}", Deactivate);
        group.MapGet("/requests/{requestId:guid}/attachments/reconciliation", Reconcile);
        group.MapGet("/management/queue", ManagementQueue).RequireAuthorization(AdminCorePermissions.SolicitudesVer);
        group.MapGet("/management/workflow-queue", WorkflowQueue).RequireAuthorization(AdminCorePermissions.SolicitudesVer);
        group.MapGet("/management/catalog", ManagementCatalog).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosVer);
        group.MapPut("/management/requests/{requestId:guid}/assignment", Reassign)
            .RequireAuthorization(AdminCorePermissions.SolicitudesReasignar);
        group.MapDelete("/management/requests/{requestId:guid}", DeleteResolvedRequest)
            .RequireAuthorization(AdminCorePermissions.SolicitudesReasignar);
        group.MapPost("/management/workflows/{managementId:guid}/complete",CompleteWorkflowManagement)
            .RequireAuthorization(AdminCorePermissions.SolicitudesReasignar);
        group.MapGet("/management/workflows/{managementId:guid}/form",ReadManagementStageForm)
            .RequireAuthorization(AdminCorePermissions.SolicitudesVer);
        group.MapPut("/management/workflows/{managementId:guid}/form/responses",SaveManagementStageResponses)
            .RequireAuthorization(AdminCorePermissions.SolicitudesReasignar);
        group.MapPost("/management/workflows/{managementId:guid}/take",TakeWorkflowManagement)
            .RequireAuthorization(AdminCorePermissions.SolicitudesVer);
        group.MapPost("/management/workflows/{managementId:guid}/resume",ResumeWorkflowFromManagement)
            .RequireAuthorization(AdminCorePermissions.SolicitudesReasignar);
        group.MapPut("/management/workflows/{managementId:guid}/assignment",ReassignWorkflowManagement)
            .RequireAuthorization(AdminCorePermissions.SolicitudesReasignar);
        group.MapPost("/requests/{requestId:guid}/workflow/reopen",ReopenWorkflow)
            .RequireAuthorization(AdminCorePermissions.SolicitudesReasignar);
        group.MapPost("/workflows/{managementId:guid}/requester-response",ResumeWorkflowFromRequester)
            .RequireAuthorization(AdminCorePermissions.IntranetSolicitudesVer);
        group.MapGet("/administration", Administration).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosVer);
        group.MapGet("/administration/export", AdministrationExport).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosVer);
        group.MapPost("/administration/services", CreateService).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPut("/administration/services/{id:guid}", UpdateService).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPost("/administration/forms", CreateForm).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPost("/administration/forms/{id:guid}/publish", PublishForm).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPost("/administration/workflows/{flowId:guid}/publish",PublishWorkflow).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapGet("/administration/services/{serviceId:guid}/workflows",ListWorkflows).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosVer);
        group.MapPost("/administration/workflows",CreateWorkflow).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapGet("/administration/workflows/{flowId:guid}",ReadWorkflow).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosVer);
        group.MapPut("/administration/workflows/{flowId:guid}",UpdateWorkflow).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapDelete("/administration/workflows/{flowId:guid}",DeleteWorkflow).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPost("/administration/workflows/{flowId:guid}/steps",CreateWorkflowStep).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPut("/administration/workflows/{flowId:guid}/steps/{stepId:guid}",UpdateWorkflowStep).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPost("/administration/workflows/{flowId:guid}/steps/{stepId:guid}/duplicate",DuplicateWorkflowStep).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapDelete("/administration/workflows/{flowId:guid}/steps/{stepId:guid}",DeleteWorkflowStep).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapGet("/administration/workflow-steps/{stepId:guid}/form",ReadStageForm).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosVer);
        group.MapPut("/administration/workflow-steps/{stepId:guid}/form",SaveStageForm).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPost("/administration/workflow-steps/{stepId:guid}/form/fields",CreateStageFormField).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPut("/administration/workflow-steps/{stepId:guid}/form/fields/{fieldId:guid}",UpdateStageFormField).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapDelete("/administration/workflow-steps/{stepId:guid}/form/fields/{fieldId:guid}",DeleteStageFormField).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPost("/administration/workflows/{flowId:guid}/routes",CreateWorkflowRoute).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPut("/administration/workflows/{flowId:guid}/routes/{routeId:guid}",UpdateWorkflowRoute).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapGet("/administration/forms/{id:guid}", ReadAdminForm).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosVer);
        group.MapPost("/administration/forms/{formId:guid}/fields", CreateField).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapPut("/administration/forms/{formId:guid}/fields/{fieldId:guid}", UpdateField).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapDelete("/administration/forms/{formId:guid}/fields/{fieldId:guid}", DeleteField).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        return endpoints;
    }

    private static async Task<IResult> PortalCatalog(ISolicitudesPortalReader reader, CancellationToken token)
    {
        try { return Results.Ok(await reader.ReadCatalogAsync(token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> Portal(ClaimsPrincipal principal, ISecurityStore security,
        ISolicitudesPortalReader reader, CancellationToken token)
    {
        try { return Results.Ok(await reader.ReadAsync(await Actor(security, principal, token), token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> ServiceForm(Guid serviceId,ISolicitudesFormReader reader,CancellationToken token)
    {
        try { var form=await reader.ReadForServiceAsync(serviceId,token);return form is null?Results.NoContent():Results.Ok(form); }
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> CreateRequest(CreateSolicitudesRequest request, ClaimsPrincipal principal,
        ISecurityStore security, ISolicitudesRequestApplication application, CancellationToken token)
    {
        try
        {
            var created = await application.CreateAsync(request, await Actor(security, principal, token), token);
            return Results.Created($"/api/solicitudes/requests/{created.Id:D}", created);
        }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> RequestDetail(Guid requestId, ClaimsPrincipal principal, ISecurityStore security,
        IAdminCoreAuthorization authorization, ISolicitudesConversationApplication application, CancellationToken token)
    {
        try { var managementAccess=await authorization.HasPermissionAsync(principal,AdminCorePermissions.SolicitudesVer,token);return Results.Ok(await application.ReadAsync(requestId,await Actor(security,principal,token),managementAccess,token)); }
        catch (Exception error) { return Problem(error); }
    }
    private static async Task<IResult> RequestWorkflow(Guid requestId,ClaimsPrincipal principal,ISecurityStore security,
        IAdminCoreAuthorization authorization,SolicitudesWorkflowApplication application,CancellationToken token)
    {
        try{var management=await authorization.HasPermissionAsync(principal,AdminCorePermissions.SolicitudesVer,token);var value=await application.ReadRequestStateAsync(requestId,await Actor(security,principal,token),management,token);return value is null?Results.NoContent():Results.Ok(value);}
        catch(Exception error){return Problem(error);}
    }
    private static async Task<IResult> DeleteResolvedRequest(Guid requestId,ClaimsPrincipal principal,
        ISecurityStore security,ISolicitudesManagementApplication application,CancellationToken token)
    {
        try
        {
            await application.DeleteResolvedAsync(requestId,await Actor(security,principal,token),token);
            return Results.NoContent();
        }
        catch(Exception error){return Problem(error);}
    }
    private static async Task<IResult> WorkflowQueue(string? queue,ClaimsPrincipal principal,ISecurityStore security,
        SolicitudesWorkflowApplication application,CancellationToken token)
    {
        try{return Results.Ok(await application.ReadWorkQueueAsync(await Actor(security,principal,token),queue??"mine",token));}
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> AddComment(Guid requestId, AddSolicitudesComment request, ClaimsPrincipal principal,
        ISecurityStore security, IAdminCoreAuthorization authorization, ISolicitudesConversationApplication application, CancellationToken token)
    {
        try { var managementAccess=await authorization.HasPermissionAsync(principal,AdminCorePermissions.SolicitudesVer,token);return Results.Created($"/api/solicitudes/requests/{requestId:D}",await application.AddAsync(requestId,await Actor(security,principal,token),request,managementAccess,token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> ApplyTransition(Guid requestId, ApplySolicitudesTransition request,
        ClaimsPrincipal principal, ISecurityStore security, IAdminCoreAuthorization authorization, ISolicitudesConversationApplication application, CancellationToken token)
    {
        try { var managementAccess=await authorization.HasPermissionAsync(principal,AdminCorePermissions.SolicitudesVer,token);return Results.Ok(await application.TransitionAsync(requestId,await Actor(security,principal,token),request,managementAccess,token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> AttendObservation(Guid requestId, HttpRequest httpRequest,
        ClaimsPrincipal principal, ISecurityStore security, ISolicitudesObservationApplication application,
        SolicitudesWorkflowApplication workflow,
        CancellationToken token)
    {
        try
        {
            if (!httpRequest.HasFormContentType) return Invalid("La respuesta debe usar multipart/form-data.");
            var form = await httpRequest.ReadFormAsync(token);
            if (form.Files.Count > 1)
                return Invalid("Puedes adjuntar máximo un documento.");
            var file = form.Files.GetFile("file");
            if (!Guid.TryParse(form["transitionId"], out var transitionId) || transitionId == Guid.Empty)
                return Invalid("La transición de la respuesta no es válida.");
            await using var stream = file?.OpenReadStream() ?? Stream.Null;
            var actor = await Actor(security, principal, token);
            var result = await application.AttendAsync(new(
                requestId,
                transitionId,
                actor,
                form["comment"].ToString(),
                file?.FileName,
                file?.ContentType,
                file?.Length ?? 0), stream, token);
            var state = await workflow.ReadRequestStateAsync(requestId, actor, false, token);
            var waiting = state?.Managements.FirstOrDefault(item => item.Status == SolicitudesWorkflowValues.ManagementWaiting);
            if (waiting is not null)
                await workflow.ResumeFromRequesterAsync(waiting.Id, actor, form["comment"].ToString(), file is not null, token);
            return Results.Ok(result);
        }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> Upload(Guid requestId, HttpRequest httpRequest, ClaimsPrincipal principal,
        ISecurityStore security, ISolicitudesAttachmentApplication application, CancellationToken token)
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
            if (!OptionalGuid(form["commentId"], out var commentId) || !OptionalGuid(form["fieldResponseId"], out var fieldResponseId) || !OptionalGuid(form["managementId"],out var managementId)||!OptionalGuid(form["managementFieldResponseId"],out var managementFieldResponseId))
                return Invalid("La relación opcional del adjunto no es válida.");
            await using var stream = file.OpenReadStream();
            var result = await application.UploadAsync(new(requestId, commentId, fieldResponseId, actor,
                visibility, file.FileName, file.ContentType, file.Length,managementId,managementFieldResponseId), stream, token);
            if(managementId.HasValue&&result.ManagementId!=managementId)throw new InvalidOperationException("No fue posible asociar el adjunto a la gestión.");
            return Results.Created($"/api/solicitudes/attachments/{result.Id:D}", result);
        }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> List(Guid requestId, ClaimsPrincipal principal, ISecurityStore security,
        ISolicitudesAttachmentApplication application, CancellationToken token)
    {
        try { return Results.Ok(await application.ListAsync(requestId, await Actor(security, principal, token), token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> Download(Guid attachmentId, HttpContext context, ClaimsPrincipal principal,
        ISecurityStore security, ISolicitudesAttachmentApplication application, CancellationToken token)
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
        ISolicitudesAttachmentApplication application, CancellationToken token)
    {
        try
        {
            await application.DeactivateAsync(attachmentId, await Actor(security, principal, token), token);
            return Results.NoContent();
        }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> Reconcile(Guid requestId, ClaimsPrincipal principal, ISecurityStore security,
        ISolicitudesAttachmentApplication application, CancellationToken token)
    {
        try { return Results.Ok(await application.ReconcileAsync(requestId, await Actor(security, principal, token), token)); }
        catch (Exception error) { return Problem(error); }
    }

    private static async Task<IResult> ManagementQueue(string? search,Guid? serviceId,Guid? stateId,
        Guid? responsibleId,bool? overdue,int? page,int? pageSize,string? sort,string? continuationToken,ClaimsPrincipal principal,ISecurityStore security,ISolicitudesManagementApplication application,CancellationToken token)
    {
        try{return Results.Ok(await application.ReadQueueAsync(await Actor(security,principal,token),new(search,serviceId,stateId,responsibleId,overdue,page??1,pageSize??25,sort??"submitted-desc",continuationToken),token));}
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> ManagementCatalog(ISolicitudesManagementApplication application,CancellationToken token)
    {
        try{return Results.Ok(await application.ReadCatalogAsync(token));}catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> Reassign(Guid requestId,ReassignSolicitudesRequest request,ClaimsPrincipal principal,
        ISecurityStore security,ISolicitudesManagementApplication application,CancellationToken token)
    {
        try{await application.ReassignAsync(requestId,await Actor(security,principal,token),request,token);return Results.NoContent();}
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> CompleteWorkflowManagement(Guid managementId,CompleteSolicitudesManagement request,
        ClaimsPrincipal principal,ISecurityStore security,SolicitudesWorkflowApplication application,CancellationToken token)
    {
        try{await application.CompleteAsync(await Actor(security,principal,token),request with{ManagementId=managementId},token);return Results.NoContent();}
        catch(Exception error){return Problem(error);}
    }
    private static async Task<IResult> ReadManagementStageForm(Guid managementId,ClaimsPrincipal principal,ISecurityStore security,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{return Results.Ok(await application.ReadManagementFormAsync(managementId,await Actor(security,principal,token),token));}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> SaveManagementStageResponses(Guid managementId,SaveSolicitudesManagementAnswers request,ClaimsPrincipal principal,ISecurityStore security,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{return Results.Ok(await application.SaveManagementAnswersAsync(managementId,await Actor(security,principal,token),request,token));}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> TakeWorkflowManagement(Guid managementId,ClaimsPrincipal principal,
        ISecurityStore security,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{await application.TakeAsync(managementId,await Actor(security,principal,token),token);return Results.NoContent();}catch(Exception error){return Problem(error);}}

    private static async Task<IResult> ReassignWorkflowManagement(Guid managementId,ReassignSolicitudesManagement request,
        ClaimsPrincipal principal,ISecurityStore security,SolicitudesWorkflowApplication application,CancellationToken token)
    {
        try{await application.ReassignAsync(managementId,await Actor(security,principal,token),request,token);return Results.NoContent();}
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> ResumeWorkflowFromRequester(Guid managementId,ResumeSolicitudesWorkflowManagement request,
        ClaimsPrincipal principal,ISecurityStore security,SolicitudesWorkflowApplication application,CancellationToken token)
    {
        try{await application.ResumeFromRequesterAsync(managementId,await Actor(security,principal,token),request.Comment??string.Empty,request.HasFile,token);return Results.NoContent();}
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> ResumeWorkflowFromManagement(Guid managementId,ClaimsPrincipal principal,
        ISecurityStore security,SolicitudesWorkflowApplication application,CancellationToken token)
    {
        try{await application.ResumeFromManagementAsync(managementId,await Actor(security,principal,token),token);return Results.NoContent();}
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> ReopenWorkflow(Guid requestId,ClaimsPrincipal principal,ISecurityStore security,
        SolicitudesWorkflowApplication application,CancellationToken token)
    {
        try{await application.ReopenAsync(requestId,await Actor(security,principal,token),token);return Results.NoContent();}
        catch(Exception error){return Problem(error);}
    }

    private static async Task<IResult> PublishWorkflow(Guid flowId,ClaimsPrincipal principal,ISecurityStore security,
        SolicitudesWorkflowApplication application,CancellationToken token)
    {
        try{await application.PublishAsync(flowId,await Actor(security,principal,token),token);return Results.NoContent();}
        catch(Exception error){return Problem(error);}
    }
    private static async Task<IResult> ListWorkflows(Guid serviceId,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{return Results.Ok(await application.ListAsync(serviceId,token));}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> ReadWorkflow(Guid flowId,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{var value=await application.ReadAsync(flowId,token);return value is null?Results.NotFound():Results.Ok(value);}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateWorkflow(CreateSolicitudesWorkflowDraft request,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{var id=await application.CreateDraftAsync(request,token);return Results.Created($"/api/solicitudes/administration/workflows/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> UpdateWorkflow(Guid flowId,UpdateSolicitudesWorkflowDraft request,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{await application.UpdateDraftAsync(flowId,request,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> DeleteWorkflow(Guid flowId,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{await application.DeleteDraftAsync(flowId,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateWorkflowStep(Guid flowId,SaveSolicitudesWorkflowStep request,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{var id=await application.SaveStepAsync(flowId,null,request,token);return Results.Created($"/api/solicitudes/administration/workflows/{flowId:D}/steps/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> UpdateWorkflowStep(Guid flowId,Guid stepId,SaveSolicitudesWorkflowStep request,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{await application.SaveStepAsync(flowId,stepId,request,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> DuplicateWorkflowStep(Guid flowId,Guid stepId,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{var id=await application.DuplicateStepAsync(flowId,stepId,token);return Results.Created($"/api/solicitudes/administration/workflows/{flowId:D}/steps/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> DeleteWorkflowStep(Guid flowId,Guid stepId,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{await application.DeleteStepAsync(flowId,stepId,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> ReadStageForm(Guid stepId,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{var value=await application.ReadStageFormAsync(stepId,token);return value is null?Results.NoContent():Results.Ok(value);}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> SaveStageForm(Guid stepId,SaveSolicitudesStageForm request,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{var id=await application.SaveStageFormAsync(stepId,request,token);return Results.Ok(new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateStageFormField(Guid stepId,SaveSolicitudesFormField request,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{var id=await application.SaveStageFormFieldAsync(stepId,null,request,token);return Results.Created($"/api/solicitudes/administration/workflow-steps/{stepId:D}/form/fields/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> UpdateStageFormField(Guid stepId,Guid fieldId,SaveSolicitudesFormField request,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{await application.SaveStageFormFieldAsync(stepId,fieldId,request,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> DeleteStageFormField(Guid stepId,Guid fieldId,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{await application.DeleteStageFormFieldAsync(stepId,fieldId,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateWorkflowRoute(Guid flowId,SaveSolicitudesWorkflowRoute request,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{var id=await application.SaveRouteAsync(flowId,null,request,token);return Results.Created($"/api/solicitudes/administration/workflows/{flowId:D}/routes/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> UpdateWorkflowRoute(Guid flowId,Guid routeId,SaveSolicitudesWorkflowRoute request,SolicitudesWorkflowApplication application,CancellationToken token)
    {try{await application.SaveRouteAsync(flowId,routeId,request,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}

    private static async Task<IResult> Administration(ISolicitudesManagementApplication application,CancellationToken token)
    {try{return Results.Ok(await application.ReadAdministrationAsync(token));}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> AdministrationExport(ISolicitudesManagementApplication application,CancellationToken token)
    {try{return Results.Ok(await application.ReadExportAsync(token));}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateService(SaveSolicitudesService request,ISolicitudesManagementApplication application,CancellationToken token)
    {try{var id=await application.SaveServiceAsync(null,request,token);return Results.Created($"/api/solicitudes/administration/services/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> UpdateService(Guid id,SaveSolicitudesService request,ISolicitudesManagementApplication application,CancellationToken token)
    {try{await application.SaveServiceAsync(id,request,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateForm(CreateSolicitudesFormDraft request,ISolicitudesManagementApplication application,CancellationToken token)
    {try{var id=await application.CreateFormDraftAsync(request,token);return Results.Created($"/api/solicitudes/administration/forms/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> PublishForm(Guid id,ClaimsPrincipal principal,ISecurityStore security,ISolicitudesManagementApplication application,CancellationToken token)
    {try{await application.PublishFormAsync(id,await Actor(security,principal,token),token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> ReadAdminForm(Guid id,ISolicitudesManagementApplication application,CancellationToken token)
    {try{return Results.Ok(await application.ReadFormAsync(id,token));}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> CreateField(Guid formId,SaveSolicitudesFormField request,ISolicitudesManagementApplication application,CancellationToken token)
    {try{var id=await application.SaveFormFieldAsync(formId,null,request,token);return Results.Created($"/api/solicitudes/administration/forms/{formId:D}/fields/{id:D}",new{id});}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> UpdateField(Guid formId,Guid fieldId,SaveSolicitudesFormField request,ISolicitudesManagementApplication application,CancellationToken token)
    {try{await application.SaveFormFieldAsync(formId,fieldId,request,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}
    private static async Task<IResult> DeleteField(Guid formId,Guid fieldId,ISolicitudesManagementApplication application,CancellationToken token)
    {try{await application.DeleteFormFieldAsync(formId,fieldId,token);return Results.NoContent();}catch(Exception error){return Problem(error);}}

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
    private static IResult Problem(Exception error)
    {
        // El middleware de la API transforma este caso en 401/reauth_required para que
        // la interfaz reinicie el flujo interactivo. No debe convertirse en un 422 con
        // el texto interno IDW10502.
        if (RequiresInteractiveAuthentication(error))
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();

        return error switch
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
        _ => Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "No fue posible completar la operación", detail: error.Message)
        };
    }

    private static bool RequiresInteractiveAuthentication(Exception error)
    {
        for (Exception? current=error;current is not null;current=current.InnerException)
            if (current.GetType().FullName is "Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException"
                or "Microsoft.Identity.Client.MsalUiRequiredException") return true;
        return false;
    }
}
