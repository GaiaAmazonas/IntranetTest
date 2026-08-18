using System.Net.Http.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationSiteCreator(
    IDataverseDelegatedClientFactory clientFactory) : IOrganizationSiteCreator
{
    public async Task<SiteCreateResult> CreateAsync(
        SiteCreateCommand command, CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync();
        var escaped = command.Code.Replace("'", "''", StringComparison.Ordinal);
        var filter = Uri.EscapeDataString($"gaia_codigo eq '{escaped}'");
        var existing = await DataverseJson.ReadAllAsync(client,
            $"{DataverseOrganizationSiteReader.EntitySet}?$select={DataverseOrganizationSiteReader.PrimaryId}&$filter={filter}&$top=1",
            cancellationToken);
        if (existing.Count != 0) return new(true, null);

        var payload = Payload(command.Code, command.Name, command.City, command.Address, command.IsActive);
        using var response = await client.PostAsJsonAsync(
            DataverseOrganizationSiteReader.EntitySet, payload, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Dataverse rechazó la creación de la sede ({(int)response.StatusCode}): {content}");

        var id = ReadCreatedId(response);
        var item = await DataverseOrganizationSiteReader.ReadAsync(client, id, cancellationToken)
            ?? throw new InvalidOperationException("Dataverse no devolvió la sede creada.");
        return new(false, item);
    }

    internal static Dictionary<string, object?> Payload(
        string code, string name, string? city, string? address, bool isActive) => new()
    {
        ["gaia_codigo"] = code,
        ["gaia_name"] = name,
        ["gaia_ciudad"] = city,
        ["gaia_direccion"] = address,
        ["gaia_activo"] = isActive,
        ["statecode"] = isActive ? 0 : 1
    };

    private static Guid ReadCreatedId(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("OData-EntityId", out var values))
            throw new InvalidOperationException("Dataverse creó la sede sin devolver su identificador.");
        var value = values.Single();
        return Guid.Parse(value[(value.LastIndexOf('(') + 1)..value.LastIndexOf(')')]);
    }
}
