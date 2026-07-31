using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.Identity.Infrastructure;

public sealed class GaiaIdentityDbContext(
    DbContextOptions<GaiaIdentityDbContext> options)
    : IdentityDbContext<GaiaUser, GaiaRole, Guid>(options)
{
    public const string Schema = "identity";

    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<GaiaUser>(entity =>
        {
            entity.ToTable("users");
            entity.Property(user => user.DisplayName).HasMaxLength(200);
            entity.HasIndex(user => user.Email).IsUnique();
        });
        builder.Entity<GaiaRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");

        builder.Entity<LoginAudit>(entity =>
        {
            entity.ToTable("login_audits");
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Email).HasMaxLength(256);
            entity.Property(audit => audit.IpAddress).HasMaxLength(64);
            entity.Property(audit => audit.FailureReason).HasMaxLength(100);
            entity.HasIndex(audit => audit.OccurredAtUtc);
        });
    }
}

public sealed class LoginAudit
{
    public long Id { get; init; }

    public Guid? UserId { get; init; }

    public required string Email { get; init; }

    public bool WasSuccessful { get; init; }

    public string? FailureReason { get; init; }

    public string? IpAddress { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
