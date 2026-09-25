using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

internal static class FileStorageRegistration
{
    public static IServiceCollection AddGaiaFileStorage(this IServiceCollection services, IConfiguration configuration, string environment)
    {
        var source = configuration["FileStorage:SharePoint:ConfigurationSource"] ?? "Server";
        var useDevelopmentStorage = source != "Dataverse"
            && environment.Equals("Development", StringComparison.OrdinalIgnoreCase)
            && configuration.GetValue("FileStorage:Development:Enabled", false);
        services.AddFileStorageDiagnostics(registerServer: source != "Dataverse" && !useDevelopmentStorage);
        services.AddSingleton(TimeProvider.System);
        // PublicLoginSnapshotStore always needs repository metadata when an administrator
        // publishes the login experience, regardless of the storage provider used for
        // ordinary files in the current environment.
        services.AddScoped<ISharePointRepositoryReader, DataverseSharePointConfigurationReader>();
        if (useDevelopmentStorage)
        {
            services.AddScoped<DevelopmentFileStorage>();
            services.AddScoped<IFileStorage>(provider => provider.GetRequiredService<DevelopmentFileStorage>());
            services.AddScoped<IFileStorageMaintenance>(provider => provider.GetRequiredService<DevelopmentFileStorage>());
            services.AddScoped<IFileStorageDiagnostics>(provider => provider.GetRequiredService<DevelopmentFileStorage>());
            return services;
        }
        services.AddHttpClient("GraphFiles", client => client.Timeout = TimeSpan.FromMinutes(10))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, UseCookies = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 16
            }).RemoveAllLoggers(); // Signed transfer URLs must never enter default HttpClient logs.
        if (source == "Dataverse")
        {
            services.AddScoped<ISharePointConfigurationValidationRecorder, DataverseSharePointValidationRecorder>();
            services.AddScoped<DataverseConfiguredFileStorage>();
            services.AddScoped<IFileStorage>(provider => provider.GetRequiredService<DataverseConfiguredFileStorage>());
            services.AddScoped<IFileStorageMaintenance>(provider => provider.GetRequiredService<DataverseConfiguredFileStorage>());
            services.AddScoped<IFileStorageDiagnostics>(provider => provider.GetRequiredService<DataverseConfiguredFileStorage>());
            services.AddScoped<IFileStorageMigration, FileStorageMigration>();
            return services;
        }
        if (source != "Server") throw new FileStorageException(FileStorageError.MissingConfiguration);
        var options = SharePointStorageConfiguration.From(configuration, environment);
        services.AddSingleton(options);
        // Loads credentials locally only; no token acquisition, Graph I/O or writes at startup.
        IGraphApplicationTokenProvider provider = options.Enabled
            ? GraphApplicationTokenProvider.Create(options, configuration) : new DisabledTokenProvider();
        services.AddSingleton(_ => provider);
        services.AddScoped(serviceProvider => new GraphFileTransport(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("GraphFiles"),
            serviceProvider.GetRequiredService<IGraphApplicationTokenProvider>(), options,
            serviceProvider.GetRequiredService<TimeProvider>()));
        services.AddScoped<SharePointFileStorage>();
        services.AddScoped<IFileStorage>(serviceProvider => serviceProvider.GetRequiredService<SharePointFileStorage>());
        services.AddScoped<IFileStorageMaintenance>(serviceProvider => serviceProvider.GetRequiredService<SharePointFileStorage>());
        services.AddScoped<IFileStorageMigration, FileStorageMigration>();
        return services;
    }

    private sealed class DisabledTokenProvider : IGraphApplicationTokenProvider
    {
        public ValueTask<string> GetAsync(CancellationToken cancellationToken) =>
            throw new FileStorageException(FileStorageError.MissingConfiguration);
    }
}
