using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

internal static class RepositoryStorageConfiguration
{
    internal static (SharePointStorageConfiguration Options, string CredentialPrefix) Build(
        SharePointRepositorySettings repository, IConfiguration server, string environment)
    {
        var alias = string.IsNullOrWhiteSpace(repository.CredentialReference) && repository.AuthenticationMethod is 1 or 2
            ? "Default" : repository.CredentialReference;
        if (string.IsNullOrWhiteSpace(alias) || alias.Length > 200 || !alias.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            throw new FileStorageException(FileStorageError.MissingConfiguration);
        var prefix = "FileStorage:Credentials:" + alias + ":";
        var method = repository.AuthenticationMethod switch
        {
            1 or 2 => "ManagedIdentity", 3 => "Certificate", 4 => "ClientSecret",
            _ => throw new FileStorageException(FileStorageError.MissingConfiguration)
        };
        // An editable Dataverse row cannot redirect a trusted credential to an unapproved tenant/site/host.
        if (!string.Equals(server[prefix + "TenantId"], repository.TenantId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(server[prefix + "ClientId"], repository.ClientId, StringComparison.OrdinalIgnoreCase)
            || server[prefix + "AuthenticationMethod"] != method
            || !server.GetSection(prefix + "AllowedSiteIds").GetChildren().Any(child => child.Value == repository.SiteId))
            throw new FileStorageException(FileStorageError.FileUnauthorized);
        if (repository.AuthenticationMethod is 1 or 2 && server.GetValue(prefix + "UseSystemAssignedManagedIdentity", false)
            != (repository.AuthenticationMethod == 1)) throw new FileStorageException(FileStorageError.FileUnauthorized);
        var hosts = repository.TransferHosts.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var trustedHosts = server.GetSection(prefix + "AllowedTransferHosts").GetChildren()
            .Select(child => child.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (hosts.Length == 0 || hosts.Any(host => !trustedHosts.Contains(host)))
            throw new FileStorageException(FileStorageError.FileUnauthorized);
        if (!Uri.TryCreate(repository.SiteUrl, UriKind.Absolute, out var siteUri) || siteUri.Scheme != "https"
            || !siteUri.IsDefaultPort || siteUri.UserInfo.Length != 0 || siteUri.Query.Length != 0 || siteUri.Fragment.Length != 0
            || !siteUri.Host.Equals(repository.SiteId.Split(',')[0], StringComparison.OrdinalIgnoreCase))
            throw new FileStorageException(FileStorageError.MissingConfiguration);
        const string target = "FileStorage:SharePoint:";
        // Only copy nonsecret policy values. The actual credential provider reads secrets from the original root.
        var values = new Dictionary<string, string?>();
        foreach (var key in new[] { "MaximumFileBytes", "SimpleUploadThresholdBytes", "UploadChunkBytes", "RequireContentScan" })
            if (server[target + key] is { } value) values[target + key] = value;
        foreach (var key in new[] { "AllowedExtensions", "AllowedMimeTypes" })
            foreach (var child in server.GetSection(target + key).GetChildren()) values[child.Path] = child.Value;
        values[target + "Enabled"] = "true";
        values[target + "TenantId"] = repository.TenantId; values[target + "ClientId"] = repository.ClientId;
        values[target + "AuthenticationMethod"] = method;
        values[target + "UseSystemAssignedManagedIdentity"] = (repository.AuthenticationMethod == 1).ToString();
        values[target + "SiteId"] = repository.SiteId; values[target + "DriveId"] = repository.DriveId;
        values[target + "RootFolder"] = repository.RootFolder; values[target + "LibraryName"] = repository.LibraryName;
        values[target + "CertificatePath"] = server[prefix + "CertificatePath"];
        values[target + "CertificateThumbprint"] = server[prefix + "CertificateThumbprint"];
        for (var index = 0; index < hosts.Length; index++) values[target + "TransferAllowedHosts:" + index] = hosts[index];
        return (SharePointStorageConfiguration.From(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), environment), prefix);
    }
}
