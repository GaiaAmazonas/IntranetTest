using System.Text.Json;
using Gaia.Api.Infrastructure.Dataverse;

namespace Gaia.ArchitectureTests;

public sealed class DataverseJsonTests
{
    [Fact]
    public void OptionalEncodedInt32ReadsDataverseTextClassification()
    {
        using var document = JsonDocument.Parse("{\"gaia_tipocorreo\":\"2\"}");

        Assert.Equal(2, DataverseJson.OptionalEncodedInt32(document.RootElement, "gaia_tipocorreo"));
    }
    [Fact]
    public async Task ReadPageReturnsRowsAndDataverseCount()
    {
        var handler = new StubHandler("""{"@odata.count":171,"value":[{"gaia_terceroid":"11111111-1111-1111-1111-111111111111"}]}""");
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };

        var result = await DataverseJson.ReadPageAsync(client, "gaia_terceros?$count=true&$top=24", default);

        Assert.Equal(171, result.Total);
        Assert.Single(result.Items);
    }

    [Fact]
    public void OptionalInt32ReturnsNullForDataverseNull()
    {
        using var document = JsonDocument.Parse("""{"gaia_ordenvisual":null}""");

        var result = DataverseJson.OptionalInt32(document.RootElement, "gaia_ordenvisual");

        Assert.Null(result);
    }

    [Fact]
    public void OptionalInt32ReturnsNullForMissingProperty()
    {
        using var document = JsonDocument.Parse("{}");

        var result = DataverseJson.OptionalInt32(document.RootElement, "gaia_ordenvisual");

        Assert.Null(result);
    }

    [Fact]
    public void OptionalInt32ReturnsNumberWhenPresent()
    {
        using var document = JsonDocument.Parse("""{"gaia_ordenvisual":40}""");

        var result = DataverseJson.OptionalInt32(document.RootElement, "gaia_ordenvisual");

        Assert.Equal(40, result);
    }

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
    }
}
