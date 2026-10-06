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
        Assert.Contains(AdminCorePermissions.IntranetSolicitudesVer, DefaultRolePermissions.Consulta);
        Assert.DoesNotContain(AdminCorePermissions.IntranetAdminCoreVer, DefaultRolePermissions.Consulta);
        Assert.DoesNotContain(AdminCorePermissions.OrgUnidadesVer, DefaultRolePermissions.Consulta);
    }

    [Fact]
    public void EveryDefaultConsultaPermissionIsRegistered()
    {
        Assert.All(DefaultRolePermissions.Consulta, permission =>
            Assert.Contains(permission, AdminCorePermissions.All));
    }

    [Fact]
    public void AdministratorLoginDoesNotRestorePermissionsRemovedFromTheRole()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gaia.Platform.slnx"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
            "src", "Gaia.Api", "Infrastructure", "Dataverse", "Security", "DataverseSecurityStore.cs"));
        var provisioningStart = source.IndexOf("private async Task<SecurityContextResponse> GetOrProvisionUnsafeAsync", StringComparison.Ordinal);
        var contextStart = source.IndexOf("private static async Task<SecurityContextResponse> LoadContext", provisioningStart, StringComparison.Ordinal);
        var provisioning = source[provisioningStart..contextStart];

        Assert.DoesNotContain("EnsureRolePermissions", provisioning, StringComparison.Ordinal);
        Assert.DoesNotContain("EnsureAdministratorPermissions", provisioning, StringComparison.Ordinal);
    }

    [Fact]
    public void SecuritySynchronizationPreservesCustomizedExistingRoles()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gaia.Platform.slnx"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
            "src", "Gaia.Api", "Infrastructure", "Dataverse", "Security", "DataverseSecurityStore.cs"));
        var bootstrapStart = source.IndexOf("public async Task<SecurityBootstrapResult> BootstrapAsync", StringComparison.Ordinal);
        var bootstrapEnd = source.IndexOf("public Task<SecurityContextResponse> GetOrProvisionAsync", bootstrapStart, StringComparison.Ordinal);
        var bootstrap = source[bootstrapStart..bootstrapEnd];

        Assert.Contains("existingPermissionCodes", bootstrap, StringComparison.Ordinal);
        Assert.Contains("existingRoleCodes.Contains(\"ADMIN\") ? newPermissionIds : permissionIds.Values", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("adminId, permissionIds.Values", bootstrap, StringComparison.Ordinal);
    }

    [Fact]
    public void InactiveModuleDeletionOwnsItsPermissionRecordsButProtectsActiveRoleAssignments()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gaia.Platform.slnx"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
            "src", "Gaia.Api", "Infrastructure", "Dataverse", "Security", "DataverseSecurityStore.cs"));
        var deletionStart = source.IndexOf("public async Task DeleteModuleAsync", StringComparison.Ordinal);
        var deletionEnd = source.IndexOf("private static bool IsValidModuleRoute", deletionStart, StringComparison.Ordinal);
        var deletion = source[deletionStart..deletionEnd];

        Assert.DoesNotContain("Inactiva primero todos los permisos del módulo", deletion, StringComparison.Ordinal);
        Assert.Contains("Finaliza primero las asignaciones activas de sus permisos a roles", deletion, StringComparison.Ordinal);
        Assert.True(
            deletion.IndexOf("assignmentsByPermission[permissionId]=assignments", StringComparison.Ordinal)
            < deletion.IndexOf("foreach(var assignment in assignmentsByPermission[permissionId])", StringComparison.Ordinal));
    }

    [Fact]
    public void SolicitudesSeparatesPortalAndAdministrationAuthorization()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gaia.Platform.slnx"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(),
            "src", "Modules", "Solicitudes", "Gaia.Modules.Solicitudes", "SolicitudesEndpoints.cs"))
            .Replace("\r\n", "\n");

        Assert.Contains("MapGroup(\"/api/solicitudes\").WithTags(\"Solicitudes\")\n            .RequireAuthorization();", source);
        Assert.Contains("MapGet(\"/portal/catalog\", PortalCatalog)\n            .RequireAuthorization(AdminCorePermissions.IntranetSolicitudesVer)", source);
        Assert.Contains("MapGet(\"/management/queue\", ManagementQueue).RequireAuthorization(AdminCorePermissions.SolicitudesVer)", source);
        Assert.Contains("MapGet(\"/administration\", Administration).RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosVer)", source);
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
