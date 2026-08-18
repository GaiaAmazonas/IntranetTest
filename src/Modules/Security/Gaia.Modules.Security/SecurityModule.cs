using Gaia.BuildingBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Modules.Security;
public sealed class SecurityModule:IModule { public static string Name=>"Security"; }
public sealed record AdminCorePermissionRequirement(string Permission):IAuthorizationRequirement;
internal sealed class AdminCorePermissionHandler(IAdminCoreAuthorization authorization):AuthorizationHandler<AdminCorePermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,AdminCorePermissionRequirement requirement)
    { if(await authorization.HasPermissionAsync(context.User,requirement.Permission)) context.Succeed(requirement); }
}
public static class SecurityModuleExtensions
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler,AdminCorePermissionHandler>();
        var authorization=services.AddAuthorizationBuilder();
        foreach(var permission in AdminCorePermissions.All) authorization.AddPolicy(permission,p=>p.AddRequirements(new AdminCorePermissionRequirement(permission)));
        return services;
    }
    public static IEndpointRouteBuilder MapSecurityEndpoints(this IEndpointRouteBuilder endpoints){SecurityEndpoints.Map(endpoints);return endpoints;}
}
