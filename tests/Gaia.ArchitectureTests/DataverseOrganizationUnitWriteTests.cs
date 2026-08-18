using System.Net;
using System.Text;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.Organization;
using Gaia.Modules.Organization;

namespace Gaia.ArchitectureTests;

public sealed class DataverseOrganizationUnitWriteTests
{
    [Fact]
    public async Task CreateValidatesDuplicateCodeBeforePosting()
    {
        var handler = new QueueHandler(Json(HttpStatusCode.OK,
            $"{{\"value\":[{{\"gaia_organizacionid\":\"{Guid.NewGuid():D}\"}}]}}"));
        var result = await Creator(handler).CreateAsync(Command(), CancellationToken.None);

        Assert.Equal(OrganizationUnitCreateStatus.DuplicateCode, result.Status);
        Assert.Single(handler.Methods);
        Assert.Equal(HttpMethod.Get, handler.Methods[0]);
    }

    [Fact]
    public async Task CreateRejectsInactiveUnitTypeWithoutPosting()
    {
        var handler = new QueueHandler(
            Json(HttpStatusCode.OK, "{\"value\":[]}"),
            Json(HttpStatusCode.OK, "{\"gaia_activo\":false,\"statecode\":0}"));
        var result = await Creator(handler).CreateAsync(Command(), CancellationToken.None);

        Assert.Equal(OrganizationUnitCreateStatus.InvalidUnitType, result.Status);
        Assert.Equal(2, handler.Methods.Count);
    }

    [Fact]
    public async Task CreateRootPersistsContractAndUsesDataverseLevelOne()
    {
        var createdId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var handler = new QueueHandler(
            Json(HttpStatusCode.OK, "{\"value\":[]}"),
            Json(HttpStatusCode.OK, "{\"gaia_activo\":true,\"statecode\":0}"),
            Relationships(),
            Json(HttpStatusCode.OK, "{\"EntitySetName\":\"gaia_sedes\"}"),
            Json(HttpStatusCode.OK, "{\"gaia_codigo\":\"BOG\",\"statecode\":0}"),
            Created(createdId));

        var result = await Creator(handler).CreateAsync(
            Command(siteId: siteId), CancellationToken.None);

        Assert.Equal(OrganizationUnitCreateStatus.Created, result.Status);
        Assert.Equal(createdId, result.Id);
        var body = handler.Bodies[^1];
        Assert.Contains("\"gaia_codigo\":\"NVA\"", body);
        Assert.Contains("\"gaia_nombrecorto\":\"Nueva\"", body);
        Assert.Contains("\"gaia_ordenvisual\":25", body);
        Assert.Contains("\"gaia_nivel\":1", body);
        Assert.Contains("\"gaia_fechainiciovigencia\":\"2026-08-10\"", body);
        Assert.Contains("nav_tipo_unidad@odata.bind", body);
    }

    [Fact]
    public async Task CreateWithRelationshipsUsesODataNavigationPropertiesForAllLookups()
    {
        var parentId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var handler = new QueueHandler(
            Json(HttpStatusCode.OK, "{\"value\":[]}"),
            Json(HttpStatusCode.OK, "{\"gaia_activo\":true,\"statecode\":0}"),
            Relationships(),
            Json(HttpStatusCode.OK, "{\"gaia_nivel\":3}"),
            Json(HttpStatusCode.OK, "{\"EntitySetName\":\"gaia_sedes\"}"),
            Json(HttpStatusCode.OK, "{\"gaia_codigo\":\"BOG\",\"statecode\":0}"),
            Created(Guid.NewGuid()));

        var result = await Creator(handler).CreateAsync(
            Command(parentId, siteId), CancellationToken.None);

        Assert.Equal(OrganizationUnitCreateStatus.Created, result.Status);
        var body = handler.Bodies[^1];
        Assert.Contains("\"gaia_nivel\":4", body);
        Assert.Contains($"\"nav_tipo_unidad@odata.bind\":\"/gaia_tipounidadorganizacionals(", body);
        Assert.Contains($"\"nav_unidad_padre@odata.bind\":\"/gaia_organizacions({parentId:D})\"", body);
        Assert.Contains($"\"nav_sede@odata.bind\":\"/gaia_sedes({siteId:D})\"", body);
        Assert.DoesNotContain("\"gaia_tipounidad@odata.bind\"", body);
        Assert.DoesNotContain("\"gaia_unidadpadre@odata.bind\"", body);
        Assert.DoesNotContain("\"gaia_sede@odata.bind\"", body);
    }

    private static DataverseOrganizationUnitCreator Creator(HttpMessageHandler handler) =>
        new(new StubFactory(handler));

    private static OrganizationUnitCreateCommand Command(
        Guid? parentId = null,
        Guid? siteId = null) => new(
        "NVA", "Nueva unidad", "Nueva", Guid.NewGuid(), parentId, siteId,
        "Descripción", 25, new DateOnly(2026, 8, 10), null, true, "user@gaia.org");

    private static HttpResponseMessage Relationships() => Json(HttpStatusCode.OK,
        "{\"value\":[" +
        "{\"ReferencingAttribute\":\"gaia_tipounidadorganizacional\",\"ReferencingEntityNavigationPropertyName\":\"nav_tipo_unidad\",\"ReferencedEntity\":\"gaia_tipounidadorganizacional\"}," +
        "{\"ReferencingAttribute\":\"gaia_unidadpadre\",\"ReferencingEntityNavigationPropertyName\":\"nav_unidad_padre\",\"ReferencedEntity\":\"gaia_organizacion\"}," +
        "{\"ReferencingAttribute\":\"gaia_sede\",\"ReferencingEntityNavigationPropertyName\":\"nav_sede\",\"ReferencedEntity\":\"gaia_sede\"}" +
        "]}");

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Created(Guid id)
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        response.Headers.TryAddWithoutValidation("OData-EntityId",
            $"https://example.test/api/data/v9.2/gaia_organizacions({id:D})");
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
            Bodies.Add(request.Content is null ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return queue.Dequeue();
        }
    }
}
