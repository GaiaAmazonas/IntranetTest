using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace Gaia.Api.Infrastructure.Dataverse;

internal interface IDataverseDelegatedClientFactory
{
    Task<HttpClient> CreateAsync();
}

internal sealed class DataverseDelegatedClientFactory(
    ITokenAcquisition tokenAcquisition,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration) : IDataverseDelegatedClientFactory
{
    public async Task<HttpClient> CreateAsync()
    {
        var scope = configuration["Dataverse:Scope"]
            ?? throw new InvalidOperationException("Dataverse:Scope is required.");
        var token = await tokenAcquisition.GetAccessTokenForUserAsync(
            [scope],
            authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);
        var client = httpClientFactory.CreateClient("Dataverse");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Prefer",
            "odata.include-annotations=\"OData.Community.Display.V1.FormattedValue\"");
        return client;
    }
}
