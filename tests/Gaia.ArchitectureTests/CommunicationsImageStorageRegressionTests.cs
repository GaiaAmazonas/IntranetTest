namespace Gaia.ArchitectureTests;

public sealed class CommunicationsImageStorageRegressionTests
{
    private static readonly string StoreSource = File.ReadAllText(Path.Combine(
        FindRoot(), "src", "Gaia.Api", "Infrastructure", "Dataverse", "Communications",
        "DataverseCommunicationsStore.cs"));

    [Fact]
    public void HighlightImagesRequireExternalSharePointReferences()
    {
        Assert.Contains("HasSharePointReference(x,f.DesktopImage)", StoreSource, StringComparison.Ordinal);
        Assert.Contains("HasSharePointReference(x,f.MobileImage)", StoreSource, StringComparison.Ordinal);
        Assert.Contains("if(!TryDecode(Str(row,field),out var external))return null", StoreSource, StringComparison.Ordinal);
        Assert.DoesNotContain("/{field}/$value", StoreSource, StringComparison.Ordinal);
    }

    [Fact]
    public void DataverseConfigurationTakesPriorityOverLocalDevelopmentStorage()
    {
        var registration = File.ReadAllText(Path.Combine(FindRoot(), "src", "Gaia.Api", "Infrastructure",
            "Files", "FileStorageRegistration.cs"));
        Assert.Contains("source != \"Dataverse\"", registration, StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gaia.Platform.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
    }
}
