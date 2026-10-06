using System.Net;
using System.Net.Http.Json;
using System.Text;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Security;

namespace Gaia.Api.Infrastructure.Dataverse.Solicitudes;

internal static class DevelopmentSolicitudesDataReset
{
    private const string Confirmation = "BORRAR DATOS DE PRUEBA SOLICITUDES";
    private static readonly Action<ILogger,Guid,Exception?> LogServicePurgeFailure =
        LoggerMessage.Define<Guid>(LogLevel.Error,new EventId(4210,nameof(LogServicePurgeFailure)),"No fue posible eliminar el servicio de prueba {ServiceId} y sus relaciones.");
    private static readonly string[] DeleteOrder =
    [
        "gaia_adjuntosolicitud", "gaia_respuestaopciongestion", "gaia_respuestacampogestion",
        "gaia_respuestaopcioncampo", "gaia_respuestacampo", "gaia_dependenciagestion",
        "gaia_historialsolicitud", "gaia_comentariosolicitud", "gaia_calificacionsolicitud",
        "gaia_gestionsolicitud", "gaia_instanciaflujo", "gaia_solicitud",
        "gaia_opcioncampoformulariopaso", "gaia_campoformulariopaso", "gaia_formulariopaso",
        "gaia_rutaflujo", "gaia_pasoflujo", "gaia_flujogestion",
        "gaia_opcioncampoformulario", "gaia_campoformulario", "gaia_formularioservicio",
        "gaia_servicio"
    ];

    public static IEndpointRouteBuilder MapDevelopmentSolicitudesDataReset(this IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/development/maintenance/solicitudes-test-data")
            .RequireAuthorization(AdminCorePermissions.SolicitudesCatalogosAdministrar);
        group.MapGet("",Preview);
        group.MapPost("",Execute).DisableAntiforgery();
        group.MapDelete("/services/{serviceId:guid}",ExecuteService);
        return endpoints;
    }

    private static async Task<IResult> ExecuteService(Guid serviceId,HttpContext context,IDataverseDelegatedClientFactory clients,IFileStorage storage,IFileStorageMaintenance maintenance,ILoggerFactory loggerFactory,CancellationToken token)
    {
        try
        {
            DemandLoopback(context);
            var client=await clients.CreateAsync();
            var inventory=await ServiceInventory(client,serviceId,token);
            if(inventory[^1].Ids.Count==0)return Results.NotFound(new{detail="El servicio no existe o ya fue eliminado."});
            await DeleteAttachmentFiles(client,inventory.First(item=>item.LogicalName=="gaia_adjuntosolicitud").Ids,storage,maintenance,token);
            await ClearCurrentServiceDefinitions(client,serviceId,token);
            foreach(var item in inventory)
                foreach(var id in item.Ids)
                {
                    using var request=new HttpRequestMessage(HttpMethod.Delete,$"{item.EntitySet}({id:D})");
                    request.Headers.TryAddWithoutValidation("If-Match","*");
                    using var response=await client.SendAsync(request,token);
                    if(response.StatusCode==HttpStatusCode.NotFound)continue;
                    if(!response.IsSuccessStatusCode)
                    {
                        var detail=await response.Content.ReadAsStringAsync(token);
                        throw new InvalidOperationException($"No fue posible eliminar {item.LogicalName} ({id:D}): {detail}");
                    }
                }
            var remaining=await ServiceInventory(client,serviceId,token);
            if(remaining.Sum(item=>item.Ids.Count)!=0)throw new InvalidOperationException("La verificación encontró registros relacionados pendientes.");
            return Results.Ok(new{serviceId,deleted=inventory.Sum(item=>item.Ids.Count)});
        }
        catch(Exception error) when(error is InvalidOperationException or FileStorageException or HttpRequestException)
        {
            LogServicePurgeFailure(loggerFactory.CreateLogger(typeof(DevelopmentSolicitudesDataReset)),serviceId,error);
            return Results.Problem(
                title:"No fue posible eliminar el servicio y sus datos relacionados.",
                detail:error.Message,
                statusCode:StatusCodes.Status409Conflict);
        }
    }

    private static async Task DeleteAttachmentFiles(HttpClient client,IReadOnlyList<Guid> attachmentIds,IFileStorage storage,IFileStorageMaintenance maintenance,CancellationToken token)
    {
        if(attachmentIds.Count==0)return;
        var table=await DataverseMetadataResolver.TableAsync(client,"gaia_adjuntosolicitud",token);
        var repository=table.Attribute("gaia_RepositorioExternoId");var container=table.Attribute("gaia_ContenedorExternoId");var file=table.Attribute("gaia_ArchivoExternoId");
        foreach(var attachmentId in attachmentIds)
        {
            var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{table.EntitySetName}({attachmentId:D})?$select={repository},{container},{file}",token);
            if(row is null)continue;
            var repositoryId=Text(row.Value,repository);var containerId=Text(row.Value,container);var fileId=Text(row.Value,file);
            if(string.IsNullOrWhiteSpace(repositoryId)||string.IsNullOrWhiteSpace(containerId)||string.IsNullOrWhiteSpace(fileId))continue;
            var external=new ExternalFileId("SharePoint",repositoryId,containerId,fileId);
            try
            {
                var metadata=await storage.GetMetadataAsync(external,token);
                await maintenance.DeletePhysicallyAsync(new(external,metadata.ETag,"Temporary Solicitudes service purge"),token);
            }
            catch(FileStorageException error)when(error.Code==FileStorageError.FileNotFound){}
        }
    }

    private static async Task<IResult> Preview(HttpContext context,IDataverseDelegatedClientFactory clients,CancellationToken token)
    {
        DemandLoopback(context);
        var inventory=await Inventory(await clients.CreateAsync(),token);
        return Results.Content(Page(inventory,false,null),"text/html",Encoding.UTF8);
    }

    private static async Task<IResult> Execute(HttpContext context,IDataverseDelegatedClientFactory clients,CancellationToken token)
    {
        DemandLoopback(context);
        var form=await context.Request.ReadFormAsync(token);
        if(!string.Equals(form["confirmation"],Confirmation,StringComparison.Ordinal))
            return Results.Content(Page([],false,"La confirmación no coincide. No se eliminó ningún registro."),"text/html",Encoding.UTF8,statusCode:400);
        var client=await clients.CreateAsync();
        var inventory=await Inventory(client,token);
        await ClearCurrentServiceDefinitions(client,token);
        foreach(var item in inventory)
            foreach(var id in item.Ids)
            {
                using var request=new HttpRequestMessage(HttpMethod.Delete,$"{item.EntitySet}({id:D})");
                request.Headers.TryAddWithoutValidation("If-Match","*");
                using var response=await client.SendAsync(request,token);
                if(response.StatusCode==HttpStatusCode.NotFound)continue;
                if(!response.IsSuccessStatusCode)
                {
                    var detail=await response.Content.ReadAsStringAsync(token);
                    throw new InvalidOperationException($"No fue posible eliminar {item.LogicalName} ({id:D}): {detail}");
                }
            }
        var remaining=await Inventory(client,token);
        if(remaining.Sum(x=>x.Ids.Count)!=0)throw new InvalidOperationException("La verificación encontró datos de prueba de Solicitudes pendientes.");
        return Results.Content(Page(remaining,true,$"Se eliminaron físicamente {inventory.Sum(x=>x.Ids.Count)} registros de datos y configuración de Solicitudes."),"text/html",Encoding.UTF8);
    }

    private static async Task<IReadOnlyList<TableInventory>> Inventory(HttpClient client,CancellationToken token)
    {
        var result=new List<TableInventory>();
        foreach(var logicalName in DeleteOrder)
        {
            var table=await DataverseMetadataResolver.TableAsync(client,logicalName,token);
            var rows=await DataverseJson.ReadAllAsync(client,$"{table.EntitySetName}?$select={table.PrimaryIdAttribute}",token);
            result.Add(new(logicalName,table.EntitySetName,rows.Select(row=>RequiredGuid(row,table.PrimaryIdAttribute)).ToArray()));
        }
        return result;
    }

    private static async Task ClearCurrentServiceDefinitions(HttpClient client,CancellationToken token)
    {
        var service=await DataverseMetadataResolver.TableAsync(client,"gaia_servicio",token);
        var currentForm=service.Relationship("gaia_FormularioVigente","gaia_formularioservicio");
        var currentFlow=service.Relationship("gaia_FlujoVigente","gaia_flujogestion");
        var rows=await DataverseJson.ReadAllAsync(client,$"{service.EntitySetName}?$select={service.PrimaryIdAttribute}",token);
        foreach(var row in rows)
        {
            var id=RequiredGuid(row,service.PrimaryIdAttribute);
            using var request=new HttpRequestMessage(HttpMethod.Patch,$"{service.EntitySetName}({id:D})")
            {
                Content=JsonContent.Create(new Dictionary<string,object?>
                {
                    [currentForm.NavigationProperty+"@odata.bind"]=null,
                    [currentFlow.NavigationProperty+"@odata.bind"]=null
                })
            };
            request.Headers.TryAddWithoutValidation("If-Match","*");
            using var response=await client.SendAsync(request,token);
            if(!response.IsSuccessStatusCode)
            {
                var detail=await response.Content.ReadAsStringAsync(token);
                throw new InvalidOperationException($"No fue posible liberar las referencias vigentes del servicio {id:D}: {detail}");
            }
        }
    }

    private static async Task ClearCurrentServiceDefinitions(HttpClient client,Guid serviceId,CancellationToken token)
    {
        var service=await DataverseMetadataResolver.TableAsync(client,"gaia_servicio",token);
        var currentForm=service.Relationship("gaia_FormularioVigente","gaia_formularioservicio");
        var currentFlow=service.Relationship("gaia_FlujoVigente","gaia_flujogestion");
        using var request=new HttpRequestMessage(HttpMethod.Patch,$"{service.EntitySetName}({serviceId:D})")
        {
            Content=JsonContent.Create(new Dictionary<string,object?>
            {
                [currentForm.NavigationProperty+"@odata.bind"]=null,
                [currentFlow.NavigationProperty+"@odata.bind"]=null
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match","*");
        using var response=await client.SendAsync(request,token);
        if(response.StatusCode==HttpStatusCode.NotFound)return;
        if(!response.IsSuccessStatusCode)
        {
            var detail=await response.Content.ReadAsStringAsync(token);
            throw new InvalidOperationException($"No fue posible liberar las referencias vigentes del servicio {serviceId:D}: {detail}");
        }
    }

    private static async Task<IReadOnlyList<TableInventory>> ServiceInventory(HttpClient client,Guid serviceId,CancellationToken token)
    {
        var ids=new Dictionary<string,HashSet<Guid>>(StringComparer.OrdinalIgnoreCase);
        async Task<HashSet<Guid>> Related(string tableName,string relationship,string targetTable,IEnumerable<Guid> parents)
        {
            var parentIds=parents.Distinct().ToArray();
            if(parentIds.Length==0)return [];
            var table=await DataverseMetadataResolver.TableAsync(client,tableName,token);
            var lookup=table.Relationship(relationship,targetTable).ReferencingAttribute;
            var filter=string.Join(" or ",parentIds.Select(id=>$"_{lookup}_value eq {id:D}"));
            var rows=await DataverseJson.ReadAllAsync(client,$"{table.EntitySetName}?$select={table.PrimaryIdAttribute}&$filter={filter}",token);
            return rows.Select(row=>RequiredGuid(row,table.PrimaryIdAttribute)).ToHashSet();
        }
        async Task Add(string tableName,string relationship,string targetTable,IEnumerable<Guid> parents)
        {
            var values=await Related(tableName,relationship,targetTable,parents);
            if(ids.TryGetValue(tableName,out var existing))existing.UnionWith(values);else ids[tableName]=values;
        }

        var service=await DataverseMetadataResolver.TableAsync(client,"gaia_servicio",token);
        var serviceRow=await DataverseMetadataResolver.ReadOneAsync(client,$"{service.EntitySetName}({serviceId:D})?$select={service.PrimaryIdAttribute}",token);
        ids["gaia_servicio"]=serviceRow is null?[]:[serviceId];
        await Add("gaia_formularioservicio","gaia_Servicio","gaia_servicio",[serviceId]);
        await Add("gaia_campoformulario","gaia_Formulario","gaia_formularioservicio",ids["gaia_formularioservicio"]);
        await Add("gaia_opcioncampoformulario","gaia_CampoFormulario","gaia_campoformulario",ids["gaia_campoformulario"]);
        await Add("gaia_flujogestion","gaia_Servicio","gaia_servicio",[serviceId]);
        await Add("gaia_pasoflujo","gaia_FlujoGestion","gaia_flujogestion",ids["gaia_flujogestion"]);
        await Add("gaia_rutaflujo","gaia_FlujoGestion","gaia_flujogestion",ids["gaia_flujogestion"]);
        await Add("gaia_formulariopaso","gaia_PasoFlujo","gaia_pasoflujo",ids["gaia_pasoflujo"]);
        await Add("gaia_campoformulariopaso","gaia_FormularioPaso","gaia_formulariopaso",ids["gaia_formulariopaso"]);
        await Add("gaia_opcioncampoformulariopaso","gaia_CampoFormularioPaso","gaia_campoformulariopaso",ids["gaia_campoformulariopaso"]);
        await Add("gaia_solicitud","gaia_Servicio","gaia_servicio",[serviceId]);
        await Add("gaia_instanciaflujo","gaia_Solicitud","gaia_solicitud",ids["gaia_solicitud"]);
        await Add("gaia_instanciaflujo","gaia_FlujoGestion","gaia_flujogestion",ids["gaia_flujogestion"]);
        await Add("gaia_gestionsolicitud","gaia_Solicitud","gaia_solicitud",ids["gaia_solicitud"]);
        await Add("gaia_gestionsolicitud","gaia_InstanciaFlujo","gaia_instanciaflujo",ids["gaia_instanciaflujo"]);
        await Add("gaia_dependenciagestion","gaia_InstanciaFlujo","gaia_instanciaflujo",ids["gaia_instanciaflujo"]);
        await Add("gaia_respuestacampo","gaia_Solicitud","gaia_solicitud",ids["gaia_solicitud"]);
        await Add("gaia_respuestaopcioncampo","gaia_RespuestaCampo","gaia_respuestacampo",ids["gaia_respuestacampo"]);
        await Add("gaia_respuestacampogestion","gaia_GestionSolicitud","gaia_gestionsolicitud",ids["gaia_gestionsolicitud"]);
        await Add("gaia_respuestaopciongestion","gaia_RespuestaCampoGestion","gaia_respuestacampogestion",ids["gaia_respuestacampogestion"]);
        await Add("gaia_adjuntosolicitud","gaia_Solicitud","gaia_solicitud",ids["gaia_solicitud"]);
        await Add("gaia_historialsolicitud","gaia_Solicitud","gaia_solicitud",ids["gaia_solicitud"]);
        await Add("gaia_comentariosolicitud","gaia_Solicitud","gaia_solicitud",ids["gaia_solicitud"]);
        await Add("gaia_calificacionsolicitud","gaia_Solicitud","gaia_solicitud",ids["gaia_solicitud"]);

        var result=new List<TableInventory>();
        foreach(var logicalName in DeleteOrder)
        {
            var table=await DataverseMetadataResolver.TableAsync(client,logicalName,token);
            result.Add(new(logicalName,table.EntitySetName,ids.GetValueOrDefault(logicalName,[]).ToArray()));
        }
        return result;
    }

    private static void DemandLoopback(HttpContext context)
    {
        var address=context.Connection.RemoteIpAddress;
        if(address is null||!IPAddress.IsLoopback(address))throw new UnauthorizedAccessException("Esta utilidad solo admite conexiones locales.");
    }

    private static Guid RequiredGuid(System.Text.Json.JsonElement row,string name)
    {
        if(row.TryGetProperty(name,out var value)&&Guid.TryParse(value.GetString(),out var id))return id;
        throw new InvalidOperationException($"Dataverse no devolvió {name}.");
    }
    private static string? Text(System.Text.Json.JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind==System.Text.Json.JsonValueKind.String?value.GetString():null;

    private static string Page(IReadOnlyList<TableInventory> inventory,bool completed,string? message)
    {
        var rows=string.Join("",inventory.Select(item=>$"<tr><td>{WebUtility.HtmlEncode(item.LogicalName)}</td><td>{item.Ids.Count}</td></tr>"));
        var total=inventory.Sum(x=>x.Ids.Count);
        var action=completed?"<p class='ok'>Verificación final: las tablas de datos y configuración de Solicitudes quedaron en cero.</p>":$"<form method='post'><label>Escribe <strong>{Confirmation}</strong><input name='confirmation' autocomplete='off' required></label><button type='submit'>Eliminar físicamente {total} registros</button></form>";
        return $$"""
        <!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Limpieza Solicitudes</title>
        <style>body{font-family:Segoe UI,sans-serif;max-width:820px;margin:50px auto;padding:0 22px;color:#17231f}section{border:1px solid #ccd8d2;border-radius:22px;padding:28px;box-shadow:0 12px 35px #173d3020}h1{margin-top:0}table{width:100%;border-collapse:collapse;margin:22px 0}td{padding:10px;border-bottom:1px solid #e3eae6}td:last-child{text-align:right;font-weight:700}label,input{display:block}input{width:100%;box-sizing:border-box;margin:8px 0 16px;padding:12px;border:1px solid #899c93;border-radius:10px}button{border:0;border-radius:12px;background:#96394b;color:white;font-weight:700;padding:13px 18px}.ok{padding:14px;border-radius:12px;background:#e5f5eb;color:#245f42}.error{padding:14px;border-radius:12px;background:#fff0f0;color:#96394b}</style>
        <body><section><p>Development · localhost · utilidad protegida</p><h1>Reiniciar Solicitudes desde cero</h1><p>Se eliminan solicitudes, adjuntos, gestiones y toda la configuración creada: servicios, formularios, campos, opciones, flujos, etapas, rutas y conexiones. Se conservan las tablas de Dataverse y los catálogos estructurales de estados, transiciones y calendarios.</p>{{(message is null?"":$"<p class='{(completed?"ok":"error")}'>{WebUtility.HtmlEncode(message)}</p>")}}<table>{{rows}}</table>{{action}}</section></body></html>
        """;
    }

    private sealed record TableInventory(string LogicalName,string EntitySet,IReadOnlyList<Guid> Ids);
}
