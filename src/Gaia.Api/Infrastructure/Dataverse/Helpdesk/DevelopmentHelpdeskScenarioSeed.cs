using System.Net;
using System.Security.Claims;
using System.Text;
using Gaia.Modules.Helpdesk;
using Gaia.Modules.Security;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal static class DevelopmentHelpdeskScenarioSeed
{
    public static IEndpointRouteBuilder MapDevelopmentHelpdeskScenarioSeed(this IEndpointRouteBuilder endpoints)
    {
        var group=endpoints.MapGroup("/development/maintenance/helpdesk-scenarios")
            .RequireAuthorization(AdminCorePermissions.HelpdeskCatalogosAdministrar);
        group.MapGet("",()=>Results.Content(Page(null),"text/html",Encoding.UTF8));
        group.MapPost("",Seed).DisableAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> Seed(HttpContext context,ClaimsPrincipal principal,ISecurityStore security,
        IHelpdeskManagementApplication management,HelpdeskWorkflowApplication workflows,CancellationToken token)
    {
        DemandLoopback(context);
        var snapshot=await management.ReadAdministrationAsync(token);
        var expected=new[]{"ADQUISICION_TECNOLOGICA","HERRAMIENTA_DIGITAL","INICIATIVA_TECNOLOGICA"};
        if(snapshot.Services.Any(x=>!expected.Contains(x.Code)))throw new InvalidOperationException("El catálogo contiene servicios ajenos a estos escenarios.");
        var ti=Unit(snapshot,"300204");var direction=Unit(snapshot,"30");var finance=Unit(snapshot,"300203");
        var actor=(await security.GetOrProvisionAsync(principal,token)).User.ThirdPartyId
            ?? throw new UnauthorizedAccessException("El administrador no está asociado con un tercero activo.");

        await CreateSequential(management,workflows,snapshot,actor,ti,direction,finance,false,token);
        snapshot=await management.ReadAdministrationAsync(token);
        await CreateSequential(management,workflows,snapshot,actor,ti,direction,finance,true,token);
        snapshot=await management.ReadAdministrationAsync(token);
        await CreateParallel(management,workflows,snapshot,actor,ti,direction,finance,token);
        return Results.Content(Page("Se crearon tres servicios, sus formularios y sus flujos completos en estado Borrador. No se publicó ningún elemento."),"text/html",Encoding.UTF8);
    }

    private static async Task CreateSequential(IHelpdeskManagementApplication management,HelpdeskWorkflowApplication workflows,HelpdeskAdminSnapshot snapshot,
        Guid actor,HelpdeskAdminUnit ti,HelpdeskAdminUnit direction,HelpdeskAdminUnit finance,bool financeFirst,CancellationToken token)
    {
        var code=financeFirst?"HERRAMIENTA_DIGITAL":"ADQUISICION_TECNOLOGICA";
        var name=financeFirst?"Solicitud de herramienta digital":"Adquisición de equipos tecnológicos";
        var description=financeFirst
            ?"Evalúa licencias, plataformas y servicios digitales con validación financiera previa a la aprobación directiva."
            :"Gestiona la compra de equipos con concepto técnico, aprobación directiva y disponibilidad presupuestal.";
        var service=snapshot.Services.FirstOrDefault(x=>x.Code==code)?.Id??await management.SaveServiceAsync(null,new(code,name,description,
            "Describe claramente la necesidad, las personas beneficiarias y el resultado esperado.",10,true,5,25,false,
            financeFirst?20:10,actor,ti.Id,true),token);
        var form=(await management.ReadAdministrationAsync(token)).Forms.Single(x=>x.ServiceId==service&&x.Status==299540030).Id;
        await AddRequesterFields(management,form,financeFirst,token);
        var flow=await workflows.CreateDraftAsync(new(service,$"Flujo · {name}",description),token);
        var technical=await Step(workflows,flow,"VALIDACION_TECNICA",299540141,10,true,false,ti.Id,true,true,3,token);
        var firstUnit=financeFirst?finance:direction;var secondUnit=financeFirst?direction:finance;
        var firstCode=financeFirst?"APROBACION_FINANCIERA":"APROBACION_DIRECTIVA";
        var secondCode=financeFirst?"APROBACION_DIRECTIVA":"APROBACION_FINANCIERA";
        var first=await Step(workflows,flow,firstCode,299540142,20,false,false,firstUnit.Id,true,true,3,token);
        var second=await Step(workflows,flow,secondCode,299540142,30,false,false,secondUnit.Id,true,true,3,token);
        var execute=await Step(workflows,flow,"EJECUCION_TI",299540140,40,false,false,ti.Id,false,true,5,token);
        var close=await Step(workflows,flow,"RESPUESTA_FINAL",299540144,50,false,true,ti.Id,false,true,1,token);
        var reject=await Step(workflows,flow,"CIERRE_NO_APROBADO",299540144,60,false,true,ti.Id,false,true,1,token);
        await StageForm(workflows,technical,"Validación técnica",[
            Text("CONCEPTO_TECNICO","Concepto técnico",0,true,"Explica viabilidad, alcance y recomendaciones."),
            Choice("NIVEL_PRIORIDAD","Prioridad recomendada",1,["Baja","Media","Alta"])
        ],token);
        await ApprovalForm(workflows,first,financeFirst?"Decisión financiera":"Decisión directiva",token);
        await ApprovalForm(workflows,second,financeFirst?"Decisión directiva":"Decisión financiera",token);
        await StageForm(workflows,execute,"Ejecución por Tecnología",[
            Text("ACCIONES_REALIZADAS","Acciones realizadas",0,true,"Detalla compra, configuración o entrega."),
            Text("REFERENCIA","Referencia o número de orden",1,false,"Número de orden, licencia o activo, si aplica.")
        ],token);
        await StageForm(workflows,close,"Respuesta final",[Text("RESULTADO_ENTREGADO","Resultado entregado",0,true,"Resume lo entregado y las recomendaciones de uso.")],token);
        await StageForm(workflows,reject,"Comunicación de no aprobación",[Text("MOTIVO_CIERRE","Motivo y orientación",0,true,"Explica la decisión y las alternativas disponibles.")],token);
        await Route(workflows,flow,"TECNICA_APROBADA",technical,first,299540171,10,token);
        await Route(workflows,flow,"TECNICA_RECHAZADA",technical,reject,299540172,20,token);
        await Route(workflows,flow,"PRIMERA_APROBADA",first,second,299540171,30,token);
        await Route(workflows,flow,"PRIMERA_RECHAZADA",first,reject,299540172,40,token);
        await Route(workflows,flow,"SEGUNDA_APROBADA",second,execute,299540171,50,token);
        await Route(workflows,flow,"SEGUNDA_RECHAZADA",second,reject,299540172,60,token);
        await Route(workflows,flow,"EJECUCION_COMPLETA",execute,close,299540170,70,token);
    }

    private static async Task CreateParallel(IHelpdeskManagementApplication management,HelpdeskWorkflowApplication workflows,HelpdeskAdminSnapshot snapshot,
        Guid actor,HelpdeskAdminUnit ti,HelpdeskAdminUnit direction,HelpdeskAdminUnit finance,CancellationToken token)
    {
        const string name="Iniciativa tecnológica institucional";
        var service=snapshot.Services.FirstOrDefault(x=>x.Code=="INICIATIVA_TECNOLOGICA")?.Id??await management.SaveServiceAsync(null,new("INICIATIVA_TECNOLOGICA",name,
            "Evalúa iniciativas que requieren conceptos simultáneos de Dirección y Coordinación Financiera.",
            "Presenta el propósito, alcance, costo estimado e impacto institucional.",15,true,8,25,false,30,actor,ti.Id,true),token);
        var form=(await management.ReadAdministrationAsync(token)).Forms.Single(x=>x.ServiceId==service&&x.Status==299540030).Id;
        await AddRequesterFields(management,form,false,token);
        await EnsureField(management,form,Money("PRESUPUESTO_ESTIMADO","Presupuesto estimado",4,true),token);
        var flow=await workflows.CreateDraftAsync(new(service,$"Flujo paralelo · {name}","Dirección y Financiera revisan al mismo tiempo; el cierre espera ambos resultados."),token);
        var technical=await Step(workflows,flow,"VALIDACION_TECNICA",299540141,10,true,false,ti.Id,true,true,3,token);
        var dir=await Step(workflows,flow,"APROBACION_DIRECTIVA",299540142,20,false,false,direction.Id,true,true,4,token);
        var fin=await Step(workflows,flow,"APROBACION_FINANCIERA",299540142,30,false,false,finance.Id,true,true,4,token);
        var dirResult=await Step(workflows,flow,"RESULTADO_DIRECTIVA",299540141,40,false,false,ti.Id,false,false,1,token);
        var finResult=await Step(workflows,flow,"RESULTADO_FINANCIERO",299540141,50,false,false,ti.Id,false,false,1,token);
        var consolidate=await Step(workflows,flow,"CONSOLIDAR_RESULTADOS",299540141,60,false,false,ti.Id,false,true,2,token,299540161);
        var close=await Step(workflows,flow,"RESPUESTA_FINAL",299540144,70,false,true,ti.Id,false,true,1,token);
        await StageForm(workflows,technical,"Validación técnica de la iniciativa",[Text("CONCEPTO_TECNICO","Concepto técnico",0,true,"Describe viabilidad, dependencias y riesgos."),Choice("IMPACTO","Impacto esperado",1,["Equipo","Área","Institucional"])],token);
        await ApprovalForm(workflows,dir,"Aprobación de Dirección",token);await ApprovalForm(workflows,fin,"Aprobación financiera",token);
        await StageForm(workflows,dirResult,"Registrar resultado directivo",[Text("SINTESIS_DIRECTIVA","Síntesis del resultado",0,true,"Registra condiciones o motivos de la decisión.")],token);
        await StageForm(workflows,finResult,"Registrar resultado financiero",[Text("SINTESIS_FINANCIERA","Síntesis del resultado",0,true,"Registra disponibilidad y condiciones presupuestales.")],token);
        await StageForm(workflows,consolidate,"Consolidar decisiones",[Choice("DECISION_CONSOLIDADA","Resultado consolidado",0,["Aprobada","Aprobada con condiciones","No aprobada"]),Text("CONDICIONES","Condiciones y próximos pasos",1,true,"Integra ambos conceptos para preparar la respuesta.")],token);
        await StageForm(workflows,close,"Respuesta final al solicitante",[Text("RESPUESTA_SOLICITANTE","Respuesta final",0,true,"Comunica la decisión conjunta y los pasos siguientes.")],token);
        await Route(workflows,flow,"TECNICA_A_DIRECTIVA",technical,dir,299540171,10,token);await Route(workflows,flow,"TECNICA_A_FINANCIERA",technical,fin,299540171,20,token);
        await Route(workflows,flow,"DIRECTIVA_APROBADA",dir,dirResult,299540171,30,token);await Route(workflows,flow,"DIRECTIVA_RECHAZADA",dir,dirResult,299540172,40,token);
        await Route(workflows,flow,"FINANCIERA_APROBADA",fin,finResult,299540171,50,token);await Route(workflows,flow,"FINANCIERA_RECHAZADA",fin,finResult,299540172,60,token);
        await Route(workflows,flow,"RESULTADO_DIRECTIVO_LISTO",dirResult,consolidate,299540170,70,token);await Route(workflows,flow,"RESULTADO_FINANCIERO_LISTO",finResult,consolidate,299540170,80,token);
        await Route(workflows,flow,"CONSOLIDACION_COMPLETA",consolidate,close,299540170,90,token);
    }

    private static async Task AddRequesterFields(IHelpdeskManagementApplication management,Guid form,bool software,CancellationToken token)
    {
        var fields=new[]{Text("NECESIDAD","Necesidad o problema",0,true,"Explica qué necesitas resolver y por qué."),Text("JUSTIFICACION","Justificación",1,true,"Describe el beneficio esperado."),Choice("URGENCIA","Nivel de urgencia",2,["Baja","Media","Alta","Crítica"]),Text("BENEFICIARIOS","Personas o áreas beneficiarias",3,true,"Indica quiénes utilizarán el resultado.")};
        foreach(var field in fields)await EnsureField(management,form,field,token);
        if(software)await EnsureField(management,form,Text("HERRAMIENTA_REFERENCIA","Herramienta o proveedor sugerido",4,false,"Incluye un nombre o enlace de referencia, si existe."),token);
    }
    private static async Task EnsureField(IHelpdeskManagementApplication management,Guid form,SaveHelpdeskFormField field,CancellationToken token){var current=await management.ReadFormAsync(form,token);if(current.Fields.All(x=>x.Code!=field.Code))await management.SaveFormFieldAsync(form,null,field,token);}
    private static async Task<Guid> Step(HelpdeskWorkflowApplication app,Guid flow,string code,int type,int order,bool initial,bool final,Guid unit,bool decision,bool observation,int days,CancellationToken token,int activation=299540160)=>
        await app.SaveStepAsync(flow,null,new(code,type,order,initial,final,false,299540150,unit,null,activation,decision,observation,false,false,days,true),token);
    private static Task<Guid> Route(HelpdeskWorkflowApplication app,Guid flow,string code,Guid from,Guid to,int result,int order,CancellationToken token)=>app.SaveRouteAsync(flow,null,new(code,from,to,result,order,true),token);
    private static async Task ApprovalForm(HelpdeskWorkflowApplication app,Guid step,string title,CancellationToken token)=>await StageForm(app,step,title,[Text("JUSTIFICACION_DECISION","Justificación de la decisión",0,true,"Explica criterios, condiciones o motivos."),Text("CONDICIONES_APROBACION","Condiciones u observaciones",1,false,"Registra compromisos o restricciones, si aplica.")],token);
    private static async Task StageForm(HelpdeskWorkflowApplication app,Guid step,string title,IEnumerable<SaveHelpdeskFormField> fields,CancellationToken token){await app.SaveStageFormAsync(step,new(title,"Completa la información necesaria antes de finalizar esta etapa."),token);foreach(var field in fields)await app.SaveStageFormFieldAsync(step,null,field,token);}
    private static SaveHelpdeskFormField Text(string code,string label,int order,bool required,string help)=>new(code,label,299540040,299540051,help,"Escribe una respuesta clara",required,order,12,null,2000,null,null,false,null,null,true,[]);
    private static SaveHelpdeskFormField Money(string code,string label,int order,bool required)=>new(code,label,299540042,299540052,"Valor estimado en pesos colombianos.","0",required,order,6,null,null,0,null,false,null,null,true,[]);
    private static SaveHelpdeskFormField Choice(string code,string label,int order,IReadOnlyList<string> options)=>new(code,label,299540046,299540058,"Selecciona la opción que mejor corresponda.",null,true,order,6,null,null,null,null,false,null,null,true,options.Select((x,i)=>new SaveHelpdeskFormOption(null,Code(x),x,i,i==0)).ToArray());
    private static string Code(string value)=>string.Concat(value.Normalize(System.Text.NormalizationForm.FormD).Where(c=>System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)!=System.Globalization.UnicodeCategory.NonSpacingMark)).ToUpperInvariant().Replace(' ','_');
    private static HelpdeskAdminUnit Unit(HelpdeskAdminSnapshot snapshot,string code)=>snapshot.Units.SingleOrDefault(x=>x.Code==code)??throw new InvalidOperationException($"No se encontró la unidad organizacional {code}.");
    private static void DemandLoopback(HttpContext context){if(context.Connection.RemoteIpAddress is not { } address||!IPAddress.IsLoopback(address))throw new UnauthorizedAccessException("Esta utilidad solo admite conexiones locales.");}
    private static string Page(string? message)=>$$"""<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Escenarios Helpdesk</title><style>body{font-family:Segoe UI,sans-serif;max-width:820px;margin:50px auto;padding:0 22px;color:#17231f}section{border:1px solid #ccd8d2;border-radius:22px;padding:28px;box-shadow:0 12px 35px #173d3020}li{margin:12px 0}button{border:0;border-radius:12px;background:#245f58;color:white;font-weight:700;padding:13px 18px}.ok{padding:14px;border-radius:12px;background:#e5f5eb;color:#245f42}</style><body><section><p>Development · localhost</p><h1>Crear escenarios de demostración</h1><p>Los tres servicios, formularios y flujos quedarán en <strong>Borrador</strong>. No se publicará nada.</p><ol><li>Adquisición tecnológica: TI → Dirección → Financiera.</li><li>Herramienta digital: TI → Financiera → Dirección.</li><li>Iniciativa tecnológica: TI → Dirección y Financiera en paralelo → consolidación.</li></ol>{{(message is null?"<form method='post'><button>Crear los tres escenarios en borrador</button></form>":$"<p class='ok'>{WebUtility.HtmlEncode(message)}</p>")}}</section></body></html>""";
}
