using System.Text.Json;
using Gaia.Modules.ThirdParties;

namespace Gaia.Api.Infrastructure.Dataverse.ThirdParties;

internal sealed class DataverseIntranetDirectoryReader(IDataverseDelegatedClientFactory clientFactory)
    : IIntranetDirectoryReader
{
    private const string ThirdPartyTable = "gaia_terceros";
    private const string EmailTable = "gaia_correocolaborador";
    private const string PhoneTable = "gaia_telefonocolaborador";

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
        var contacts = await Task.WhenAll(ReadInstitutionalEmails(client, email, ids, token), ReadCorporatePhones(client, phone, ids, token));
        var emails = contacts[0];
        var phones = contacts[1];

        var people = selected.Select(row =>
        {
            var id = GuidValue(row, thirdParty.PrimaryIdAttribute);
            return new IntranetPerson(id, StringValue(row, name) ?? "Sin nombre", null, null, null,
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

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static Guid GuidValue(JsonElement item, string property) => OptionalGuid(item, property)
        ?? throw new InvalidOperationException($"Dataverse no devolvió {property}.");
    private static Guid? OptionalGuid(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id) ? id : null;
    private static string? StringValue(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool BoolValue(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
