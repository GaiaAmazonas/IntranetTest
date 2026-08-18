using Gaia.BuildingBlocks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Identity.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gaia.Modules.Identity;

public sealed class IdentityModule : IModule
{
    public static string Name => "Identity";
}

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        var dataverseScope = configuration["Dataverse:Scope"]
            ?? throw new InvalidOperationException("Dataverse:Scope is required.");

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddMicrosoftIdentityWebApp(
                configuration.GetSection("MicrosoftEntra"),
                openIdConnectScheme: OpenIdConnectDefaults.AuthenticationScheme,
                cookieScheme: CookieAuthenticationDefaults.AuthenticationScheme)
            .EnableTokenAcquisitionToCallDownstreamApi([dataverseScope])
            .AddInMemoryTokenCaches();

        services.Configure<CookieAuthenticationOptions>(
            CookieAuthenticationDefaults.AuthenticationScheme,
            options =>
        {
            options.Cookie.Name = "__Host-Gaia.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = isDevelopment
                ? SameSiteMode.None
                : SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.Configure<OpenIdConnectOptions>(
            OpenIdConnectDefaults.AuthenticationScheme,
            options =>
            {
                options.ResponseType = "code";
                options.SaveTokens = false;
                options.GetClaimsFromUserInfoEndpoint = false;
            });

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        IdentityEndpoints.Map(endpoints);
        return endpoints;
    }
}
