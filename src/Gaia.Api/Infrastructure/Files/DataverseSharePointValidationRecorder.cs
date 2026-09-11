using System.Net.Http.Json;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

internal interface ISharePointConfigurationValidationRecorder
{
    Task RecordAsync(int result, string detail, CancellationToken cancellationToken);
}

internal sealed class DataverseSharePointValidationRecorder(ISharePointRepositoryReader repositories,
    IDataverseDelegatedClientFactory clients, IConfiguration configuration) : ISharePointConfigurationValidationRecorder
{
    public async Task RecordAsync(int result, string detail, CancellationToken cancellationToken)
    {
        if (result is < 1 or > 4 || string.IsNullOrWhiteSpace(detail) || detail.Length > 500)
            throw new FileStorageException(FileStorageError.MissingConfiguration);
        var repository = await repositories.ReadAsync(null, cancellationToken);
        using var client = await clients.CreateAsync();
        var table = configuration["FileStorage:SharePoint:ConfigurationTable"] ?? DataverseSharePointConfigurationReader.Table;
        var metadata = await DataverseMetadataResolver.TableAsync(client, table, cancellationToken);
        var payload = new Dictionary<string, object>
        {
            [metadata.Attribute("gaia_resultadovalidacion")] = result,
            [metadata.Attribute("gaia_detallevalidacion")] = detail,
            [metadata.Attribute("gaia_ultimavalidacion")] = DateTimeOffset.UtcNow
        };
        using var request = new HttpRequestMessage(HttpMethod.Patch,
            $"{metadata.EntitySetName}({repository.Id:D})") { Content = JsonContent.Create(payload) };
        request.Headers.TryAddWithoutValidation("If-Match", "*");
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new FileStorageException(FileStorageError.TransientFailure);
    }
}
