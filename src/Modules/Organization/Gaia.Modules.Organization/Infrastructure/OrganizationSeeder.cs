using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.Organization.Infrastructure;

internal static class OrganizationSeeder
{
    public static async Task SeedAsync(
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        if (!await context.UnitTypes.AnyAsync(cancellationToken))
        {
            context.UnitTypes.AddRange(
                Type("DIRECTIVO", "Directivo", "organization.directivo", 10),
                Type("SUBDIRECCION", "Subdirección", "organization.subdireccion", 20),
                Type("ASESORIA", "Asesoría estratégica", "organization.asesoria", 30),
                Type("COORDINACION", "Coordinación", "organization.coordinacion", 40),
                Type("OPERATIVA", "Unidad operativa", "organization.operativa", 50));
        }

        if (!await context.Sites.AnyAsync(cancellationToken))
        {
            context.Sites.Add(new Site
            {
                Code = "BOG",
                Name = "Bogotá",
                City = "Bogotá D.C.",
                CreatedBy = "system"
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static UnitType Type(
        string code,
        string name,
        string colorToken,
        int visualOrder) =>
        new()
        {
            Code = code,
            Name = name,
            ColorToken = colorToken,
            VisualOrder = visualOrder,
            CreatedBy = "system"
        };
}
