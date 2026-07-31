using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Modules.Identity.Infrastructure;

public static class IdentitySeeder
{
    public static async Task InitializeIdentityAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<GaiaIdentityDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<GaiaRole>>();
        var administratorRole = await roleManager.FindByNameAsync(
            GaiaRoles.PlatformAdministrator);

        if (administratorRole is null)
        {
            administratorRole = new GaiaRole
            {
                Name = GaiaRoles.PlatformAdministrator,
                Description = "Administración completa de la plataforma Gaia."
            };
            EnsureSucceeded(await roleManager.CreateAsync(administratorRole));
        }

        var existingClaims = await roleManager.GetClaimsAsync(administratorRole);
        foreach (var permission in GaiaPermissions.All.Except(
                     existingClaims
                         .Where(claim => claim.Type == GaiaClaims.Permission)
                         .Select(claim => claim.Value)))
        {
            EnsureSucceeded(await roleManager.AddClaimAsync(
                administratorRole,
                new Claim(GaiaClaims.Permission, permission)));
        }

        var email = configuration["Identity:BootstrapAdmin:Email"];
        var password = configuration["Identity:BootstrapAdmin:Password"];
        var displayName = configuration["Identity:BootstrapAdmin:DisplayName"] ?? "Administrador Gaia";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<GaiaUser>>();
        var administrator = await userManager.FindByEmailAsync(email);

        if (administrator is null)
        {
            administrator = new GaiaUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                IsActive = true
            };
            EnsureSucceeded(await userManager.CreateAsync(administrator, password));
        }

        if (!await userManager.IsInRoleAsync(
                administrator,
                GaiaRoles.PlatformAdministrator))
        {
            EnsureSucceeded(await userManager.AddToRoleAsync(
                administrator,
                GaiaRoles.PlatformAdministrator));
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
