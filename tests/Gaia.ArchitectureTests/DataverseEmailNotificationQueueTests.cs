using System.Net;
using System.Text;
using System.Text.Json;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.Notifications;
using Gaia.BuildingBlocks.Notifications;

namespace Gaia.ArchitectureTests;

public sealed class DataverseEmailNotificationQueueTests
{
    [Fact]
    public async Task QueueCreatesDraftAndOnlyThenReleasesItAsPending()
    {
        var emailId=Guid.NewGuid();
        var handler=new QueueHandler(Json(TemplateMetadata()),Json(EmailMetadata()),Json(AttachmentMetadata()),
            Empty(),Json(TemplateRow()),Created(emailId),NoContent());
        var queue=new DataverseEmailNotificationQueue(new Factory(handler));

        var result=await queue.QueueAsync(Notification(),default);

        Assert.Equal(emailId,result.Id);Assert.False(result.AlreadyExisted);
        Assert.Equal([HttpMethod.Get,HttpMethod.Get,HttpMethod.Get,HttpMethod.Get,HttpMethod.Get,HttpMethod.Post,HttpMethod.Patch],handler.Methods);
        Assert.Contains(handler.Bodies,body=>body.Contains("\"gaia_estadoenvio\":299540000",StringComparison.Ordinal));
        Assert.Contains(handler.Bodies,body=>body.Contains("\"gaia_destinatariosjson\":\"[{\\u0022email\\u0022:\\u0022persona@gaia.org",StringComparison.Ordinal));
        Assert.Contains(handler.Bodies,body=>body.Contains("Mar\\u00EDa \\u0026 Gaia",StringComparison.Ordinal));
        Assert.Contains(handler.Bodies,body=>body.Contains("\"gaia_estadoenvio\":299540001",StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExistingOperationIsReturnedWithoutCreatingAnotherEmail()
    {
        var emailId=Guid.NewGuid();
        var handler=new QueueHandler(Json(TemplateMetadata()),Json(EmailMetadata()),Json(AttachmentMetadata()),
            Json($"{{\"value\":[{{\"gaia_correosalienteid\":\"{emailId:D}\",\"gaia_idoperacion\":\"SOL-1\"}}]}}"));
        var result=await new DataverseEmailNotificationQueue(new Factory(handler)).QueueAsync(Notification(),default);

        Assert.Equal(emailId,result.Id);Assert.True(result.AlreadyExisted);
        Assert.DoesNotContain(handler.Methods,method=>method==HttpMethod.Post||method==HttpMethod.Patch);
    }

    [Fact]
    public async Task AttachmentFailureMarksDraftAsPermanentError()
    {
        var handler=new QueueHandler(Json(TemplateMetadata()),Json(EmailMetadata()),Json(AttachmentMetadata()),
            Empty(),Json(TemplateRow(permitsAttachments:true)),Created(Guid.NewGuid()),
            new HttpResponseMessage(HttpStatusCode.BadRequest){Content=new StringContent("{\"error\":{\"message\":\"Documento rechazado\"}}",Encoding.UTF8,"application/json")},
            NoContent());
        var queue=new DataverseEmailNotificationQueue(new Factory(handler));
        var attachment=new EmailAttachmentReference("documento.pdf","SOLICITUDES","source","site","drive","item","application/pdf",128);

        await Assert.ThrowsAsync<InvalidOperationException>(()=>queue.QueueAsync(Notification() with{Attachments=[attachment]},default));

        Assert.Contains(handler.Bodies,body=>body.Contains("\"gaia_codigoerror\":\"OUTBOX_PREPARATION_FAILED\"",StringComparison.Ordinal));
        Assert.Contains(handler.Bodies,body=>body.Contains("\"gaia_estadoenvio\":299540005",StringComparison.Ordinal));
    }

    private static QueueEmailNotification Notification()=>new("SOL-1","CORR-1","SOLICITUDES","RADICACION","source",
        "SOLICITUD_RADICADA",[new("persona@gaia.org","María")],new Dictionary<string,string?>{{"nombre","María & Gaia"}},"SOL-1","Solicitud de prueba");

    private static string TemplateRow(bool permitsAttachments=false)
    {
        var template=new Dictionary<string,object?>
        {
            ["gaia_plantillacorreoid"]=Guid.NewGuid(),
            ["gaia_codigo"]="SOLICITUD_RADICADA",
            ["gaia_asuntoplantilla"]="Solicitud {{nombre}}",
            ["gaia_cuerpohtml"]="<p>Hola {{nombre}}</p>",
            ["gaia_perfilenvio"]="GAIA_INSTITUCIONAL",
            ["gaia_permiteadjuntos"]=permitsAttachments,
            ["gaia_requiereadjunto"]=false,
            ["gaia_activada"]=true
        };
        return JsonSerializer.Serialize(new Dictionary<string,object?>{["value"]=new[]{template}});
    }

    private static string TemplateMetadata()=>Metadata("gaia_plantillacorreo","gaia_plantillacorreos","gaia_plantillacorreoid","gaia_nombreplantilla",
        [("gaia_Codigo","gaia_codigo","String"),("gaia_AsuntoPlantilla","gaia_asuntoplantilla","String"),("gaia_CuerpoHtml","gaia_cuerpohtml","Memo"),
         ("gaia_PerfilEnvio","gaia_perfilenvio","String"),("gaia_PermiteAdjuntos","gaia_permiteadjuntos","Boolean"),("gaia_RequiereAdjunto","gaia_requiereadjunto","Boolean"),
         ("gaia_CopiaPredeterminada","gaia_copiapredeterminada","String"),("gaia_CopiaOcultaPredeterminada","gaia_copiaocultapredeterminada","String"),
         ("gaia_Activa","gaia_activada","Boolean"),("gaia_InicioVigencia","gaia_iniciovigencia","DateTime"),("gaia_FinVigencia","gaia_finvigencia","DateTime")],[]);

    private static string EmailMetadata()=>Metadata("gaia_correosaliente","gaia_correosalientes","gaia_correosalienteid","gaia_numerocorreo",
        [("gaia_IdOperacion","gaia_idoperacion","String"),("gaia_IdCorrelacion","gaia_idcorrelacion","String"),("gaia_ModuloOrigen","gaia_moduloorigen","String"),
         ("gaia_EventoOrigen","gaia_eventoorigen","String"),("gaia_RegistroOrigenId","gaia_registroorigenid","String"),("gaia_NumeroReferencia","gaia_numeroreferencia","String"),
         ("gaia_DescripcionOrigen","gaia_descripcionorigen","String"),("gaia_DestinatariosJson","gaia_destinatariosjson","Memo"),("gaia_CopiasJson","gaia_copiasjson","Memo"),
         ("gaia_CopiasOcultasJson","gaia_copiasocultasjson","Memo"),("gaia_AsuntoDefinitivo","gaia_asuntodefinitivo","String"),("gaia_CuerpoHtmlDefinitivo","gaia_cuerpohtmldefinitivo","Memo"),
         ("gaia_DatosCombinacionJson","gaia_datoscombinacionjson","Memo"),("gaia_PerfilEnvio","gaia_perfilenvio","String"),("gaia_TieneAdjuntos","gaia_tieneadjuntos","Boolean"),
         ("gaia_EstadoEnvio","gaia_estadoenvio","Picklist"),("gaia_Prioridad","gaia_prioridad","Picklist"),("gaia_DisponibleDesde","gaia_disponibledesde","DateTime"),
         ("gaia_IntentosRealizados","gaia_intentosrealizados","Integer"),("gaia_MaximoIntentos","gaia_maximointentos","Integer"),("gaia_PlantillaCorreo","gaia_plantillacorreo","Lookup"),
         ("gaia_CodigoError","gaia_codigoerror","String"),("gaia_DetalleError","gaia_detalleerror","Memo")],
        [("gaia_plantillacorreo","nav_plantilla","gaia_plantillacorreo")]);

    private static string AttachmentMetadata()=>Metadata("gaia_adjuntocorreo","gaia_adjuntocorreos","gaia_adjuntocorreoid","gaia_nombredelarchivo",
        [("gaia_Orden","gaia_orden","Integer"),("gaia_ModuloOrigen","gaia_moduloorigen","String"),("gaia_RegistroOrigenId","gaia_registroorigenid","String"),
         ("gaia_Proveedor","gaia_proveedor","Picklist"),("gaia_SiteId","gaia_siteid","String"),("gaia_DriveId","gaia_driveid","String"),
         ("gaia_DriveItemId","gaia_driveitemid","String"),("gaia_ETag","gaia_etag","String"),("gaia_TipoMime","gaia_tipomime","String"),
         ("gaia_TamanoBytes","gaia_tamanobytes","BigInt"),("gaia_HashSha256","gaia_hashsha256","String"),("gaia_Obligatorio","gaia_obligatorio","Boolean"),
         ("gaia_ModoEntrega","gaia_modoentrega","Picklist"),("gaia_EstadoAdjunto","gaia_estadoadjunto","Picklist"),("gaia_CorreoSaliente","gaia_correosaliente","Lookup")],
        [("gaia_correosaliente","nav_correo","gaia_correosaliente")]);

    private static string Metadata(string logical,string set,string id,string name,
        (string Schema,string Logical,string Type)[] attributes,(string Attribute,string Navigation,string Referenced)[] relationships)
    {
        var attributeJson=string.Join(',',attributes.Select(x=>$"{{\"SchemaName\":\"{x.Schema}\",\"LogicalName\":\"{x.Logical}\",\"AttributeType\":\"{x.Type}\"}}"));
        var relationshipJson=string.Join(',',relationships.Select(x=>$"{{\"ReferencingAttribute\":\"{x.Attribute}\",\"ReferencingEntityNavigationPropertyName\":\"{x.Navigation}\",\"ReferencedEntity\":\"{x.Referenced}\"}}"));
        return $"{{\"LogicalName\":\"{logical}\",\"EntitySetName\":\"{set}\",\"PrimaryIdAttribute\":\"{id}\",\"PrimaryNameAttribute\":\"{name}\",\"Attributes\":[{attributeJson}],\"ManyToOneRelationships\":[{relationshipJson}]}}";
    }

    private static HttpResponseMessage Json(string value)=>new(HttpStatusCode.OK){Content=new StringContent(value,Encoding.UTF8,"application/json")};
    private static HttpResponseMessage Empty()=>Json("{\"value\":[]}");
    private static HttpResponseMessage NoContent()=>new(HttpStatusCode.NoContent);
    private static HttpResponseMessage Created(Guid id){var response=NoContent();response.Headers.TryAddWithoutValidation("OData-EntityId",$"https://example/gaia_correosalientes({id:D})");return response;}
    private sealed class Factory(QueueHandler handler):IDataverseDelegatedClientFactory
    {public Task<HttpClient>CreateAsync()=>Task.FromResult(new HttpClient(handler){BaseAddress=new Uri("https://example/api/data/v9.2/")});}
    private sealed class QueueHandler(params HttpResponseMessage[] responses):HttpMessageHandler
    {
        readonly Queue<HttpResponseMessage> queue=new(responses);
        public List<string>Bodies{get;}=[];public List<HttpMethod>Methods{get;}=[];
        protected override async Task<HttpResponseMessage>SendAsync(HttpRequestMessage request,CancellationToken token)
        {Methods.Add(request.Method);if(request.Content is not null)Bodies.Add(await request.Content.ReadAsStringAsync(token));return queue.Dequeue();}
    }
}
