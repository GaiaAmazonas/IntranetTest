using Gaia.BuildingBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Modules.Security;
public sealed class SecurityModule:IModule { public static string Name=>"Security"; }
public sealed record AdminCorePermissionRequirement(string Permission):IAuthorizationRequirement;
public sealed record AnyAdminCorePermissionRequirement(IReadOnlyList<string> Permissions):IAuthorizationRequirement;
internal sealed class AdminCorePermissionHandler(IAdminCoreAuthorization authorization):AuthorizationHandler<AdminCorePermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,AdminCorePermissionRequirement requirement)
    { if(await authorization.HasPermissionAsync(context.User,requirement.Permission)) context.Succeed(requirement); }
}
internal sealed class AnyAdminCorePermissionHandler(IAdminCoreAuthorization authorization):AuthorizationHandler<AnyAdminCorePermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,AnyAdminCorePermissionRequirement requirement)
    { foreach(var permission in requirement.Permissions) if(await authorization.HasPermissionAsync(context.User,permission)){context.Succeed(requirement);return;} }
}
public static class AssignmentAuthorizationPolicies
{
    public const string Read="Assignments.Read"; public const string Create="Assignments.Create"; public const string Update="Assignments.Update";
}
public static class SecurityModuleExtensions
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler,AdminCorePermissionHandler>();
        services.AddScoped<IAuthorizationHandler,AnyAdminCorePermissionHandler>();
        var authorization=services.AddAuthorizationBuilder();
        foreach(var permission in AdminCorePermissions.All)
        {
            authorization.AddPolicy(permission, policy =>
            {
                if (PermissionScope.RequiresAdminCore(permission))
                    policy.AddRequirements(new AdminCorePermissionRequirement(AdminCorePermissions.IntranetAdminCoreVer));
                policy.AddRequirements(new AdminCorePermissionRequirement(permission));
            });
        }
        authorization.AddPolicy(AssignmentAuthorizationPolicies.Read,policy=>policy.AddRequirements(new AdminCorePermissionRequirement(AdminCorePermissions.IntranetAdminCoreVer),new AnyAdminCorePermissionRequirement([AdminCorePermissions.OrgAsignacionesVer,AdminCorePermissions.ThVinculacionesVer])));
        authorization.AddPolicy(AssignmentAuthorizationPolicies.Create,policy=>policy.AddRequirements(new AdminCorePermissionRequirement(AdminCorePermissions.IntranetAdminCoreVer),new AnyAdminCorePermissionRequirement([AdminCorePermissions.OrgAsignacionesCrear,AdminCorePermissions.ThVinculacionesCrear])));
        authorization.AddPolicy(AssignmentAuthorizationPolicies.Update,policy=>policy.AddRequirements(new AdminCorePermissionRequirement(AdminCorePermissions.IntranetAdminCoreVer),new AnyAdminCorePermissionRequirement([AdminCorePermissions.OrgAsignacionesActualizar,AdminCorePermissions.ThVinculacionesActualizar])));
        return services;
    }
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder endpoints){SecurityEndpoints.Map(endpoints);return endpoints;}
}
