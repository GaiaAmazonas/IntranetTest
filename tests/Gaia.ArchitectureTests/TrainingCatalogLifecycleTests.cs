namespace Gaia.ArchitectureTests;

public sealed class TrainingCatalogLifecycleTests
{
    private static readonly string Reader = Read("src", "Gaia.Api", "Infrastructure", "Dataverse", "Training", "DataverseTrainingAdministrationReader.cs");
    private static readonly string Endpoints = Read("src", "Modules", "Training", "Gaia.Modules.Training", "TrainingEndpoints.cs");

    [Fact]
    public void InactiveTrainingCanStillReceiveItsFirstVersion()
    {
        Assert.Contains("await RequireExists(client,training,value.TrainingId,\"La capacitación no existe.\",token)", Reader);
        Assert.DoesNotContain("await RequireActive(client,training,value.TrainingId", Reader);
    }

    [Fact]
    public void DeletionIsProtectedByAnIndependentPermission()
    {
        Assert.Contains("AdminCorePermissions.TrainingCatalogDelete", Endpoints);
        Assert.Contains("CAP.CATALOGO.ELIMINAR", Read("src", "Modules", "Security", "Gaia.Modules.Security", "SecurityContracts.cs"));
    }

    [Fact]
    public void TrainingWithAnyVersionCannotBeDeleted()
    {
        Assert.Contains("Task RemoveTrainingAsync", Read("src", "Modules", "Training", "Gaia.Modules.Training", "TrainingContracts.cs"));
        Assert.Contains("if(versions.Count>0)throw new InvalidOperationException", Reader);
        Assert.Contains("HttpMethod.Delete", Reader);
        Assert.Contains("Puedes conservarla inactiva", Reader);
    }

    private static string Read(params string[] path)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gaia.Platform.slnx"))) root = root.Parent;
        return File.ReadAllText(Path.Combine(new[] { root?.FullName ?? throw new DirectoryNotFoundException() }.Concat(path).ToArray()));
    }
}
