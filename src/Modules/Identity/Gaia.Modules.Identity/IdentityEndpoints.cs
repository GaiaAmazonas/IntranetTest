using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace Gaia.Modules.Identity;

internal static class IdentityEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Identity");

        auth.MapGet("/login", Login).AllowAnonymous();
        auth.MapGet("/logout", Logout).RequireAuthorization();
        auth.MapGet("/me", GetCurrentUser).RequireAuthorization();

    }

    private static IResult Login(string? returnUrl, IConfiguration configuration) =>
        Results.Challenge(
            new AuthenticationProperties
            {
                RedirectUri = SafeReturnUrl(returnUrl, configuration)
            },
            [OpenIdConnectDefaults.AuthenticationScheme]);

    private static IResult Logout(string? returnUrl, IConfiguration configuration) =>
        Results.SignOut(
            new AuthenticationProperties
            {
                RedirectUri = SafeReturnUrl(returnUrl, configuration)
            },
            [
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme
            ]);

    private static IResult GetCurrentUser(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue("oid")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? string.Empty;
        var displayName = principal.FindFirstValue("name")
            ?? principal.Identity?.Name
            ?? "Usuario Gaia";
        var email = principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? string.Empty;
        var roles = principal.FindAll("roles")
            .Select(claim => claim.Value)
            .Concat(principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Results.Ok(new CurrentUserResponse(
            id,
            displayName,
            email,
            roles));
    }

    private static string SafeReturnUrl(string? requested, IConfiguration configuration)
    {
        var configured = configuration["WebApplication:BaseUrl"]
            ?? "https://localhost:3000";
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var allowed))
        {
            throw new InvalidOperationException("WebApplication:BaseUrl is invalid.");
        }

        if (Uri.TryCreate(requested, UriKind.Absolute, out var candidate)
            && string.Equals(candidate.Scheme, allowed.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Host, allowed.Host, StringComparison.OrdinalIgnoreCase)
            && candidate.Port == allowed.Port)
        {
            return candidate.ToString();
        }

        return allowed.ToString();
    }
}

public sealed record CurrentUserResponse(
    string Id,
    string DisplayName,
    string Email,
    IReadOnlyCollection<string> Roles);
