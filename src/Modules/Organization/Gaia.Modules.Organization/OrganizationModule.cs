using Gaia.BuildingBlocks;
using Gaia.Modules.Organization.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
        var connectionString = configuration.GetConnectionString("GaiaDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'GaiaDatabase' is not configured.");

        services.AddDbContext<OrganizationDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    OrganizationDbContext.Schema)));

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

    public static async Task InitializeOrganizationAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
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
