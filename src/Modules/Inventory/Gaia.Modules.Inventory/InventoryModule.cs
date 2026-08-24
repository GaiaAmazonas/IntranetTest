using Gaia.BuildingBlocks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Modules.Inventory;

public sealed class InventoryModule : IModule
{
    public static string Name => "Inventory";
}

public static class InventoryModuleExtensions
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(InventoryPermissions.Read, policy => Permission(policy, InventoryPermissions.Read))
            .AddPolicy(InventoryPermissions.Manage, policy => Permission(policy, InventoryPermissions.Manage));
        return services;
    }

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        InventoryEndpoints.MapUnavailable(endpoints);
        return endpoints;
    }

    private static void Permission(AuthorizationPolicyBuilder policy, string permission) => policy.RequireAssertion(context =>
        context.User.IsInRole("PlatformAdministrator") || context.User.HasClaim("gaia:permission", permission));
}

public static class InventoryPermissions
{
    public const string Read = "inventory.read";
    public const string Manage = "inventory.manage";
}
