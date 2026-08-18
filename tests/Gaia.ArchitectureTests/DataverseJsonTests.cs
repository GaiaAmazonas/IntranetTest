using System.Text.Json;
using Gaia.Api.Infrastructure.Dataverse;

namespace Gaia.ArchitectureTests;

public sealed class DataverseJsonTests
{
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
}
