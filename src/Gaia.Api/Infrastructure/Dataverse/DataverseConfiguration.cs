namespace Gaia.Api.Infrastructure.Dataverse;

internal sealed record DataverseConfiguration(
    Uri EnvironmentUrl,
    Uri WebApiEndpoint,
    string Scope)
{
    private const string ConfigurationMessage =
        "La URL de Dataverse no está configurada para el entorno actual. " +
        "Configure Dataverse:EnvironmentUrl, Dataverse:WebApiEndpoint y Dataverse:Scope " +
        "mediante User Secrets o variables de ambiente.";

    public static DataverseConfiguration From(IConfiguration configuration)
    {
        var environmentUrl = RequiredHttpsUri(configuration["Dataverse:EnvironmentUrl"], isApi: false);
        var webApiEndpoint = RequiredHttpsUri(configuration["Dataverse:WebApiEndpoint"], isApi: true);
        if (!MatchesEnvironment(environmentUrl.Host, webApiEndpoint.Host))
            throw new InvalidOperationException(ConfigurationMessage);
        var scope = RequiredScope(configuration["Dataverse:Scope"], environmentUrl);

        return new(
            NormalizeOrigin(environmentUrl),
            NormalizeApiEndpoint(webApiEndpoint),
            scope);
    }

    private static Uri RequiredHttpsUri(string? value, bool isApi)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host)
            || uri.Host.Contains("your_environment", StringComparison.OrdinalIgnoreCase)
            || !IsDataverseHost(uri.Host, isApi))
        {
            throw new InvalidOperationException(ConfigurationMessage);
        }

        return uri;
    }

    private static string RequiredScope(string? value, Uri environmentUrl)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var scopeUri)
            || scopeUri.Scheme != Uri.UriSchemeHttps
            || scopeUri.Host.Contains("your_environment", StringComparison.OrdinalIgnoreCase)
            || !scopeUri.Host.Equals(environmentUrl.Host, StringComparison.OrdinalIgnoreCase)
            || !scopeUri.AbsolutePath.TrimEnd('/').Equals("/user_impersonation", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(ConfigurationMessage);
        }

        return scopeUri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/user_impersonation";
    }

    private static bool IsDataverseHost(string host, bool isApi)
    {
        var normalized = host.ToLowerInvariant();
        var validSuffix = normalized.EndsWith(".dynamics.com", StringComparison.Ordinal)
            || normalized.EndsWith(".dynamics.cn", StringComparison.Ordinal)
            || normalized.EndsWith(".dynamics.us", StringComparison.Ordinal)
            || normalized.EndsWith(".microsoftdynamics.de", StringComparison.Ordinal);
        if (!validSuffix) return false;

        return isApi
            ? normalized.Contains(".api.crm", StringComparison.Ordinal)
            : normalized.Contains(".crm", StringComparison.Ordinal)
                && !normalized.Contains(".api.crm", StringComparison.Ordinal);
    }

    private static bool MatchesEnvironment(string environmentHost, string apiHost)
    {
        var crmMarker = environmentHost.IndexOf(".crm", StringComparison.OrdinalIgnoreCase);
        if (crmMarker <= 0) return false;

        var expectedApiHost = environmentHost.Insert(crmMarker, ".api");
        return apiHost.Equals(expectedApiHost, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri NormalizeOrigin(Uri uri) =>
        new(uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/", UriKind.Absolute);

    private static Uri NormalizeApiEndpoint(Uri uri)
    {
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!path.Equals("/api/data/v9.2", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ConfigurationMessage);

        return new(uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/api/data/v9.2/", UriKind.Absolute);
    }
}

internal sealed class DataverseConnectivityException(string message, Exception innerException)
    : HttpRequestException(message, innerException);
