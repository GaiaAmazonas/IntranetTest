using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Gaia.Modules.ThirdParties;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Web;

namespace Gaia.Api.Infrastructure.Dataverse.ThirdParties;

internal sealed class ProfilePhotoMemoryCache : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 256 });
    public bool TryGetValue(string key,out ProfilePhotoResult result) => cache.TryGetValue(key,out result!);
    public void Set(string key,ProfilePhotoResult result)
    {
        if (result.Unavailable)
        {
            var duration=result.RetryAfter is { } retry ? retry : TimeSpan.FromMinutes(1);
            cache.Set(key,result,new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow=duration,Size=1 });
            return;
        }
        cache.Set(key,result,new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow=result.Content is null?TimeSpan.FromMinutes(15):TimeSpan.FromHours(12),
            SlidingExpiration=result.Content is null?null:TimeSpan.FromHours(4), Size=1
        });
    }
    public void Dispose() => cache.Dispose();
}

internal sealed class MicrosoftGraphProfilePhotoReader(
    IDataverseDelegatedClientFactory dataverse,
    ITokenAcquisition tokenAcquisition,
    IHttpClientFactory httpClientFactory,
    ProfilePhotoMemoryCache cache,
    IConfiguration configuration,
    ILogger<MicrosoftGraphProfilePhotoReader> logger) : IProfilePhotoReader
{
    private const string UserTable="gaia_usuarioaplicacion",ThirdPartyTable="gaia_terceros",EmailTable="gaia_correocolaborador";
    private const int MaximumPhotoBytes=2*1024*1024;
    private static readonly Action<ILogger, int, Exception?> LogGraphPhotoUnavailable = LoggerMessage.Define<int>(
        LogLevel.Warning, new EventId(4301, "GraphProfilePhotoUnavailable"),
        "Microsoft Graph no entregó una fotografía institucional. Estado {StatusCode}.");
    private static readonly Action<ILogger, Exception?> LogGraphPhotoLookupFailed = LoggerMessage.Define(
        LogLevel.Warning, new EventId(4302, "GraphProfilePhotoLookupFailed"),
        "No fue posible consultar Microsoft Graph para una fotografía institucional.");
    private readonly string graphScope=configuration["MicrosoftGraph:ProfilePhotoScope"]??"https://graph.microsoft.com/ProfilePhoto.Read.All";

    public Task<ProfilePhotoResult> ReadCurrentUserAsync(string identityCacheKey,int size,CancellationToken token) =>
        ReadGraphWithFallbackAsync(
            $"me/photos/{size}x{size}/$value",
            "me/photo/$value",
            $"profile-photo:me:{identityCacheKey}:{size}",
            token);

    public async Task<ProfilePhotoResult> ReadPersonAsync(Guid personId,int size,CancellationToken token)
    {
        var key=$"profile-photo:person:{personId:D}:{size}";
        if (cache.TryGetValue(key,out var cached)) return cached;
        var target=await ResolvePersonTargetAsync(personId,token);
        if (target is null) { cache.Set(key,ProfilePhotoResult.Missing); return ProfilePhotoResult.Missing; }
        return await ReadGraphWithFallbackAsync(
            $"users/{Uri.EscapeDataString(target)}/photos/{size}x{size}/$value",
            $"users/{Uri.EscapeDataString(target)}/photo/$value",
            key,
            token);
    }

    // Some tenants expose only the primary photo and not every predefined thumbnail size.
    // In that case, keep the requested UI size but retrieve the primary image as a safe fallback.
    private async Task<ProfilePhotoResult> ReadGraphWithFallbackAsync(string sizedPath,string primaryPath,string cacheKey,CancellationToken token)
    {
        if (cache.TryGetValue(cacheKey,out var cached)) return cached;
        var sized=await ReadGraphAsync(sizedPath,$"{cacheKey}:sized",token);
        if (sized.Content is not null || sized.Unavailable) return sized;
        return await ReadGraphAsync(primaryPath,cacheKey,token);
    }

    private async Task<ProfilePhotoResult> ReadGraphAsync(string path,string cacheKey,CancellationToken token)
    {
        if (cache.TryGetValue(cacheKey,out var cached)) return cached;
        try
        {
            var accessToken=await tokenAcquisition.GetAccessTokenForUserAsync([graphScope],authenticationScheme:OpenIdConnectDefaults.AuthenticationScheme);
            var client=httpClientFactory.CreateClient("GraphProfilePhotos");
            using var request=new HttpRequestMessage(HttpMethod.Get,path);
            request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",accessToken);
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
            if (response.StatusCode==HttpStatusCode.NotFound) { cache.Set(cacheKey,ProfilePhotoResult.Missing); return ProfilePhotoResult.Missing; }
            if (response.StatusCode==HttpStatusCode.TooManyRequests)
            {
                var limited=ProfilePhotoResult.ServiceUnavailable(RetryAfter(response)); cache.Set(cacheKey,limited); return limited;
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden || (int)response.StatusCode>=500)
            {
                LogGraphPhotoUnavailable(logger, (int)response.StatusCode, null);
                return ProfilePhotoResult.ServiceUnavailable();
            }
            if (!response.IsSuccessStatusCode) return ProfilePhotoResult.ServiceUnavailable();
            var contentType=response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(contentType)||!contentType.StartsWith("image/",StringComparison.OrdinalIgnoreCase)||response.Content.Headers.ContentLength>MaximumPhotoBytes)
                return ProfilePhotoResult.ServiceUnavailable();
            var bytes=await response.Content.ReadAsByteArrayAsync(token);
            if (bytes.Length==0||bytes.Length>MaximumPhotoBytes) return ProfilePhotoResult.ServiceUnavailable();
            var result=new ProfilePhotoResult(new(bytes,contentType)); cache.Set(cacheKey,result); return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            LogGraphPhotoLookupFailed(logger, exception);
            return ProfilePhotoResult.ServiceUnavailable();
        }
    }

    private async Task<string?> ResolvePersonTargetAsync(Guid personId,CancellationToken token)
    {
        var client=await dataverse.CreateAsync();
        var third=await DataverseMetadataResolver.TableAsync(client,ThirdPartyTable,token);
        var directOid=third.OptionalAttribute("gaia_EntraObjectId");
        var thirdSelect=string.Join(',',new[]{third.PrimaryIdAttribute,"statecode",directOid}.Where(value=>value is not null));
        var person=await DataverseMetadataResolver.ReadOneAsync(client,$"{third.EntitySetName}({personId:D})?$select={thirdSelect}",token);
        if (person is not { } thirdParty || Number(thirdParty,"statecode")!=0) return null;
        if (directOid is not null&&Guid.TryParse(Text(thirdParty,directOid),out var thirdPartyOid)) return thirdPartyOid.ToString("D");

        var user=await DataverseMetadataResolver.TableAsync(client,UserTable,token);
        var relation=user.Relationship("gaia_Tercero",ThirdPartyTable);
        var oid=user.Attribute("gaia_EntraObjectId"); var email=user.Attribute("gaia_Correo");
        var users=await DataverseJson.ReadAllAsync(client,$"{user.EntitySetName}?$select={oid},{email}&$filter=_{relation.ReferencingAttribute}_value eq {personId:D} and statecode eq 0&$top=10",token);
        foreach(var row in users) if(Guid.TryParse(Text(row,oid),out var objectId)) return objectId.ToString("D");
        foreach(var row in users){var address=CorporateEmail(Text(row,email));if(address is not null)return address;}
        return await ResolveInstitutionalEmailAsync(client,personId,token);
    }

    private static async Task<string?> ResolveInstitutionalEmailAsync(HttpClient client,Guid personId,CancellationToken token)
    {
        var email=await DataverseMetadataResolver.TableAsync(client,EmailTable,token);var parent=email.Attribute("gaia_Tercero");var address=email.Attribute("gaia_Correoelectronico");var primary=email.Attribute("gaia_Principal");var type=email.Attribute("gaia_Tipocorreo");var corporate=email.EncodedIntegerLiteral("gaia_Tipocorreo",2);
        var rows=await DataverseJson.ReadAllAsync(client,$"{email.EntitySetName}?$select={address},{primary}&$filter=_{parent}_value eq {personId:D} and {type} eq {corporate} and statecode eq 0&$top=10",token);
        return rows.OrderByDescending(row=>Boolean(row,primary)).Select(row=>CorporateEmail(Text(row,address))).FirstOrDefault(value=>value is not null);
    }

    private static string? CorporateEmail(string? value) => !string.IsNullOrWhiteSpace(value)&&value.Trim().EndsWith("@gaiaamazonas.org",StringComparison.OrdinalIgnoreCase)?value.Trim():null;
    private static TimeSpan RetryAfter(HttpResponseMessage response) => response.Headers.RetryAfter?.Delta is { } delay ? delay : TimeSpan.FromMinutes(1);
    private static string? Text(JsonElement item,string field) => item.TryGetProperty(field,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    private static int? Number(JsonElement item,string field) => DataverseJson.OptionalEncodedInt32(item,field);
    private static bool Boolean(JsonElement item,string field) => item.TryGetProperty(field,out var value)
        && value.ValueKind == JsonValueKind.True && value.GetBoolean();
}
