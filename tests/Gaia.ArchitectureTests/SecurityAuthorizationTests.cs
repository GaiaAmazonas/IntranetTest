using System.Security.Claims;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.ArchitectureTests;

public sealed class SecurityAuthorizationTests
{
    [Fact]
    public void AdminCorePermissionsAreUniqueAndCompatibleWithDataverseCodeLength()
    {
        Assert.Equal(AdminCorePermissions.All.Length, AdminCorePermissions.All.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(AdminCorePermissions.All, permission =>
        {
            Assert.False(string.IsNullOrWhiteSpace(permission));
            Assert.True(permission.Length <= 30, $"{permission} supera los 30 caracteres permitidos.");
            Assert.Equal(permission.ToUpperInvariant(), permission);
        });
    }

    [Fact]
    public async Task RegisteredPolicySucceedsOnlyWhenStoreGrantsThePermission()
    {
        var authorization = new ConfigurableAuthorization();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton<IAdminCoreAuthorization>(authorization); services.AddSecurityModule();
        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test"));

        authorization.Allowed = AdminCorePermissions.TiRolesVer;
        var allowed = await service.AuthorizeAsync(principal, null, AdminCorePermissions.TiRolesVer);
        var denied = await service.AuthorizeAsync(principal, null, AdminCorePermissions.TiRolesAdministrar);

        Assert.True(allowed.Succeeded);
        Assert.False(denied.Succeeded);
    }

    [Theory]
    [InlineData("TI.USUARIOS.VER")]
    [InlineData("TI.ROLES.VER")]
    [InlineData("TI.MODULOS.VER")]
    [InlineData("TI.USUARIOS.ADMINISTRAR")]
    [InlineData("TI.ROLES.ADMINISTRAR")]
    [InlineData("TI.MODULOS.ADMINISTRAR")]
    public void CriticalSecurityPolicyIsRegistered(string permission)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton<IAdminCoreAuthorization>(new ConfigurableAuthorization()); services.AddSecurityModule();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>().Value;

        Assert.NotNull(options.GetPolicy(permission));
    }

    private sealed class ConfigurableAuthorization : IAdminCoreAuthorization
    {
        public string? Allowed { get; set; }
        public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken token = default) =>
            Task.FromResult(string.Equals(Allowed, permission, StringComparison.OrdinalIgnoreCase));
    }
}
