namespace Gaia.Modules.Helpdesk;

public static class HelpdeskWorkflowValues
{
    public const int Draft=299540130, Published=299540131, Retired=299540132;
    public const int UnitQueue=299540150, SpecificPerson=299540151, RequestOwner=299540152;
    public const int AnyIncoming=299540160, AllIncoming=299540161;
    public const int Completed=299540170, Approved=299540171, Rejected=299540172,
        Returned=299540173, RequiresApproval=299540174, NotApplicable=299540175;
    public const int InstanceRunning=299540180, InstanceWaiting=299540181,
        InstanceCompleted=299540182, InstanceCancelled=299540183;
    public const int ManagementBlocked=299540190, ManagementAvailable=299540191,
        ManagementInProgress=299540192, ManagementWaiting=299540193,
        ManagementCompleted=299540194, ManagementCancelled=299540195;
}

public sealed record HelpdeskWorkflowStep(Guid Id,string Code,int Type,int Order,bool Initial,bool Final,
    bool ReopeningEntry,int AssignmentStrategy,Guid? UnitId,Guid? PersonId,int ActivationRule,
    bool RequiresDecision,bool RequiresObservation,bool RequiresFile,bool AllowsRequesterReturn,
    int? TargetDays,bool Active=true);
public sealed record HelpdeskWorkflowRoute(Guid Id,string Code,Guid SourceStepId,Guid TargetStepId,
    int RequiredResult,int Order,bool Active=true);
public sealed record HelpdeskWorkflowDefinition(Guid Id,Guid ServiceId,int Version,int Status,
    IReadOnlyList<HelpdeskWorkflowStep> Steps,IReadOnlyList<HelpdeskWorkflowRoute> Routes);
public sealed record HelpdeskManagementExecution(Guid Id,Guid StepId,int Execution,int Status,int? Result=null);
public sealed record HelpdeskWorkflowDependency(Guid SourceManagementId,Guid RouteId,Guid TargetManagementId);
public sealed record HelpdeskWorkflowActivation(Guid StepId,int Execution,int Status,
    IReadOnlyList<Guid> SatisfiedRouteIds,bool Reused);
public sealed record CompleteHelpdeskManagement(Guid ManagementId,int Result,string? Observation,
    bool HasRelatedFile,string OperationId);
public sealed record ReassignHelpdeskManagement(Guid? ResponsibleId,string Reason);
public sealed record ResumeHelpdeskWorkflowManagement(string? Comment,bool HasFile);
public sealed record HelpdeskWorkflowSummary(Guid Id,Guid ServiceId,int Version,int Status,string Name,string? Description,DateTimeOffset? PublishedAt,int StepCount,int RouteCount);
public sealed record CreateHelpdeskWorkflowDraft(Guid ServiceId,string Name,string? Description);
public sealed record SaveHelpdeskWorkflowStep(string Code,int Type,int Order,bool Initial,bool Final,bool ReopeningEntry,
    int AssignmentStrategy,Guid? UnitId,Guid? PersonId,int ActivationRule,bool RequiresDecision,
    bool RequiresObservation,bool RequiresFile,bool AllowsRequesterReturn,int? TargetDays,bool Active=true);
public sealed record SaveHelpdeskWorkflowRoute(string Code,Guid SourceStepId,Guid TargetStepId,int RequiredResult,int Order,bool Active=true);
public sealed record HelpdeskWorkflowManagementItem(Guid Id,Guid StepId,string StepCode,int Execution,int Status,int? Result,
    string? Observation,Guid? UnitId,Guid? ResponsibleId,DateTimeOffset? AvailableAt,DateTimeOffset? CompletedAt,
    bool RequiresDecision,bool RequiresObservation,bool RequiresFile,bool AllowsRequesterReturn,bool Final);
public sealed record HelpdeskRequestWorkflowState(Guid InstanceId,Guid FlowId,int Version,int Status,
    IReadOnlyList<HelpdeskWorkflowManagementItem> Managements);
public sealed record HelpdeskWorkflowQueueItem(Guid ManagementId,Guid RequestId,string RequestNumber,string Subject,
    string StepCode,int Execution,int Status,Guid? UnitId,Guid? ResponsibleId,DateTimeOffset? AvailableAt,bool RequiresDecision);
public sealed record HelpdeskStageForm(Guid Id,Guid StepId,string Title,string? Instructions,
    IReadOnlyList<HelpdeskFormField> Fields);
public sealed record SaveHelpdeskStageForm(string Title,string? Instructions);
public sealed record HelpdeskManagementFieldValue(Guid ResponseId,Guid FieldId,string? Value,IReadOnlyList<Guid> OptionIds);
public sealed record HelpdeskManagementForm(Guid ManagementId,Guid RequestId,HelpdeskStageForm? Form,
    IReadOnlyList<HelpdeskManagementFieldValue> Answers);
public sealed record SaveHelpdeskManagementAnswers(IReadOnlyList<HelpdeskFieldAnswer> Answers);
public sealed record SavedHelpdeskManagementAnswer(Guid FieldId,Guid ResponseId);

public static class HelpdeskWorkflowAuthorization
{
    public static bool CanWork(Guid actorId,Guid? responsibleId,Guid? unitId,IReadOnlyCollection<Guid> actorUnitIds,bool administrator)
    {
        if(administrator)return true;
        if(actorId==Guid.Empty)return false;
        if(responsibleId.HasValue)return responsibleId.Value==actorId;
        return unitId.HasValue&&actorUnitIds.Contains(unitId.Value);
    }
    public static void Demand(Guid actorId,Guid? responsibleId,Guid? unitId,IReadOnlyCollection<Guid> actorUnitIds,bool administrator)
    {
        if(!CanWork(actorId,responsibleId,unitId,actorUnitIds,administrator))
            throw new UnauthorizedAccessException("No estás autorizado para gestionar este paso.");
    }
}

public static class HelpdeskWorkflowRules
{
    private static readonly int[] Results=[HelpdeskWorkflowValues.Completed,HelpdeskWorkflowValues.Approved,
        HelpdeskWorkflowValues.Rejected,HelpdeskWorkflowValues.Returned,
        HelpdeskWorkflowValues.RequiresApproval,HelpdeskWorkflowValues.NotApplicable];

    public static IReadOnlyList<string> ValidateForPublication(HelpdeskWorkflowDefinition flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        var errors=new List<string>();var active=flow.Steps.Where(x=>x.Active).ToArray();
        var ids=active.Select(x=>x.Id).ToHashSet();var routes=flow.Routes.Where(x=>x.Active).ToArray();
        if(active.All(x=>!x.Initial))errors.Add("Debe existir al menos un paso inicial.");
        if(active.All(x=>!x.Final))errors.Add("Debe existir al menos un paso final.");
        if(active.Count(x=>x.ReopeningEntry)>1)errors.Add("Solo puede existir un paso de entrada de reapertura.");
        if(active.Select(x=>x.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=active.Length)errors.Add("Los códigos de paso deben ser únicos.");
        if(routes.Select(x=>x.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=routes.Length)errors.Add("Los códigos de ruta deben ser únicos.");
        foreach(var step in active)
        {
            if(!step.Final&&!routes.Any(x=>x.SourceStepId==step.Id))errors.Add($"El paso {step.Code} no tiene ruta de salida.");
            if(step.AssignmentStrategy==HelpdeskWorkflowValues.UnitQueue&&!step.UnitId.HasValue)errors.Add($"El paso {step.Code} requiere unidad destino.");
            if(step.AssignmentStrategy==HelpdeskWorkflowValues.SpecificPerson&&!step.PersonId.HasValue)errors.Add($"El paso {step.Code} requiere persona destino.");
        }
        foreach(var route in routes)
        {
            if(!ids.Contains(route.SourceStepId)||!ids.Contains(route.TargetStepId))errors.Add($"La ruta {route.Code} conecta pasos fuera del flujo.");
            if(!Results.Contains(route.RequiredResult))errors.Add($"La ruta {route.Code} tiene un resultado inválido.");
        }
        var reachable=active.Where(x=>x.Initial).Select(x=>x.Id).ToHashSet();var changed=true;
        while(changed){changed=false;foreach(var route in routes.Where(x=>reachable.Contains(x.SourceStepId)))changed|=reachable.Add(route.TargetStepId);}
        foreach(var step in active.Where(x=>!reachable.Contains(x.Id)))errors.Add($"El paso {step.Code} no es alcanzable.");
        return errors.Distinct().ToArray();
    }

    public static IReadOnlyList<HelpdeskWorkflowActivation> ActivateInitial(
        HelpdeskWorkflowDefinition flow,IReadOnlyList<HelpdeskManagementExecution> existing)
    {
        RequirePublished(flow);return flow.Steps.Where(x=>x.Active&&x.Initial)
            .Select(step=>Activation(step,NextExecution(step.Id,existing),HelpdeskWorkflowValues.ManagementAvailable,[],existing)).ToArray();
    }

    public static IReadOnlyList<HelpdeskWorkflowActivation> ActivateAfter(
        HelpdeskWorkflowDefinition flow,HelpdeskManagementExecution completed,
        IReadOnlyList<HelpdeskManagementExecution> existing,IReadOnlyList<HelpdeskWorkflowDependency> dependencies)
    {
        RequirePublished(flow);
        if(completed.Status!=HelpdeskWorkflowValues.ManagementCompleted||!completed.Result.HasValue)throw new InvalidOperationException("La gestión debe estar completada y tener resultado.");
        var compatible=flow.Routes.Where(x=>x.Active&&x.SourceStepId==completed.StepId&&x.RequiredResult==completed.Result).ToArray();
        var result=new List<HelpdeskWorkflowActivation>();
        foreach(var group in compatible.GroupBy(x=>x.TargetStepId))
        {
            var step=flow.Steps.Single(x=>x.Id==group.Key&&x.Active);var execution=NextExecution(step.Id,existing);
            var current=existing.Where(x=>x.StepId==step.Id).OrderByDescending(x=>x.Execution).FirstOrDefault();
            if(current is not null&&current.Status is HelpdeskWorkflowValues.ManagementBlocked or HelpdeskWorkflowValues.ManagementAvailable or HelpdeskWorkflowValues.ManagementInProgress or HelpdeskWorkflowValues.ManagementWaiting)
                execution=current.Execution;
            var satisfied=dependencies.Where(x=>current is not null&&x.TargetManagementId==current.Id).Select(x=>x.RouteId).Concat(group.Select(x=>x.Id)).Distinct().ToArray();
            var incoming=flow.Routes.Where(x=>x.Active&&x.TargetStepId==step.Id).Select(x=>x.Id).Distinct().ToArray();
            var enabled=step.ActivationRule==HelpdeskWorkflowValues.AnyIncoming||incoming.All(satisfied.Contains);
            result.Add(Activation(step,execution,enabled?HelpdeskWorkflowValues.ManagementAvailable:HelpdeskWorkflowValues.ManagementBlocked,satisfied,existing));
        }
        return result;
    }

    public static void ValidateCompletion(HelpdeskWorkflowStep step,CompleteHelpdeskManagement command)
    {
        if(!Results.Contains(command.Result))throw new ArgumentException("El resultado no es válido.");
        if(step.RequiresDecision&&command.Result==HelpdeskWorkflowValues.Completed)throw new ArgumentException("El paso exige una decisión explícita.");
        if(command.Result==HelpdeskWorkflowValues.Returned&&!step.AllowsRequesterReturn)throw new ArgumentException("El paso no permite devolver la gestión al solicitante.");
        if(step.RequiresObservation&&string.IsNullOrWhiteSpace(command.Observation))throw new ArgumentException("La observación es obligatoria.");
        if(step.Final&&command.Result!=HelpdeskWorkflowValues.Returned&&string.IsNullOrWhiteSpace(command.Observation))throw new ArgumentException("La gestión final requiere el resumen de la solución.");
        if(command.Observation?.Length>4000)throw new ArgumentException("La observación supera 4.000 caracteres.");
        if(step.RequiresFile&&!command.HasRelatedFile)throw new ArgumentException("Debes adjuntar al menos un archivo a la gestión.");
        if(string.IsNullOrWhiteSpace(command.OperationId)||command.OperationId.Length>100)throw new ArgumentException("La operación idempotente no es válida.");
    }

    public static bool RequestMustWait(IEnumerable<HelpdeskManagementExecution> executions)
    {
        var active=executions.Where(x=>x.Status is HelpdeskWorkflowValues.ManagementAvailable or HelpdeskWorkflowValues.ManagementInProgress).Any();
        var waiting=executions.Any(x=>x.Status==HelpdeskWorkflowValues.ManagementWaiting);
        return !active&&waiting;
    }

    public static bool CanCompleteInstance(HelpdeskWorkflowDefinition flow,IEnumerable<HelpdeskManagementExecution> executions)
    {
        var values=executions.ToArray();
        if(values.Any(x=>x.Status is HelpdeskWorkflowValues.ManagementBlocked or HelpdeskWorkflowValues.ManagementAvailable or HelpdeskWorkflowValues.ManagementInProgress or HelpdeskWorkflowValues.ManagementWaiting))return false;
        return values.Any(x=>x.Status==HelpdeskWorkflowValues.ManagementCompleted&&flow.Steps.Any(s=>s.Id==x.StepId&&s.Final));
    }

    private static HelpdeskWorkflowActivation Activation(HelpdeskWorkflowStep step,int execution,int status,IReadOnlyList<Guid> routes,IReadOnlyList<HelpdeskManagementExecution> existing)
    {
        var current=existing.FirstOrDefault(x=>x.StepId==step.Id&&x.Execution==execution);
        return new(step.Id,execution,status,routes,current is not null);
    }
    private static int NextExecution(Guid stepId,IReadOnlyList<HelpdeskManagementExecution> existing)=>existing.Where(x=>x.StepId==stepId).Select(x=>x.Execution).DefaultIfEmpty(0).Max()+1;
    private static void RequirePublished(HelpdeskWorkflowDefinition flow){if(flow.Status!=HelpdeskWorkflowValues.Published)throw new InvalidOperationException("El flujo debe estar publicado.");}
}

public interface IHelpdeskWorkflowStore
{
    Task<HelpdeskWorkflowDefinition?> ReadFlowAsync(Guid flowId,CancellationToken token);
    Task<IReadOnlyList<string>> ValidateForPublicationAsync(Guid flowId,CancellationToken token);
    Task PublishAsync(Guid flowId,Guid actorId,DateTimeOffset now,CancellationToken token);
    Task StartForRequestAsync(Guid requestId,Guid flowId,Guid actorId,DateTimeOffset now,CancellationToken token);
    Task CompleteAsync(Guid actorId,CompleteHelpdeskManagement command,DateTimeOffset now,CancellationToken token);
    Task ReassignAsync(Guid managementId,Guid actorId,ReassignHelpdeskManagement command,DateTimeOffset now,CancellationToken token);
    Task TakeAsync(Guid managementId,Guid actorId,DateTimeOffset now,CancellationToken token);
    Task ResumeFromRequesterAsync(Guid managementId,Guid actorId,string comment,bool hasFile,DateTimeOffset now,CancellationToken token);
    Task ReopenAsync(Guid requestId,Guid actorId,DateTimeOffset now,CancellationToken token);
    Task<IReadOnlyList<HelpdeskWorkflowSummary>> ListAsync(Guid serviceId,CancellationToken token);
    Task<Guid> CreateDraftAsync(CreateHelpdeskWorkflowDraft command,CancellationToken token);
    Task<Guid> SaveStepAsync(Guid flowId,Guid? stepId,SaveHelpdeskWorkflowStep command,CancellationToken token);
    Task<Guid> SaveRouteAsync(Guid flowId,Guid? routeId,SaveHelpdeskWorkflowRoute command,CancellationToken token);
    Task<HelpdeskStageForm?> ReadStageFormAsync(Guid stepId,CancellationToken token);
    Task<Guid> SaveStageFormAsync(Guid stepId,SaveHelpdeskStageForm command,CancellationToken token);
    Task<Guid> SaveStageFormFieldAsync(Guid stepId,Guid? fieldId,SaveHelpdeskFormField command,CancellationToken token);
    Task<HelpdeskManagementForm> ReadManagementFormAsync(Guid managementId,Guid actorId,CancellationToken token);
    Task<IReadOnlyList<SavedHelpdeskManagementAnswer>> SaveManagementAnswersAsync(Guid managementId,Guid actorId,SaveHelpdeskManagementAnswers command,CancellationToken token);
    Task<HelpdeskRequestWorkflowState?> ReadRequestStateAsync(Guid requestId,Guid actorId,bool managementAccess,CancellationToken token);
    Task<IReadOnlyList<HelpdeskWorkflowQueueItem>> ReadWorkQueueAsync(Guid actorId,string queue,CancellationToken token);
}

public sealed class HelpdeskWorkflowApplication(IHelpdeskWorkflowStore store,TimeProvider timeProvider)
{
    public async Task PublishAsync(Guid flowId,Guid actorId,CancellationToken token)
    {
        if(flowId==Guid.Empty||actorId==Guid.Empty)throw new ArgumentException("Flujo y actor son obligatorios.");
        var errors=await store.ValidateForPublicationAsync(flowId,token);
        if(errors.Count>0)throw new InvalidOperationException(string.Join(" ",errors));
        await store.PublishAsync(flowId,actorId,timeProvider.GetUtcNow(),token);
    }
    public async Task CompleteAsync(Guid actorId,CompleteHelpdeskManagement command,CancellationToken token)
    {
        if(actorId==Guid.Empty||command.ManagementId==Guid.Empty)throw new ArgumentException("Gestión y actor son obligatorios.");
        await store.CompleteAsync(actorId,command with{Observation=command.Observation?.Trim(),OperationId=command.OperationId.Trim()},timeProvider.GetUtcNow(),token);
    }
    public Task ReassignAsync(Guid managementId,Guid actorId,ReassignHelpdeskManagement command,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(command);
        if(managementId==Guid.Empty||actorId==Guid.Empty)throw new ArgumentException("Gestión y actor son obligatorios.");
        var reason=command.Reason?.Trim();
        if(string.IsNullOrWhiteSpace(reason)||reason.Length is <5 or >500)throw new ArgumentException("El motivo debe tener entre 5 y 500 caracteres.");
        return store.ReassignAsync(managementId,actorId,command with{Reason=reason},timeProvider.GetUtcNow(),token);
    }
    public Task TakeAsync(Guid managementId,Guid actorId,CancellationToken token)
    {if(managementId==Guid.Empty||actorId==Guid.Empty)throw new ArgumentException("Gestión y actor son obligatorios.");return store.TakeAsync(managementId,actorId,timeProvider.GetUtcNow(),token);}
    public Task ResumeFromRequesterAsync(Guid managementId,Guid actorId,string comment,bool hasFile,CancellationToken token)
    {
        var normalized=comment?.Trim();
        if(managementId==Guid.Empty||actorId==Guid.Empty)throw new ArgumentException("Gestión y actor son obligatorios.");
        if(string.IsNullOrWhiteSpace(normalized)&&!hasFile)throw new ArgumentException("La respuesta debe incluir comentario o archivo.");
        if(normalized?.Length>4000)throw new ArgumentException("El comentario supera 4.000 caracteres.");
        return store.ResumeFromRequesterAsync(managementId,actorId,normalized??string.Empty,hasFile,timeProvider.GetUtcNow(),token);
    }
    public Task ReopenAsync(Guid requestId,Guid actorId,CancellationToken token)
    {
        if(requestId==Guid.Empty||actorId==Guid.Empty)throw new ArgumentException("Solicitud y actor son obligatorios.");
        return store.ReopenAsync(requestId,actorId,timeProvider.GetUtcNow(),token);
    }
    public Task<IReadOnlyList<HelpdeskWorkflowSummary>> ListAsync(Guid serviceId,CancellationToken token)
    {if(serviceId==Guid.Empty)throw new ArgumentException("Selecciona un servicio válido.");return store.ListAsync(serviceId,token);}
    public Task<HelpdeskWorkflowDefinition?> ReadAsync(Guid flowId,CancellationToken token)
    {if(flowId==Guid.Empty)throw new ArgumentException("Selecciona un flujo válido.");return store.ReadFlowAsync(flowId,token);}
    public Task<Guid> CreateDraftAsync(CreateHelpdeskWorkflowDraft command,CancellationToken token)
    {ArgumentNullException.ThrowIfNull(command);var name=command.Name?.Trim();if(command.ServiceId==Guid.Empty||string.IsNullOrWhiteSpace(name)||name.Length>200)throw new ArgumentException("Servicio y nombre son obligatorios.");return store.CreateDraftAsync(command with{Name=name,Description=command.Description?.Trim()},token);}
    public Task<Guid> SaveStepAsync(Guid flowId,Guid? stepId,SaveHelpdeskWorkflowStep command,CancellationToken token)
    {ArgumentNullException.ThrowIfNull(command);var code=command.Code?.Trim().ToUpperInvariant();if(flowId==Guid.Empty||string.IsNullOrWhiteSpace(code)||code.Length>80||!System.Text.RegularExpressions.Regex.IsMatch(code,"^[A-Z0-9_-]+$"))throw new ArgumentException("El código del paso no es válido.");if(command.Order<0||command.TargetDays is <1 or >3650)throw new ArgumentException("Orden o plazo no válido.");return store.SaveStepAsync(flowId,stepId,command with{Code=code},token);}
    public Task<Guid> SaveRouteAsync(Guid flowId,Guid? routeId,SaveHelpdeskWorkflowRoute command,CancellationToken token)
    {ArgumentNullException.ThrowIfNull(command);var code=command.Code?.Trim().ToUpperInvariant();if(flowId==Guid.Empty||command.SourceStepId==Guid.Empty||command.TargetStepId==Guid.Empty||command.SourceStepId==command.TargetStepId||string.IsNullOrWhiteSpace(code)||code.Length>80)throw new ArgumentException("La ruta no es válida.");return store.SaveRouteAsync(flowId,routeId,command with{Code=code},token);}
    public Task<HelpdeskStageForm?> ReadStageFormAsync(Guid stepId,CancellationToken token)
    {if(stepId==Guid.Empty)throw new ArgumentException("Selecciona una etapa válida.");return store.ReadStageFormAsync(stepId,token);}
    public Task<Guid> SaveStageFormAsync(Guid stepId,SaveHelpdeskStageForm command,CancellationToken token)
    {ArgumentNullException.ThrowIfNull(command);var title=command.Title?.Trim();if(stepId==Guid.Empty||string.IsNullOrWhiteSpace(title)||title.Length>200)throw new ArgumentException("Etapa y título son obligatorios.");return store.SaveStageFormAsync(stepId,command with{Title=title,Instructions=command.Instructions?.Trim()},token);}
    public Task<Guid> SaveStageFormFieldAsync(Guid stepId,Guid? fieldId,SaveHelpdeskFormField command,CancellationToken token)
    {if(stepId==Guid.Empty)throw new ArgumentException("Selecciona una etapa válida.");return store.SaveStageFormFieldAsync(stepId,fieldId,HelpdeskFormFieldValidation.Normalize(command),token);}
    public Task<HelpdeskManagementForm> ReadManagementFormAsync(Guid managementId,Guid actorId,CancellationToken token)
    {if(managementId==Guid.Empty||actorId==Guid.Empty)throw new ArgumentException("Gestión y actor son obligatorios.");return store.ReadManagementFormAsync(managementId,actorId,token);}
    public Task<IReadOnlyList<SavedHelpdeskManagementAnswer>> SaveManagementAnswersAsync(Guid managementId,Guid actorId,SaveHelpdeskManagementAnswers command,CancellationToken token)
    {ArgumentNullException.ThrowIfNull(command);if(managementId==Guid.Empty||actorId==Guid.Empty)throw new ArgumentException("Gestión y actor son obligatorios.");if(command.Answers.GroupBy(x=>x.FieldId).Any(x=>x.Count()>1))throw new ArgumentException("No se puede enviar dos veces el mismo campo.");return store.SaveManagementAnswersAsync(managementId,actorId,command,token);}
    public Task<HelpdeskRequestWorkflowState?> ReadRequestStateAsync(Guid requestId,Guid actorId,bool managementAccess,CancellationToken token)
    {if(requestId==Guid.Empty||actorId==Guid.Empty)throw new ArgumentException("Solicitud y actor son obligatorios.");return store.ReadRequestStateAsync(requestId,actorId,managementAccess,token);}
    public Task<IReadOnlyList<HelpdeskWorkflowQueueItem>> ReadWorkQueueAsync(Guid actorId,string queue,CancellationToken token)
    {
        if(actorId==Guid.Empty)throw new ArgumentException("El actor es obligatorio.");
        var normalized=queue?.Trim().ToLowerInvariant();
        if(normalized is not ("unit" or "mine" or "approvals" or "waiting"))throw new ArgumentException("La bandeja solicitada no es válida.");
        return store.ReadWorkQueueAsync(actorId,normalized,token);
    }
}

public static class HelpdeskFormFieldValidation
{
    public static SaveHelpdeskFormField Normalize(SaveHelpdeskFormField request)
    {
        ArgumentNullException.ThrowIfNull(request);var code=request.Code?.Trim().ToUpperInvariant();var label=request.Label?.Trim();if(string.IsNullOrWhiteSpace(code)||code.Length>80||!System.Text.RegularExpressions.Regex.IsMatch(code,"^[A-Z0-9_-]+$")||string.IsNullOrWhiteSpace(label)||label.Length>150)throw new ArgumentException("Código y etiqueta del campo no son válidos.");if(request.DataType is <299540040 or >299540047||request.ControlType is <299540050 or >299540062||request.Width is <1 or >12||request.Order<0)throw new ArgumentException("Tipo, control, ancho u orden del campo no son válidos.");if(request.MinimumLength.HasValue&&request.MaximumLength.HasValue&&request.MinimumLength>request.MaximumLength||request.MinimumValue.HasValue&&request.MaximumValue.HasValue&&request.MinimumValue>request.MaximumValue)throw new ArgumentException("Los valores mínimos no pueden superar los máximos.");if(request.DataType==299540046&&request.Options.Count==0)throw new ArgumentException("Un campo de opción debe tener opciones.");var optionCodes=request.Options.Select(x=>x.Code.Trim().ToUpperInvariant()).ToArray();if(optionCodes.Distinct().Count()!=optionCodes.Length||request.Options.Any(x=>string.IsNullOrWhiteSpace(x.Code)||string.IsNullOrWhiteSpace(x.Label)))throw new ArgumentException("Las opciones deben tener códigos y etiquetas únicos.");return request with{Code=code,Label=label,HelpText=request.HelpText?.Trim(),Placeholder=request.Placeholder?.Trim(),ValidationPattern=request.ValidationPattern?.Trim(),ValidationMessage=request.ValidationMessage?.Trim(),DefaultValue=request.DefaultValue?.Trim(),ConfigurationJson=request.ConfigurationJson?.Trim(),Options=request.Options.Select(x=>x with{Code=x.Code.Trim().ToUpperInvariant(),Label=x.Label.Trim()}).ToArray()};
    }
}
