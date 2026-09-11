using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

internal enum StorageAuthenticationMethod { ManagedIdentity, Certificate, ClientSecret }

// Credentials themselves are intentionally excluded, including generated record ToString output.
internal sealed class SharePointStorageConfiguration
{
    public bool Enabled { get; private init; }
    public string TenantId { get; private init; } = "";
    public string ClientId { get; private init; } = "";
    public StorageAuthenticationMethod AuthenticationMethod { get; private init; }
    public string? CertificatePath { get; private init; }
    public string? CertificateThumbprint { get; private init; }
    public string SiteId { get; private init; } = "";
    public string DriveId { get; private init; } = "";
    public string LibraryName { get; private init; } = "";
    public string RootFolder { get; private init; } = "";
    public long MaximumFileBytes { get; private init; }
    public long SimpleUploadThresholdBytes { get; private init; }
    public IReadOnlySet<string> AllowedExtensions { get; private init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedMimeTypes { get; private init; } = new HashSet<string>();
    public bool RealTestsEnabled { get; private init; }
    public string? RealTestsEnvironment { get; private init; }
    public bool UseSystemAssignedManagedIdentity { get; private init; }
    public bool RequireContentScan { get; private init; }
    public int UploadChunkBytes { get; private init; } = 3_276_800;
    public IReadOnlySet<string> TransferAllowedHosts { get; private init; } = new HashSet<string>();

    public static SharePointStorageConfiguration From(IConfiguration configuration, string environmentName)
    {
        var section = configuration.GetSection("FileStorage:SharePoint");
        if (section["Enabled"] is null or "false" or "False") return new();
        if (!bool.TryParse(section["Enabled"], out var enabled) || !enabled) throw Invalid();
        if (!Enum.TryParse<StorageAuthenticationMethod>(section["AuthenticationMethod"], true, out var method)
            || !Enum.IsDefined(method)) throw Invalid();
        if (!Guid.TryParse(section["TenantId"], out _) || !Guid.TryParse(section["ClientId"], out _)) throw Invalid();
        if (method == StorageAuthenticationMethod.ClientSecret && !environmentName.Equals("Development", StringComparison.Ordinal))
            throw Invalid();
        var path = section["CertificatePath"];
        var thumbprint = section["CertificateThumbprint"];
        if (method == StorageAuthenticationMethod.Certificate
            && (string.IsNullOrWhiteSpace(path) == string.IsNullOrWhiteSpace(thumbprint))) throw Invalid();
        if (!long.TryParse(section["MaximumFileBytes"], out var maximum) || maximum <= 0
            || !long.TryParse(section["SimpleUploadThresholdBytes"], out var threshold) || threshold <= 0 || threshold > maximum)
            throw Invalid();
        var extensions = Values(section, "AllowedExtensions");
        var mimeTypes = Values(section, "AllowedMimeTypes");
        if (extensions.Count == 0 || mimeTypes.Count == 0
            || extensions.Any(value => value.Length < 2 || value[0] != '.' || !value[1..].All(char.IsAsciiLetterOrDigit))
            || mimeTypes.Any(value => value.Count(character => character == '/') != 1
                || value.StartsWith('/') || value.EndsWith('/')
                || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || "!#$&^_.+-/".Contains(character))))) throw Invalid();
        var tests = section["RealTestsEnabled"];
        if (tests is not null && !bool.TryParse(tests, out _)) throw Invalid();
        var testsEnabled = bool.TryParse(tests, out var parsed) && parsed;
        var testsEnvironment = section["RealTestsEnvironment"];
        if (testsEnabled && (environmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(testsEnvironment, environmentName, StringComparison.Ordinal))) throw Invalid();
        var chunk = section.GetValue("UploadChunkBytes", 3_276_800);
        // Alignment is a Graph protocol requirement, not Gaia's maximum file size.
        if (chunk <= 0 || chunk % 327_680 != 0 || chunk > 10 * 1024 * 1024) throw Invalid();
        var hosts = Values(section, "TransferAllowedHosts");
        if (hosts.Count == 0 && Uri.CheckHostName((section["SiteId"] ?? "").Split(',')[0]) == UriHostNameType.Dns)
            hosts.Add(section["SiteId"]!.Split(',')[0].ToLowerInvariant());
        if (hosts.Any(host => Uri.CheckHostName(host) != UriHostNameType.Dns || !host.Contains('.') || host.Contains('*'))) throw Invalid();
        return new()
        {
            Enabled = true, TenantId = section["TenantId"]!, ClientId = section["ClientId"]!,
            AuthenticationMethod = method, CertificatePath = path, CertificateThumbprint = thumbprint,
            SiteId = Identifier(section, "SiteId"), DriveId = Identifier(section, "DriveId"),
            LibraryName = section["LibraryName"] ?? "", RootFolder = FileUploadRules.NormalizeRoot(section["RootFolder"] ?? ""),
            MaximumFileBytes = maximum, SimpleUploadThresholdBytes = threshold,
            AllowedExtensions = extensions, AllowedMimeTypes = mimeTypes,
            RealTestsEnabled = testsEnabled, RealTestsEnvironment = testsEnvironment,
            UseSystemAssignedManagedIdentity = section.GetValue("UseSystemAssignedManagedIdentity", false),
            RequireContentScan = section.GetValue("RequireContentScan", false),
            UploadChunkBytes = chunk, TransferAllowedHosts = hosts
        };
    }

    private static HashSet<string> Values(IConfiguration section, string name) =>
        section.GetSection(name).GetChildren().Select(item => item.Value?.ToLowerInvariant() ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Identifier(IConfiguration section, string name)
    {
        var value = section[name];
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => char.IsWhiteSpace(character)
            || char.IsControl(character) || "/\\?#%".Contains(character))) throw Invalid();
        return value;
    }

    private static FileStorageException Invalid() => new(FileStorageError.MissingConfiguration);
}
