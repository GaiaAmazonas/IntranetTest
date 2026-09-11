using System.Net;
using System.Text.Json;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Files;
using Gaia.BuildingBlocks.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.ArchitectureTests;

public sealed class SharePointRepositoryConfigurationTests
{
    private const string Site = "tenant.sharepoint.com,site,web";
    private static readonly Guid RepositoryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly string[] Fields = ["gaia_siteid", "gaia_driveid", "gaia_carpetaraiz", "gaia_tenantid", "gaia_clientid",
        "gaia_metodoautenticacio", "gaia_referenciacredencial", "gaia_nombrebiblioteca", "gaia_urlsitio",
        "gaia_hoststransferencia", "gaia_estadooperativo", "gaia_entorno", "gaia_proveedor", "gaia_predeterminado"];
    private static SharePointRepositorySettings Row => new(RepositoryId, Site, "drive", "Gaia/Files",
        "11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", 2,
        "GaiaTest", "Documents", "https://tenant.sharepoint.com/sites/Gaia", "tenant.sharepoint.com", 2);

    [Theory]
    [InlineData("Development", 1)] [InlineData("Staging", 2)] [InlineData("Testing", 2)] [InlineData("Production", 3)]
    public void EnvironmentMatchesSuppliedChoices(string name, int value) =>
        Assert.Equal(value, DataverseSharePointConfigurationReader.EnvironmentValue(name));

    [Fact]
    public void UnknownEnvironmentDoesNotFallBackToProduction() => Assert.Throws<FileStorageException>(() =>
        DataverseSharePointConfigurationReader.EnvironmentValue("Unknown"));

    [Fact]
    public void RepositoryMapsToValidatedOptionsWithoutCopyingSecrets()
    {
        var (options, prefix) = RepositoryStorageConfiguration.Build(Row, Server(), "Development");
        Assert.Equal("FileStorage:Credentials:GaiaTest:", prefix); Assert.Equal(Row.SiteId, options.SiteId);
        Assert.Equal(Row.DriveId, options.DriveId); Assert.Equal(1000, options.MaximumFileBytes);
        Assert.Equal(StorageAuthenticationMethod.ManagedIdentity, options.AuthenticationMethod);
        Assert.False(options.UseSystemAssignedManagedIdentity);
    }

    [Theory]
    [InlineData("alias")] [InlineData("tenant")] [InlineData("host")] [InlineData("site")]
    [InlineData("method5")] [InlineData("url")] [InlineData("systemIdentity")]
    public void UntrustedOrInvalidRepositoryIsRejected(string change)
    {
        var row = change switch
        {
            "alias" => Row with { CredentialReference = "../../secret" },
            "tenant" => Row with { TenantId = Guid.NewGuid().ToString() },
            "host" => Row with { TransferHosts = "untrusted.example" },
            "site" => Row with { SiteId = "other.sharepoint.com,site,web" },
            "method5" => Row with { AuthenticationMethod = 5 },
            "url" => Row with { SiteUrl = "https://other.example/sites/Gaia" },
            _ => Row with { AuthenticationMethod = 1 }
        };
        Assert.Throws<FileStorageException>(() => RepositoryStorageConfiguration.Build(row, Server(), "Development"));
    }

    [Fact]
    public void ClientSecretIsRejectedOutsideDevelopment()
    {
        var config = Server(); config["FileStorage:Credentials:GaiaTest:AuthenticationMethod"] = "ClientSecret";
        Assert.Throws<FileStorageException>(() => RepositoryStorageConfiguration.Build(Row with { AuthenticationMethod = 4 }, config, "Production"));
    }

    [Theory]
    [InlineData(1, true, false)] [InlineData(2, true, true)] [InlineData(3, true, false)]
    [InlineData(4, true, false)] [InlineData(5, true, false)]
    [InlineData(1, false, false)] [InlineData(2, false, true)] [InlineData(3, false, true)]
    [InlineData(4, false, false)] [InlineData(5, false, false)]
    public void OperationalStateControlsWritesAndReads(int state, bool write, bool allowed)
    {
        var error = Record.Exception(() => DataverseConfiguredFileStorage.ValidateState(state, write));
        if (allowed) Assert.Null(error); else Assert.IsType<FileStorageException>(error);
    }

    [Fact]
    public async Task ReaderUsesMetadataEntitySetAndConfirmedChoiceValues()
    {
        using var handler = new DataverseHandler(1);
        var reader = new DataverseSharePointConfigurationReader(new Clients(handler), Server(), new Environment());
        var row = await reader.ReadAsync(null, default);
        Assert.Equal(RepositoryId, row.Id); Assert.Equal(2, row.AuthenticationMethod);
        var path = Uri.UnescapeDataString(handler.Paths.Last());
        Assert.Contains("actual_entity_set?", path, StringComparison.Ordinal);
        Assert.Contains("gaia_entorno eq 1", path, StringComparison.Ordinal);
        Assert.Contains("gaia_proveedor eq 1", path, StringComparison.Ordinal);
        Assert.Contains("gaia_predeterminado eq true", path, StringComparison.Ordinal);
        Assert.Contains("$top=2", path, StringComparison.Ordinal); Assert.DoesNotContain("$skip", path, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)] [InlineData(2)]
    public async Task MissingOrDuplicateDefaultsFailClosed(int count)
    {
        using var handler = new DataverseHandler(count);
        var reader = new DataverseSharePointConfigurationReader(new Clients(handler), Server(), new Environment());
        await Assert.ThrowsAsync<FileStorageException>(() => reader.ReadAsync(null, default));
    }

    [Fact]
    public async Task ExistingFileResolvesItsOriginalRepositoryNotTheNewDefault()
    {
        using var handler = new DataverseHandler(1);
        var reader = new DataverseSharePointConfigurationReader(new Clients(handler), Server(), new Environment());
        await reader.ReadAsync(new("SharePoint", Site, "old'drive", "file"), default);
        var path = Uri.UnescapeDataString(handler.Paths.Last());
        Assert.Contains("gaia_driveid eq 'old''drive'", path, StringComparison.Ordinal);
        Assert.DoesNotContain("gaia_predeterminado eq true", path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitRepositoryEnvironmentAllowsLocalValidationOfStaging()
    {
        using var handler = new DataverseHandler(1);
        var config = Server(); config["FileStorage:SharePoint:RepositoryEnvironment"] = "2";
        var reader = new DataverseSharePointConfigurationReader(new Clients(handler), config, new Environment());
        await reader.ReadAsync(null, default);
        Assert.Contains("gaia_entorno eq 2", Uri.UnescapeDataString(handler.Paths.Last()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")] [InlineData("4")]
    public async Task InvalidExplicitRepositoryEnvironmentFailsBeforeDataverse(string value)
    {
        using var handler = new DataverseHandler(1);
        var config = Server(); config["FileStorage:SharePoint:RepositoryEnvironment"] = value;
        var reader = new DataverseSharePointConfigurationReader(new Clients(handler), config, new Environment());
        await Assert.ThrowsAsync<FileStorageException>(() => reader.ReadAsync(null, default));
        Assert.Empty(handler.Paths);
    }

    [Fact]
    public async Task SuccessfulProbeResultIsPersistedWithoutProviderDetails()
    {
        using var handler = new ValidationHandler();
        var clients = new Clients(handler);
        var config = Server();
        var reader = new DataverseSharePointConfigurationReader(clients, config, new Environment());
        var recorder = new DataverseSharePointValidationRecorder(reader, clients, config);
        await recorder.RecordAsync(3, "Lectura y escritura verificadas por la prueba controlada.", default);
        Assert.NotNull(handler.Payload);
        Assert.Equal(3, handler.Payload!.RootElement.GetProperty("gaia_resultadovalidacion").GetInt32());
        Assert.Equal("Lectura y escritura verificadas por la prueba controlada.",
            handler.Payload.RootElement.GetProperty("gaia_detallevalidacion").GetString());
        Assert.True(handler.Payload.RootElement.TryGetProperty("gaia_ultimavalidacion", out _));
    }

    [Fact]
    public void ScreenshotExtraAuthenticationOptionIsRejected()
    {
        var row = Data(); row["gaia_metodoautenticacio"] = 5;
        Assert.Throws<FileStorageException>(() => DataverseSharePointConfigurationReader.Map(JsonSerializer.SerializeToElement(row),
            "gaia_configuracionsharepointid", Fields.ToDictionary(field => field)));
    }

    [Fact]
    public async Task DataverseRegistrationBuildsWithoutServerDestinationAndDoesNotReadAtStartup()
    {
        using var handler = new DataverseHandler(1);
        var config = Server(); config["FileStorage:SharePoint:ConfigurationSource"] = "Dataverse";
        var services = new ServiceCollection(); services.AddLogging(); services.AddRouting();
        services.AddSingleton<IConfiguration>(config); services.AddSingleton<IHostEnvironment>(new Environment());
        services.AddSingleton<IDataverseDelegatedClientFactory>(new Clients(handler));
        services.AddGaiaFileStorage(config, "Development");
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        Assert.IsType<DataverseConfiguredFileStorage>(scope.ServiceProvider.GetRequiredService<IFileStorage>());
        var report = await scope.ServiceProvider.GetRequiredService<IFileStorageDiagnostics>().CheckAsync(default);
        Assert.Equal("MissingConfiguration", report.ErrorCode);
        Assert.Empty(handler.Paths);
    }

    private static IConfigurationRoot Server()
    {
        const string storage = "FileStorage:SharePoint:";
        const string credential = "FileStorage:Credentials:GaiaTest:";
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [storage + "MaximumFileBytes"] = "1000", [storage + "SimpleUploadThresholdBytes"] = "100",
            [storage + "AllowedExtensions:0"] = ".pdf", [storage + "AllowedMimeTypes:0"] = "application/pdf",
            [credential + "TenantId"] = Row.TenantId, [credential + "ClientId"] = Row.ClientId,
            [credential + "AuthenticationMethod"] = "ManagedIdentity", [credential + "AllowedSiteIds:0"] = Site,
            [credential + "AllowedTransferHosts:0"] = "tenant.sharepoint.com"
        }).Build();
    }

    private static Dictionary<string, object?> Data() => new()
    {
        ["gaia_configuracionsharepointid"] = RepositoryId, ["gaia_siteid"] = Row.SiteId, ["gaia_driveid"] = Row.DriveId,
        ["gaia_carpetaraiz"] = Row.RootFolder, ["gaia_tenantid"] = Row.TenantId, ["gaia_clientid"] = Row.ClientId,
        ["gaia_metodoautenticacio"] = Row.AuthenticationMethod, ["gaia_referenciacredencial"] = Row.CredentialReference,
        ["gaia_nombrebiblioteca"] = Row.LibraryName, ["gaia_urlsitio"] = Row.SiteUrl,
        ["gaia_hoststransferencia"] = Row.TransferHosts, ["gaia_estadooperativo"] = Row.OperationalState
    };
    private sealed class DataverseHandler(int count) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method); Paths.Add(request.RequestUri!.PathAndQuery);
            object data = Paths.Count == 1 ? new
            {
                EntitySetName = "actual_entity_set", PrimaryIdAttribute = "gaia_configuracionsharepointid", PrimaryNameAttribute = "gaia_nombre",
                Attributes = Fields.Select(field => new { LogicalName = field, SchemaName = field, AttributeType = "String" }),
                ManyToOneRelationships = Array.Empty<object>()
            } : new { value = Enumerable.Range(0, count).Select(_ => Data()) };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data)) });
        }
    }
    private sealed class ValidationHandler : HttpMessageHandler
    {
        public JsonDocument? Payload { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Patch)
            {
                Payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                Assert.Contains($"({RepositoryId:D})", request.RequestUri!.OriginalString, StringComparison.Ordinal);
                Assert.Equal("*", request.Headers.IfMatch.Single().Tag);
                return new(HttpStatusCode.NoContent);
            }
            object data = request.RequestUri!.OriginalString.Contains("EntityDefinitions", StringComparison.Ordinal)
                ? new
                {
                    EntitySetName = "actual_entity_set", PrimaryIdAttribute = "gaia_configuracionsharepointid", PrimaryNameAttribute = "gaia_nombre",
                    Attributes = Fields.Concat(["gaia_resultadovalidacion", "gaia_detallevalidacion", "gaia_ultimavalidacion"])
                        .Select(field => new { LogicalName = field, SchemaName = field, AttributeType = "String" }),
                    ManyToOneRelationships = Array.Empty<object>()
                }
                : new { value = new[] { Data() } };
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(data)) };
        }

        protected override void Dispose(bool disposing) { if (disposing) Payload?.Dispose(); base.Dispose(disposing); }
    }
    private sealed class Clients(HttpMessageHandler handler) : IDataverseDelegatedClientFactory
    {
        public Task<HttpClient> CreateAsync() => Task.FromResult(new HttpClient(handler, false) { BaseAddress = new("https://tenant.crm.dynamics.com/api/data/v9.2/") });
    }
    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
