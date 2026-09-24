using System.Net;
using Gaia.Api.Infrastructure.Dataverse;

namespace Gaia.ArchitectureTests;

public sealed class DataverseChangeSetTests
{
    [Fact]
    public async Task SendsWritesInsideOneMultipartChangeSet()
    {
        var handler=new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent("--batchresponse--")});using var client=new HttpClient(handler){BaseAddress=new("https://example.crm.dynamics.com/api/data/v9.2/")};
        await DataverseChangeSet.ExecuteAsync(client,[("PATCH","gaia_rows(00000000-0000-0000-0000-000000000001)",new{id=1}),("POST","gaia_rows",new{id=2})],default);
        Assert.EndsWith("/$batch",handler.Path,StringComparison.Ordinal);Assert.Contains("multipart/mixed",handler.ContentType);Assert.Contains("PATCH https://example.crm.dynamics.com/api/data/v9.2/gaia_rows",handler.Body);Assert.Contains("If-Match: *",handler.Body);Assert.Contains("Content-ID: 2",handler.Body);
    }

    [Theory]
    [InlineData("HTTP/1.1 409 Conflict")]
    [InlineData("HTTP/1.1 412 Precondition Failed")]
    [InlineData("HTTP/1.0 500 Internal Server Error")]
    public async Task RejectsEmbeddedOperationFailureAndForcesRollback(string embedded)
    {
        var handler=new Handler(_=>new(HttpStatusCode.OK){Content=new StringContent(embedded)});using var client=new HttpClient(handler){BaseAddress=new("https://example.crm.dynamics.com/api/data/v9.2/")};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>DataverseChangeSet.ExecuteAsync(client,[("POST","gaia_rows",new{id=1})],default));
    }

    [Fact]
    public async Task RejectsOuterBatchFailure()
    {
        var handler=new Handler(_=>new(HttpStatusCode.Conflict){Content=new StringContent("duplicate alternate key")});using var client=new HttpClient(handler){BaseAddress=new("https://example.crm.dynamics.com/api/data/v9.2/")};
        await Assert.ThrowsAsync<InvalidOperationException>(()=>DataverseChangeSet.ExecuteAsync(client,[("POST","gaia_rows",new{id=1})],default));
    }

    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> reply):HttpMessageHandler
    {
        public string Body {get;private set;}="";public string ContentType {get;private set;}="";public string Path {get;private set;}="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Path=request.RequestUri!.ToString();ContentType=request.Content?.Headers.ContentType?.ToString()??"";Body=request.Content is null?"":await request.Content.ReadAsStringAsync(token);return reply(request);}
    }
}
