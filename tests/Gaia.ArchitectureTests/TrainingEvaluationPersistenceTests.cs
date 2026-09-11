namespace Gaia.ArchitectureTests;

public sealed class TrainingEvaluationPersistenceTests
{
    private static readonly string Source = File.ReadAllText(Path.Combine(
        FindRoot(), "src", "Gaia.Api", "Infrastructure", "Dataverse", "Training",
        "DataverseTrainingAdministrationReader.cs"));

    [Fact]
    public void EvaluationQueriesExcludeInactiveQuestionsAndOptions()
    {
        Assert.Contains("and statecode eq 0&$orderby={qf.Order} asc", Source);
        Assert.Contains("and statecode eq 0&$orderby={of.Order} asc", Source);
    }

    [Fact]
    public void YesNoQuestionsAreNormalizedAndProtected()
    {
        Assert.Contains("EnsureYesNoOptions", Source);
        Assert.Contains("Las respuestas Sí y No son obligatorias", Source);
    }

    [Fact]
    public void TrainingVideosSupportMp4AndValidatedYouTubeLinks()
    {
        var endpoints = File.ReadAllText(Path.Combine(FindRoot(), "src", "Modules", "Training", "Gaia.Modules.Training", "TrainingEndpoints.cs"));
        Assert.Contains("video/mp4", endpoints);
        Assert.Contains("IsYouTubeUrl", Source);
        Assert.Contains("Sube un archivo MP4 o escribe un enlace válido de YouTube", Source);
    }

    [Fact]
    public void NewSectionsUseAnOrderThatAlsoAccountsForInactiveHistory()
    {
        Assert.Contains("await NextSectionOrder(client,table,f,versionId,token)", Source);
        Assert.Contains("$select={f.Order}&$filter=_{f.VersionLookup}_value eq {versionId:D}", Source);
        Assert.DoesNotContain("$select={f.Order}&$filter=_{f.VersionLookup}_value eq {versionId:D} and statecode eq 0", Source);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Gaia.Platform.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz de la solución.");
    }
}
