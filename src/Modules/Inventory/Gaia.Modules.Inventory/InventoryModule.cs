using Gaia.BuildingBlocks;
using Gaia.Modules.Inventory.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
        var connection = configuration.GetConnectionString("GaiaDatabase")
            ?? throw new InvalidOperationException("Connection string 'GaiaDatabase' is not configured.");
        services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(connection, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema)));
        services.AddAuthorizationBuilder()
            .AddPolicy(InventoryPermissions.Read, policy => Permission(policy, InventoryPermissions.Read))
            .AddPolicy(InventoryPermissions.Manage, policy => Permission(policy, InventoryPermissions.Manage));
        return services;
    }

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        InventoryEndpoints.Map(endpoints);
        return endpoints;
    }

    public static async Task InitializeInventoryAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database.MigrateAsync(cancellationToken);
    }

    private static void Permission(AuthorizationPolicyBuilder policy, string permission) => policy.RequireAssertion(context =>
        context.User.IsInRole("PlatformAdministrator") || context.User.HasClaim("gaia:permission", permission));
}

public static class InventoryPermissions
{
    public const string Read = "inventory.read";
    public const string Manage = "inventory.manage";
}
