using Gaia.Api.Infrastructure.Dataverse;
using Microsoft.Extensions.Configuration;

namespace Gaia.ArchitectureTests;

public sealed class DataverseConfigurationTests
{
    [Fact]
    public void ValidConfigurationIsNormalized()
    {
        var result = DataverseConfiguration.From(Configuration(
            "https://org.crm2.dynamics.com/",
            "https://org.api.crm2.dynamics.com/api/data/v9.2/",
            "https://org.crm2.dynamics.com/user_impersonation/"));

        Assert.Equal("https://org.crm2.dynamics.com/", result.EnvironmentUrl.AbsoluteUri);
        Assert.Equal("https://org.api.crm2.dynamics.com/api/data/v9.2/", result.WebApiEndpoint.AbsoluteUri);
        Assert.Equal("https://org.crm2.dynamics.com/user_impersonation", result.Scope);
    }

    [Theory]
    [InlineData(null, "https://org.api.crm2.dynamics.com/api/data/v9.2", "https://org.crm2.dynamics.com/user_impersonation")]
    [InlineData("https://YOUR_ENVIRONMENT.crm.dynamics.com", "https://YOUR_ENVIRONMENT.api.crm.dynamics.com/api/data/v9.2", "https://YOUR_ENVIRONMENT.crm.dynamics.com/user_impersonation")]
    [InlineData("http://org.crm2.dynamics.com", "https://org.api.crm2.dynamics.com/api/data/v9.2", "https://org.crm2.dynamics.com/user_impersonation")]
    [InlineData("https://org.crm2.dynamics.com", "https://example.org/api/data/v9.2", "https://org.crm2.dynamics.com/user_impersonation")]
    [InlineData("https://org.crm2.dynamics.com", "https://other.api.crm2.dynamics.com/api/data/v9.2", "https://org.crm2.dynamics.com/user_impersonation")]
    [InlineData("https://org.crm2.dynamics.com", "https://org.api.crm2.dynamics.com/api/data/v9.2/api/data/v9.2", "https://org.crm2.dynamics.com/user_impersonation")]
    public void InvalidConfigurationFailsWithControlledMessage(string? environment, string endpoint, string scope)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            DataverseConfiguration.From(Configuration(environment, endpoint, scope)));

        Assert.Contains("La URL de Dataverse no está configurada", exception.Message);
        Assert.DoesNotContain("ClientSecret", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration Configuration(string? environment, string endpoint, string scope) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dataverse:EnvironmentUrl"] = environment,
            ["Dataverse:WebApiEndpoint"] = endpoint,
            ["Dataverse:Scope"] = scope
        }).Build();
}
