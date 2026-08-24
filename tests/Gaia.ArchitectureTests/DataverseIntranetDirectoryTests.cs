using Gaia.Api.Infrastructure.Dataverse;

namespace Gaia.ArchitectureTests;

public sealed class DataverseIntranetDirectoryTests
{
    [Fact]
    public void ChoiceContactTypeUsesUnquotedODataInteger()
    {
        var metadata = Metadata("Picklist");

        Assert.Equal("2", metadata.EncodedIntegerLiteral("gaia_Tipocorreo", 2));
        Assert.IsType<int>(metadata.EncodedIntegerValue("gaia_Tipocorreo", 2));
    }

    [Fact]
    public void LegacyStringContactTypeRemainsQuoted()
    {
        var metadata = Metadata("String");

        Assert.Equal("'2'", metadata.EncodedIntegerLiteral("gaia_Tipocorreo", 2));
        Assert.IsType<string>(metadata.EncodedIntegerValue("gaia_Tipocorreo", 2));
    }

    private static DataverseTableMetadata Metadata(string type) => new(
        "gaia_correocolaborador",
        "gaia_correocolaboradors",
        "gaia_correocolaboradorid",
        "gaia_correoelectronico",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gaia_Tipocorreo"] = "gaia_tipocorreo"
        },
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gaia_tipocorreo"] = type
        },
        []);
}
