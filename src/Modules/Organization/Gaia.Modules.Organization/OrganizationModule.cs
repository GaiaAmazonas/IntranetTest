using Gaia.BuildingBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Modules.Organization;

public sealed class OrganizationModule : IModule
{
    public static string Name => "Organization";
}

public static class OrganizationModuleExtensions
{
    public static IServiceCollection AddOrganizationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(
                OrganizationPermissions.Read,
                policy => RequirePermissionOrAdministrator(policy, OrganizationPermissions.Read))
            .AddPolicy(
                OrganizationPermissions.Manage,
                policy => RequirePermissionOrAdministrator(policy, OrganizationPermissions.Manage));

        return services;
    }

    public static IEndpointRouteBuilder MapOrganizationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        OrganizationEndpoints.Map(endpoints);
        return endpoints;
    }

    private static void RequirePermissionOrAdministrator(
        AuthorizationPolicyBuilder policy,
        string permission)
    {
        policy.RequireAssertion(context =>
            context.User.IsInRole("PlatformAdministrator")
            || context.User.HasClaim("gaia:permission", permission));
    }
}

public static class OrganizationPermissions
{
    public const string Read = "organization.read";
    public const string Manage = "organization.manage";
}
