using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Gaia.Api.Infrastructure.Dataverse.ThirdParties;

internal sealed record PersonnelWorkbook(IReadOnlyDictionary<string, SheetData> Sheets)
{
    public SheetData Required(string name) => Sheets.TryGetValue(name, out var sheet)
        ? sheet : throw new InvalidDataException($"Falta la hoja obligatoria '{name}'.");
}
internal sealed record SheetData(string Name, IReadOnlyList<string> Headers, IReadOnlyList<WorkbookRow> Rows);
internal sealed record WorkbookRow(int Number, IReadOnlyDictionary<string, string> Values)
{
    public string Get(string header) => Values.TryGetValue(PersonnelWorkbookReader.Key(header), out var value) ? value : "";
}

internal static partial class PersonnelWorkbookReader
{
    private static readonly string[] RequiredSheets = ["Base Personal activo 2026", "Directorio personal", "Listado de cargos",
        "Cargos por persona", "Correos", "Teléfonos"];
    internal static PersonnelWorkbook Read(Stream input, IReadOnlyCollection<string>? requiredSheets = null)
    {
        using var memory = new MemoryStream(); input.CopyTo(memory); memory.Position = 0;
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        var shared = ReadSharedStrings(archive); var relationships = ReadRelationships(archive);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var sheets = new Dictionary<string, SheetData>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in workbook.Descendants(main + "sheet"))
        {
            var name = (string?)element.Attribute("name") ?? "";
            if (!relationships.TryGetValue((string?)element.Attribute(rel + "id") ?? "", out var target)) continue;
            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.Replace("../", "", StringComparison.Ordinal);
            sheets[name] = ReadSheet(archive, path, name, shared);
        }
        foreach (var required in requiredSheets ?? RequiredSheets) if (!sheets.ContainsKey(required)) throw new InvalidDataException($"Falta la hoja obligatoria '{required}'.");
        return new(sheets);
    }

    private static SheetData ReadSheet(ZipArchive archive, string path, string name, IReadOnlyList<string> shared)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = LoadXml(archive, path).Descendants(ns + "row").Select(row => new
        {
            Number = (int?)row.Attribute("r") ?? 0,
            Cells = row.Elements(ns + "c").ToDictionary(cell => Column((string?)cell.Attribute("r") ?? "A1"), cell => CellValue(cell, ns, shared))
        }).ToArray();
        if (rows.Length == 0) return new(name, [], []);
        var header = rows.OrderByDescending(x => x.Cells.Values.Count(v => !string.IsNullOrWhiteSpace(v))).ThenBy(x => x.Number).First();
        var max = header.Cells.Keys.DefaultIfEmpty(-1).Max();
        var headers = Enumerable.Range(0, max + 1).Select(i => Normalize(header.Cells.GetValueOrDefault(i))).ToArray();
        var data = rows.Where(x => x.Number > header.Number && x.Cells.Values.Any(v => !string.IsNullOrWhiteSpace(v))).Select(x =>
            new WorkbookRow(x.Number, headers.Select((h, i) => (h, Value: Normalize(x.Cells.GetValueOrDefault(i))))
                .Where(x => x.h.Length > 0).ToDictionary(x => Key(x.h), x => x.Value, StringComparer.OrdinalIgnoreCase))).ToArray();
        return new(name, headers, data);
    }
    private static string CellValue(XElement cell, XNamespace ns, IReadOnlyList<string> shared)
    {
        var type = (string?)cell.Attribute("t"); var raw = (string?)cell.Element(ns + "v") ?? "";
        if (type == "s" && int.TryParse(raw, out var index) && index < shared.Count) return shared[index];
        if (type == "inlineStr") return string.Concat(cell.Descendants(ns + "t").Select(x => x.Value));
        return raw;
    }
    private static string[] ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml"); if (entry is null) return [];
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var stream = entry.Open(); var document = XDocument.Load(stream);
        return document.Descendants(ns + "si").Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToArray();
    }
    private static Dictionary<string, string> ReadRelationships(ZipArchive archive)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
        return LoadXml(archive, "xl/_rels/workbook.xml.rels").Descendants(ns + "Relationship")
            .ToDictionary(x => (string)x.Attribute("Id")!, x => (string)x.Attribute("Target")!);
    }
    private static XDocument LoadXml(ZipArchive archive, string path)
    { using var stream = archive.GetEntry(path)?.Open() ?? throw new InvalidDataException($"El Excel no contiene {path}."); return XDocument.Load(stream); }
    private static int Column(string reference)
    { var letters = CellLetters().Match(reference).Value; var result = 0; foreach (var c in letters) result = result * 26 + c - 'A' + 1; return result - 1; }
    internal static string Normalize(string? value) => MultipleSpaces().Replace(value?.Trim() ?? "", " ");
    internal static string Key(string? value) => RemoveDiacritics(Normalize(value)).ToUpperInvariant();
    internal static string? Optional(string? value)
    { var cleaned = Normalize(value); return cleaned.Length == 0 || Key(cleaned) is "NO SE TIENE" or "NO TIENE" or "N/A" or "NA" ? null : cleaned; }
    private static string RemoveDiacritics(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Normalize(NormalizationForm.FormC);
    [GeneratedRegex("^[A-Z]+")]
    private static partial Regex CellLetters();
    [GeneratedRegex(@"\s+")]
    private static partial Regex MultipleSpaces();
}
