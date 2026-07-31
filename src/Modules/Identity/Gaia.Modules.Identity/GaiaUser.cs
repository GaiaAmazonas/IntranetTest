using Microsoft.AspNetCore.Identity;

namespace Gaia.Modules.Identity;

public sealed class GaiaUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
