using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed class DataverseHelpdeskAttachmentStore(IDataverseDelegatedClientFactory clients) : IHelpdeskAttachmentStore
{
    private const string Table = "gaia_adjuntosolicitud";

    public async Task<HelpdeskAttachment> CreateAsync(PersistHelpdeskAttachment attachment, CancellationToken token)
    {
        HelpdeskAttachmentRules.Validate(attachment);
        var client = await clients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token);
        var request = await DataverseMetadataResolver.TableAsync(client, "gaia_solicitud", token);
        var thirdParty = await DataverseMetadataResolver.TableAsync(client, "gaia_terceros", token);
        if(attachment.ManagementId.HasValue)
        {
            var management=await DataverseMetadataResolver.TableAsync(client,"gaia_gestionsolicitud",token);var managementRequest=management.Relationship("gaia_Solicitud","gaia_solicitud");var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{management.EntitySetName}({attachment.ManagementId:D})?$select=_{managementRequest.ReferencingAttribute}_value,statecode",token);
            if(row is null||DataverseJson.OptionalInt32(row.Value,"statecode")!=0||OptionalGuid(row.Value,$"_{managementRequest.ReferencingAttribute}_value")!=attachment.RequestId)throw new ArgumentException("La gestión del adjunto no pertenece a la solicitud.");
        }
        if(attachment.ManagementFieldResponseId.HasValue)
        {
            var answer=await DataverseMetadataResolver.TableAsync(client,"gaia_respuestacampogestion",token);var answerManagement=answer.Relationship("gaia_GestionSolicitud","gaia_gestionsolicitud");var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{answer.EntitySetName}({attachment.ManagementFieldResponseId:D})?$select=_{answerManagement.ReferencingAttribute}_value,statecode",token);
            if(row is null||DataverseJson.OptionalInt32(row.Value,"statecode")!=0||OptionalGuid(row.Value,$"_{answerManagement.ReferencingAttribute}_value")!=attachment.ManagementId)throw new ArgumentException("La respuesta del adjunto no pertenece a la gestión indicada.");
        }
        var payload = new Dictionary<string, object?>
        {
            [metadata.PrimaryNameAttribute] = attachment.File.OriginalName,
            [metadata.Attribute("gaia_NombreAlmacenado")] = attachment.File.StoredName,
            [metadata.Attribute("gaia_TipoMIME")] = attachment.File.ContentType,
            [metadata.Attribute("gaia_TamanoBytes")] = attachment.File.Length,
            [metadata.Attribute("gaia_HashSHA256")] = attachment.File.Sha256,
            [metadata.Attribute("gaia_ProveedorAlmacenamiento")] = metadata.EncodedIntegerValue("gaia_ProveedorAlmacenamiento", HelpdeskAttachmentRules.SharePointProvider),
            [metadata.Attribute("gaia_RepositorioExternoId")] = attachment.File.Id.RepositoryId,
            [metadata.Attribute("gaia_ContenedorExternoId")] = attachment.File.Id.ContainerId,
            [metadata.Attribute("gaia_ArchivoExternoId")] = attachment.File.Id.FileId,
            [metadata.Attribute("gaia_ETag")] = attachment.File.ETag,
            [metadata.Attribute("gaia_UrlWeb")] = attachment.File.WebUrl?.AbsoluteUri,
            [metadata.Attribute("gaia_RutaLogica")] = attachment.File.LogicalPath,
            [metadata.Attribute("gaia_Visibilidad")] = metadata.EncodedIntegerValue("gaia_Visibilidad", (int)attachment.Visibility),
            [metadata.Attribute("gaia_FechaCarga")] = attachment.File.UploadedAt,
            [metadata.Relationship("gaia_Solicitud", "gaia_solicitud").NavigationProperty + "@odata.bind"] = $"/{request.EntitySetName}({attachment.RequestId:D})",
            [metadata.Relationship("gaia_CargadoPor", "gaia_terceros").NavigationProperty + "@odata.bind"] = $"/{thirdParty.EntitySetName}({attachment.UploadedByThirdPartyId:D})",
            ["statecode"] = 0
        };
        await BindOptional(client, metadata, payload, "gaia_ComentarioSolicitud", "gaia_comentariosolicitud", attachment.CommentId, token);
        await BindOptional(client, metadata, payload, "gaia_RespuestaCampo", "gaia_respuestacampo", attachment.FieldResponseId, token);
        await BindOptional(client, metadata, payload, "gaia_GestionSolicitud", "gaia_gestionsolicitud", attachment.ManagementId, token);
        await BindOptional(client, metadata, payload, "gaia_RespuestaCampoGestion", "gaia_respuestacampogestion", attachment.ManagementFieldResponseId, token);

        using var message = new HttpRequestMessage(HttpMethod.Patch, $"{metadata.EntitySetName}({attachment.Id:D})")
        { Content = JsonContent.Create(payload) };
        message.Headers.TryAddWithoutValidation("If-None-Match", "*");
        using var response = await client.SendAsync(message, token);
        if (response.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
            throw new InvalidOperationException("El identificador del adjunto ya existe.");
        await EnsureSuccess(response, token);
        return await GetRequiredAsync(client, metadata, attachment.Id, token);
    }

    public async Task<HelpdeskAttachment?> GetAsync(Guid id, CancellationToken token)
    {
        var client = await clients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token);
        var row = await Read(client, metadata, $"{metadata.EntitySetName}({id:D})?$select={Select(metadata)}", token);
        return row is null ? null : Map(row.Value, metadata);
    }

    public async Task<IReadOnlyList<HelpdeskAttachment>> ListByRequestAsync(Guid requestId, CancellationToken token)
    {
        var client = await clients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token);
        var requestLookup = metadata.Attribute("gaia_Solicitud");
        var path = $"{metadata.EntitySetName}?$select={Select(metadata)}&$filter=_{requestLookup}_value eq {requestId:D} and statecode eq 0&$orderby={metadata.Attribute("gaia_FechaCarga")} asc";
        return (await DataverseJson.ReadAllAsync(client, path, token)).Select(row => Map(row, metadata)).ToArray();
    }

    public async Task DeactivateAsync(Guid id, CancellationToken token)
    {
        var client = await clients.CreateAsync();
        var metadata = await DataverseMetadataResolver.TableAsync(client, Table, token);
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"{metadata.EntitySetName}({id:D})")
        { Content = JsonContent.Create(new { statecode = 1 }) };
        message.Headers.TryAddWithoutValidation("If-Match", "*");
        using var response = await client.SendAsync(message, token);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new KeyNotFoundException("El adjunto no existe.");
        await EnsureSuccess(response, token);
    }

    private static async Task BindOptional(HttpClient client, DataverseTableMetadata metadata,
        Dictionary<string, object?> payload, string lookupSchema, string target, Guid? id, CancellationToken token)
    {
        if (!id.HasValue) return;
        var targetMetadata = await DataverseMetadataResolver.TableAsync(client, target, token);
        payload[metadata.Relationship(lookupSchema, target).NavigationProperty + "@odata.bind"] = $"/{targetMetadata.EntitySetName}({id:D})";
    }

    private static async Task<HelpdeskAttachment> GetRequiredAsync(HttpClient client, DataverseTableMetadata metadata, Guid id, CancellationToken token) =>
        Map((await Read(client, metadata, $"{metadata.EntitySetName}({id:D})?$select={Select(metadata)}", token))
            ?? throw new KeyNotFoundException("El adjunto no existe."), metadata);

    private static Task<JsonElement?> Read(HttpClient client, DataverseTableMetadata metadata, string path, CancellationToken token) =>
        DataverseMetadataResolver.ReadOneAsync(client, path, token);

    private static string Select(DataverseTableMetadata m) => string.Join(',', new[]
    {
        m.PrimaryIdAttribute, m.PrimaryNameAttribute, m.Attribute("gaia_NombreAlmacenado"), m.Attribute("gaia_TipoMIME"),
        m.Attribute("gaia_TamanoBytes"), m.Attribute("gaia_HashSHA256"), m.Attribute("gaia_ProveedorAlmacenamiento"),
        m.Attribute("gaia_RepositorioExternoId"), m.Attribute("gaia_ContenedorExternoId"), m.Attribute("gaia_ArchivoExternoId"),
        m.Attribute("gaia_ETag"), m.Attribute("gaia_UrlWeb"), m.Attribute("gaia_RutaLogica"), m.Attribute("gaia_Visibilidad"),
        m.Attribute("gaia_FechaCarga"), "_" + m.Attribute("gaia_Solicitud") + "_value",
        "_" + m.Attribute("gaia_ComentarioSolicitud") + "_value", "_" + m.Attribute("gaia_RespuestaCampo") + "_value",
        "_" + m.Attribute("gaia_CargadoPor") + "_value", "_" + m.Attribute("gaia_GestionSolicitud") + "_value",
        "_" + m.Attribute("gaia_RespuestaCampoGestion") + "_value", "statecode"
    });

    private static HelpdeskAttachment Map(JsonElement row, DataverseTableMetadata m)
    {
        var provider = DataverseJson.OptionalEncodedInt32(row, m.Attribute("gaia_ProveedorAlmacenamiento")) == HelpdeskAttachmentRules.SharePointProvider ? "SharePoint" : "Other";
        var visibility = (AttachmentVisibility)(DataverseJson.OptionalEncodedInt32(row, m.Attribute("gaia_Visibilidad")) ?? 0);
        var stored = new StoredFile(new(provider, Required(row, m.Attribute("gaia_RepositorioExternoId")),
            Required(row, m.Attribute("gaia_ContenedorExternoId")), Required(row, m.Attribute("gaia_ArchivoExternoId"))),
            Optional(row, m.Attribute("gaia_ETag")) ?? "", Optional(row, m.PrimaryNameAttribute),
            Required(row, m.Attribute("gaia_NombreAlmacenado")), Required(row, m.Attribute("gaia_TipoMIME")),
            row.GetProperty(m.Attribute("gaia_TamanoBytes")).GetInt64(), ParseUri(Optional(row, m.Attribute("gaia_UrlWeb"))),
            Optional(row, m.Attribute("gaia_RutaLogica")), Optional(row, m.Attribute("gaia_HashSHA256")),
            DateTimeOffset.TryParse(Optional(row, m.Attribute("gaia_FechaCarga")), out var uploaded) ? uploaded : null);
        return new(GuidValue(row, m.PrimaryIdAttribute), GuidValue(row, "_" + m.Attribute("gaia_Solicitud") + "_value"),
            OptionalGuid(row, "_" + m.Attribute("gaia_ComentarioSolicitud") + "_value"),
            OptionalGuid(row, "_" + m.Attribute("gaia_RespuestaCampo") + "_value"),
            GuidValue(row, "_" + m.Attribute("gaia_CargadoPor") + "_value"), visibility, stored,
            (DataverseJson.OptionalInt32(row, "statecode") ?? 0) == 0,
            OptionalGuid(row, "_" + m.Attribute("gaia_GestionSolicitud") + "_value"),
            OptionalGuid(row, "_" + m.Attribute("gaia_RespuestaCampoGestion") + "_value"));
    }

    private static string Required(JsonElement row, string name) => Optional(row, name)
        ?? throw new InvalidOperationException($"Dataverse no devolvió el campo obligatorio {name}.");
    private static string? Optional(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static Guid GuidValue(JsonElement row, string name) => OptionalGuid(row, name) ?? throw new InvalidOperationException($"Dataverse no devolvió el identificador {name}.");
    private static Guid? OptionalGuid(JsonElement row, string name) => Optional(row, name) is { } text && Guid.TryParse(text, out var value) ? value : null;
    private static Uri? ParseUri(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        _ = await response.Content.ReadAsStringAsync(token);
        throw new InvalidOperationException("Dataverse no pudo completar la operación de adjuntos.");
    }
}
