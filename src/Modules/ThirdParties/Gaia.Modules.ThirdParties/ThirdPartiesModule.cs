using Gaia.BuildingBlocks;
using Gaia.Modules.ThirdParties.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Modules.ThirdParties;

public sealed class ThirdPartiesModule : IModule
{
    public static string Name => "ThirdParties";
}

public static class ThirdPartiesModuleExtensions
{
    public static IServiceCollection AddThirdPartiesModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GaiaDatabase")
            ?? throw new InvalidOperationException("Connection string 'GaiaDatabase' is not configured.");
        services.AddDbContext<ThirdPartiesDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ThirdPartiesDbContext.Schema)));
        services.AddAuthorizationBuilder()
            .AddPolicy(ThirdPartyPermissions.Read, policy => Permission(policy, ThirdPartyPermissions.Read))
            .AddPolicy(ThirdPartyPermissions.Manage, policy => Permission(policy, ThirdPartyPermissions.Manage));
        return services;
    }

    public static IEndpointRouteBuilder MapThirdPartiesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ThirdPartiesEndpoints.Map(endpoints);
        return endpoints;
    }

    public static async Task InitializeThirdPartiesAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ThirdPartiesDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }

    private static void Permission(AuthorizationPolicyBuilder policy, string permission) =>
        policy.RequireAssertion(context =>
            context.User.IsInRole("PlatformAdministrator")
            || context.User.HasClaim("gaia:permission", permission));
}

public static class ThirdPartyPermissions
{
    public const string Read = "third-parties.read";
    public const string Manage = "third-parties.manage";
}
