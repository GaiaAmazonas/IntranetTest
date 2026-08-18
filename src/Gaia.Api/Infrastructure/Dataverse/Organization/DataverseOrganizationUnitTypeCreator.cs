using System.Net.Http.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationUnitTypeCreator(
    IDataverseDelegatedClientFactory clientFactory) : IOrganizationUnitTypeCreator
{
    private const string EntitySet = "gaia_tipounidadorganizacionals";
    private const string PrimaryId = "gaia_tipounidadorganizacionalid";

    public async Task<UnitTypeCreateResult> CreateAsync(
        UnitTypeCreateCommand command,
        CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync();
        if (await ExistsAsync(client, command.Code, cancellationToken))
        {
            return new UnitTypeCreateResult(true, null);
        }

        var payload = new Dictionary<string, object?>
        {
            ["gaia_codigo"] = command.Code,
            ["gaia_nombre"] = command.Name,
            ["gaia_descripcion"] = command.Description,
            ["gaia_colortoken"] = command.ColorToken,
            ["gaia_ordenvisual"] = command.VisualOrder,
            ["gaia_activo"] = command.IsActive
        };
        using var response = await client.PostAsJsonAsync(EntitySet, payload, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Dataverse rechazó la creación ({(int)response.StatusCode}): {content}");
        }

        var now = DateTimeOffset.UtcNow;
        var item = new UnitTypeResponse(
            ReadCreatedId(response),
            now,
            command.CreatedBy,
            null,
            null,
            command.Code,
            command.Name,
            command.Description,
            command.ColorToken,
            command.VisualOrder,
            command.IsActive);
        return new UnitTypeCreateResult(false, item);
    }

    private static async Task<bool> ExistsAsync(
        HttpClient client,
        string code,
        CancellationToken cancellationToken)
    {
        var escapedCode = code.Replace("'", "''", StringComparison.Ordinal);
        var filter = Uri.EscapeDataString($"gaia_codigo eq '{escapedCode}'");
        var path = $"{EntitySet}?$select={PrimaryId}&$filter={filter}&$top=1";
        var records = await DataverseJson.ReadAllAsync(client, path, cancellationToken);
        return records.Count > 0;
    }

    private static Guid ReadCreatedId(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("OData-EntityId", out var values))
        {
            throw new InvalidOperationException("Dataverse creó el registro sin devolver su identificador.");
        }
        var entityId = values.Single();
        var start = entityId.LastIndexOf('(') + 1;
        var end = entityId.LastIndexOf(')');
        return Guid.Parse(entityId[start..end]);
    }
}
