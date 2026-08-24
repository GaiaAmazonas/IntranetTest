using System.Net;
using System.Text;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.ThirdParties;
using Gaia.Modules.ThirdParties;

namespace Gaia.ArchitectureTests;

public sealed class DataverseCollaboratorContactTests
{
    [Fact]
    public async Task EmailCreateUsesMetadataRelationshipAndClearsPreviousPrimary()
    {
        var parent = Guid.NewGuid(); var previous = Guid.NewGuid(); var created = Guid.NewGuid();
        var handler = new QueueHandler(Json(EmailMetadata()), Json(ParentMetadata()), Entity(parent), Empty(),
            Json($"{{\"value\":[{{\"gaia_correocolaboradorid\":\"{previous:D}\"}}]}}"), NoContent(), Created(created));
        var store = new DataverseCollaboratorEmailStore(new Factory(handler));

        var result = await store.CreateAsync(parent, new("persona@gaia.org", null, true, true, "test"), default);

        Assert.Equal(RelatedWriteStatus.Created, result.Status);
        Assert.Contains(handler.Bodies, body => body.Contains("\"gaia_principal\":false", StringComparison.Ordinal));
        Assert.Contains(handler.Bodies, body => body.Contains("\"gaia_correoelectronico\":\"persona@gaia.org\"", StringComparison.Ordinal));
        Assert.Contains(handler.Bodies, body => body.Contains("\"gaia_observaciones\":null", StringComparison.Ordinal));
        Assert.Contains(handler.Bodies, body => body.Contains("\"gaia_principal\":true", StringComparison.Ordinal));
        Assert.Contains(handler.Bodies, body => body.Contains("\"gaia_tipocorreo\":1", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Bodies, body => body.Contains("\"gaia_tipocorreo\":\"1\"", StringComparison.Ordinal));
        Assert.Contains(handler.Bodies, body => body.Contains($"\"nav_tercero@odata.bind\":\"/gaia_terceroses({parent:D})\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EmailDuplicateForSameCollaboratorIsRejected()
    {
        var parent = Guid.NewGuid(); var handler = new QueueHandler(Json(EmailMetadata()), Json(ParentMetadata()), Entity(parent),
            Json($"{{\"value\":[{{\"gaia_correocolaboradorid\":\"{Guid.NewGuid():D}\"}}]}}"));
        var result = await new DataverseCollaboratorEmailStore(new Factory(handler))
            .CreateAsync(parent, new("persona@gaia.org", null, false, true, "test"), default);
        Assert.Equal(RelatedWriteStatus.Duplicate, result.Status);
    }

    [Fact]
    public async Task PhoneCreateResolvesChoiceAndNavigationFromMetadata()
    {
        var parent = Guid.NewGuid(); var handler = new QueueHandler(Json(PhoneMetadata()), Json(ParentMetadata()), Entity(parent), Empty(),
            Json("{\"OptionSet\":{\"Options\":[{\"Value\":7001,\"Label\":{\"UserLocalizedLabel\":{\"Label\":\"CELULAR\"}}}]}}"), Empty(), Created(Guid.NewGuid()));
        var result = await new DataverseCollaboratorPhoneStore(new Factory(handler))
            .CreateAsync(parent, new("3001234567", null, null, true, "CELULAR", true, "test"), default);

        Assert.Equal(RelatedWriteStatus.Created, result.Status);
        Assert.Contains(handler.Bodies, body => body.Contains("\"gaia_tipodetelefono\":7001", StringComparison.Ordinal));
        Assert.Contains(handler.Bodies, body => body.Contains("\"gaia_tipotelefono\":1", StringComparison.Ordinal));
        Assert.Contains(handler.Bodies, body => body.Contains($"\"nav_tercero@odata.bind\":\"/gaia_terceroses({parent:D})\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PhoneUpdateRejectsRecordOwnedByAnotherCollaborator()
    {
        var parent = Guid.NewGuid(); var other = Guid.NewGuid(); var phone = Guid.NewGuid();
        var handler = new QueueHandler(Json(PhoneMetadata()), Json(ParentMetadata()), Entity(parent),
            Json($"{{\"gaia_telefonocolaboradorid\":\"{phone:D}\",\"_gaia_tercero_value\":\"{other:D}\"}}"));
        var result = await new DataverseCollaboratorPhoneStore(new Factory(handler))
            .UpdateAsync(parent, phone, new("3001234567", null, null, false, "CELULAR", true, "test"), default);
        Assert.Equal(RelatedWriteStatus.NotFound, result.Status);
    }

    private static string EmailMetadata() => Metadata("gaia_correocolaboradors", "gaia_correocolaboradorid", "gaia_correoelectronico",
        "gaia_Correoelectronico", "gaia_Observaciones", "gaia_Principal", "gaia_Tercero", "gaia_Tipocorreo");
    private static string PhoneMetadata() => Metadata("gaia_telefonocolaboradors", "gaia_telefonocolaboradorid", "gaia_numero",
        "gaia_Numero", "gaia_Extension", "gaia_Observaciones", "gaia_Principal", "gaia_Tercero", "gaia_Tipodetelefono", "gaia_TipoTelefono");
    private static string ParentMetadata() => Metadata("gaia_terceroses", "gaia_tercerosid", "gaia_nombretercero", "gaia_Nombretercero");
    private static string Metadata(string set, string id, string name, params string[] schemas)
    {
        var attributes = string.Join(',', schemas.Select(x => $"{{\"SchemaName\":\"{x}\",\"LogicalName\":\"{x.ToLowerInvariant()}\",\"AttributeType\":\"{AttributeType(x)}\"}}"));
        var relations = schemas.Contains("gaia_Tercero", StringComparer.Ordinal) ? "[{\"ReferencingAttribute\":\"gaia_tercero\",\"ReferencingEntityNavigationPropertyName\":\"nav_tercero\",\"ReferencedEntity\":\"gaia_terceros\"}]" : "[]";
        return $"{{\"EntitySetName\":\"{set}\",\"PrimaryIdAttribute\":\"{id}\",\"PrimaryNameAttribute\":\"{name}\",\"Attributes\":[{attributes}],\"ManyToOneRelationships\":{relations}}}";
    }
    private static string AttributeType(string schema) => schema switch
    {
        "gaia_Tipocorreo" or "gaia_TipoTelefono" or "gaia_Tipodetelefono" => "Picklist",
        "gaia_Principal" => "Boolean",
        _ => "String"
    };
    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Empty() => Json("{\"value\":[]}");
    private static HttpResponseMessage Entity(Guid id) => Json($"{{\"gaia_tercerosid\":\"{id:D}\"}}");
    private static HttpResponseMessage NoContent() => new(HttpStatusCode.NoContent);
    private static HttpResponseMessage Created(Guid id) { var response = NoContent(); response.Headers.TryAddWithoutValidation("OData-EntityId", $"https://example/entity({id:D})"); return response; }
    private sealed class Factory(QueueHandler handler) : IDataverseDelegatedClientFactory
    { public Task<HttpClient> CreateAsync() => Task.FromResult(new HttpClient(handler) { BaseAddress = new Uri("https://example/api/data/v9.2/") }); }
    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> queue = new(responses);
        public List<string> Bodies { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { if (request.Content is not null) Bodies.Add(await request.Content.ReadAsStringAsync(token)); return queue.Dequeue(); }
    }
}
