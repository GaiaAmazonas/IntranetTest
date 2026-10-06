using Gaia.Modules.Solicitudes;

namespace Gaia.ArchitectureTests;

public sealed class SolicitudesManagementApplicationTests
{
    [Fact]
    public async Task QueueRejectsUnboundedPageSize()
    {
        var store=new Store();var application=new SolicitudesManagementApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>application.ReadQueueAsync(Guid.NewGuid(),new(PageSize:101),default));
        Assert.False(store.QueueRead);
    }

    [Fact]
    public async Task QueueRejectsUnknownSort()
    {
        var store=new Store();var application=new SolicitudesManagementApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>application.ReadQueueAsync(Guid.NewGuid(),new(Sort:"subject-asc"),default));
        Assert.False(store.QueueRead);
    }

    [Fact]
    public async Task QueuePreservesPageCursorAndStableSort()
    {
        var store=new Store();var application=new SolicitudesManagementApplication(store,TimeProvider.System);
        await application.ReadQueueAsync(Guid.NewGuid(),new(Search:"  acceso  ",Page:3,PageSize:10,Sort:"submitted-desc",ContinuationToken:"protected"),default);
        Assert.Equal(new SolicitudesQueueFilter("acceso",null,null,null,null,3,10,"submitted-desc","protected"),store.Filter);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    public async Task ReassignmentRequiresAuditableReason(string reason)
    {
        var store=new Store();var application=new SolicitudesManagementApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>application.ReassignAsync(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),null,reason),default));
        Assert.Null(store.Reassignment);
    }

    [Fact]
    public async Task ReassignmentIsNormalizedBeforePersistence()
    {
        var store=new Store();var application=new SolicitudesManagementApplication(store,TimeProvider.System);
        await application.ReassignAsync(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),null,"  Cambio por cobertura  "),default);
        Assert.Equal("Cambio por cobertura",store.Reassignment!.Reason);
    }

    [Fact]
    public async Task DeleteResolvedRequiresAValidRequestAndActor()
    {
        var store=new Store();var application=new SolicitudesManagementApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>application.DeleteResolvedAsync(Guid.Empty,Guid.NewGuid(),default));
        await Assert.ThrowsAsync<ArgumentException>(()=>application.DeleteResolvedAsync(Guid.NewGuid(),Guid.Empty,default));
        Assert.False(store.DeleteRequested);
    }

    [Fact]
    public async Task DeleteResolvedDelegatesTheProtectedOperation()
    {
        var store=new Store();var application=new SolicitudesManagementApplication(store,TimeProvider.System);
        await application.DeleteResolvedAsync(Guid.NewGuid(),Guid.NewGuid(),default);
        Assert.True(store.DeleteRequested);
    }

    private sealed class Store:ISolicitudesManagementStore
    {
        public bool QueueRead{get;private set;}public bool DeleteRequested{get;private set;}public SolicitudesQueueFilter? Filter{get;private set;}public ReassignSolicitudesRequest? Reassignment{get;private set;}
        public Task<SolicitudesQueuePage> ReadQueueAsync(Guid actorId,SolicitudesQueueFilter filter,CancellationToken token){QueueRead=true;Filter=filter;return Task.FromResult(new SolicitudesQueuePage(0,1,25,[]));}
        public Task<SolicitudesManagementCatalog> ReadCatalogAsync(CancellationToken token)=>Task.FromResult(new SolicitudesManagementCatalog([],[],[],[]));
        public Task<IReadOnlyList<SolicitudesRequestExportRow>> ReadExportAsync(CancellationToken token)=>Task.FromResult<IReadOnlyList<SolicitudesRequestExportRow>>([]);
        public Task ReassignAsync(Guid requestId,Guid actorId,ReassignSolicitudesRequest request,DateTimeOffset now,CancellationToken token){Reassignment=request;return Task.CompletedTask;}
        public Task DeleteResolvedAsync(Guid requestId,Guid actorId,DateTimeOffset now,CancellationToken token){DeleteRequested=true;return Task.CompletedTask;}
        public Task<SolicitudesAdminSnapshot> ReadAdministrationAsync(CancellationToken token)=>Task.FromResult(new SolicitudesAdminSnapshot([],[],[],[],[]));
        public Task<Guid> SaveServiceAsync(Guid? id,SaveSolicitudesService request,CancellationToken token)=>Task.FromResult(id??Guid.NewGuid());
        public Task<Guid> CreateFormDraftAsync(CreateSolicitudesFormDraft request,CancellationToken token)=>Task.FromResult(Guid.NewGuid());
        public Task PublishFormAsync(Guid formId,Guid actorId,DateTimeOffset now,CancellationToken token)=>Task.CompletedTask;
        public Task<SolicitudesAdminFormDefinition> ReadFormAsync(Guid formId,CancellationToken token)=>throw new NotImplementedException();
        public Task<Guid> SaveFormFieldAsync(Guid formId,Guid? fieldId,SaveSolicitudesFormField request,CancellationToken token)=>Task.FromResult(fieldId??Guid.NewGuid());
        public Task DeleteFormFieldAsync(Guid formId,Guid fieldId,CancellationToken token)=>Task.CompletedTask;
    }
}
