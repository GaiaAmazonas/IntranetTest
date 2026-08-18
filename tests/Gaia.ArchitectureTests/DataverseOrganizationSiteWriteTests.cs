using System.Net;
using System.Text;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.Organization;
using Gaia.Modules.Organization;

namespace Gaia.ArchitectureTests;

public sealed class DataverseOrganizationSiteWriteTests
{
    [Fact]
    public async Task ReaderMapsDataverseSiteContract()
    {
        var id = Guid.NewGuid();
        var handler = new QueueHandler(Json(HttpStatusCode.OK,
            $"{{\"value\":[{SiteJson(id)}]}}"));
        var reader = new DataverseOrganizationSiteReader(new StubFactory(handler));

        var sites = await reader.ListAsync(CancellationToken.None);

        var site = Assert.Single(sites);
        Assert.Equal(id, site.Id);
        Assert.Equal("BOG", site.Code);
        Assert.Equal("Bogotá", site.Name);
        Assert.True(site.IsActive);
    }

    [Fact]
    public async Task CreatorRejectsDuplicateCodeWithoutPosting()
    {
        var handler = new QueueHandler(Json(HttpStatusCode.OK,
            $"{{\"value\":[{{\"gaia_sedeid\":\"{Guid.NewGuid():D}\"}}]}}"));
        var creator = new DataverseOrganizationSiteCreator(new StubFactory(handler));

        var result = await creator.CreateAsync(
            new SiteCreateCommand("BOG", "Bogotá", "Bogotá D.C.", null, true, "user"),
            CancellationToken.None);

        Assert.True(result.IsDuplicate);
        Assert.Single(handler.Methods);
        Assert.Equal(HttpMethod.Get, handler.Methods[0]);
    }

    [Fact]
    public async Task CreatorPersistsFieldsAndReturnsDataverseGuid()
    {
        var id = Guid.NewGuid();
        var handler = new QueueHandler(
            Json(HttpStatusCode.OK, "{\"value\":[]}"),
            Created(id),
            Json(HttpStatusCode.OK, SiteJson(id)));
        var creator = new DataverseOrganizationSiteCreator(new StubFactory(handler));

        var result = await creator.CreateAsync(
            new SiteCreateCommand("LET", "Leticia", "Leticia", "Centro", true, "user"),
            CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal(id, result.Item?.Id);
        Assert.Contains("\"gaia_codigo\":\"LET\"", handler.Bodies[1]);
        Assert.Contains("\"gaia_ciudad\":\"Leticia\"", handler.Bodies[1]);
        Assert.Contains("\"gaia_activo\":true", handler.Bodies[1]);
    }

    [Fact]
    public async Task UpdaterReturnsNotFoundWithoutPatching()
    {
        var handler = new QueueHandler(new HttpResponseMessage(HttpStatusCode.NotFound));
        var updater = new DataverseOrganizationSiteUpdater(new StubFactory(handler));

        var result = await updater.UpdateAsync(Guid.NewGuid(),
            new SiteUpdateCommand("BOG", "Bogotá", null, null, true), CancellationToken.None);

        Assert.True(result.NotFound);
        Assert.Single(handler.Methods);
        Assert.Equal(HttpMethod.Get, handler.Methods[0]);
    }

    private static string SiteJson(Guid id) =>
        $"{{\"gaia_sedeid\":\"{id:D}\",\"gaia_codigo\":\"BOG\",\"gaia_name\":\"Bogotá\"," +
        "\"gaia_ciudad\":\"Bogotá D.C.\",\"gaia_direccion\":null,\"gaia_activo\":true," +
        "\"statecode\":0,\"createdon\":\"2026-08-10T15:00:00Z\",\"modifiedon\":\"2026-08-10T15:00:00Z\"}";

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Created(Guid id)
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        response.Headers.TryAddWithoutValidation("OData-EntityId",
            $"https://example.test/api/data/v9.2/gaia_sedes({id:D})");
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
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);
            Bodies.Add(request.Content is null ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return queue.Dequeue();
        }
    }
}
