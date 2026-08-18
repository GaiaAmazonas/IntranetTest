using System.Net.Http.Json;
using Gaia.Modules.Organization;

namespace Gaia.Api.Infrastructure.Dataverse.Organization;

internal sealed class DataverseOrganizationSiteUpdater(
    IDataverseDelegatedClientFactory clientFactory) : IOrganizationSiteUpdater
{
    public async Task<SiteUpdateResult> UpdateAsync(
        Guid id, SiteUpdateCommand command, CancellationToken cancellationToken)
    {
        var client = await clientFactory.CreateAsync();
        if (await DataverseOrganizationSiteReader.ReadAsync(client, id, cancellationToken) is null)
            return new(true, null);

        using var request = new HttpRequestMessage(
            HttpMethod.Patch, $"{DataverseOrganizationSiteReader.EntitySet}({id:D})")
        {
            Content = JsonContent.Create(DataverseOrganizationSiteCreator.Payload(
                command.Code, command.Name, command.City, command.Address, command.IsActive))
        };
        request.Headers.TryAddWithoutValidation("If-Match", "*");
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Dataverse rechazó la actualización de la sede ({(int)response.StatusCode}): {content}");

        var item = await DataverseOrganizationSiteReader.ReadAsync(client, id, cancellationToken)
            ?? throw new InvalidOperationException("Dataverse no devolvió la sede actualizada.");
        return new(false, item);
    }
}
