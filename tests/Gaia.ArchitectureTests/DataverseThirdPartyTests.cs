using Gaia.Api.Infrastructure.Dataverse.ThirdParties;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Modules.ThirdParties;
using System.Net;
using System.Text;

namespace Gaia.ArchitectureTests;

public sealed class DataverseThirdPartyTests
{
    [Theory]
    [InlineData("1987-06-15", 1987, 6, 15)]
    [InlineData("1987-06-15T00:00:00Z", 1987, 6, 15)]
    public void BirthDateAcceptsDataverseDateFormats(string value, int year, int month, int day)
    {
        var result = DataverseThirdPartyStore.ParseDateOnly(value);

        Assert.Equal(new DateOnly(year, month, day), result);
    }

    [Fact]
    public void BirthDateRemainsOptional() => Assert.Null(DataverseThirdPartyStore.ParseDateOnly(null));

    [Fact]
    public void FullNameUsesNamesAndSurnamesInBusinessOrder()
    {
        var name = DataverseThirdPartyStore.BuildFullName(Command(
            "Edgar", "Eduardo", "Munar", "Guevara"));

        Assert.Equal("Edgar Eduardo Munar Guevara", name);
    }

    [Fact]
    public void FullNameOmitsMissingOptionalPartsWithoutDoubleSpaces()
    {
        var name = DataverseThirdPartyStore.BuildFullName(Command(
            " Edgar ", null, " Munar ", " "));

        Assert.Equal("Edgar Munar", name);
    }

    [Fact]
    public async Task CreateUsesMetadataNavigationPropertyAndDerivedName()
    {
        var createdId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var handler = new QueueHandler(
            Json(Metadata("gaia_terceroses", "gaia_tercerosid", "gaia_nombretercero", true)),
            Json(Metadata("gaia_tipodocumentos", "gaia_tipodocumentoid", "gaia_nombre", false)),
            Json($"{{\"gaia_tipodocumentoid\":\"{typeId:D}\",\"statecode\":0}}"),
            Json("{\"OptionSet\":{\"Options\":[{\"Value\":1,\"Label\":{\"UserLocalizedLabel\":{\"Label\":\"MASCULINO\"}}},{\"Value\":2,\"Label\":{\"UserLocalizedLabel\":{\"Label\":\"FEMENINO\"}}}]}}"),
            Json("{\"value\":[]}"),
            Created(createdId));
        var store = new DataverseThirdPartyStore(new Factory(handler));

        var result = await store.CreateAsync(Command(
            "Edgar", "Eduardo", "Munar", "Guevara") with { DocumentTypeId = typeId },
            CancellationToken.None);

        Assert.Equal(ThirdPartyWriteStatus.Created, result.Status);
        Assert.Equal(createdId, result.Id);
        Assert.Contains("\"gaia_nombretercero\":\"Edgar Eduardo Munar Guevara\"", handler.LastBody);
        Assert.Contains($"\"nav_tipo_documento@odata.bind\":\"/gaia_tipodocumentos({typeId:D})\"", handler.LastBody);
    }

    private static ThirdPartyWriteCommand Command(
        string firstName, string? middleName, string firstSurname, string? secondSurname) => new(
            Guid.NewGuid(), "123", firstName, middleName, firstSurname, secondSurname,
            "MASCULINO", null, null, true, "test");

    private static string Metadata(string set, string id, string name, bool thirdParty)
    {
        var schemas = thirdParty
            ? new[] { "gaia_Nombretercero", "gaia_TipoDocumento", "gaia_NumeroDocumento", "gaia_PrimerNombre", "gaia_SegundoNombre", "gaia_PrimerApellido", "gaia_SegundoApellido", "gaia_Sexo", "gaia_FechaNacimiento", "gaia_Observaciones" }
            : Array.Empty<string>();
        var attributes = string.Join(',', schemas.Select(schema =>
            $"{{\"SchemaName\":\"{schema}\",\"LogicalName\":\"{schema.ToLowerInvariant()}\"}}"));
        var relationships = thirdParty
            ? "[{\"ReferencingAttribute\":\"gaia_tipodocumento\",\"ReferencingEntityNavigationPropertyName\":\"nav_tipo_documento\",\"ReferencedEntity\":\"gaia_tipodocumento\"}]"
            : "[]";
        return $"{{\"EntitySetName\":\"{set}\",\"PrimaryIdAttribute\":\"{id}\",\"PrimaryNameAttribute\":\"{name}\",\"Attributes\":[{attributes}],\"ManyToOneRelationships\":{relationships}}}";
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Created(Guid id)
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);
        response.Headers.TryAddWithoutValidation("OData-EntityId", $"https://example/gaia_terceroses({id:D})");
        return response;
    }

    private sealed class Factory(QueueHandler handler) : IDataverseDelegatedClientFactory
    {
        public Task<HttpClient> CreateAsync() => Task.FromResult(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example/api/data/v9.2/")
        });
    }

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> queue = new(responses);
        public string LastBody { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null) LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            return queue.Dequeue();
        }
    }
}
