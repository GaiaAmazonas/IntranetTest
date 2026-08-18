using System.Net.Http.Json;
using Gaia.Modules.ThirdParties;

namespace Gaia.Api.Infrastructure.Dataverse.ThirdParties;

internal sealed class DataversePersonnelImporter(IDataverseDelegatedClientFactory clientFactory,
    IThirdPartyReader thirdPartyReader, IThirdPartyWriter thirdPartyWriter, IDocumentTypeReader documentTypeReader,
    ICollaboratorEmailStore emailStore, ICollaboratorPhoneStore phoneStore) : IAdministrativePersonnelImporter
{
    private const string JobTable = "gaia_cargo";
    internal const string OmittedDocument = "CI 43.532.683 / PASAPORTE FY598210";

    public async Task<PersonnelImportValidation> ValidateAsync(Stream workbook, CancellationToken token)
    {
        PersonnelWorkbook source;
        try { source = PersonnelWorkbookReader.Read(workbook); }
        catch (Exception exception) when (exception is not OperationCanceledException) { return Invalid(exception.Message); }
        var client = await clientFactory.CreateAsync();
        var types = await documentTypeReader.ListAsync(token);
        var thirdParty = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        var job = await DataverseMetadataResolver.TableAsync(client, JobTable, token);
        var email = await DataverseMetadataResolver.TableAsync(client, "gaia_correocolaborador", token);
        var phone = await DataverseMetadataResolver.TableAsync(client, "gaia_telefonocolaborador", token);
        var sexes = await DataverseMetadataResolver.ChoicesAsync(client, "gaia_terceros", thirdParty.Attribute("gaia_Sexo"), token);
        _ = await DataverseMetadataResolver.ChoicesAsync(client, "gaia_telefonocolaborador", phone.Attribute("gaia_Tipodetelefono"), token);
        var constraints = new PersonnelConstraints(
            await DataverseMetadataResolver.ConstraintsAsync(client, "gaia_terceros", token),
            await DataverseMetadataResolver.ConstraintsAsync(client, JobTable, token),
            await DataverseMetadataResolver.ConstraintsAsync(client, "gaia_correocolaborador", token),
            await DataverseMetadataResolver.ConstraintsAsync(client, "gaia_telefonocolaborador", token));
        var validation = Analyze(source, types.Select(x => x.Name), sexes.Keys, constraints);
        if (!validation.Valid) return validation;
        return await AddPreview(validation, source, client, job, constraints, token);
    }

    public async Task<PersonnelImportResult> ImportAsync(Stream workbook, CancellationToken token)
    {
        using var copy = new MemoryStream(); await workbook.CopyToAsync(copy, token); copy.Position = 0;
        var validation = await ValidateAsync(copy, token); if (!validation.Valid) return Empty(validation);
        copy.Position = 0; var source = PersonnelWorkbookReader.Read(copy);
        var excluded = ExcludedDocuments(source, validation.Issues);
        var types = (await documentTypeReader.ListAsync(token)).Where(x => x.IsActive)
            .ToDictionary(x => PersonnelWorkbookReader.Key(x.Name), StringComparer.OrdinalIgnoreCase);
        var existingParties = (await thirdPartyReader.ListAsync(null, token)).ToDictionary(
            x => Identity(x.DocumentType, x.DocumentNumber), StringComparer.OrdinalIgnoreCase);
        var client = await clientFactory.CreateAsync(); var jobsMetadata = await DataverseMetadataResolver.TableAsync(client, JobTable, token);
        var existingJobs = await ReadJobs(client, jobsMetadata, token); var jobsCreated = 0; var jobsExisting = 0;
        foreach (var row in source.Required("Listado de cargos").Rows)
        {
            var name = row.Get("Cargo"); var key = PersonnelWorkbookReader.Key(name);
            if (existingJobs.Contains(key)) { jobsExisting++; continue; }
            using var response = await client.PostAsJsonAsync(jobsMetadata.EntitySetName,
                new Dictionary<string, object?> { [jobsMetadata.PrimaryNameAttribute] = name, ["statecode"] = 0 }, token);
            await Ensure(response, token); existingJobs.Add(key); jobsCreated++;
        }
        var baseByDocument = source.Required("Base Personal activo 2026").Rows
            .Where(x => Document(x.Get("CEDULA")).Length > 0).GroupBy(x => Document(x.Get("CEDULA")))
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var created = 0; var already = 0; var collaboratorErrors = 0; var incidents = validation.Issues.ToList();
        foreach (var row in source.Required("Directorio personal").Rows)
        {
            var document = Document(row.Get("Número de documento")); if (excluded.Contains(document)) continue;
            var typeKey = PersonnelWorkbookReader.Key(row.Get("Tipo de documento")); var identity = Identity(typeKey, document);
            if (existingParties.ContainsKey(identity)) { already++; continue; }
            var sex = baseByDocument.TryGetValue(document, out var baseRow) ? PersonnelWorkbookReader.Key(baseRow.Get("SEXO")) : "";
            var command = new ThirdPartyWriteCommand(types[typeKey].Id, document, row.Get("Primer nombre"), PersonnelWorkbookReader.Optional(row.Get("Segundo nombre")),
                row.Get("Primer apellido"), PersonnelWorkbookReader.Optional(row.Get("Segundo apellido")), sex, null, null, true, "administrative-import");
            var result = await thirdPartyWriter.CreateAsync(command, token);
            if (result.Status == ThirdPartyWriteStatus.Created && result.Id.HasValue)
            {
                created++; existingParties[identity] = new(result.Id.Value, DataverseThirdPartyStore.BuildFullName(command), types[typeKey].Id,
                    types[typeKey].Name, document, command.FirstName, command.MiddleName, command.FirstSurname, command.SecondSurname, command.Sex, null, null, true);
            }
            else { collaboratorErrors++; incidents.Add(new(row.Number, "Directorio personal", "COLLABORATOR_NOT_CREATED", $"No se creó el documento {document}: {result.Status}.")); }
        }
        var byDocument = existingParties.Values.GroupBy(x => Document(x.DocumentNumber)).ToDictionary(x => x.Key, x => x.First());
        var emailCounts = await ImportEmails(source.Required("Correos"), byDocument, excluded, incidents, token);
        var phoneCounts = await ImportPhones(source.Required("Teléfonos"), byDocument, excluded, incidents, token);
        return new(validation, validation.Collaborators, created, already, validation.Jobs, jobsCreated, jobsExisting,
            emailCounts.Created, emailCounts.Existing, phoneCounts.Created, phoneCounts.Existing, excluded.Count, incidents,
            CollaboratorsErrors: collaboratorErrors, EmailsUpdated: emailCounts.Updated, EmailsOmitted: emailCounts.Omitted,
            EmailsErrors: emailCounts.Errors, PhonesUpdated: phoneCounts.Updated, PhonesOmitted: phoneCounts.Omitted, PhonesErrors: phoneCounts.Errors);
    }

    internal static PersonnelImportValidation Analyze(PersonnelWorkbook source, IEnumerable<string> documentTypes,
        IEnumerable<string> sexes, PersonnelConstraints? constraints = null)
    {
        constraints ??= PersonnelConstraints.Default;
        var issues = new List<AdministrativeImportIssue>(); var directory = source.Required("Directorio personal"); var jobs = source.Required("Listado de cargos");
        var jobsByPerson = source.Required("Cargos por persona"); var emails = source.Required("Correos"); var phones = source.Required("Teléfonos");
        RequireHeaders(directory, ["Tipo de documento", "Número de documento", "Primer nombre", "Segundo nombre", "Primer apellido", "Segundo apellido"], issues);
        RequireHeaders(jobs, ["Cargo"], issues); RequireHeaders(jobsByPerson, ["Número de documento", "Cargo"], issues);
        RequireHeaders(emails, ["Número de documento", "Correo institucional", "Correo personal"], issues);
        RequireHeaders(phones, ["Número de documento", "Celular personal", "Celular corporativo"], issues);
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in directory.Rows)
        {
            var document = Document(row.Get("Número de documento"));
            var reason = document.Equals(OmittedDocument, StringComparison.OrdinalIgnoreCase) ? "Registro excluido por decisión funcional: contiene dos identificaciones y será revisado manualmente."
                : ValidateCollaboratorRow(row, constraints);
            if (reason is null) continue; excluded.Add(document);
            var name = string.Join(' ', new[] { row.Get("Primer nombre"), row.Get("Segundo nombre"), row.Get("Primer apellido"), row.Get("Segundo apellido") }.Where(x => x.Length > 0));
            issues.Add(new(row.Number, directory.Name, "COLLABORATOR_OMITTED", $"{name} | Documento: {document} | {reason}", "warning"));
        }
        var docs = directory.Rows.Select(x => Document(x.Get("Número de documento"))).Where(x => x.Length > 0 && !excluded.Contains(x)).ToArray();
        AddDuplicates(docs, directory.Name, "DUPLICATE_DOCUMENT", issues);
        var allDocs = directory.Rows.Select(x => Document(x.Get("Número de documento"))).Where(x => x.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var jobSet = jobs.Rows.Select(x => PersonnelWorkbookReader.Key(x.Get("Cargo"))).Where(x => x.Length > 0).ToHashSet();
        AddDuplicates(jobs.Rows.Select(x => PersonnelWorkbookReader.Key(x.Get("Cargo"))), jobs.Name, "DUPLICATE_JOB", issues);
        foreach (var row in jobs.Rows) ValidateLength(row, jobs.Name, row.Get("Cargo"), PersonnelConstraints.Max(constraints.Jobs, "gaia_Nombre"), "JOB_NAME_TOO_LONG", issues, false);
        foreach (var row in jobsByPerson.Rows.Where(x => !excluded.Contains(Document(x.Get("Número de documento"))) && !jobSet.Contains(PersonnelWorkbookReader.Key(x.Get("Cargo"))))) issues.Add(new(row.Number, jobsByPerson.Name, "JOB_NOT_IN_CATALOG", row.Get("Cargo")));
        foreach (var sheet in new[] { jobsByPerson, emails, phones }) foreach (var row in sheet.Rows.Where(x => !excluded.Contains(Document(x.Get("Número de documento"))) && !allDocs.Contains(Document(x.Get("Número de documento"))))) issues.Add(new(row.Number, sheet.Name, "DOCUMENT_NOT_IN_DIRECTORY", row.Get("Número de documento")));
        var knownTypes = documentTypes.Select(PersonnelWorkbookReader.Key).ToHashSet(); foreach (var row in directory.Rows.Where(x => !excluded.Contains(Document(x.Get("Número de documento"))) && !knownTypes.Contains(PersonnelWorkbookReader.Key(x.Get("Tipo de documento"))))) issues.Add(new(row.Number, directory.Name, "UNKNOWN_DOCUMENT_TYPE", row.Get("Tipo de documento")));
        var baseRows = source.Required("Base Personal activo 2026"); var knownSexes = sexes.Select(PersonnelWorkbookReader.Key).ToHashSet();
        foreach (var row in baseRows.Rows.Where(x => !excluded.Contains(Document(x.Get("CEDULA"))) && Document(x.Get("CEDULA")).Length > 0 && !knownSexes.Contains(PersonnelWorkbookReader.Key(x.Get("SEXO"))))) issues.Add(new(row.Number, baseRows.Name, "UNKNOWN_SEX", row.Get("SEXO")));
        ValidateContacts(emails, ["Correo institucional", "Correo personal"], PersonnelConstraints.Max(constraints.Emails, "gaia_Correoelectronico"), "EMAIL_TOO_LONG", excluded, issues);
        ValidateContacts(phones, ["Celular personal", "Celular corporativo"], PersonnelConstraints.Max(constraints.Phones, "gaia_Numero"), "PHONE_TOO_LONG", excluded, issues);
        AddContactDuplicates(emails, ["Correo institucional", "Correo personal"], "DUPLICATE_EMAIL", "SAME_EMAIL_IN_COLUMNS", excluded, issues);
        AddContactDuplicates(phones, ["Celular personal", "Celular corporativo"], "DUPLICATE_PHONE", "SAME_PHONE_IN_COLUMNS", excluded, issues);
        var withoutEmail = emails.Rows.Count(x => !excluded.Contains(Document(x.Get("Número de documento"))) && PersonnelWorkbookReader.Optional(x.Get("Correo institucional")) is null && PersonnelWorkbookReader.Optional(x.Get("Correo personal")) is null);
        var withoutPhone = phones.Rows.Count(x => !excluded.Contains(Document(x.Get("Número de documento"))) && PersonnelWorkbookReader.Optional(x.Get("Celular personal")) is null && PersonnelWorkbookReader.Optional(x.Get("Celular corporativo")) is null);
        foreach (var row in emails.Rows.Where(x => !excluded.Contains(Document(x.Get("Número de documento"))) && PersonnelWorkbookReader.Optional(x.Get("Correo institucional")) is null && PersonnelWorkbookReader.Optional(x.Get("Correo personal")) is null)) issues.Add(new(row.Number, emails.Name, "WITHOUT_EMAIL", "No se crearán correos.", "warning"));
        foreach (var row in phones.Rows.Where(x => !excluded.Contains(Document(x.Get("Número de documento"))) && PersonnelWorkbookReader.Optional(x.Get("Celular personal")) is null && PersonnelWorkbookReader.Optional(x.Get("Celular corporativo")) is null)) issues.Add(new(row.Number, phones.Name, "WITHOUT_PHONE", "No se crearán teléfonos.", "warning"));
        var rows = source.Sheets.ToDictionary(x => x.Key, x => x.Value.Rows.Count, StringComparer.OrdinalIgnoreCase);
        return new(!issues.Any(x => x.Severity == "error"), rows, directory.Rows.Count - excluded.Count, jobSet.Count,
            emails.Rows.Count(x => !excluded.Contains(Document(x.Get("Número de documento"))) && PersonnelWorkbookReader.Optional(x.Get("Correo institucional")) is not null),
            emails.Rows.Count(x => !excluded.Contains(Document(x.Get("Número de documento"))) && PersonnelWorkbookReader.Optional(x.Get("Correo personal")) is not null),
            phones.Rows.Count(x => !excluded.Contains(Document(x.Get("Número de documento"))) && PersonnelWorkbookReader.Optional(x.Get("Celular personal")) is not null),
            phones.Rows.Count(x => !excluded.Contains(Document(x.Get("Número de documento"))) && PersonnelWorkbookReader.Optional(x.Get("Celular corporativo")) is not null), withoutEmail, withoutPhone, issues);
    }

    private async Task<PersonnelImportValidation> AddPreview(PersonnelImportValidation validation, PersonnelWorkbook source,
        HttpClient client, DataverseTableMetadata jobMetadata, PersonnelConstraints constraints, CancellationToken token)
    {
        var excluded = ExcludedDocuments(source, validation.Issues); var parties = await thirdPartyReader.ListAsync(null, token);
        var identities = parties.Select(x => Identity(x.DocumentType, x.DocumentNumber)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directory = source.Required("Directorio personal").Rows.Where(x => !excluded.Contains(Document(x.Get("Número de documento")))).ToArray();
        var collaboratorExisting = directory.Count(x => identities.Contains(Identity(x.Get("Tipo de documento"), x.Get("Número de documento"))));
        var jobs = await ReadJobs(client, jobMetadata, token); var jobNames = source.Required("Listado de cargos").Rows.Select(x => PersonnelWorkbookReader.Key(x.Get("Cargo"))).Distinct().ToArray();
        var byDocument = parties.GroupBy(x => Document(x.DocumentNumber)).ToDictionary(x => x.Key, x => x.First());
        var emailPlan = await PreviewEmails(source.Required("Correos"), byDocument, excluded, token);
        var phonePlan = await PreviewPhones(source.Required("Teléfonos"), byDocument, excluded, token);
        return validation with {
            CollaboratorPlan = new(directory.Length - collaboratorExisting, collaboratorExisting, 0, excluded.Count, 0),
            JobPlan = new(jobNames.Count(x => !jobs.Contains(x)), jobNames.Count(jobs.Contains), 0, 0, 0),
            EmailPlan = emailPlan, PhonePlan = phonePlan
        };
    }

    private async Task<ImportEntityPreview> PreviewEmails(SheetData sheet, Dictionary<string, ThirdPartyResponse> parties, HashSet<string> excluded, CancellationToken token)
    {
        var create=0;var existing=0;var update=0;var omitted=0;
        foreach(var row in sheet.Rows){var doc=Document(row.Get("Número de documento"));var candidates=EmailCandidates(row).ToArray();if(excluded.Contains(doc)){omitted+=candidates.Length;continue;}if(!parties.TryGetValue(doc,out var party)){create+=candidates.Length;continue;}var current=await emailStore.ListAsync(party.Id,token);foreach(var item in candidates){var found=current.FirstOrDefault(x=>NormalizeEmail(x.Email)==NormalizeEmail(item.Value));if(found is null)create++;else if(found.IsPrimary!=item.Primary||!found.IsActive)update++;else existing++;}}
        return new(create,existing,update,omitted,0);
    }
    private async Task<ImportEntityPreview> PreviewPhones(SheetData sheet, Dictionary<string, ThirdPartyResponse> parties, HashSet<string> excluded, CancellationToken token)
    {
        var create=0;var existing=0;var update=0;var omitted=0;
        foreach(var row in sheet.Rows){var doc=Document(row.Get("Número de documento"));var candidates=PhoneCandidates(row).ToArray();if(excluded.Contains(doc)){omitted+=candidates.Length;continue;}if(!parties.TryGetValue(doc,out var party)){create+=candidates.Length;continue;}var current=await phoneStore.ListAsync(party.Id,token);foreach(var item in candidates){var found=current.FirstOrDefault(x=>NormalizePhone(x.Number)==NormalizePhone(item.Value));if(found is null)create++;else if(found.IsPrimary!=item.Primary||!found.IsActive||!found.PhoneType.Equals("CELULAR",StringComparison.OrdinalIgnoreCase))update++;else existing++;}}
        return new(create,existing,update,omitted,0);
    }

    private async Task<ContactCounts> ImportEmails(SheetData sheet, Dictionary<string, ThirdPartyResponse> parties, HashSet<string> excluded, List<AdministrativeImportIssue> issues, CancellationToken token)
    {
        var counts=new ContactCounts();foreach(var row in sheet.Rows){var doc=Document(row.Get("Número de documento"));var candidates=EmailCandidates(row).ToArray();if(excluded.Contains(doc)){counts.Omitted+=candidates.Length;continue;}if(!parties.TryGetValue(doc,out var party)){counts.Errors+=candidates.Length;continue;}var current=await emailStore.ListAsync(party.Id,token);foreach(var item in candidates){var found=current.FirstOrDefault(x=>NormalizeEmail(x.Email)==NormalizeEmail(item.Value));if(found is not null){if(found.IsPrimary!=item.Primary||!found.IsActive){var result=await emailStore.UpdateAsync(party.Id,found.Id,new(NormalizeEmail(found.Email),found.Observations,item.Primary,true,"administrative-import"),token);if(result.Status==RelatedWriteStatus.Updated)counts.Updated++;else counts.Errors++;}else counts.Existing++;continue;}var created=await emailStore.CreateAsync(party.Id,new(NormalizeEmail(item.Value),null,item.Primary,true,"administrative-import"),token);if(created.Status==RelatedWriteStatus.Created)counts.Created++;else{counts.Errors++;issues.Add(new(row.Number,sheet.Name,"EMAIL_NOT_CREATED",$"{item.Value}: {created.Status}"));}}}return counts;
    }
    private async Task<ContactCounts> ImportPhones(SheetData sheet, Dictionary<string, ThirdPartyResponse> parties, HashSet<string> excluded, List<AdministrativeImportIssue> issues, CancellationToken token)
    {
        var counts=new ContactCounts();foreach(var row in sheet.Rows){var doc=Document(row.Get("Número de documento"));var candidates=PhoneCandidates(row).ToArray();if(excluded.Contains(doc)){counts.Omitted+=candidates.Length;continue;}if(!parties.TryGetValue(doc,out var party)){counts.Errors+=candidates.Length;continue;}var current=await phoneStore.ListAsync(party.Id,token);foreach(var item in candidates){var found=current.FirstOrDefault(x=>NormalizePhone(x.Number)==NormalizePhone(item.Value));if(found is not null){if(found.IsPrimary!=item.Primary||!found.IsActive||!found.PhoneType.Equals("CELULAR",StringComparison.OrdinalIgnoreCase)){var result=await phoneStore.UpdateAsync(party.Id,found.Id,new(NormalizePhone(found.Number),found.Extension,found.Observations,item.Primary,"CELULAR",true,"administrative-import"),token);if(result.Status==RelatedWriteStatus.Updated)counts.Updated++;else counts.Errors++;}else counts.Existing++;continue;}var created=await phoneStore.CreateAsync(party.Id,new(NormalizePhone(item.Value),null,null,item.Primary,"CELULAR",true,"administrative-import"),token);if(created.Status==RelatedWriteStatus.Created)counts.Created++;else{counts.Errors++;issues.Add(new(row.Number,sheet.Name,"PHONE_NOT_CREATED",$"{item.Value}: {created.Status}"));}}}return counts;
    }

    private static IEnumerable<(string Value,bool Primary)> EmailCandidates(WorkbookRow row){var institutional=PersonnelWorkbookReader.Optional(row.Get("Correo institucional"));var personal=PersonnelWorkbookReader.Optional(row.Get("Correo personal"));return new[]{(Value:institutional,Primary:true),(Value:personal,Primary:institutional is null)}.Where(x=>x.Value is not null).GroupBy(x=>NormalizeEmail(x.Value!),StringComparer.OrdinalIgnoreCase).Select(x=>(x.Key,x.Any(y=>y.Primary)));}
    private static IEnumerable<(string Value,bool Primary)> PhoneCandidates(WorkbookRow row){var personal=PersonnelWorkbookReader.Optional(row.Get("Celular personal"));var corporate=PersonnelWorkbookReader.Optional(row.Get("Celular corporativo"));return new[]{(Value:personal,Primary:corporate is null),(Value:corporate,Primary:true)}.Where(x=>x.Value is not null).GroupBy(x=>NormalizePhone(x.Value!),StringComparer.OrdinalIgnoreCase).Select(x=>(x.Key,x.Any(y=>y.Primary)));}
    private static string? ValidateCollaboratorRow(WorkbookRow row, PersonnelConstraints constraints)
    {
        foreach(var item in new[]{("Número de documento",row.Get("Número de documento"),PersonnelConstraints.Max(constraints.Parties,"gaia_NumeroDocumento")),("Primer nombre",row.Get("Primer nombre"),PersonnelConstraints.Max(constraints.Parties,"gaia_PrimerNombre")),("Primer apellido",row.Get("Primer apellido"),PersonnelConstraints.Max(constraints.Parties,"gaia_PrimerApellido"))}){if(string.IsNullOrWhiteSpace(item.Item2))return $"{item.Item1} es obligatorio.";if(item.Item3.HasValue&&item.Item2.Length>item.Item3)return $"{item.Item1} excede la longitud máxima de {item.Item3}.";}
        return null;
    }
    private static void ValidateContacts(SheetData sheet,string[] columns,int? max,string code,HashSet<string> excluded,List<AdministrativeImportIssue> issues){if(!max.HasValue)return;foreach(var row in sheet.Rows.Where(x=>!excluded.Contains(Document(x.Get("Número de documento")))))foreach(var column in columns){var value=PersonnelWorkbookReader.Optional(row.Get(column));if(value?.Length>max)issues.Add(new(row.Number,sheet.Name,code,$"{column} excede la longitud máxima de {max}."));}}
    private static void ValidateLength(WorkbookRow row,string sheet,string value,int? max,string code,List<AdministrativeImportIssue> issues,bool warning){if(max.HasValue&&value.Length>max)issues.Add(new(row.Number,sheet,code,$"El valor excede la longitud máxima de {max}.",warning?"warning":"error"));}
    private static HashSet<string> ExcludedDocuments(PersonnelWorkbook source,IReadOnlyList<AdministrativeImportIssue> issues){var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var row in source.Required("Directorio personal").Rows)if(row.Get("Número de documento").Equals(OmittedDocument,StringComparison.OrdinalIgnoreCase))result.Add(Document(row.Get("Número de documento")));foreach(var issue in issues.Where(x=>x.Code=="COLLABORATOR_OMITTED")){var row=source.Required("Directorio personal").Rows.FirstOrDefault(x=>x.Number==issue.Row);if(row is not null)result.Add(Document(row.Get("Número de documento")));}return result;}
    private static async Task<HashSet<string>> ReadJobs(HttpClient client,DataverseTableMetadata metadata,CancellationToken token)=>(await DataverseJson.ReadAllAsync(client,$"{metadata.EntitySetName}?$select={metadata.PrimaryNameAttribute}",token)).Select(x=>PersonnelWorkbookReader.Key(x.GetProperty(metadata.PrimaryNameAttribute).GetString())).ToHashSet();
    private static void RequireHeaders(SheetData sheet,string[] headers,List<AdministrativeImportIssue> issues){var set=sheet.Headers.Select(PersonnelWorkbookReader.Key).ToHashSet();foreach(var header in headers.Where(x=>!set.Contains(PersonnelWorkbookReader.Key(x))))issues.Add(new(1,sheet.Name,"MISSING_HEADER",header));}
    private static void AddDuplicates(IEnumerable<string> values,string sheet,string code,List<AdministrativeImportIssue> issues){foreach(var item in values.Where(x=>x.Length>0).GroupBy(x=>x,StringComparer.OrdinalIgnoreCase).Where(x=>x.Count()>1))issues.Add(new(0,sheet,code,item.Key));}
    private static void AddContactDuplicates(SheetData sheet,string[] columns,string duplicateCode,string sameRowCode,HashSet<string> excluded,List<AdministrativeImportIssue> issues){var values=columns.SelectMany(column=>sheet.Rows.Where(row=>!excluded.Contains(Document(row.Get("Número de documento")))).Select(row=>new{row.Number,Document=Document(row.Get("Número de documento")),Value=PersonnelWorkbookReader.Optional(row.Get(column))})).Where(x=>x.Value is not null).GroupBy(x=>x.Value!,StringComparer.OrdinalIgnoreCase).Where(x=>x.Count()>1);foreach(var group in values){var rows=group.ToArray();var same=rows.Select(x=>x.Document).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1;issues.Add(new(rows[0].Number,sheet.Name,same?sameRowCode:duplicateCode,same?$"El valor {group.Key} aparece en dos columnas; se consolidará.":$"El valor {group.Key} está asociado a diferentes colaboradores.",same?"warning":"error"));}}
    private static string Document(string value)=>PersonnelWorkbookReader.Normalize(value).Replace(".0","",StringComparison.Ordinal);
    private static string Identity(string type,string document)=>$"{PersonnelWorkbookReader.Key(type)}:{Document(document)}";
    private static string NormalizeEmail(string value)=>PersonnelWorkbookReader.Normalize(value).ToLowerInvariant();
    private static string NormalizePhone(string value)=>PersonnelWorkbookReader.Normalize(value);
    private static PersonnelImportValidation Invalid(string detail)=>new(false,new Dictionary<string,int>(),0,0,0,0,0,0,0,0,[new(0,"Libro","INVALID_WORKBOOK",detail)]);
    private static PersonnelImportResult Empty(PersonnelImportValidation validation)=>new(validation,0,0,0,0,0,0,0,0,0,0,validation.Issues.Count,validation.Issues);
    private static async Task Ensure(HttpResponseMessage response,CancellationToken token){if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Dataverse rechazó la carga de cargos ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(token)}");}
    private sealed class ContactCounts{public int Created;public int Existing;public int Updated;public int Omitted;public int Errors;}
}

internal sealed record PersonnelConstraints(IReadOnlyDictionary<string,DataverseAttributeConstraint> Parties,
    IReadOnlyDictionary<string,DataverseAttributeConstraint> Jobs,IReadOnlyDictionary<string,DataverseAttributeConstraint> Emails,
    IReadOnlyDictionary<string,DataverseAttributeConstraint> Phones)
{
    public static int? Max(IReadOnlyDictionary<string,DataverseAttributeConstraint> source,string schema)=>source.TryGetValue(schema,out var value)?value.MaxLength:null;
    internal static PersonnelConstraints Default=>new(Dictionary("gaia_NumeroDocumento",30),Dictionary("gaia_Nombre",200),Dictionary("gaia_Correoelectronico",320),Dictionary("gaia_Numero",100));
    private static Dictionary<string,DataverseAttributeConstraint> Dictionary(string schema,int max)=>new(StringComparer.OrdinalIgnoreCase){[schema]=new(schema.ToLowerInvariant(),max,"ApplicationRequired")};
}
