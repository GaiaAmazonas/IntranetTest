using System.Net;
using System.Net.Http.Json;
using System.Text;
using Gaia.Modules.Security;

namespace Gaia.Api.Infrastructure.Dataverse.Solicitudes;

internal static class DevelopmentSolicitudesDataReset
{
    private const string Confirmation = "BORRAR DATOS DE PRUEBA SOLICITUDES";
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
        return endpoints;
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
