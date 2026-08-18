using System.Net;
using System.Text;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.Organization;
using Gaia.Modules.Organization;

namespace Gaia.ArchitectureTests;

public sealed class DataverseOrganizationUnitUpdateTests
{
    [Fact]
    public async Task UpdateRejectsHierarchyCycleWithoutPatching()
    {
        var id = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var handler = new QueueHandler(
            Relationships(),
            Hierarchy((id, null, 2), (childId, id, 3)),
            Json(HttpStatusCode.OK, "{\"value\":[]}"),
            Json(HttpStatusCode.OK, "{\"gaia_activo\":true,\"statecode\":0}"));

        var result = await Updater(handler).UpdateAsync(
            id, Command(parentId: childId), CancellationToken.None);

        Assert.Equal(OrganizationUnitUpdateStatus.HierarchyCycle, result.Status);
        Assert.DoesNotContain(HttpMethod.Patch, handler.Methods);
    }

    [Fact]
    public async Task ChangingParentUsesNavigationPropertiesAndRecalculatesDescendants()
    {
        var id = Guid.NewGuid();
        var newParentId = Guid.NewGuid();
        var childId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var handler = new QueueHandler(
            Relationships(),
            Hierarchy((id, null, 1), (newParentId, null, 1), (childId, id, 2)),
            Json(HttpStatusCode.OK, "{\"value\":[]}"),
            Json(HttpStatusCode.OK, "{\"gaia_activo\":true,\"statecode\":0}"),
            Json(HttpStatusCode.OK, "{\"gaia_codigo\":\"BOG\",\"statecode\":0}"),
            NoContent(),
            NoContent());

        var result = await Updater(handler).UpdateAsync(
            id, Command(typeId, newParentId, siteId), CancellationToken.None);

        Assert.Equal(OrganizationUnitUpdateStatus.Updated, result.Status);
        var mainPayload = handler.Bodies[5];
        Assert.Contains($"\"nav_tipo@odata.bind\":\"/gaia_tipounidadorganizacionals({typeId:D})\"", mainPayload);
        Assert.Contains($"\"nav_padre@odata.bind\":\"/gaia_organizacions({newParentId:D})\"", mainPayload);
        Assert.Contains($"\"nav_sede@odata.bind\":\"/gaia_sedes({siteId:D})\"", mainPayload);
        Assert.Contains("\"gaia_nivel\":2", mainPayload);
        Assert.Contains("\"gaia_nivel\":3", handler.Bodies[6]);
        Assert.Equal(2, handler.Methods.Count(method => method == HttpMethod.Patch));
    }

    [Fact]
    public async Task UpdateRejectsNonBogotaSite()
    {
        var id = Guid.NewGuid();
        var handler = new QueueHandler(
            Relationships(),
            Hierarchy((id, null, 1)),
            Json(HttpStatusCode.OK, "{\"value\":[]}"),
            Json(HttpStatusCode.OK, "{\"gaia_activo\":true,\"statecode\":0}"),
            Json(HttpStatusCode.OK, "{\"gaia_codigo\":\"LET\",\"statecode\":0}"));

        var result = await Updater(handler).UpdateAsync(
            id, Command(siteId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(OrganizationUnitUpdateStatus.SiteNotFound, result.Status);
        Assert.DoesNotContain(HttpMethod.Patch, handler.Methods);
    }

    private static DataverseOrganizationUnitUpdater Updater(HttpMessageHandler handler) =>
        new(new StubFactory(handler));

    private static OrganizationUnitUpdateCommand Command(
        Guid? typeId = null, Guid? parentId = null, Guid? siteId = null) => new(
        "3001", "Unidad", "Unidad", typeId ?? Guid.NewGuid(), parentId,
        siteId ?? Guid.NewGuid(), null, 10, new DateOnly(2021, 1, 1), null, true, "user");

    private static HttpResponseMessage Relationships() => Json(HttpStatusCode.OK,
        "{\"value\":[" +
        "{\"ReferencingAttribute\":\"gaia_tipounidadorganizacional\",\"ReferencingEntityNavigationPropertyName\":\"nav_tipo\",\"ReferencedEntity\":\"gaia_tipounidadorganizacional\"}," +
        "{\"ReferencingAttribute\":\"gaia_unidadpadre\",\"ReferencingEntityNavigationPropertyName\":\"nav_padre\",\"ReferencedEntity\":\"gaia_organizacion\"}," +
        "{\"ReferencingAttribute\":\"gaia_sede\",\"ReferencingEntityNavigationPropertyName\":\"nav_sede\",\"ReferencedEntity\":\"gaia_sede\"}]}");

    private static HttpResponseMessage Hierarchy(params (Guid Id, Guid? ParentId, int Level)[] units)
    {
        var values = units.Select(unit =>
            $"{{\"gaia_organizacionid\":\"{unit.Id:D}\",\"gaia_nivel\":{unit.Level}," +
            $"\"_gaia_unidadpadre_value\":{(unit.ParentId.HasValue ? $"\"{unit.ParentId.Value:D}\"" : "null")}}}");
        return Json(HttpStatusCode.OK, $"{{\"value\":[{string.Join(',', values)}]}}");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string content) => new(status)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);

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
