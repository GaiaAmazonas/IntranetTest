using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.ThirdParties.Infrastructure;

public sealed class ThirdPartiesDbContext(DbContextOptions<ThirdPartiesDbContext> options)
    : DbContext(options)
{
    public const string Schema = "third_parties";
    public DbSet<ThirdParty> ThirdParties => Set<ThirdParty>();
    public DbSet<Engagement> Engagements => Set<Engagement>();
    public DbSet<OrganizationalAssignment> Assignments => Set<OrganizationalAssignment>();
    public DbSet<Education> Studies => Set<Education>();
    public DbSet<LanguageSkill> Languages => Set<LanguageSkill>();
    public DbSet<Training> Trainings => Set<Training>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<ImportIssue> ImportIssues => Set<ImportIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<ThirdParty>(entity =>
        {
            entity.ToTable("third_parties");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DocumentType).HasMaxLength(30);
            entity.Property(item => item.DocumentNumber).HasMaxLength(50);
            entity.Property(item => item.FullName).HasMaxLength(250);
            entity.Property(item => item.PersonType).HasMaxLength(30);
            entity.Property(item => item.CreatedBy).HasMaxLength(256);
            entity.HasIndex(item => new { item.DocumentType, item.DocumentNumber }).IsUnique();
            entity.HasIndex(item => item.FullName);
        });
        Child<Engagement>(modelBuilder, "engagements");
        Child<OrganizationalAssignment>(modelBuilder, "organizational_assignments");
        Child<Education>(modelBuilder, "studies");
        Child<LanguageSkill>(modelBuilder, "languages");
        Child<Training>(modelBuilder, "trainings");
        Child<Experience>(modelBuilder, "experiences");
        Child<EmergencyContact>(modelBuilder, "emergency_contacts");
        modelBuilder.Entity<ImportIssue>(entity =>
        {
            entity.ToTable("import_issues");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.BatchId).HasMaxLength(50);
            entity.Property(item => item.Severity).HasMaxLength(20);
            entity.Property(item => item.Code).HasMaxLength(50);
            entity.HasIndex(item => item.BatchId);
        });
    }

    private static void Child<TEntity>(ModelBuilder modelBuilder, string table)
        where TEntity : ThirdPartyChild
    {
        modelBuilder.Entity<TEntity>(entity =>
        {
            entity.ToTable(table);
            entity.HasKey(item => item.Id);
            entity.HasOne(item => item.ThirdParty)
                .WithMany()
                .HasForeignKey(item => item.ThirdPartyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
