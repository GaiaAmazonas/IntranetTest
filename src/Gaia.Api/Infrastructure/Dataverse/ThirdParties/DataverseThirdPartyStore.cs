using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Modules.ThirdParties;

namespace Gaia.Api.Infrastructure.Dataverse.ThirdParties;

internal sealed class DataverseThirdPartyStore(IDataverseDelegatedClientFactory clientFactory)
    : IThirdPartyReader, IThirdPartyWriter, IDocumentTypeReader
{
    private const string Table = "gaia_terceros";
    private const string DocumentTypeTable = "gaia_tipodocumento";

    public async Task<IReadOnlyList<ThirdPartyDirectoryResponse>> ListDirectoryAsync(string? search, CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token);
        var fields = Fields.From(metadata);
        var filter = string.IsNullOrWhiteSpace(search) ? "" : $"&$filter=contains({fields.FullName},'{Escape(search.Trim())}') or contains({fields.DocumentNumber},'{Escape(search.Trim())}')";
        var lookup = $"_{fields.DocumentType}_value";
        var records = await DataverseJson.ReadAllAsync(client, $"{metadata.EntitySetName}?$select={fields.Id},{fields.FullName},{lookup},{fields.DocumentNumber},statecode&$orderby={fields.FullName}{filter}", token);
        return records.Select(item => new ThirdPartyDirectoryResponse(GuidValue(item, fields.Id), RequiredString(item, fields.FullName), OptionalString(item, $"{lookup}@OData.Community.Display.V1.FormattedValue") ?? "No disponible", RequiredString(item, fields.DocumentNumber), (DataverseJson.OptionalInt32(item, "statecode") ?? 0) == 0)).ToArray();
    }

    public async Task<IReadOnlyList<ThirdPartyResponse>> ListAsync(string? search, CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token);
        var fields = Fields.From(metadata);
        var types = await DocumentTypesAsync(client, token);
        var sexes = (await DataverseMetadataResolver.ChoicesAsync(client, Table, fields.Sex, token))
            .ToDictionary(x => x.Value, x => x.Key);
        var filter = string.IsNullOrWhiteSpace(search) ? "" :
            $"&$filter=contains({fields.FullName},'{Escape(search.Trim())}') or contains({fields.DocumentNumber},'{Escape(search.Trim())}')";
        var records = await DataverseJson.ReadAllAsync(client,
            $"{metadata.EntitySetName}?$select={fields.Select}&$orderby={fields.FullName}{filter}", token);
        return records.Select(item => Map(item, fields, types, sexes)).ToArray();
    }

    public async Task<ThirdPartyResponse?> GetAsync(Guid id, CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token);
        var fields = Fields.From(metadata);
        var item = await DataverseMetadataResolver.ReadOneAsync(client, $"{metadata.EntitySetName}({id:D})?$select={fields.Select}", token);
        if (item is null) return null;
        var sexes = (await DataverseMetadataResolver.ChoicesAsync(client, Table, fields.Sex, token))
            .ToDictionary(x => x.Value, x => x.Key);
        return Map(item.Value, fields, await DocumentTypesAsync(client, token), sexes);
    }

    public async Task<IReadOnlyList<DocumentTypeResponse>> ListAsync(CancellationToken token) =>
        (await DocumentTypesAsync(await clientFactory.CreateAsync(), token)).Values.OrderBy(x => x.Name).ToArray();

    public Task<ThirdPartyWriteResult> CreateAsync(ThirdPartyWriteCommand command, CancellationToken token) => WriteAsync(null, command, token);
    public Task<ThirdPartyWriteResult> UpdateAsync(Guid id, ThirdPartyWriteCommand command, CancellationToken token) => WriteAsync(id, command, token);

    private async Task<ThirdPartyWriteResult> WriteAsync(Guid? id, ThirdPartyWriteCommand command, CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var thirdParty = await DataverseMetadataResolver.TableAsync(client, Table, token);
        var documentType = await DataverseMetadataResolver.TableAsync(client, DocumentTypeTable, token);
        var fields = Fields.From(thirdParty);
        if (id.HasValue && await DataverseMetadataResolver.ReadOneAsync(client, $"{thirdParty.EntitySetName}({id:D})?$select={thirdParty.PrimaryIdAttribute}", token) is null)
            return new(ThirdPartyWriteStatus.NotFound);
        var type = await DataverseMetadataResolver.ReadOneAsync(client, $"{documentType.EntitySetName}({command.DocumentTypeId:D})?$select={documentType.PrimaryIdAttribute},statecode", token);
        if (type is null || (DataverseJson.OptionalInt32(type.Value, "statecode") ?? 0) != 0) return new(ThirdPartyWriteStatus.InvalidDocumentType);
        var choices = await DataverseMetadataResolver.ChoicesAsync(client, Table, fields.Sex, token);
        if (!choices.TryGetValue(command.Sex, out var sexValue)) return new(ThirdPartyWriteStatus.InvalidSex);
        var own = id.HasValue ? $" and {thirdParty.PrimaryIdAttribute} ne {id:D}" : "";
        var duplicate = $"_{fields.DocumentType}_value eq {command.DocumentTypeId:D} and {fields.DocumentNumber} eq '{Escape(command.DocumentNumber)}'{own}";
        if ((await DataverseJson.ReadAllAsync(client, $"{thirdParty.EntitySetName}?$select={thirdParty.PrimaryIdAttribute}&$filter={duplicate}&$top=1", token)).Count > 0)
            return new(ThirdPartyWriteStatus.DuplicateDocument);
        var relation = thirdParty.Relationship("gaia_TipoDocumento", DocumentTypeTable);
        var payload = new Dictionary<string, object?> {
            [fields.FullName] = BuildFullName(command), [fields.DocumentNumber] = command.DocumentNumber,
            [fields.FirstName] = command.FirstName, [fields.MiddleName] = command.MiddleName,
            [fields.FirstSurname] = command.FirstSurname, [fields.SecondSurname] = command.SecondSurname,
            [fields.Sex] = sexValue, [fields.BirthDate] = command.BirthDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            [fields.Observations] = command.Observations, ["statecode"] = command.IsActive ? 0 : 1,
            [$"{relation.NavigationProperty}@odata.bind"] = $"/{documentType.EntitySetName}({command.DocumentTypeId:D})" };
        if (!id.HasValue)
        {
            using var response = await client.PostAsJsonAsync(thirdParty.EntitySetName, payload, token);
            await EnsureAsync(response, token);
            return new(ThirdPartyWriteStatus.Created, ReadCreatedId(response));
        }
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{thirdParty.EntitySetName}({id:D})") { Content = JsonContent.Create(payload) };
        request.Headers.TryAddWithoutValidation("If-Match", "*");
        using var updated = await client.SendAsync(request, token); await EnsureAsync(updated, token);
        return new(ThirdPartyWriteStatus.Updated, id);
    }

    internal static string BuildFullName(ThirdPartyWriteCommand command) => string.Join(' ',
        new[] { command.FirstName, command.MiddleName, command.FirstSurname, command.SecondSurname }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));

    private static ThirdPartyResponse Map(JsonElement item, Fields fields, Dictionary<Guid, DocumentTypeResponse> types,
        Dictionary<int, string> sexes)
    {
        var typeId = GuidValue(item, $"_{fields.DocumentType}_value"); types.TryGetValue(typeId, out var type);
        var sexValue = DataverseJson.OptionalInt32(item, fields.Sex);
        var formattedSex = OptionalString(item, $"{fields.Sex}@OData.Community.Display.V1.FormattedValue")
            ?? (sexValue.HasValue && sexes.TryGetValue(sexValue.Value, out var label) ? label : "");
        return new(GuidValue(item, fields.Id), RequiredString(item, fields.FullName), typeId, type?.Name ?? "No disponible",
            RequiredString(item, fields.DocumentNumber), RequiredString(item, fields.FirstName), OptionalString(item, fields.MiddleName),
            RequiredString(item, fields.FirstSurname), OptionalString(item, fields.SecondSurname), formattedSex,
            ParseDateOnly(OptionalString(item, fields.BirthDate)),
            OptionalString(item, fields.Observations), (DataverseJson.OptionalInt32(item, "statecode") ?? 0) == 0);
    }

    private static async Task<Dictionary<Guid, DocumentTypeResponse>> DocumentTypesAsync(HttpClient client, CancellationToken token)
    {
        var metadata = await DataverseMetadataResolver.TableAsync(client, DocumentTypeTable, token);
        var rows = await DataverseJson.ReadAllAsync(client, $"{metadata.EntitySetName}?$select={metadata.PrimaryIdAttribute},{metadata.PrimaryNameAttribute},statecode", token);
        return rows.Select(x => new DocumentTypeResponse(GuidValue(x, metadata.PrimaryIdAttribute), RequiredString(x, metadata.PrimaryNameAttribute),
            (DataverseJson.OptionalInt32(x, "statecode") ?? 0) == 0)).ToDictionary(x => x.Id);
    }
    private static async Task EnsureAsync(HttpResponseMessage response, CancellationToken token)
    { if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Dataverse rechazó la operación ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(token)}"); }
    private static Guid? ReadCreatedId(HttpResponseMessage response) { var text = response.Headers.TryGetValues("OData-EntityId", out var v) ? v.SingleOrDefault() : null; var match = text is null ? null : System.Text.RegularExpressions.Regex.Match(text, @"\(([0-9a-f-]{36})\)$"); return match?.Success == true ? Guid.Parse(match.Groups[1].Value) : null; }
    private static Guid GuidValue(JsonElement x, string p) => x.TryGetProperty(p, out var v) && Guid.TryParse(v.GetString(), out var id) ? id : throw new InvalidOperationException($"Dataverse no devolvió {p}.");
    private static string RequiredString(JsonElement x, string p) => OptionalString(x, p) ?? "";
    private static string? OptionalString(JsonElement x, string p) => x.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    internal static DateOnly? ParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)
            ? DateOnly.FromDateTime(timestamp.DateTime)
            : null;
    }
    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private sealed record Fields(string Id, string FullName, string DocumentType, string DocumentNumber, string FirstName,
        string MiddleName, string FirstSurname, string SecondSurname, string Sex, string BirthDate, string Observations)
    {
        public string Select => string.Join(',', Id, FullName, $"_{DocumentType}_value", DocumentNumber, FirstName, MiddleName,
            FirstSurname, SecondSurname, Sex, BirthDate, Observations, "statecode");
        public static Fields From(DataverseTableMetadata m) => new(m.PrimaryIdAttribute, m.Attribute("gaia_Nombretercero"),
            m.Attribute("gaia_TipoDocumento"), m.Attribute("gaia_NumeroDocumento"), m.Attribute("gaia_PrimerNombre"),
            m.Attribute("gaia_SegundoNombre"), m.Attribute("gaia_PrimerApellido"), m.Attribute("gaia_SegundoApellido"),
            m.Attribute("gaia_Sexo"), m.Attribute("gaia_FechaNacimiento"), m.Attribute("gaia_Observaciones"));
    }
}
