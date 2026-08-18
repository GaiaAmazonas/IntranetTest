using System.Net;
using System.Text;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.Organization;
using Gaia.Modules.Organization;

namespace Gaia.ArchitectureTests;

public sealed class DataverseOrganizationUnitTypeWriteTests
{
    [Fact]
    public async Task CreateSendsContractFieldsAndReturnsCreatedId()
    {
        var id = Guid.NewGuid();
        var handler = new QueueHandler(
            Json(HttpStatusCode.OK, "{\"value\":[]}"),
            Created(id));
        var creator = new DataverseOrganizationUnitTypeCreator(new StubFactory(handler));

        var result = await creator.CreateAsync(
            new UnitTypeCreateCommand(
                "TEC", "Técnica", "Descripción", "organization.tecnica", 70, true, "user@gaia.org"),
            CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal(id, result.Item?.Id);
        Assert.Equal("organization.tecnica", result.Item?.ColorToken);
        Assert.Contains("\"gaia_codigo\":\"TEC\"", handler.Bodies[1]);
        Assert.Contains("\"gaia_colortoken\":\"organization.tecnica\"", handler.Bodies[1]);
        Assert.Contains("\"gaia_ordenvisual\":70", handler.Bodies[1]);
    }

    [Fact]
    public async Task CreateDoesNotPostWhenCodeAlreadyExists()
    {
        var handler = new QueueHandler(Json(
            HttpStatusCode.OK,
            $"{{\"value\":[{{\"gaia_tipounidadorganizacionalid\":\"{Guid.NewGuid():D}\"}}]}}"));
        var creator = new DataverseOrganizationUnitTypeCreator(new StubFactory(handler));

        var result = await creator.CreateAsync(
            new UnitTypeCreateCommand("DIR", "Directivos", null, "organization.directivos", 10, true, "user"),
            CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.Single(handler.Methods);
        Assert.Equal(HttpMethod.Get, handler.Methods[0]);
    }

    [Fact]
    public async Task UpdateReturnsNotFoundWithoutSendingPatch()
    {
        var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.NotFound));
        var updater = new DataverseOrganizationUnitTypeUpdater(new StubFactory(handler));

        var result = await updater.UpdateAsync(
            Guid.NewGuid(),
            new UnitTypeUpdateCommand("TEC", "Técnica", null, "organization.tecnica", 70, true),
            CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.Single(handler.Methods);
        Assert.Equal(HttpMethod.Get, handler.Methods[0]);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Created(Guid id)
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        response.Headers.TryAddWithoutValidation(
            "OData-EntityId",
            $"https://example.test/api/data/v9.2/gaia_tipounidadorganizacionals({id:D})");
        return response;
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IDataverseDelegatedClientFactory
    {
        public Task<HttpClient> CreateAsync() => Task.FromResult(new HttpClient(handler, false)
        {
            BaseAddress = new Uri("https://example.test/api/data/v9.2/")
        });
    }

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> queue = new(responses);
        public List<HttpMethod> Methods { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return queue.Dequeue();
        }
    }
}
