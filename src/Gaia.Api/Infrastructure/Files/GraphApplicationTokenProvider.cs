using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;
using Gaia.BuildingBlocks.Files;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace Gaia.Api.Infrastructure.Files;

internal interface IGraphApplicationTokenProvider
{
    ValueTask<string> GetAsync(CancellationToken cancellationToken);
}

internal sealed class GraphApplicationTokenProvider(TokenCredential credential, X509Certificate2? certificate = null)
    : IGraphApplicationTokenProvider, IDisposable
{
    private static readonly TokenRequestContext Context = new(["https://graph.microsoft.com/.default"]);

    public async ValueTask<string> GetAsync(CancellationToken cancellationToken)
    {
        try { return (await credential.GetTokenAsync(Context, cancellationToken)).Token; }
        catch (AuthenticationFailedException) { throw new FileStorageException(FileStorageError.InvalidCredentials); }
        catch (Azure.RequestFailedException) { throw new FileStorageException(FileStorageError.InvalidCredentials); }
    }

    public void Dispose() => certificate?.Dispose();

    public static GraphApplicationTokenProvider Create(SharePointStorageConfiguration options, IConfiguration configuration,
        string credentialPrefix = "FileStorage:SharePoint:")
    {
        try
        {
            if (options.AuthenticationMethod == StorageAuthenticationMethod.ManagedIdentity)
                return new(new ManagedIdentityCredential(new ManagedIdentityCredentialOptions(
                    options.UseSystemAssignedManagedIdentity ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(options.ClientId))
                { Diagnostics = { IsLoggingEnabled = false } }));
            if (options.AuthenticationMethod == StorageAuthenticationMethod.ClientSecret)
                return new(new ClientSecretCredential(options.TenantId, options.ClientId,
                    ExternalSecret(configuration, "ClientSecret", required: true, credentialPrefix)!,
                    new ClientSecretCredentialOptions { Diagnostics = { IsLoggingEnabled = false }, Retry = { MaxRetries = 0 } }));

            var certificate = LoadCertificate(options, configuration, credentialPrefix);
            return new(new ClientCertificateCredential(options.TenantId, options.ClientId, certificate,
                new ClientCertificateCredentialOptions { Diagnostics = { IsLoggingEnabled = false }, Retry = { MaxRetries = 0 } }), certificate);
        }
        catch (Exception error) when (error is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException)
        { throw new FileStorageException(FileStorageError.MissingConfiguration); }
    }

    internal static string? ExternalSecret(IConfiguration configuration, string name, bool required,
        string credentialPrefix = "FileStorage:SharePoint:")
    {
        var key = credentialPrefix + name;
        if (configuration is IConfigurationRoot root)
        {
            foreach (var provider in root.Providers.Reverse())
            {
                if (!provider.TryGet(key, out var value)) continue;
                var permitted = provider is EnvironmentVariablesConfigurationProvider
                    || provider is JsonConfigurationProvider json && string.Equals(json.Source.Path, "secrets.json", StringComparison.OrdinalIgnoreCase);
                if (!permitted || string.IsNullOrWhiteSpace(value)) throw new FileStorageException(FileStorageError.MissingConfiguration);
                return value;
            }
        }
        if (required) throw new FileStorageException(FileStorageError.MissingConfiguration);
        return null;
    }

    private static X509Certificate2 LoadCertificate(SharePointStorageConfiguration options, IConfiguration configuration, string credentialPrefix)
    {
        X509Certificate2 certificate;
        if (options.CertificatePath is not null)
            certificate = X509CertificateLoader.LoadPkcs12FromFile(options.CertificatePath,
                ExternalSecret(configuration, "CertificatePassword", required: false, credentialPrefix), X509KeyStorageFlags.EphemeralKeySet);
        else
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, options.CertificateThumbprint!, validOnly: true);
            if (found.Count != 1) throw new FileStorageException(FileStorageError.MissingConfiguration);
            certificate = found[0];
        }
        if (!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow
            || certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow)
        { certificate.Dispose(); throw new FileStorageException(FileStorageError.MissingConfiguration); }
        return certificate;
    }
}
