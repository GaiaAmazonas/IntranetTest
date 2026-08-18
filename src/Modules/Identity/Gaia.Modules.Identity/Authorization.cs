namespace Gaia.Modules.Identity;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;

public static class GaiaClaims
{
    public const string Permission = "gaia:permission";
}

public static class GaiaRoles
{
    public const string PlatformAdministrator = "PlatformAdministrator";
}

public static class GaiaPermissions
{
    public const string UsersRead = "identity.users.read";
    public const string UsersManage = "identity.users.manage";

    public static readonly string[] All =
    [
        UsersRead,
        UsersManage
    ];
}

internal sealed class GaiaClaimsTransformation(IConfiguration configuration)
    : IClaimsTransformation
{
    private static readonly string[] ReadPermissions =
    [
        GaiaPermissions.UsersRead,
        "organization.read",
        "third-parties.read",
        "inventory.read"
    ];

    private static readonly string[] ManagePermissions =
    [
        .. ReadPermissions,
        GaiaPermissions.UsersManage,
        "organization.manage",
        "third-parties.manage",
        "inventory.manage"
    ];

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var email = principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? string.Empty;
        var roles = principal.FindAll("roles")
            .Select(claim => claim.Value)
            .Concat(principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var administrators = configuration
            .GetSection("Authorization:BootstrapAdministrators")
            .Get<string[]>() ?? [];

        var isAdministrator = roles.Contains("Gaia.Administrator")
            || administrators.Contains(email, StringComparer.OrdinalIgnoreCase);
        var permissions = isAdministrator ? ManagePermissions : ReadPermissions;

        foreach (var permission in permissions)
        {
            if (!identity.HasClaim(GaiaClaims.Permission, permission))
            {
                identity.AddClaim(new Claim(GaiaClaims.Permission, permission));
            }
        }

        return Task.FromResult(principal);
    }
}
