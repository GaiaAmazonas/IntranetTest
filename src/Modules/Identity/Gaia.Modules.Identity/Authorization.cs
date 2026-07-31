namespace Gaia.Modules.Identity;

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
