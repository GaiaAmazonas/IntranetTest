using Gaia.Modules.Helpdesk;

namespace Gaia.ArchitectureTests;

public sealed class HelpdeskRequestApplicationTests
{
    [Fact]
    public async Task ValidRequestIsNormalizedBeforePersistence()
    {
        var store = new Store();
        var service = new HelpdeskRequestApplication(store, store, TimeProvider.System);
        await service.CreateAsync(new(Guid.NewGuid(), "  Acceso al sistema  ", "  Necesito recuperar mi acceso  "), Guid.NewGuid(), default);
        Assert.Equal("Acceso al sistema", store.Request!.Subject);
        Assert.Equal("Necesito recuperar mi acceso", store.Request.Description);
    }

    [Theory]
    [InlineData("", "Descripción suficientemente larga")]
    [InlineData("Bien", "Descripción suficientemente larga")]
    [InlineData("Asunto válido", "Corta")]
    public async Task InvalidContentIsRejectedBeforeDataverse(string subject, string description)
    {
        var store = new Store();
        var service = new HelpdeskRequestApplication(store, store, TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            new(Guid.NewGuid(), subject, description), Guid.NewGuid(), default));
        Assert.Null(store.Request);
    }

    [Fact]
    public async Task RequiredDynamicFieldIsValidatedBeforePersistence()
    {
        var fieldId=Guid.NewGuid();var store=new Store{Form=new(Guid.NewGuid(),1,"Datos",null,[new(fieldId,"detalle","Detalle",299540040,299540050,null,null,true,1,12,null,100,null,null,false,null,null,true,[])])};var service=new HelpdeskRequestApplication(store,store,TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.CreateAsync(new(Guid.NewGuid(),"Asunto válido","Descripción suficientemente larga",[]),Guid.NewGuid(),default));
        Assert.Null(store.Request);
    }

    private sealed class Store : IHelpdeskRequestStore, IHelpdeskFormReader
    {
        public CreateHelpdeskRequest? Request { get; private set; }
        public HelpdeskServiceForm? Form { get; init; }
        public Task<CreatedHelpdeskRequest> CreateAsync(CreateHelpdeskRequest request, Guid requesterThirdPartyId,
            DateTimeOffset now, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new CreatedHelpdeskRequest(Guid.NewGuid(), "HD-0000001", now, DateOnly.FromDateTime(now.Date.AddDays(1))));
        }
        public Task<HelpdeskServiceForm?> ReadForServiceAsync(Guid serviceId,CancellationToken cancellationToken)=>Task.FromResult(Form);
    }
}
