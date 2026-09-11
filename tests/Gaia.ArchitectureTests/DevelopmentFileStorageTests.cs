using System.Security.Cryptography;
using Gaia.Api.Infrastructure.Files;
using Gaia.BuildingBlocks.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Gaia.ArchitectureTests;

public sealed class DevelopmentFileStorageTests
{
    [Fact]
    public void ExplicitDevelopmentProviderWinsOverPersistedSharePointSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "gaia-storage-tests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileStorage:Development:Enabled"] = "true",
            ["FileStorage:SharePoint:Enabled"] = "true",
            ["FileStorage:SharePoint:ConfigurationSource"] = "Dataverse"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestEnvironment(root));
        services.AddGaiaFileStorage(configuration, "Development");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<DevelopmentFileStorage>(scope.ServiceProvider.GetRequiredService<IFileStorage>());
        Assert.IsType<DevelopmentFileStorage>(scope.ServiceProvider.GetRequiredService<IFileStorageDiagnostics>());
    }

    [Fact]
    public async Task UploadedFileAndMetadataSurviveAProviderInstanceRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "gaia-storage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var environment = new TestEnvironment(root);
            var first = new DevelopmentFileStorage(environment);
            var content = "%PDF-1.4\nGaia Helpdesk observation evidence\n%%EOF"u8.ToArray();
            var fileId = Guid.NewGuid();
            var upload = new FileUpload(new(Guid.NewGuid().ToString("D")), "evidencia-observacion.pdf",
                "application/pdf", content.Length, fileId, DateTimeOffset.UtcNow);

            var stored = await first.UploadAsync(upload, new MemoryStream(content), default);

            Assert.Equal("SharePoint", stored.Id.Provider);
            Assert.Equal("development-local", stored.Id.RepositoryId);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), stored.Sha256);
            Assert.True(File.Exists(Path.Combine(root, "App_Data", "helpdesk-files", stored.StoredName)));

            var restarted = new DevelopmentFileStorage(environment);
            var metadata = await restarted.GetMetadataAsync(stored.Id, default);
            Assert.Equal(stored, metadata);
            await using (var download = await restarted.DownloadAsync(stored.Id, default))
            {
                using var output = new MemoryStream();
                await download.Content.CopyToAsync(output);
                Assert.Equal(content, output.ToArray());
            }

            await restarted.DeletePhysicallyAsync(new(stored.Id, stored.ETag, "Test cleanup"), default);
            Assert.False(await restarted.ExistsAsync(stored.Id, default));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Gaia.Api";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
