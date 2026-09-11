using Gaia.Modules.Helpdesk;

namespace Gaia.ArchitectureTests;

public sealed class HelpdeskManagementApplicationTests
{
    [Fact]
    public async Task QueueRejectsUnboundedPageSize()
    {
        var store=new Store();var application=new HelpdeskManagementApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>application.ReadQueueAsync(new(PageSize:101),default));
        Assert.False(store.QueueRead);
    }

    [Fact]
    public async Task QueueRejectsUnknownSort()
    {
        var store=new Store();var application=new HelpdeskManagementApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>application.ReadQueueAsync(new(Sort:"subject-asc"),default));
        Assert.False(store.QueueRead);
    }

    [Fact]
    public async Task QueuePreservesPageCursorAndStableSort()
    {
        var store=new Store();var application=new HelpdeskManagementApplication(store,TimeProvider.System);
        await application.ReadQueueAsync(new(Search:"  acceso  ",Page:3,PageSize:10,Sort:"submitted-desc",ContinuationToken:"protected"),default);
        Assert.Equal(new HelpdeskQueueFilter("acceso",null,null,null,null,3,10,"submitted-desc","protected"),store.Filter);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    public async Task ReassignmentRequiresAuditableReason(string reason)
    {
        var store=new Store();var application=new HelpdeskManagementApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>application.ReassignAsync(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),null,reason),default));
        Assert.Null(store.Reassignment);
    }

    [Fact]
    public async Task ReassignmentIsNormalizedBeforePersistence()
    {
        var store=new Store();var application=new HelpdeskManagementApplication(store,TimeProvider.System);
        await application.ReassignAsync(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),null,"  Cambio por cobertura  "),default);
        Assert.Equal("Cambio por cobertura",store.Reassignment!.Reason);
    }

    private sealed class Store:IHelpdeskManagementStore
    {
        public bool QueueRead{get;private set;}public HelpdeskQueueFilter? Filter{get;private set;}public ReassignHelpdeskRequest? Reassignment{get;private set;}
        public Task<HelpdeskQueuePage> ReadQueueAsync(HelpdeskQueueFilter filter,CancellationToken token){QueueRead=true;Filter=filter;return Task.FromResult(new HelpdeskQueuePage(0,1,25,[]));}
        public Task<HelpdeskManagementCatalog> ReadCatalogAsync(CancellationToken token)=>Task.FromResult(new HelpdeskManagementCatalog([],[],[],[]));
        public Task ReassignAsync(Guid requestId,Guid actorId,ReassignHelpdeskRequest request,DateTimeOffset now,CancellationToken token){Reassignment=request;return Task.CompletedTask;}
        public Task<HelpdeskAdminSnapshot> ReadAdministrationAsync(CancellationToken token)=>Task.FromResult(new HelpdeskAdminSnapshot([],[],[],[]));
        public Task<Guid> SaveServiceAsync(Guid? id,SaveHelpdeskService request,CancellationToken token)=>Task.FromResult(id??Guid.NewGuid());
        public Task<Guid> CreateFormDraftAsync(CreateHelpdeskFormDraft request,CancellationToken token)=>Task.FromResult(Guid.NewGuid());
        public Task PublishFormAsync(Guid formId,Guid actorId,DateTimeOffset now,CancellationToken token)=>Task.CompletedTask;
        public Task<HelpdeskAdminFormDefinition> ReadFormAsync(Guid formId,CancellationToken token)=>throw new NotImplementedException();
        public Task<Guid> SaveFormFieldAsync(Guid formId,Guid? fieldId,SaveHelpdeskFormField request,CancellationToken token)=>Task.FromResult(fieldId??Guid.NewGuid());
    }
}
