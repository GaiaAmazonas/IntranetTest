using Gaia.Modules.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
namespace Gaia.Modules.Communications;
public static class CommunicationsModule
{
 public static IEndpointRouteBuilder MapCommunicationsEndpoints(this IEndpointRouteBuilder endpoints){
  var admin=endpoints.MapGroup("/api/communications").WithTags("Communications").RequireAuthorization();
  admin.MapGet("/event-types",(bool includeInactive,ICommunicationsStore s,CancellationToken ct)=>s.ListEventTypesAsync(includeInactive,ct)).RequireAuthorization(AdminCorePermissions.ComEventTypesRead);
  admin.MapPost("/event-types",(EventTypeWriteRequest r,ICommunicationsStore s,CancellationToken ct)=>Safe(()=>s.SaveEventTypeAsync(null,r,ct))).RequireAuthorization(AdminCorePermissions.ComEventTypesManage);
  admin.MapPut("/event-types/{id:guid}",(Guid id,EventTypeWriteRequest r,ICommunicationsStore s,CancellationToken ct)=>Safe(()=>s.SaveEventTypeAsync(id,r,ct))).RequireAuthorization(AdminCorePermissions.ComEventTypesManage);
  admin.MapGet("/events",(bool includeInactive,ICommunicationsStore s,CancellationToken ct)=>s.ListEventsAsync(includeInactive,ct)).RequireAuthorization(AdminCorePermissions.ComEventsRead);
  admin.MapPost("/events",(EventWriteRequest r,ICommunicationsStore s,CancellationToken ct)=>Safe(()=>s.SaveEventAsync(null,r,ct))).RequireAuthorization(AdminCorePermissions.ComEventsCreate);
  admin.MapPut("/events/{id:guid}",(Guid id,EventWriteRequest r,ICommunicationsStore s,CancellationToken ct)=>Safe(()=>s.SaveEventAsync(id,r,ct))).RequireAuthorization(AdminCorePermissions.ComEventsEdit);
  foreach(var action in new[]{"publish","cancel","finish","deactivate","restore"}){var captured=action;admin.MapPost($"/events/{{id:guid}}/{action}",(Guid id,ICommunicationsStore s,CancellationToken ct)=>Safe(()=>s.ChangeEventStateAsync(id,captured,ct))).RequireAuthorization(AdminCorePermissions.ComEventsState);}
  admin.MapGet("/highlights",(bool includeInactive,Guid? eventId,ICommunicationsStore s,CancellationToken ct)=>s.ListBannersAsync(includeInactive,eventId,ct)).RequireAuthorization(AdminCorePermissions.ComBannersRead);
  admin.MapPost("/highlights",(BannerWriteRequest r,ICommunicationsStore s,CancellationToken ct)=>Safe(()=>s.SaveBannerAsync(null,r,ct))).RequireAuthorization(AdminCorePermissions.ComBannersCreate);
  admin.MapPut("/highlights/{id:guid}",(Guid id,BannerWriteRequest r,ICommunicationsStore s,CancellationToken ct)=>Safe(()=>s.SaveBannerAsync(id,r,ct))).RequireAuthorization(AdminCorePermissions.ComBannersEdit);
  foreach(var action in new[]{"design","publish","close","reject","deactivate","restore"}){var captured=action;admin.MapPost($"/highlights/{{id:guid}}/{action}",(Guid id,BannerStateRequest? r,ICommunicationsStore s,CancellationToken ct)=>Safe(()=>s.ChangeBannerStateAsync(id,captured,r?.Reason,ct))).RequireAuthorization(AdminCorePermissions.ComBannersState);}
  admin.MapPut("/highlights/{id:guid}/images/{variant}",UploadImage).DisableAntiforgery().RequireAuthorization(AdminCorePermissions.ComBannersEdit);
  var pub=endpoints.MapGroup("/api/intranet").WithTags("Intranet").RequireAuthorization(AdminCorePermissions.IntranetVer);
  pub.MapGet("/events",(DateTimeOffset? from,DateTimeOffset? to,ICommunicationsStore s,CancellationToken ct)=>s.ListPublicEventsAsync(from??DateTimeOffset.UtcNow.AddMonths(-1),to??DateTimeOffset.UtcNow.AddMonths(6),ct)).RequireAuthorization(AdminCorePermissions.IntranetCalendarioVer);
  pub.MapGet("/events/{id:guid}",GetPublicEvent).RequireAuthorization(AdminCorePermissions.IntranetCalendarioVer);
  pub.MapGet("/banners",(ICommunicationsStore s,CancellationToken ct)=>s.ListPublicBannersAsync(DateTimeOffset.UtcNow,ct)).RequireAuthorization(AdminCorePermissions.IntranetInicioVer);
  pub.MapGet("/banners/{id:guid}/images/{variant}",GetImage).RequireAuthorization(AdminCorePermissions.IntranetVer);
  return endpoints;
 }
 private static async Task<IResult> UploadImage(Guid id,string variant,IFormFile file,ICommunicationsStore store,CancellationToken token){var allowedContentType=file.ContentType is "image/jpeg" or "image/png" or "image/webp";if(variant is not("desktop" or "mobile")||file.Length==0||file.Length>2*1024*1024||!allowedContentType)return Results.ValidationProblem(new Dictionary<string,string[]>{{"image",["Carga una imagen JPG, PNG o WebP de máximo 2 MB."]}});await using var stream=file.OpenReadStream();await store.UploadBannerImageAsync(id,variant,stream,file.ContentType,token);return Results.NoContent();}
 private static async Task<IResult> GetPublicEvent(Guid id,ICommunicationsStore store,CancellationToken token){var item=await store.GetPublicEventAsync(id,token);return item is null?Results.NotFound():Results.Ok(item);}
 private static async Task<IResult> GetImage(Guid id,string variant,HttpContext context,ICommunicationsStore store,CancellationToken token){var item=await store.ReadBannerImageAsync(id,variant,token);if(item is null)return Results.NotFound();context.Response.GetTypedHeaders().CacheControl=new Microsoft.Net.Http.Headers.CacheControlHeaderValue{Private=true,MaxAge=TimeSpan.FromMinutes(20)};return Results.File(item.Bytes,item.ContentType,enableRangeProcessing:false);}
 private static async Task<IResult> Safe<T>(Func<Task<T>> operation){try{return Results.Ok(await operation());}catch(ArgumentException e){return Results.ValidationProblem(new Dictionary<string,string[]>{{"request",[e.Message]}});}catch(KeyNotFoundException e){return Results.NotFound(new{detail=e.Message});}catch(InvalidOperationException e){return Results.Problem(statusCode:422,title:"No fue posible completar la operación",detail:e.Message);}}
}
public sealed record BannerStateRequest(string? Reason);
