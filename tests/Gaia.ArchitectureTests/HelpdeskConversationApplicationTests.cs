using Gaia.Modules.Helpdesk;

namespace Gaia.ArchitectureTests;

public sealed class HelpdeskConversationApplicationTests
{
    [Fact]
    public async Task EmptyCommentIsRejectedBeforePersistence()
    {
        var store=new Store();var app=new HelpdeskConversationApplication(store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>app.AddAsync(Guid.NewGuid(),Guid.NewGuid(),new(" "),false,default));
        Assert.False(store.Called);
    }

    private sealed class Store:IHelpdeskConversationStore
    {
        public bool Called{get;private set;}
        public Task<HelpdeskRequestDetail?> ReadAsync(Guid requestId,Guid actorThirdPartyId,bool managementAccess,CancellationToken cancellationToken)=>Task.FromResult<HelpdeskRequestDetail?>(null);
        public Task<HelpdeskComment> AddAsync(Guid requestId,Guid actorThirdPartyId,string content,bool internalOnly,bool managementAccess,DateTimeOffset now,CancellationToken cancellationToken){Called=true;return Task.FromResult(new HelpdeskComment(Guid.NewGuid(),content,now,internalOnly,true,"Solicitante"));}
        public Task<HelpdeskRequestDetail> TransitionAsync(Guid requestId,Guid actorThirdPartyId,ApplyHelpdeskTransition transition,bool managementAccess,DateTimeOffset now,CancellationToken cancellationToken)=>throw new NotSupportedException();
    }
}
