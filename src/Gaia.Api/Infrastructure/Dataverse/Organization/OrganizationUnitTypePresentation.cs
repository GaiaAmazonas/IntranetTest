namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal static class OrganizationUnitTypePresentation
{
    private static readonly Dictionary<string, Presentation> ByCode =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DIR"] = new("organization.directivos", 10),
            ["SUB"] = new("organization.subdireccion", 20),
            ["ASE"] = new("organization.asesoria", 30),
            ["COD"] = new("organization.coordinacion-directa", 40),
            ["COT"] = new("organization.coordinacion-transversal", 50),
            ["OPE"] = new("organization.operativa", 60)
        };

    public static Presentation Get(string code) =>
        ByCode.TryGetValue(code, out var value)
            ? value
            : new Presentation("organization.default", 0);

    internal sealed record Presentation(string ColorToken, int VisualOrder);
}
