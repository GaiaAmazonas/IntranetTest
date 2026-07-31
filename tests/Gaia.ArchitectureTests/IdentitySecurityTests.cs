using Gaia.Modules.Identity;
using Microsoft.AspNetCore.Identity;

namespace Gaia.ArchitectureTests;

public sealed class IdentitySecurityTests
{
    [Fact]
    public void PasswordHasherDoesNotStorePlainTextAndVerifiesPassword()
    {
        var user = new GaiaUser
        {
            Id = Guid.NewGuid(),
            UserName = "test@gaia.local",
            Email = "test@gaia.local",
            DisplayName = "Test User"
        };
        const string password = "Secure!Password2026";
        var hasher = new PasswordHasher<GaiaUser>();

        var hash = hasher.HashPassword(user, password);
        var verification = hasher.VerifyHashedPassword(user, hash, password);

        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
        Assert.NotEqual(PasswordVerificationResult.Failed, verification);
    }

    [Fact]
    public void PermissionsAreUniqueAndNamespaced()
    {
        Assert.Equal(GaiaPermissions.All.Length, GaiaPermissions.All.Distinct().Count());
        Assert.All(
            GaiaPermissions.All,
            permission => Assert.StartsWith("identity.", permission, StringComparison.Ordinal));
    }
}
