using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.Organization.Infrastructure;

internal static class OrganizationSeeder
{
    private static readonly UnitTypeSeed[] UnitTypes =
    [
        new("DIRECTIVOS", "Directivos", "organization.directivos", "#A0384D", 10),
        new("SUBDIRECCION", "Subdirección", "organization.subdireccion", "#3C838C", 20),
        new("ASESORIA ESTRATEGICA", "Asesoría estratégica", "organization.asesoria", "#6F3873", 30),
        new("COORDINACION DIRECTA", "Coordinación directa", "organization.coordinacion-directa", "#386037", 40),
        new("COORDINACION TRANSVERSAL", "Coordinación transversal", "organization.coordinacion-transversal", "#2F5048", 50),
        new("OPERATIVA", "Operativa", "organization.operativa", "#52685E", 60)
    ];

    // Fuente: RelacionEquipos.xlsx, hoja Organizacional, filas 2-28.
    // La imagen de organigrama difiere en los códigos 20/2001; se conserva el Excel.
    private static readonly UnitSeed[] Units =
    [
        new("10", "JUNTA DIRECTIVA", "DIRECTIVOS", null, 1),
        new("20", "PRESIDENCIA EJECUTIVA", "DIRECTIVOS", null, 1),
        new("2001", "EQUIPO ASESOR", "ASESORIA ESTRATEGICA", "20", 2),
        new("30", "DIRECCION", "DIRECTIVOS", null, 1),
        new("3001", "SUBDIRECCION TECNICA Y POLITICA", "SUBDIRECCION", "30", 2),
        new("300101", "EQUIPO ASESOR", "ASESORIA ESTRATEGICA", "3001", 3),
        new("300102", "ESTRATEGIA REGIONAL PARA LA PROTECCION DE LA AMAZONIA", "COORDINACION DIRECTA", "3001", 3),
        new("30010201", "AMBITOS PUTUMAYO, CAQUETA, ISANA", "OPERATIVA", "300102", 4),
        new("30010202", "AMBITO MACROTERRITORIAL", "OPERATIVA", "300102", 4),
        new("300103", "UNIDAD ADMINISTRATIVA", "OPERATIVA", "3001", 3),
        new("300104", "ESTRATEGIA DIVERSIFICADA DE SOSTENIBILIDAD FINANCIERA", "OPERATIVA", "3001", 3),
        new("300105", "LABORATORIO SOCIOJURIDICO", "COORDINACION TRANSVERSAL", "3001", 3),
        new("300106", "GESTION PUBLICA", "COORDINACION TRANSVERSAL", "3001", 3),
        new("300107", "ORDENAMIENTO TERRITORIAL", "COORDINACION TRANSVERSAL", "3001", 3),
        new("300108", "COMUNICACIONES ESTRATEGICAS", "COORDINACION TRANSVERSAL", "3001", 3),
        new("300109", "SISTEMAS DE INFORMACION", "COORDINACION TRANSVERSAL", "3001", 3),
        new("300110", "ALIANZAS REGIONALES", "COORDINACION DIRECTA", "3001", 3),
        new("3002", "SUBDIRECCION DESARROLLO ESTRATEGICO", "SUBDIRECCION", "30", 2),
        new("300201", "COORDINACION INTEGRAL DE PROYECTOS", "COORDINACION DIRECTA", "3002", 3),
        new("300202", "COORDINACION TALENTO HUMANO, BIENESTAR Y CULTURA ORGANIZACIONAL", "COORDINACION DIRECTA", "3002", 3),
        new("300203", "COORDINACION FINANCIERA", "COORDINACION DIRECTA", "3002", 3),
        new("300204", "TECNOLOGIAS DE LA INFORMACION", "COORDINACION TRANSVERSAL", "3002", 3),
        new("300205", "GESTION DE PROCESOS Y CUMPLIMIENTO INSTITUCIONAL", "COORDINACION TRANSVERSAL", "3002", 3),
        new("300206", "ASESORIA JURIDICA Y LEGAL", "COORDINACION TRANSVERSAL", "3002", 3),
        new("300207", "SERVICIOS LOGISTICOS Y COMPRAS", "COORDINACION TRANSVERSAL", "3002", 3),
        new("300208", "ASESORIAS TRANSVERSALES", "ASESORIA ESTRATEGICA", "3002", 3),
        new("3003", "EQUIPO ASESOR", "ASESORIA ESTRATEGICA", "30", 2)
    ];

    public static async Task SeedAsync(
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        await SeedUnitTypesAsync(context, cancellationToken);
        var site = await SeedSiteAsync(context, cancellationToken);
        await SeedUnitsAsync(context, site.Id, cancellationToken);
    }

    private static async Task SeedUnitTypesAsync(
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var existing = await context.UnitTypes.ToListAsync(cancellationToken);
        var legacyDirectivo = existing.FirstOrDefault(item => item.Code == "DIRECTIVO");
        if (legacyDirectivo is not null)
        {
            legacyDirectivo.Code = "DIRECTIVOS";
        }

        var legacyAdvisory = existing.FirstOrDefault(item => item.Code == "ASESORIA");
        if (legacyAdvisory is not null)
        {
            legacyAdvisory.Code = "ASESORIA ESTRATEGICA";
        }

        var legacyCoordination = existing.FirstOrDefault(item => item.Code == "COORDINACION");
        if (legacyCoordination is not null)
        {
            legacyCoordination.IsActive = false;
            legacyCoordination.Description =
                "Catálogo legado reemplazado por coordinación directa y transversal.";
            legacyCoordination.UpdatedAtUtc = DateTimeOffset.UtcNow;
            legacyCoordination.UpdatedBy = "excel-import";
        }

        foreach (var source in UnitTypes)
        {
            var item = existing.FirstOrDefault(candidate => candidate.Code == source.Code);
            if (item is null)
            {
                item = new UnitType
                {
                    Code = source.Code,
                    Name = source.Name,
                    ColorToken = source.ColorToken,
                    VisualOrder = source.VisualOrder,
                    IsActive = true,
                    Description = $"Color digital de referencia: {source.HexColor}.",
                    CreatedBy = "excel-import"
                };
                context.UnitTypes.Add(item);
                existing.Add(item);
            }
            else
            {
                item.Name = source.Name;
                item.ColorToken = source.ColorToken;
                item.VisualOrder = source.VisualOrder;
                item.IsActive = true;
                item.Description = $"Color digital de referencia: {source.HexColor}.";
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Site> SeedSiteAsync(
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var site = await context.Sites.FirstOrDefaultAsync(
            item => item.Code == "BOG",
            cancellationToken);
        if (site is null)
        {
            site = new Site
            {
                Code = "BOG",
                Name = "Bogotá",
                City = "Bogotá D.C.",
                CreatedBy = "excel-import"
            };
            context.Sites.Add(site);
            await context.SaveChangesAsync(cancellationToken);
        }

        return site;
    }

    private static async Task SeedUnitsAsync(
        OrganizationDbContext context,
        Guid siteId,
        CancellationToken cancellationToken)
    {
        var typesByCode = await context.UnitTypes
            .Where(item => item.IsActive)
            .ToDictionaryAsync(item => item.Code, cancellationToken);
        var existing = await context.Units.ToDictionaryAsync(
            item => item.Code,
            cancellationToken);

        foreach (var source in Units)
        {
            if (!existing.TryGetValue(source.Code, out var item))
            {
                item = new OrganizationalUnit
                {
                    Code = source.Code,
                    Name = source.Name,
                    ShortName = source.Name,
                    UnitTypeId = typesByCode[source.UnitTypeCode].Id,
                    SiteId = siteId,
                    Level = source.SourceLevel - 1,
                    EffectiveFrom = new DateOnly(2021, 1, 1),
                    IsActive = true,
                    CreatedBy = "excel-import"
                };
                context.Units.Add(item);
                existing.Add(source.Code, item);
            }
            else
            {
                item.Name = source.Name;
                item.ShortName = source.Name;
                item.UnitTypeId = typesByCode[source.UnitTypeCode].Id;
                item.SiteId ??= siteId;
                item.Level = source.SourceLevel - 1;
                item.IsActive = true;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var source in Units)
        {
            var item = existing[source.Code];
            item.ParentId = source.ParentCode is null
                ? null
                : existing[source.ParentCode].Id;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record UnitTypeSeed(
        string Code,
        string Name,
        string ColorToken,
        string HexColor,
        int VisualOrder);

    private sealed record UnitSeed(
        string Code,
        string Name,
        string UnitTypeCode,
        string? ParentCode,
        int SourceLevel);
}
