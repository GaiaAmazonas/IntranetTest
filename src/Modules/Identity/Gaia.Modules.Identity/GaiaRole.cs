using Microsoft.AspNetCore.Identity;

namespace Gaia.Modules.Identity;

public sealed class GaiaRole : IdentityRole<Guid>
{
    public string? Description { get; set; }
}
