using System.Security.Claims;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.ArchitectureTests;

public sealed class SecurityEndpointAuthorizationTests
{
    [Theory]
    [InlineData("/api/security/users", "GET", "TI.USUARIOS.VER")]
    [InlineData("/api/security/users/{userId:guid}/roles", "POST", "TI.USUARIOS.ADMINISTRAR")]
    [InlineData("/api/security/users/preprovision-audit", "GET", "TI.USUARIOS.ADMINISTRAR")]
    [InlineData("/api/security/users/preprovision", "POST", "TI.USUARIOS.ADMINISTRAR")]
    [InlineData("/api/security/roles", "GET", "TI.ROLES.VER")]
    [InlineData("/api/security/roles", "POST", "TI.ROLES.CREAR")]
    [InlineData("/api/security/roles/{id:guid}", "PUT", "TI.ROLES.ACTUALIZAR")]
    [InlineData("/api/security/roles/{id:guid}/permissions", "PUT", "TI.ROLES.ADMINISTRAR")]
    [InlineData("/api/security/modules", "GET", "TI.MODULOS.VER")]
    [InlineData("/api/security/modules", "POST", "TI.MODULOS.CREAR")]
    [InlineData("/api/security/modules/{id:guid}", "PUT", "TI.MODULOS.ACTUALIZAR")]
    public void SensitiveEndpointRequiresItsSpecificPolicy(string route, string method, string policy)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<ISecurityStore, UnusedSecurityStore>();
        builder.Services.AddSingleton<IAdminCoreAuthorization, DenyAllAuthorization>();
        builder.Services.AddSecurityModule();
        var application = builder.Build(); application.MapSecurityEndpoints();

        var endpoint = ((IEndpointRouteBuilder)application).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == route && candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);
        var authorization = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();

        Assert.NotEmpty(authorization);
        Assert.Contains(authorization, item => string.Equals(item.Policy, policy, StringComparison.Ordinal));
    }

    [Fact]
    public void SecurityDoesNotPublishDeleteBeforeARealSafeOperationExists()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<ISecurityStore, UnusedSecurityStore>();
        builder.Services.AddSingleton<IAdminCoreAuthorization, DenyAllAuthorization>();
        builder.Services.AddSecurityModule();
        var application = builder.Build(); application.MapSecurityEndpoints();

        var deleteEndpoints = ((IEndpointRouteBuilder)application).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains("DELETE") == true);

        Assert.Empty(deleteEndpoints);
    }

    private sealed class DenyAllAuthorization : IAdminCoreAuthorization
    {
        public Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permission, CancellationToken token = default) => Task.FromResult(false);
    }

    private sealed class UnusedSecurityStore : ISecurityStore
    {
        private static Task<T> Unused<T>() => Task.FromException<T>(new NotSupportedException());
        private static Task UnusedAction() => Task.FromException(new NotSupportedException());
        public Task<SecurityMetadataAudit> AuditMetadataAsync(CancellationToken token) => Unused<SecurityMetadataAudit>();
        public Task<SecurityBootstrapResult> BootstrapAsync(ClaimsPrincipal principal, CancellationToken token) => Unused<SecurityBootstrapResult>();
        public Task<SecurityContextResponse> GetOrProvisionAsync(ClaimsPrincipal principal, CancellationToken token) => Unused<SecurityContextResponse>();
        public Task<IReadOnlyList<SecurityUserDetail>> ListUsersAsync(CancellationToken token) => Unused<IReadOnlyList<SecurityUserDetail>>();
        public Task<SecurityPreprovisionAudit> AuditPreprovisionAsync(CancellationToken token) => Unused<SecurityPreprovisionAudit>();
        public Task<SecurityPreprovisionResult> PreprovisionEligibleUsersAsync(CancellationToken token) => Unused<SecurityPreprovisionResult>();
        public Task<IReadOnlyList<SecurityRoleItem>> ListRolesAsync(CancellationToken token) => Unused<IReadOnlyList<SecurityRoleItem>>();
        public Task<IReadOnlyList<SecurityModuleItem>> ListModulesAsync(CancellationToken token) => Unused<IReadOnlyList<SecurityModuleItem>>();
        public Task<IReadOnlyList<SecurityPermissionItem>> ListPermissionsAsync(CancellationToken token) => Unused<IReadOnlyList<SecurityPermissionItem>>();
        public Task<Guid> UpsertRoleAsync(Guid? id, RoleWriteRequest request, CancellationToken token) => Unused<Guid>();
        public Task SetRolePermissionsAsync(Guid roleId, RolePermissionsRequest request, CancellationToken token) => UnusedAction();
        public Task<Guid> AssignUserRoleAsync(Guid userId, UserRoleWriteRequest request, CancellationToken token) => Unused<Guid>();
        public Task EndUserRoleAsync(Guid userId, Guid assignmentId, DateOnly endDate, CancellationToken token) => UnusedAction();
        public Task<Guid> UpsertModuleAsync(Guid? id, ModuleWriteRequest request, CancellationToken token) => Unused<Guid>();
    }
}
