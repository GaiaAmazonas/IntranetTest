using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Modules.ThirdParties;

namespace Gaia.Api.Infrastructure.Dataverse.ThirdParties;

internal sealed class DataverseOrganizationalAssignmentStore(IDataverseDelegatedClientFactory clientFactory)
    : IOrganizationalAssignmentStore
{
    private const string AssignmentTable = "gaia_asignacionorganizacional";
    private const string ThirdPartyTable = "gaia_terceros";
    private const string PositionTable = "gaia_cargo";
    private const string UnitTable = "gaia_organizacion";

    public async Task<IReadOnlyList<OrganizationalAssignmentResponse>> ListAsync(CancellationToken token)
    {
        var client = await clientFactory.CreateAsync();
        var schema = await Schema.LoadAsync(client, token);
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{schema.Assignment.EntitySetName}?$select={schema.Select}&$orderby={schema.Assignment.PrimaryNameAttribute} asc", token);
        var parties = await NamesAsync(client, schema.ThirdParty, schema.ThirdPartyName, schema.DocumentNumber, token);
        var positions = await NamesAsync(client, schema.Position, schema.Position.PrimaryNameAttribute, null, token);
        var units = await NamesAsync(client, schema.Unit, schema.Unit.PrimaryNameAttribute, schema.UnitCode, token);
        return rows.Select(row => Map(row, schema, parties, positions, units)).ToArray();
    }

    public Task<OrganizationalAssignmentWriteResult> CreateAsync(OrganizationalAssignmentCommand command, CancellationToken token) => WriteAsync(null, command, token);
    public Task<OrganizationalAssignmentWriteResult> UpdateAsync(Guid id, OrganizationalAssignmentCommand command, CancellationToken token) => WriteAsync(id, command, token);

    private async Task<OrganizationalAssignmentWriteResult> WriteAsync(Guid? id, OrganizationalAssignmentCommand command, CancellationToken token)
    {
        if (command.EndDate.HasValue && command.StartDate.HasValue && command.EndDate < command.StartDate)
            throw new InvalidOperationException("La fecha final no puede ser anterior a la fecha inicial.");
        var client = await clientFactory.CreateAsync(); var schema = await Schema.LoadAsync(client, token);
        if (id.HasValue && await ExistsAsync(client, schema.Assignment, id.Value, false, token) is null) return new(OrganizationalAssignmentWriteStatus.NotFound);
        var party = await ExistsAsync(client, schema.ThirdParty, command.ThirdPartyId, true, token); if (party is null) return new(OrganizationalAssignmentWriteStatus.InvalidThirdParty);
        var position = await ExistsAsync(client, schema.Position, command.PositionId, true, token); if (position is null) return new(OrganizationalAssignmentWriteStatus.InvalidPosition);
        var unit = await ExistsAsync(client, schema.Unit, command.OrganizationalUnitId, true, token); if (unit is null) return new(OrganizationalAssignmentWriteStatus.InvalidUnit);
        var own = id.HasValue ? $" and {schema.Assignment.PrimaryIdAttribute} ne {id:D}" : "";
        var duplicate = $"_{schema.ThirdPartyLookup.ReferencingAttribute}_value eq {command.ThirdPartyId:D} and statecode eq 0{own}";
        if ((await DataverseJson.ReadAllAsync(client, $"{schema.Assignment.EntitySetName}?$select={schema.Assignment.PrimaryIdAttribute}&$filter={duplicate}&$top=1", token)).Count > 0)
            return new(OrganizationalAssignmentWriteStatus.Duplicate);
        var partyName = String(party.Value, schema.ThirdParty.PrimaryNameAttribute) ?? command.ThirdPartyId.ToString("D");
        var positionName = String(position.Value, schema.Position.PrimaryNameAttribute) ?? "Cargo";
        var payload = new Dictionary<string, object?> {
            [schema.Assignment.PrimaryNameAttribute] = AssignmentName(partyName, positionName),
            [$"{schema.ThirdPartyLookup.NavigationProperty}@odata.bind"] = $"/{schema.ThirdParty.EntitySetName}({command.ThirdPartyId:D})",
            [$"{schema.PositionLookup.NavigationProperty}@odata.bind"] = $"/{schema.Position.EntitySetName}({command.PositionId:D})",
            [$"{schema.UnitLookup.NavigationProperty}@odata.bind"] = $"/{schema.Unit.EntitySetName}({command.OrganizationalUnitId:D})",
            ["statecode"] = command.IsActive ? 0 : 1
        };
        Put(payload, schema.StartDate, command.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Put(payload, schema.EndDate, command.EndDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Put(payload, schema.IsPrimary, command.IsPrimary);
        Put(payload, schema.Observations, command.Observations);
        if (!id.HasValue)
        {
            using var response = await client.PostAsJsonAsync(schema.Assignment.EntitySetName, payload, token); await Ensure(response, token);
            return new(OrganizationalAssignmentWriteStatus.Created, CreatedId(response));
        }
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{schema.Assignment.EntitySetName}({id:D})") { Content = JsonContent.Create(payload) };
        request.Headers.TryAddWithoutValidation("If-Match", "*"); using var updated = await client.SendAsync(request, token); await Ensure(updated, token);
        return new(OrganizationalAssignmentWriteStatus.Updated, id);
    }

    private static OrganizationalAssignmentResponse Map(JsonElement row, Schema s,
        Dictionary<Guid, (string Name, string? Detail)> parties, Dictionary<Guid, (string Name, string? Detail)> positions,
        Dictionary<Guid, (string Name, string? Detail)> units)
    {
        var partyId = GuidValue(row, $"_{s.ThirdPartyLookup.ReferencingAttribute}_value");
        var positionId = GuidValue(row, $"_{s.PositionLookup.ReferencingAttribute}_value");
        var unitId = GuidValue(row, $"_{s.UnitLookup.ReferencingAttribute}_value");
        parties.TryGetValue(partyId, out var party); positions.TryGetValue(positionId, out var position); units.TryGetValue(unitId, out var unit);
        return new(GuidValue(row, s.Assignment.PrimaryIdAttribute), partyId, party.Name ?? "No disponible", party.Detail ?? "",
            positionId, position.Name ?? "No disponible", unitId, unit.Detail ?? "", unit.Name ?? "No disponible",
            Date(row, s.StartDate), Date(row, s.EndDate), Bool(row, s.IsPrimary) ?? false,
            String(row, s.Observations), (DataverseJson.OptionalInt32(row, "statecode") ?? 0) == 0);
    }

    private static async Task<Dictionary<Guid, (string Name, string? Detail)>> NamesAsync(HttpClient client,
        DataverseTableMetadata table, string nameField, string? detailField, CancellationToken token)
    {
        var select = detailField is null ? $"{table.PrimaryIdAttribute},{nameField}" : $"{table.PrimaryIdAttribute},{nameField},{detailField}";
        var rows = await DataverseJson.ReadAllAsync(client, $"{table.EntitySetName}?$select={select}", token);
        return rows.ToDictionary(x => GuidValue(x, table.PrimaryIdAttribute), x => (String(x, nameField) ?? "", detailField is null ? null : String(x, detailField)));
    }
    private static async Task<JsonElement?> ExistsAsync(HttpClient client, DataverseTableMetadata table, Guid id, bool active, CancellationToken token) =>
        await DataverseMetadataResolver.ReadOneAsync(client, $"{table.EntitySetName}({id:D})?$select={table.PrimaryIdAttribute},{table.PrimaryNameAttribute},statecode", token) is { } row
        && (!active || (DataverseJson.OptionalInt32(row, "statecode") ?? 0) == 0) ? row : null;
    private static void Put(Dictionary<string, object?> target, string? field, object? value) { if (field is not null) target[field] = value; }
    private static string? String(JsonElement row, string? field) => field is not null && row.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool? Bool(JsonElement row, string? field) => field is not null && row.TryGetProperty(field, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    private static DateOnly? Date(JsonElement row, string? field) => DateOnly.TryParse(String(row, field), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    private static Guid GuidValue(JsonElement row, string field) => row.TryGetProperty(field, out var value) && Guid.TryParse(value.GetString(), out var id) ? id : Guid.Empty;
    private static Guid CreatedId(HttpResponseMessage response) { var uri=response.Headers.TryGetValues("OData-EntityId",out var values)?values.SingleOrDefault():null;var match=System.Text.RegularExpressions.Regex.Match(uri??"",@"\(([0-9a-f-]{36})\)$");return match.Success?Guid.Parse(match.Groups[1].Value):throw new InvalidOperationException("Dataverse no devolvió el GUID de la asignación."); }
    internal static string AssignmentName(string partyName, string positionName)
    {
        const int maximumLength = 150;
        var value = $"{partyName.Trim()} · {positionName.Trim()}";
        return value.Length <= maximumLength ? value : value[..maximumLength].TrimEnd();
    }
    private static async Task Ensure(HttpResponseMessage response, CancellationToken token) { if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Dataverse rechazó la asignación organizacional ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(token)}"); }

    private sealed record Schema(DataverseTableMetadata Assignment, DataverseTableMetadata ThirdParty,
        DataverseTableMetadata Position, DataverseTableMetadata Unit, DataverseRelationship ThirdPartyLookup,
        DataverseRelationship PositionLookup, DataverseRelationship UnitLookup, string ThirdPartyName,
        string DocumentNumber, string UnitCode, string? StartDate, string? EndDate, string? IsPrimary, string? Observations)
    {
        public string Select => string.Join(',', new[] { Assignment.PrimaryIdAttribute, Assignment.PrimaryNameAttribute,
            $"_{ThirdPartyLookup.ReferencingAttribute}_value", $"_{PositionLookup.ReferencingAttribute}_value",
            $"_{UnitLookup.ReferencingAttribute}_value", StartDate, EndDate, IsPrimary, Observations, "statecode" }.Where(x => x is not null));
        public static async Task<Schema> LoadAsync(HttpClient client, CancellationToken token)
        {
            var a=await DataverseMetadataResolver.TableAsync(client,AssignmentTable,token); var t=await DataverseMetadataResolver.TableAsync(client,ThirdPartyTable,token);
            var p=await DataverseMetadataResolver.TableAsync(client,PositionTable,token); var u=await DataverseMetadataResolver.TableAsync(client,UnitTable,token);
            return new(a,t,p,u,a.RelationshipTo(ThirdPartyTable),a.RelationshipTo(PositionTable),a.RelationshipTo(UnitTable),
                t.Attribute("gaia_Nombretercero"),t.Attribute("gaia_NumeroDocumento"),u.Attribute("gaia_Codigo"),
                Optional(a,"gaia_FechaInicio","gaia_VigenteDesde"),Optional(a,"gaia_FechaFin","gaia_VigenteHasta"),
                Optional(a,"gaia_EsPrincipal","gaia_Principal"),Optional(a,"gaia_Observaciones"));
        }
        private static string? Optional(DataverseTableMetadata table, params string[] names) => names.Select(table.OptionalAttribute).FirstOrDefault(x => x is not null);
    }
}
