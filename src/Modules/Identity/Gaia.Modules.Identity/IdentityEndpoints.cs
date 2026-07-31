using Gaia.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.Identity;

internal static class IdentityEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Identity");

        auth.MapPost("/login", LoginAsync).AllowAnonymous();
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();
        auth.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        var users = endpoints.MapGroup("/api/identity/users").WithTags("Identity");
        users.MapGet("/", ListUsersAsync)
            .RequireAuthorization(GaiaPermissions.UsersRead);
        users.MapPost("/", CreateUserAsync)
            .RequireAuthorization(GaiaPermissions.UsersManage);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        UserManager<GaiaUser> userManager,
        RoleManager<GaiaRole> roleManager,
        SignInManager<GaiaUser> signInManager,
        GaiaIdentityDbContext context,
        HttpContext httpContext)
    {
        var normalizedEmail = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(normalizedEmail);
        var result = user is { IsActive: true }
            ? await signInManager.PasswordSignInAsync(
                user,
                request.Password,
                isPersistent: false,
                lockoutOnFailure: true)
            : Microsoft.AspNetCore.Identity.SignInResult.Failed;

        context.LoginAudits.Add(new LoginAudit
        {
            UserId = user?.Id,
            Email = normalizedEmail,
            WasSuccessful = result.Succeeded,
            FailureReason = result.Succeeded
                ? null
                : result.IsLockedOut ? "locked_out" : "invalid_credentials",
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString()
        });
        await context.SaveChangesAsync(httpContext.RequestAborted);

        return result.Succeeded
            ? Results.Ok(await BuildCurrentUserAsync(user!, userManager, roleManager))
            : Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "No fue posible iniciar sesión.",
                detail: "Verifica las credenciales o contacta al administrador.");
    }

    private static async Task<IResult> LogoutAsync(SignInManager<GaiaUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        UserManager<GaiaUser> userManager,
        RoleManager<GaiaRole> roleManager)
    {
        var user = await userManager.GetUserAsync(context.User);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(await BuildCurrentUserAsync(user, userManager, roleManager));
    }

    private static async Task<IResult> ListUsersAsync(
        GaiaIdentityDbContext context,
        CancellationToken cancellationToken)
    {
        var users = await context.Users
            .AsNoTracking()
            .OrderBy(user => user.DisplayName)
            .Select(user => new
            {
                user.Id,
                user.DisplayName,
                user.Email,
                user.IsActive,
                user.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(users);
    }

    private static async Task<IResult> CreateUserAsync(
        [FromBody] CreateUserRequest request,
        UserManager<GaiaUser> userManager)
    {
        var user = new GaiaUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            EmailConfirmed = true,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, request.TemporaryPassword);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                result.Errors
                    .GroupBy(error => error.Code)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(error => error.Description).ToArray()));
        }

        return Results.Created($"/api/identity/users/{user.Id}", new
        {
            user.Id,
            user.DisplayName,
            user.Email,
            user.IsActive
        });
    }

    private static async Task<CurrentUserResponse> BuildCurrentUserAsync(
        GaiaUser user,
        UserManager<GaiaUser> userManager,
        RoleManager<GaiaRole> roleManager)
    {
        var roles = await userManager.GetRolesAsync(user);
        var claims = await userManager.GetClaimsAsync(user);
        var roleClaims = new List<System.Security.Claims.Claim>();

        foreach (var roleName in roles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is not null)
            {
                roleClaims.AddRange(await roleManager.GetClaimsAsync(role));
            }
        }

        return new CurrentUserResponse(
            user.Id,
            user.DisplayName,
            user.Email!,
            roles,
            claims
                .Where(claim => claim.Type == GaiaClaims.Permission)
                .Select(claim => claim.Value)
                .Concat(roleClaims
                    .Where(claim => claim.Type == GaiaClaims.Permission)
                    .Select(claim => claim.Value))
                .Distinct()
                .Order()
                .ToArray());
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record CreateUserRequest(
    string DisplayName,
    string Email,
    string TemporaryPassword);

public sealed record CurrentUserResponse(
    Guid Id,
    string DisplayName,
    string Email,
    IList<string> Roles,
    IReadOnlyCollection<string> Permissions);
