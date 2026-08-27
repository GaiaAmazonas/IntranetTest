using System.Text.Json;
using Gaia.Modules.ThirdParties;

namespace Gaia.Api.Infrastructure.Dataverse.ThirdParties;

internal sealed class DataverseIntranetDirectoryReader(IDataverseDelegatedClientFactory clientFactory)
    : IIntranetDirectoryReader
{
    private const string ThirdPartyTable = "gaia_terceros";
    private const string EmailTable = "gaia_correocolaborador";
    private const string PhoneTable = "gaia_telefonocolaborador";
    private const string AssignmentTable = "gaia_asignacionorganizacional";
    private const string PositionTable = "gaia_cargo";
    private const string UnitTable = "gaia_organizacion";
    private const string SiteTable = "gaia_sede";

    public async Task<IntranetPeoplePage> ListPeopleAsync(string? search, int page, int pageSize, CancellationToken token)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 12, 60);
        var client = await clientFactory.CreateAsync();
        var thirdPartyTask = DataverseMetadataResolver.TableAsync(client, ThirdPartyTable, token);
        var emailTask = DataverseMetadataResolver.TableAsync(client, EmailTable, token);
        var phoneTask = DataverseMetadataResolver.TableAsync(client, PhoneTable, token);
        await Task.WhenAll(thirdPartyTask, emailTask, phoneTask);
        var thirdParty = await thirdPartyTask;
        var email = await emailTask;
        var phone = await phoneTask;
        var name = thirdParty.Attribute("gaia_Nombretercero");
        var filter = "statecode eq 0";
        if (!string.IsNullOrWhiteSpace(search))
            filter += $" and contains({name},'{Escape(search.Trim())}')";

        // Dataverse no admite $skip. Solicitamos únicamente los registros necesarios
        // para alcanzar la página actual y recortamos el bloque final localmente.
        var requiredRows = page * pageSize;
        var result = await DataverseJson.ReadPageAsync(client,
            $"{thirdParty.EntitySetName}?$select={thirdParty.PrimaryIdAttribute},{name}&$filter={filter}&$orderby={name}&$count=true&$top={requiredRows}", token);
        var selected = result.Items.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var ids = selected.Select(row => GuidValue(row, thirdParty.PrimaryIdAttribute)).ToArray();
        var emailRows = ReadInstitutionalEmails(client, email, ids, token);
        var phoneRows = ReadCorporatePhones(client, phone, ids, token);
        var assignmentRows = ReadOrganizationalDetails(client, ids, token);
        await Task.WhenAll(emailRows, phoneRows, assignmentRows);
        var emails = await emailRows;
        var phones = await phoneRows;
        var assignments = await assignmentRows;

        var people = selected.Select(row =>
        {
            var id = GuidValue(row, thirdParty.PrimaryIdAttribute);
            assignments.TryGetValue(id, out var assignment);
            return new IntranetPerson(id, StringValue(row, name) ?? "Sin nombre", assignment?.Position, assignment?.Unit, assignment?.Site,
                emails.GetValueOrDefault(id), phones.GetValueOrDefault(id), null);
        }).ToArray();
        return new(people, page, pageSize, result.Total);
    }

    public async Task<IReadOnlyList<IntranetBirthday>> ListBirthdaysAsync(int month, CancellationToken token)
    {
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var client = await clientFactory.CreateAsync();
        var thirdParty = await DataverseMetadataResolver.TableAsync(client, ThirdPartyTable, token);
        var name = thirdParty.Attribute("gaia_Nombretercero");
        var birthDate = thirdParty.Attribute("gaia_FechaNacimiento");
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{thirdParty.EntitySetName}?$select={thirdParty.PrimaryIdAttribute},{name},{birthDate}&$filter=statecode eq 0 and {birthDate} ne null&$orderby={name}", token);
        return rows.Select(row => new
            {
                Id = GuidValue(row, thirdParty.PrimaryIdAttribute),
                Name = StringValue(row, name) ?? "Sin nombre",
                BirthDate = DataverseThirdPartyStore.ParseDateOnly(StringValue(row, birthDate))
            })
            .Where(item => item.BirthDate?.Month == month)
            .OrderBy(item => item.BirthDate!.Value.Day)
            .ThenBy(item => item.Name)
            .Select(item => new IntranetBirthday(item.Id, item.Name, item.BirthDate!.Value.Day, month, null))
            .ToArray();
    }

    private static async Task<Dictionary<Guid, string>> ReadInstitutionalEmails(
        HttpClient client,
        DataverseTableMetadata email,
        Guid[] ids,
        CancellationToken token)
    {
        if (ids.Length == 0) return [];
        var parent = email.Attribute("gaia_Tercero");
        var address = email.Attribute("gaia_Correoelectronico");
        var primary = email.Attribute("gaia_Principal");
        var contactType = email.Attribute("gaia_Tipocorreo");
        var corporateType = email.EncodedIntegerLiteral("gaia_Tipocorreo", 2);
        var idFilter = string.Join(" or ", ids.Select(id => $"_{parent}_value eq {id:D}"));
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{email.EntitySetName}?$select=_{parent}_value,{address},{primary}&$filter=statecode eq 0 and {contactType} eq {corporateType} and ({idFilter})", token);
        return rows.Select(row => new
            {
                Parent = OptionalGuid(row, $"_{parent}_value"),
                Address = StringValue(row, address),
                Primary = BoolValue(row, primary)
            })
            .Where(item => item.Parent.HasValue && !string.IsNullOrWhiteSpace(item.Address))
            .GroupBy(item => item.Parent!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Primary).ThenBy(item => item.Address).First().Address!);
    }

    private static async Task<Dictionary<Guid, string>> ReadCorporatePhones(HttpClient client, DataverseTableMetadata phone, Guid[] ids, CancellationToken token)
    {
        if (ids.Length == 0) return [];
        var parent = phone.Attribute("gaia_Tercero");
        var number = phone.Attribute("gaia_Numero");
        var primary = phone.Attribute("gaia_Principal");
        var contactType = phone.Attribute("gaia_TipoTelefono");
        var corporateType = phone.EncodedIntegerLiteral("gaia_TipoTelefono", 2);
        var idFilter = string.Join(" or ", ids.Select(id => $"_{parent}_value eq {id:D}"));
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{phone.EntitySetName}?$select=_{parent}_value,{number},{primary}&$filter=statecode eq 0 and {contactType} eq {corporateType} and ({idFilter})", token);
        return rows.Select(row => new { Parent=OptionalGuid(row,$"_{parent}_value"), Number=StringValue(row,number), Primary=BoolValue(row,primary) })
            .Where(item => item.Parent.HasValue && !string.IsNullOrWhiteSpace(item.Number))
            .GroupBy(item => item.Parent!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Primary).ThenBy(item => item.Number).First().Number!);
    }

    private static async Task<Dictionary<Guid, DirectoryAssignment>> ReadOrganizationalDetails(HttpClient client, Guid[] ids, CancellationToken token)
    {
        if (ids.Length == 0) return [];
        var assignment = await DataverseMetadataResolver.TableAsync(client, AssignmentTable, token);
        var partyRelation = assignment.RelationshipTo(ThirdPartyTable);
        var positionRelation = assignment.RelationshipTo(PositionTable);
        var unitRelation = assignment.RelationshipTo(UnitTable);
        var start = Optional(assignment, "gaia_FechaInicio", "gaia_VigenteDesde");
        var end = Optional(assignment, "gaia_FechaFin", "gaia_VigenteHasta");
        var primary = Optional(assignment, "gaia_EsPrincipal", "gaia_Principal");
        var select = string.Join(',', new[] { $"_{partyRelation.ReferencingAttribute}_value", $"_{positionRelation.ReferencingAttribute}_value", $"_{unitRelation.ReferencingAttribute}_value", start, end, primary }.Where(value => value is not null));
        var partyFilter = string.Join(" or ", ids.Select(id => $"_{partyRelation.ReferencingAttribute}_value eq {id:D}"));
        var rows = await DataverseJson.ReadAllAsync(client, $"{assignment.EntitySetName}?$select={select}&$filter=statecode eq 0 and ({partyFilter})", token);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var current = rows.Select(row => new
            {
                Party = OptionalGuid(row, $"_{partyRelation.ReferencingAttribute}_value"),
                Position = OptionalGuid(row, $"_{positionRelation.ReferencingAttribute}_value"),
                Unit = OptionalGuid(row, $"_{unitRelation.ReferencingAttribute}_value"),
                Start = ParseDate(StringValue(row, start)),
                End = ParseDate(StringValue(row, end)),
                Primary = BoolValue(row, primary)
            })
            .Where(item => item.Party.HasValue && item.Position.HasValue && item.Unit.HasValue && (!item.Start.HasValue || item.Start <= today) && (!item.End.HasValue || item.End >= today))
            .GroupBy(item => item.Party!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Primary).ThenByDescending(item => item.Start).First());
        if (current.Count == 0) return [];

        var position = await DataverseMetadataResolver.TableAsync(client, PositionTable, token);
        var unit = await DataverseMetadataResolver.TableAsync(client, UnitTable, token);
        var site = await DataverseMetadataResolver.TableAsync(client, SiteTable, token);
        var unitSite = unit.RelationshipTo(SiteTable);
        var positionIds = current.Values.Select(item => item.Position!.Value).Distinct().ToArray();
        var unitIds = current.Values.Select(item => item.Unit!.Value).Distinct().ToArray();
        var positionNames = await ReadNames(client, position, positionIds, token);
        var unitRows = await DataverseJson.ReadAllAsync(client, $"{unit.EntitySetName}?$select={unit.PrimaryIdAttribute},{unit.PrimaryNameAttribute},_{unitSite.ReferencingAttribute}_value&$filter={IdFilter(unit.PrimaryIdAttribute, unitIds)}", token);
        var siteIds = unitRows.Select(row => OptionalGuid(row, $"_{unitSite.ReferencingAttribute}_value")).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var siteNames = await ReadNames(client, site, siteIds, token);
        var units = unitRows.ToDictionary(row => GuidValue(row, unit.PrimaryIdAttribute), row => new
        {
            Name = StringValue(row, unit.PrimaryNameAttribute),
            SiteId = OptionalGuid(row, $"_{unitSite.ReferencingAttribute}_value")
        });
        return current.ToDictionary(pair => pair.Key, pair =>
        {
            var value = pair.Value; units.TryGetValue(value.Unit!.Value, out var unitValue);
            return new DirectoryAssignment(positionNames.GetValueOrDefault(value.Position!.Value), unitValue?.Name,
                unitValue?.SiteId is { } siteId ? siteNames.GetValueOrDefault(siteId) : null);
        });
    }

    private static async Task<Dictionary<Guid, string>> ReadNames(HttpClient client, DataverseTableMetadata table, Guid[] ids, CancellationToken token)
    {
        if (ids.Length == 0) return [];
        var rows = await DataverseJson.ReadAllAsync(client, $"{table.EntitySetName}?$select={table.PrimaryIdAttribute},{table.PrimaryNameAttribute}&$filter={IdFilter(table.PrimaryIdAttribute, ids)}", token);
        return rows.ToDictionary(row => GuidValue(row, table.PrimaryIdAttribute), row => StringValue(row, table.PrimaryNameAttribute) ?? "Sin información");
    }

    private static string IdFilter(string field, IEnumerable<Guid> ids) => string.Join(" or ", ids.Select(id => $"{field} eq {id:D}"));
    private static string? Optional(DataverseTableMetadata table, params string[] fields) => fields.Select(table.OptionalAttribute).FirstOrDefault(field => field is not null);
    private static DateOnly? ParseDate(string? value) => DateOnly.TryParse(value, out var date) ? date : null;

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static Guid GuidValue(JsonElement item, string property) => OptionalGuid(item, property)
        ?? throw new InvalidOperationException($"Dataverse no devolvió {property}.");
    private static Guid? OptionalGuid(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;
    private static string? StringValue(JsonElement item, string? property) =>
        property is not null && item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool BoolValue(JsonElement item, string? property) =>
        property is not null && item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    private sealed record DirectoryAssignment(string? Position, string? Unit, string? Site);
}
