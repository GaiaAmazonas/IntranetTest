namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed record OrganizationImportRow(
    int SourceRow,
    string Code,
    string Name,
    string UnitType,
    string? ParentCode,
    string Status,
    int Level);

internal static class OrganizationImportSource
{
    public static readonly OrganizationImportRow[] Rows =
    [
        new(2, "10", "JUNTA DIRECTIVA", "DIRECTIVOS", null, "Activo", 1),
        new(3, "20", "PRESIDENCIA EJECUTIVA", "DIRECTIVOS", null, "Activo", 1),
        new(4, "2001", "EQUIPO ASESOR", "ASESORIA ESTRATEGICA", "20", "Activo", 2),
        new(5, "30", "DIRECCION", "DIRECTIVOS", null, "Activo", 1),
        new(6, "3001", "SUBDIRECCION TECNICA Y POLITICA", "SUBDIRECCION", "30", "Activo", 2),
        new(7, "300101", "EQUIPO ASESOR", "ASESORIA ESTRATEGICA", "3001", "Activo", 3),
        new(8, "300102", "ESTRATEGIA REGIONAL PARA LA PROTECCION DE LA AMAZONIA", "COORDINACION DIRECTA", "3001", "Activo", 3),
        new(9, "30010201", "AMBITOS PUTUMAYO, CAQUETA, ISANA", "OPERATIVA", "300102", "Activo", 4),
        new(10, "30010202", "AMBITO MACROTERRITORIAL", "OPERATIVA", "300102", "Activo", 4),
        new(11, "300103", "UNIDAD ADMINISTRATIVA", "OPERATIVA", "3001", "Activo", 3),
        new(12, "300104", "ESTRATEGIA DIVERSIFICADA DE SOSTENIBILIDAD FINANCIERA", "OPERATIVA", "3001", "Activo", 3),
        new(13, "300105", "LABORATORIO SOCIOJURIDICO", "COORDINACION TRANSVERSAL", "3001", "Activo", 3),
        new(14, "300106", "GESTION PUBLICA", "COORDINACION TRANSVERSAL", "3001", "Activo", 3),
        new(15, "300107", "ORDENAMIENTO TERRITORIAL", "COORDINACION TRANSVERSAL", "3001", "Activo", 3),
        new(16, "300108", "COMUNICACIONES ESTRATEGICAS", "COORDINACION TRANSVERSAL", "3001", "Activo", 3),
        new(17, "300109", "SISTEMAS DE INFORMACION", "COORDINACION TRANSVERSAL", "3001", "Activo", 3),
        new(18, "300110", "ALIANZAS REGIONALES", "COORDINACION DIRECTA", "3001", "Activo", 3),
        new(19, "3002", "SUBDIRECCION DESARROLLO ESTRATEGICO", "SUBDIRECCION", "30", "Activo", 2),
        new(20, "300201", "COORDINACION INTEGRAL DE PROYECTOS", "COORDINACION DIRECTA", "3002", "Activo", 3),
        new(21, "300202", "COORDINACION TALENTO HUMANO, BIENESTAR Y CULTURA ORGANIZACIONAL", "COORDINACION DIRECTA", "3002", "Activo", 3),
        new(22, "300203", "COORDINACION FINANCIERA", "COORDINACION DIRECTA", "3002", "Activo", 3),
        new(23, "300204", "TECNOLOGIAS DE LA INFORMACION", "COORDINACION TRANSVERSAL", "3002", "Activo", 3),
        new(24, "300205", "GESTION DE PROCESOS Y CUMPLIMIENTO INSTITUCIONAL", "COORDINACION TRANSVERSAL", "3002", "Activo", 3),
        new(25, "300206", "ASESORIA JURIDICA Y LEGAL", "COORDINACION TRANSVERSAL", "3002", "Activo", 3),
        new(26, "300207", "SERVICIOS LOGISTICOS Y COMPRAS", "COORDINACION TRANSVERSAL", "3002", "Activo", 3),
        new(27, "300208", "ASESORIAS TRANSVERSALES", "ASESORIA ESTRATEGICA", "3002", "Activo", 3),
        new(28, "3003", "EQUIPO ASESOR", "ASESORIA ESTRATEGICA", "30", "Activo", 2)
    ];
}
