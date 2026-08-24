using System.Security.Claims;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.ArchitectureTests;

public sealed class SecurityAuthorizationTests
{
    [Fact]
    public void ConsultaReceivesOnlyExplicitIntranetPermissions()
    {
        Assert.Contains(AdminCorePermissions.IntranetVer, DefaultRolePermissions.Consulta);
        Assert.Contains(AdminCorePermissions.IntranetPersonasVer, DefaultRolePermissions.Consulta);
        Assert.Contains(AdminCorePermissions.IntranetCalendarioVer, DefaultRolePermissions.Consulta);
        Assert.Contains(AdminCorePermissions.IntranetAplicacionesVer, DefaultRolePermissions.Consulta);
        Assert.Contains(AdminCorePermissions.IntranetHelpdeskVer, DefaultRolePermissions.Consulta);
        Assert.DoesNotContain(AdminCorePermissions.IntranetAdminCoreVer, DefaultRolePermissions.Consulta);
        Assert.DoesNotContain(AdminCorePermissions.OrgUnidadesVer, DefaultRolePermissions.Consulta);
    }

    [Fact]
    public void EveryDefaultConsultaPermissionIsRegistered()
    {
        Assert.All(DefaultRolePermissions.Consulta, permission =>
            Assert.Contains(permission, AdminCorePermissions.All));
    }

    [Theory]
    [InlineData("ORG.UNIDADES.VER", true)]
    [InlineData("TI.USUARIOS.VER", true)]
    [InlineData("INTRANET.VER", false)]
    [InlineData("INT.PERSONAS.VER", false)]
    [InlineData("INT.APP.ADMINCORE.VER", false)]
    public void PermissionScopeIdentifiesAdministrativeCapabilities(string permission, bool expected)
    {
        Assert.Equal(expected, PermissionScope.RequiresAdminCore(permission));
    }

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

        authorization.Allowed.Add(AdminCorePermissions.IntranetAdminCoreVer);
        authorization.Allowed.Add(AdminCorePermissions.TiRolesVer);
        var allowed = await service.AuthorizeAsync(principal, null, AdminCorePermissions.TiRolesVer);
        var denied = await service.AuthorizeAsync(principal, null, AdminCorePermissions.TiRolesAdministrar);

        Assert.True(allowed.Succeeded);
        Assert.False(denied.Succeeded);
    }

    [Theory]
    [InlineData(AssignmentAuthorizationPolicies.Read, "ORG.ASIGNACIONES.VER")]
    [InlineData(AssignmentAuthorizationPolicies.Read, "TH.VINCULACIONES.VER")]
    [InlineData(AssignmentAuthorizationPolicies.Create, "ORG.ASIGNACIONES.CREAR")]
    [InlineData(AssignmentAuthorizationPolicies.Update, "TH.VINCULACIONES.ACTUALIZAR")]
    public async Task AssignmentPoliciesAcceptTheCorrespondingOrganizationOrTalentPermission(string policy, string permission)
    {
        var authorization = new ConfigurableAuthorization();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton<IAdminCoreAuthorization>(authorization); services.AddSecurityModule();
        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Test"));

        authorization.Allowed.Add(AdminCorePermissions.IntranetAdminCoreVer);
        authorization.Allowed.Add(permission);

        Assert.True((await service.AuthorizeAsync(principal, null, policy)).Succeeded);
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
        public HashSet<string> Allowed { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken token = default) =>
            Task.FromResult(Allowed.Contains(permission));
    }
}
