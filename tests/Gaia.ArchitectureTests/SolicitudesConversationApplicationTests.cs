using Gaia.Modules.Solicitudes;

namespace Gaia.ArchitectureTests;

public sealed class SolicitudesConversationApplicationTests
{
    [Fact]
    public async Task EmptyCommentIsRejectedBeforePersistence()
    {
        var store=new Store();var app=new SolicitudesConversationApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.AddAsync(Guid.NewGuid(),Guid.NewGuid(),new(" "),false,default));
        Assert.False(store.Called);
    }

    private sealed class Store:ISolicitudesConversationStore
    {
        public bool Called{get;private set;}
        public Task<SolicitudesRequestDetail?> ReadAsync(Guid requestId,Guid actorThirdPartyId,bool managementAccess,CancellationToken cancellationToken)=>Task.FromResult<SolicitudesRequestDetail?>(null);
        public Task<SolicitudesComment> AddAsync(Guid requestId,Guid actorThirdPartyId,string content,bool internalOnly,bool managementAccess,DateTimeOffset now,CancellationToken cancellationToken){Called=true;return Task.FromResult(new SolicitudesComment(Guid.NewGuid(),content,now,internalOnly,true,"Solicitante"));}
        public Task<SolicitudesRequestDetail> TransitionAsync(Guid requestId,Guid actorThirdPartyId,ApplySolicitudesTransition transition,bool managementAccess,DateTimeOffset now,CancellationToken cancellationToken)=>throw new NotSupportedException();
    }
}
