using System.IO.Compression;
using System.Text;
using Gaia.Api.Infrastructure.Dataverse.ThirdParties;

namespace Gaia.ArchitectureTests;

public sealed class PersonnelWorkbookImportTests
{
    [Fact]
    public void ReadsRequiredSheetsAndNormalizesSentinelPhoneValues()
    {
        using var workbook = BuildWorkbook();
        var source = PersonnelWorkbookReader.Read(workbook);

        Assert.Equal(6, source.Sheets.Count);
        Assert.Equal("123", source.Required("Directorio personal").Rows[0].Get("Número de documento"));
        Assert.Null(PersonnelWorkbookReader.Optional(" NO SE TIENE "));
    }

    [Fact]
    public void DryRunIsValidAndReportsMissingContactAsWarnings()
    {
        using var workbook = BuildWorkbook();
        var source = PersonnelWorkbookReader.Read(workbook);

        var result = DataversePersonnelImporter.Analyze(source, ["CÉDULA DE CIUDADANÍA"], ["MASCULINO", "FEMENINO"]);

        Assert.True(result.Valid);
        Assert.Equal(1, result.Collaborators);
        Assert.Equal(1, result.Jobs);
        Assert.Equal(1, result.WithoutPhone);
        Assert.Contains(result.Issues, x => x.Code == "WITHOUT_PHONE" && x.Severity == "warning");
    }

    [Fact]
    public void DryRunRejectsUnknownDocumentTypeAndSex()
    {
        using var workbook = BuildWorkbook(documentType: "PASAPORTE", sex: "OTRO");
        var result = DataversePersonnelImporter.Analyze(PersonnelWorkbookReader.Read(workbook), ["CÉDULA DE CIUDADANÍA"], ["MASCULINO", "FEMENINO"]);

        Assert.False(result.Valid);
        Assert.Contains(result.Issues, x => x.Code == "UNKNOWN_DOCUMENT_TYPE");
        Assert.Contains(result.Issues, x => x.Code == "UNKNOWN_SEX");
    }

    [Fact]
    public void SameEmailInBothColumnsIsConsolidationWarningNotBlockingError()
    {
        using var workbook = BuildWorkbook(sameEmailInBothColumns: true);
        var result = DataversePersonnelImporter.Analyze(PersonnelWorkbookReader.Read(workbook), ["CÉDULA DE CIUDADANÍA"], ["MASCULINO", "FEMENINO"]);

        Assert.True(result.Valid);
        Assert.Contains(result.Issues, x => x.Code == "SAME_EMAIL_IN_COLUMNS" && x.Severity == "warning");
        Assert.DoesNotContain(result.Issues, x => x.Code == "DUPLICATE_EMAIL");
    }

    [Fact]
    public void KnownCompositeDocumentIsOmittedWithoutBlockingRemainingWorkbook()
    {
        using var workbook = BuildWorkbook(documentNumber: DataversePersonnelImporter.OmittedDocument, includeSecondValidPerson: true);
        var result = DataversePersonnelImporter.Analyze(PersonnelWorkbookReader.Read(workbook), ["CÉDULA DE CIUDADANÍA"], ["MASCULINO", "FEMENINO"]);

        Assert.True(result.Valid);
        Assert.Equal(1, result.Collaborators);
        Assert.Contains(result.Issues, x => x.Code == "COLLABORATOR_OMITTED" && x.Detail.Contains(DataversePersonnelImporter.OmittedDocument));
        Assert.Equal(1, result.InstitutionalEmails);
        Assert.Equal(1, result.PersonalPhones);
    }

    [Fact]
    public void DocumentOverDataverseMaximumIsDetectedBeforeWriting()
    {
        using var workbook = BuildWorkbook(documentNumber: new string('9', 31));
        var result = DataversePersonnelImporter.Analyze(PersonnelWorkbookReader.Read(workbook), ["CÉDULA DE CIUDADANÍA"], ["MASCULINO", "FEMENINO"]);

        Assert.True(result.Valid);
        Assert.Equal(0, result.Collaborators);
        Assert.Contains(result.Issues, x => x.Code == "COLLABORATOR_OMITTED" && x.Detail.Contains("longitud máxima de 30"));
    }

    private static MemoryStream BuildWorkbook(string documentType = "CÉDULA DE CIUDADANÍA", string sex = "MASCULINO", bool sameEmailInBothColumns = false,
        string documentNumber = "123", bool includeSecondValidPerson = false)
    {
        string[] directoryHeader = ["Tipo de documento", "Número de documento", "Primer nombre", "Segundo nombre", "Primer apellido", "Segundo apellido"];
        string[] baseHeader = ["CEDULA", "SEXO"];
        string[] jobHeader = ["Número de documento", "Cargo"];
        string[] emailHeader = ["Número de documento", "Correo institucional", "Correo personal"];
        string[] phoneHeader = ["Número de documento", "Celular personal", "Celular corporativo"];
        string[] directoryRow = [documentType, documentNumber, "ANA", "", "PÉREZ", ""];
        string[] baseRow = [documentNumber, sex];
        string[] jobRow = [documentNumber, "ANALISTA"];
        string[] emailRow = [documentNumber, "ana@gaia.org", sameEmailInBothColumns ? "ana@gaia.org" : ""];
        string[] phoneRow = [documentNumber, "NO SE TIENE", ""];
        var directory = new List<string[]> { directoryHeader, directoryRow };
        var baseRows = new List<string[]> { baseHeader, baseRow };
        var jobsByPerson = new List<string[]> { jobHeader, jobRow };
        var emails = new List<string[]> { emailHeader, emailRow };
        var phones = new List<string[]> { phoneHeader, phoneRow };
        if (includeSecondValidPerson) { directory.Add([documentType,"456","LUIS","","RUIZ",""]); baseRows.Add(["456",sex]); jobsByPerson.Add(["456","ANALISTA"]); emails.Add(["456","luis@gaia.org",""]); phones.Add(["456","3007654321",""]); }
        var sheets = new Dictionary<string, string[][]>
        {
            ["Base Personal activo 2026"] = baseRows.ToArray(),
            ["Directorio personal"] = directory.ToArray(),
            ["Listado de cargos"] = [["Cargo"], ["ANALISTA"]],
            ["Cargos por persona"] = jobsByPerson.ToArray(),
            ["Correos"] = emails.ToArray(),
            ["Teléfonos"] = phones.ToArray()
        };
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Write(zip, "xl/workbook.xml", WorkbookXml(sheets.Keys));
            Write(zip, "xl/_rels/workbook.xml.rels", RelationshipsXml(sheets.Count));
            var index = 1; foreach (var sheet in sheets) Write(zip, $"xl/worksheets/sheet{index++}.xml", SheetXml(sheet.Value));
        }
        stream.Position = 0; return stream;
    }
    private static string WorkbookXml(IEnumerable<string> names) => $"<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>{string.Join("", names.Select((name, i) => $"<sheet name=\"{Escape(name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>"))}</sheets></workbook>";
    private static string RelationshipsXml(int count) => $"<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">{string.Join("", Enumerable.Range(1, count).Select(i => $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>"))}</Relationships>";
    private static string SheetXml(string[][] rows) => $"<?xml version=\"1.0\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>{string.Join("", rows.Select((row, r) => $"<row r=\"{r + 1}\">{string.Join("", row.Select((value, c) => $"<c r=\"{Column(c)}{r + 1}\" t=\"inlineStr\"><is><t>{Escape(value)}</t></is></c>"))}</row>"))}</sheetData></worksheet>";
    private static string Column(int index) { var result = ""; for (var value = index + 1; value > 0; value = (value - 1) / 26) result = (char)('A' + (value - 1) % 26) + result; return result; }
    private static string Escape(string value) => System.Security.SecurityElement.Escape(value) ?? "";
    private static void Write(ZipArchive zip, string path, string content) { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), Encoding.UTF8); writer.Write(content); }
}
