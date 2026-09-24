namespace Gaia.Modules.Helpdesk;

public sealed record HelpdeskQueueFilter(string? Search=null,Guid? ServiceId=null,Guid? StateId=null,
    Guid? ResponsibleId=null,bool? Overdue=null,int Page=1,int PageSize=25,
    string Sort="submitted-desc",string? ContinuationToken=null);
public sealed record HelpdeskQueueItem(Guid Id,string Number,string Subject,string Service,string Status,
    string? StatusColor,string Requester,string? Responsible,string? Unit,DateTimeOffset? SubmittedAt,
    DateOnly? DueDate,bool IsOverdue);
public sealed record HelpdeskQueuePage(int Total,int Page,int PageSize,IReadOnlyList<HelpdeskQueueItem> Items,
    bool HasNextPage=false,int? TotalCount=null,string? ContinuationToken=null);
public sealed record HelpdeskManagementOption(Guid Id,string Name,string? Code=null);
public sealed record HelpdeskManagementCatalog(IReadOnlyList<HelpdeskManagementOption> Services,
    IReadOnlyList<HelpdeskManagementOption> States,IReadOnlyList<HelpdeskManagementOption> Responsibles,
    IReadOnlyList<HelpdeskManagementOption> Units);
public sealed record ReassignHelpdeskRequest(Guid ResponsibleId,Guid? UnitId,string Reason);
public sealed record HelpdeskAdminService(Guid Id,string Code,string Name,string? Description,string? Instructions,
    int BusinessDays,bool AllowsAttachments,int MaximumAttachments,int MaximumFileMb,bool Visible,int Order,
    Guid ResponsibleId,Guid UnitId,Guid? CurrentFormId,bool IsActive);
public sealed record HelpdeskAdminForm(Guid Id,Guid ServiceId,int Version,int Status,string Title,string? Instructions,
    DateTimeOffset? PublishedAt,int FieldCount,bool IsActive);
public sealed record HelpdeskAdminResponsible(Guid Id,string Name,IReadOnlyList<Guid> UnitIds);
public sealed record HelpdeskAdminUnit(Guid Id,string Code,string Name,Guid? ParentId,int Level);
public sealed record HelpdeskAdminServiceMetrics(Guid ServiceId,int TotalRequests,int OpenRequests,
    int ResolvedRequests,int PendingClosureRequests,int OverdueRequests);
public sealed record HelpdeskRequestExportRow(Guid Id,string Number,string Subject,string? Description,string Service,
    string Requester,string? RequesterUnit,DateTimeOffset? SubmittedAt,DateTimeOffset? FirstManagementAt,
    string? Responsible,string? ResponsibleUnit,string Status,bool IsFinal,DateOnly? DueDate,
    DateTimeOffset? ClosedAt,int? BusinessManagementDays,int CalendarElapsedDays,bool? MetSla,string? SolutionSummary);
public sealed record HelpdeskAdminSnapshot(IReadOnlyList<HelpdeskAdminService> Services,IReadOnlyList<HelpdeskAdminForm> Forms,
    IReadOnlyList<HelpdeskAdminResponsible> Responsibles,IReadOnlyList<HelpdeskAdminUnit> Units,
    IReadOnlyList<HelpdeskAdminServiceMetrics> Metrics);
public sealed record SaveHelpdeskService(string Code,string Name,string? Description,string? Instructions,int BusinessDays,
    bool AllowsAttachments,int MaximumAttachments,int MaximumFileMb,bool Visible,int Order,Guid ResponsibleId,Guid UnitId,bool IsActive);
public sealed record CreateHelpdeskFormDraft(Guid ServiceId,string Title,string? Instructions);
public sealed record SaveHelpdeskFormOption(Guid? Id,string Code,string Label,int Order,bool IsDefault,bool IsActive=true);
public sealed record SaveHelpdeskFormField(string Code,string Label,int DataType,int ControlType,string? HelpText,
    string? Placeholder,bool Required,int Order,int Width,int? MinimumLength,int? MaximumLength,decimal? MinimumValue,
    decimal? MaximumValue,bool AllowsMultiple,int? MaximumFiles,string? AllowedFileTypes,bool Visible,
    IReadOnlyList<SaveHelpdeskFormOption> Options,string? ValidationPattern=null,string? ValidationMessage=null,
    string? DefaultValue=null,string? ConfigurationJson=null);
public sealed record HelpdeskAdminFormDefinition(HelpdeskAdminForm Form,IReadOnlyList<HelpdeskFormField> Fields);

public interface IHelpdeskManagementStore
{
    Task<HelpdeskQueuePage> ReadQueueAsync(HelpdeskQueueFilter filter,CancellationToken token);
    Task<HelpdeskManagementCatalog> ReadCatalogAsync(CancellationToken token);
    Task ReassignAsync(Guid requestId,Guid actorId,ReassignHelpdeskRequest request,DateTimeOffset now,CancellationToken token);
    Task<HelpdeskAdminSnapshot> ReadAdministrationAsync(CancellationToken token);
    Task<IReadOnlyList<HelpdeskRequestExportRow>> ReadExportAsync(CancellationToken token);
    Task<Guid> SaveServiceAsync(Guid? id,SaveHelpdeskService request,CancellationToken token);
    Task<Guid> CreateFormDraftAsync(CreateHelpdeskFormDraft request,CancellationToken token);
    Task PublishFormAsync(Guid formId,Guid actorId,DateTimeOffset now,CancellationToken token);
    Task<HelpdeskAdminFormDefinition> ReadFormAsync(Guid formId,CancellationToken token);
    Task<Guid> SaveFormFieldAsync(Guid formId,Guid? fieldId,SaveHelpdeskFormField request,CancellationToken token);
}

public interface IHelpdeskManagementApplication
{
    Task<HelpdeskQueuePage> ReadQueueAsync(HelpdeskQueueFilter filter,CancellationToken token);
    Task<HelpdeskManagementCatalog> ReadCatalogAsync(CancellationToken token);
    Task ReassignAsync(Guid requestId,Guid actorId,ReassignHelpdeskRequest request,CancellationToken token);
    Task<HelpdeskAdminSnapshot> ReadAdministrationAsync(CancellationToken token);
    Task<IReadOnlyList<HelpdeskRequestExportRow>> ReadExportAsync(CancellationToken token);
    Task<Guid> SaveServiceAsync(Guid? id,SaveHelpdeskService request,CancellationToken token);
    Task<Guid> CreateFormDraftAsync(CreateHelpdeskFormDraft request,CancellationToken token);
    Task PublishFormAsync(Guid formId,Guid actorId,CancellationToken token);
    Task<HelpdeskAdminFormDefinition> ReadFormAsync(Guid formId,CancellationToken token);
    Task<Guid> SaveFormFieldAsync(Guid formId,Guid? fieldId,SaveHelpdeskFormField request,CancellationToken token);
}

public sealed class HelpdeskManagementApplication(IHelpdeskManagementStore store,TimeProvider timeProvider):IHelpdeskManagementApplication
{
    public Task<HelpdeskQueuePage> ReadQueueAsync(HelpdeskQueueFilter filter,CancellationToken token)
    {
        if (filter.Sort != "submitted-desc") throw new ArgumentException("Ordenamiento no válido.");
        if (filter.ContinuationToken?.Length > 16000) throw new ArgumentException("Continuación no válida.");
        ArgumentNullException.ThrowIfNull(filter);if(filter.Page<1)throw new ArgumentException("La página debe ser mayor que cero.");if(filter.PageSize is <1 or >100)throw new ArgumentException("El tamaño de página debe estar entre 1 y 100.");if(filter.Search?.Length>100)throw new ArgumentException("La búsqueda no puede superar 100 caracteres.");return store.ReadQueueAsync(filter with{Search=filter.Search?.Trim()},token);
    }
    public Task<HelpdeskManagementCatalog> ReadCatalogAsync(CancellationToken token)=>store.ReadCatalogAsync(token);
    public Task ReassignAsync(Guid requestId,Guid actorId,ReassignHelpdeskRequest request,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);if(requestId==Guid.Empty||request.ResponsibleId==Guid.Empty)throw new ArgumentException("Selecciona una solicitud y un responsable válidos.");var reason=request.Reason?.Trim();if(string.IsNullOrWhiteSpace(reason)||reason.Length is <5 or >500)throw new ArgumentException("El motivo debe tener entre 5 y 500 caracteres.");return store.ReassignAsync(requestId,actorId,request with{Reason=reason},timeProvider.GetUtcNow(),token);
    }
    public Task<HelpdeskAdminSnapshot> ReadAdministrationAsync(CancellationToken token)=>store.ReadAdministrationAsync(token);
    public Task<IReadOnlyList<HelpdeskRequestExportRow>> ReadExportAsync(CancellationToken token)=>store.ReadExportAsync(token);
    public Task<Guid> SaveServiceAsync(Guid? id,SaveHelpdeskService request,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);var code=request.Code?.Trim().ToUpperInvariant();var name=request.Name?.Trim();if(string.IsNullOrWhiteSpace(code)||code.Length>50||!System.Text.RegularExpressions.Regex.IsMatch(code,"^[A-Z0-9_-]+$"))throw new ArgumentException("El código solo admite letras, números, guion y guion bajo.");if(string.IsNullOrWhiteSpace(name)||name.Length>150)throw new ArgumentException("El nombre es obligatorio y admite máximo 150 caracteres.");if(request.BusinessDays<1||request.MaximumAttachments is <0 or >100||request.MaximumFileMb is <1 or >2048)throw new ArgumentException("Revisa los límites de atención y adjuntos.");if(request.ResponsibleId==Guid.Empty||request.UnitId==Guid.Empty)throw new ArgumentException("Responsable y unidad son obligatorios.");return store.SaveServiceAsync(id,request with{Code=code,Name=name,Description=request.Description?.Trim(),Instructions=request.Instructions?.Trim()},token);
    }
    public Task<Guid> CreateFormDraftAsync(CreateHelpdeskFormDraft request,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);var title=request.Title?.Trim();if(request.ServiceId==Guid.Empty||string.IsNullOrWhiteSpace(title)||title.Length>200)throw new ArgumentException("Servicio y título válido son obligatorios.");return store.CreateFormDraftAsync(request with{Title=title,Instructions=request.Instructions?.Trim()},token);
    }
    public Task PublishFormAsync(Guid formId,Guid actorId,CancellationToken token){if(formId==Guid.Empty)throw new ArgumentException("Selecciona un formulario válido.");return store.PublishFormAsync(formId,actorId,timeProvider.GetUtcNow(),token);}
    public Task<HelpdeskAdminFormDefinition> ReadFormAsync(Guid formId,CancellationToken token){if(formId==Guid.Empty)throw new ArgumentException("Selecciona un formulario válido.");return store.ReadFormAsync(formId,token);}
    public Task<Guid> SaveFormFieldAsync(Guid formId,Guid? fieldId,SaveHelpdeskFormField request,CancellationToken token)
    {
        if(formId==Guid.Empty)throw new ArgumentException("Selecciona un formulario válido.");
        return store.SaveFormFieldAsync(formId,fieldId,HelpdeskFormFieldValidation.Normalize(request),token);
    }
}
