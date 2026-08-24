using Gaia.Api.Infrastructure.Dataverse.ThirdParties;

namespace Gaia.ArchitectureTests;

public sealed class OrganizationalAssignmentTests
{
    [Fact]
    public void GeneratedAssignmentNameRespectsDataverseMaximumLength()
    {
        var name = DataverseOrganizationalAssignmentStore.AssignmentName(
            "COLABORADOR CON UN NOMBRE EXTENSO QUE DEBE CONSERVARSE EN EL REGISTRO",
            new string('C', 140));

        Assert.Equal(150, name.Length);
        Assert.StartsWith("COLABORADOR CON UN NOMBRE EXTENSO", name);
    }

    [Fact]
    public void GeneratedAssignmentNameKeepsShortNamesUnchanged()
    {
        Assert.Equal("Edgar Munar · Líder TI",
            DataverseOrganizationalAssignmentStore.AssignmentName("Edgar Munar", "Líder TI"));
    }
}
