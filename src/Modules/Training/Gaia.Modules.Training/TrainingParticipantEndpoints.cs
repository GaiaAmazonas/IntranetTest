using System.Security.Claims;
using System.Text;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Gaia.Modules.Training;

public static class TrainingParticipantEndpoints
{
    public static IEndpointRouteBuilder MapTrainingParticipantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var my=endpoints.MapGroup("/api/training/my/assignments/{id:guid}").RequireAuthorization(AdminCorePermissions.IntranetTrainingVer);
        my.MapGet("/evaluations",(Guid id,ClaimsPrincipal user,ISecurityStore security,ITrainingParticipantOperations operations,CancellationToken ct)=>Safe(async()=>Results.Ok(await operations.ReadEvaluationsAsync(id,await Actor(user,security,ct),ct))));
        my.MapPost("/evaluations/{evaluationId:guid}/start",(Guid id,Guid evaluationId,StartTrainingAttempt value,ClaimsPrincipal user,ISecurityStore security,ITrainingParticipantOperations operations,CancellationToken ct)=>Safe(async()=>Results.Ok(await operations.StartAttemptAsync(id,evaluationId,value.Token,await Actor(user,security,ct),ct))));
        my.MapPost("/attempts/{attemptId:guid}/submit",(Guid id,Guid attemptId,SubmitTrainingAttempt value,ClaimsPrincipal user,ISecurityStore security,ITrainingParticipantOperations operations,CancellationToken ct)=>Safe(async()=>Results.Ok(await operations.SubmitAttemptAsync(id,attemptId,value,await Actor(user,security,ct),ct))));
        my.MapGet("/resources/{resourceId:guid}",(Guid id,Guid resourceId,bool? download,HttpContext context,ISecurityStore security,ITrainingParticipantOperations operations,IFileStorage storage,CancellationToken ct)=>Safe(async()=>
        {
            var file=await storage.DownloadAsync(await operations.ReadResourceAsync(id,resourceId,await Actor(context.User,security,ct),ct),ct);
            context.Response.Headers.CacheControl="private, no-store";context.Response.Headers["X-Content-Type-Options"]="nosniff";
            var mime=file.Metadata.ContentType;var inline=mime=="application/pdf"||mime.StartsWith("video/",StringComparison.OrdinalIgnoreCase);
            var content=inline?await Seekable(file,ct):file.Content;
            return Results.Stream(content,mime,download==true||!inline?file.Metadata.OriginalName??file.Metadata.StoredName:null,enableRangeProcessing:true);
        }));
        var admin=endpoints.MapGroup("/api/training/administration");
        admin.MapGet("/versions/{versionId:guid}/resources/{resourceId:guid}",(Guid versionId,Guid resourceId,bool? download,HttpContext context,ITrainingParticipantOperations operations,IFileStorage storage,CancellationToken ct)=>Safe(async()=>
        {
            var file=await storage.DownloadAsync(await operations.ReadVersionResourceAsync(versionId,resourceId,ct),ct);
            context.Response.Headers.CacheControl="private, no-store";context.Response.Headers["X-Content-Type-Options"]="nosniff";
            var mime=file.Metadata.ContentType;var inline=mime=="application/pdf"||mime.StartsWith("video/",StringComparison.OrdinalIgnoreCase);
            return Results.Stream(inline?await Seekable(file,ct):file.Content,mime,download==true||!inline?file.Metadata.OriginalName??file.Metadata.StoredName:null,enableRangeProcessing:true);
        })).RequireAuthorization(AdminCorePermissions.TrainingContentRead);
        admin.MapGet("/pending-reviews",(ITrainingParticipantOperations operations,CancellationToken ct)=>Safe(async()=>Results.Ok(await operations.ReadPendingReviewsAsync(ct)))).RequireAuthorization(AdminCorePermissions.TrainingReview);
        admin.MapPost("/attempts/{attemptId:guid}/grade",(Guid attemptId,GradeTrainingAttempt value,ClaimsPrincipal user,ISecurityStore security,ITrainingParticipantOperations operations,CancellationToken ct)=>Safe(async()=>{await operations.GradeAttemptAsync(attemptId,value,await Actor(user,security,ct),ct);return Results.NoContent();})).RequireAuthorization(AdminCorePermissions.TrainingReview);
        admin.MapGet("/results/export",(ITrainingOperations operations,CancellationToken ct)=>Safe(async()=>
        {
            var rows=(await operations.ReadResultsAsync(ct)).Items;
            var csv=new StringBuilder("Participante;Unidad;Capacitación;Versión;Estado;Avance;Resultado;Intentos;Finalización\r\n");
            foreach(var row in rows)csv.AppendLine(string.Join(';',new[]{row.Participant,row.Unit??"",row.Training,row.Version,row.Status.ToString(System.Globalization.CultureInfo.InvariantCulture),row.Progress.ToString(System.Globalization.CultureInfo.InvariantCulture),row.Result?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"",row.Attempts.ToString(System.Globalization.CultureInfo.InvariantCulture),row.CompletedAt?.ToString("O")??""}.Select(Csv)));
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),"text/csv; charset=utf-8","resultados-capacitaciones.csv");
        })).RequireAuthorization(AdminCorePermissions.TrainingResultsExport);
        return endpoints;
    }
    static string Csv(string value)=>"\""+((value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@')?"'"+value:value).Replace("\"","\"\"")+"\"";
    // Graph returns a forward-only stream. A bounded temporary file supports MP4/PDF range requests.
    static async Task<Stream> Seekable(FileDownload file,CancellationToken ct)
    {
        if(file.Content.CanSeek)return file.Content;
        await using var download=file;
        if(file.Metadata.Length>262_144_000)throw new FileStorageException(FileStorageError.FileTooLarge);
        var stream=new FileStream(Path.GetTempFileName(),FileMode.Open,FileAccess.ReadWrite,FileShare.None,81920,FileOptions.Asynchronous|FileOptions.DeleteOnClose);
        try{var buffer=new byte[81920];long total=0;int count;while((count=await file.Content.ReadAsync(buffer,ct))>0){total+=count;if(total>262_144_000)throw new FileStorageException(FileStorageError.FileTooLarge);await stream.WriteAsync(buffer.AsMemory(0,count),ct);}stream.Position=0;return stream;}catch{await stream.DisposeAsync();throw;}
    }
    static async Task<Guid> Actor(ClaimsPrincipal user,ISecurityStore security,CancellationToken ct)=>(await security.GetOrProvisionAsync(user,ct)).User.ThirdPartyId??throw new InvalidOperationException("Tu cuenta no está vinculada a una persona institucional.");
    internal static async Task<IResult> Safe(Func<Task<IResult>> operation)
    {
        try{return await operation();}
        catch(ArgumentException e){return Results.Problem(statusCode:400,title:"Revisa los datos",detail:e.Message);}
        catch(KeyNotFoundException e){return Results.Problem(statusCode:404,title:"No disponible",detail:e.Message);}
        catch(UnauthorizedAccessException){return Results.Problem(statusCode:403,title:"Acceso no autorizado",detail:"No tienes acceso a esta capacitación.");}
        catch(FileStorageException e){return Results.Problem(statusCode:422,title:"Archivo no disponible",detail:e.Message);}
        catch(InvalidOperationException e){return Results.Problem(statusCode:409,title:"No fue posible completar la acción",detail:e.Message.StartsWith("Dataverse",StringComparison.Ordinal)?"No fue posible acceder a los datos de capacitación. Revisa la configuración publicada y vuelve a intentarlo.":e.Message);}
    }
}
