using System.Net.Http.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationUnitTypeUpdater(
    IDataverseDelegatedClientFactory clientFactory) : IOrganizationUnitTypeUpdater
{
    private const string EntitySet = "gaia_tipounidadorganizacionals";

    public async Task<UnitTypeUpdateResult> UpdateAsync(
        Guid id,
        UnitTypeUpdateCommand command,
        CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync();
        if (await ReadAsync(client, id, cancellationToken) is null)
        {
            return new UnitTypeUpdateResult(true, null);
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
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{EntitySet}({id:D})")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation("If-Match", "*");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Dataverse rechazó la actualización ({(int)response.StatusCode}): {content}");
        }

        var updated = await ReadAsync(client, id, cancellationToken)
            ?? throw new InvalidOperationException("Dataverse no devolvió el tipo actualizado.");
        return new UnitTypeUpdateResult(false, updated);
    }

    private static async Task<UnitTypeResponse?> ReadAsync(
        HttpClient client,
        Guid id,
        CancellationToken cancellationToken)
    {
        var path = $"{EntitySet}({id:D})?$select={DataverseOrganizationUnitTypeReader.SelectColumns}";
        using var response = await client.GetAsync(path, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Dataverse rechazó la lectura ({(int)response.StatusCode}): {content}");
        }
        using var document = System.Text.Json.JsonDocument.Parse(content);
        return DataverseOrganizationUnitTypeReader.Map(document.RootElement);
    }
}
