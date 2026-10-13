using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gaia.BuildingBlocks.Notifications;

namespace Gaia.Api.Infrastructure.Dataverse.Notifications;

internal sealed class DataverseEmailNotificationQueue(IDataverseDelegatedClientFactory clients) : IEmailNotificationOutbox
{
    private const string DefaultProfile = "GAIA_INSTITUCIONAL";

    public async Task<QueuedEmailNotification> QueueAsync(QueueEmailNotification value, CancellationToken token)
    {
        var notification = EmailNotificationRules.Normalize(value);
        var client = await clients.CreateAsync();
        var templateTable = await DataverseMetadataResolver.TableAsync(client, "gaia_plantillacorreo", token);
        var emailTable = await DataverseMetadataResolver.TableAsync(client, "gaia_correosaliente", token);
        var attachmentTable = await DataverseMetadataResolver.TableAsync(client, "gaia_adjuntocorreo", token);

        var operation = emailTable.Attribute("gaia_IdOperacion");
        var existing = await DataverseJson.ReadAllAsync(client,
            $"{emailTable.EntitySetName}?$select={emailTable.PrimaryIdAttribute},{operation}&$filter={operation} eq '{Escape(notification.OperationId)}'&$top=2", token);
        if (existing.Count > 1) throw new InvalidOperationException("Dataverse devolvió más de un correo para la misma operación.");
        if (existing.Count == 1)
            return new(RequiredGuid(existing[0], emailTable.PrimaryIdAttribute), notification.OperationId, true);

        var template = await ReadTemplate(client, templateTable, notification.TemplateCode, token);
        if (!template.PermitsAttachments && notification.Attachments!.Count > 0)
            throw new InvalidOperationException("La plantilla seleccionada no permite adjuntos.");
        if (template.RequiresAttachment && notification.Attachments!.Count == 0)
            throw new InvalidOperationException("La plantilla seleccionada requiere al menos un adjunto.");

        var subject = EmailNotificationRules.RenderSubject(template.Subject, notification.Variables);
        var html = EmailNotificationRules.RenderHtml(template.Html, notification.Variables);
        var recipients = EmailNotificationRules.NormalizeRecipientList(notification.To, true);
        var copies = EmailNotificationRules.NormalizeRecipientList(Merge(ParseDefaultRecipients(template.DefaultCc), notification.Cc ?? []));
        var blindCopies = EmailNotificationRules.NormalizeRecipientList(Merge(ParseDefaultRecipients(template.DefaultBcc), notification.Bcc ?? []));
        var templateLookup = emailTable.Relationship("gaia_PlantillaCorreo", templateTable.LogicalName);
        var payload = new Dictionary<string, object?>
        {
            [emailTable.Attribute("gaia_IdOperacion")] = notification.OperationId,
            [emailTable.Attribute("gaia_IdCorrelacion")] = notification.CorrelationId,
            [emailTable.Attribute("gaia_ModuloOrigen")] = notification.Module,
            [emailTable.Attribute("gaia_EventoOrigen")] = notification.Event,
            [emailTable.Attribute("gaia_RegistroOrigenId")] = notification.SourceRecordId,
            [emailTable.Attribute("gaia_NumeroReferencia")] = notification.ReferenceNumber,
            [emailTable.Attribute("gaia_DescripcionOrigen")] = notification.SourceDescription,
            [emailTable.Attribute("gaia_DestinatariosJson")] = EmailNotificationRules.RecipientsJson(recipients),
            [emailTable.Attribute("gaia_CopiasJson")] = copies.Length == 0 ? null : EmailNotificationRules.RecipientsJson(copies),
            [emailTable.Attribute("gaia_CopiasOcultasJson")] = blindCopies.Length == 0 ? null : EmailNotificationRules.RecipientsJson(blindCopies),
            [emailTable.Attribute("gaia_AsuntoDefinitivo")] = subject,
            [emailTable.Attribute("gaia_CuerpoHtmlDefinitivo")] = html,
            [emailTable.Attribute("gaia_DatosCombinacionJson")] = EmailNotificationRules.VariablesJson(notification.Variables),
            [emailTable.Attribute("gaia_PerfilEnvio")] = string.IsNullOrWhiteSpace(template.Profile) ? DefaultProfile : template.Profile,
            [emailTable.Attribute("gaia_TieneAdjuntos")] = notification.Attachments!.Count > 0,
            [emailTable.Attribute("gaia_EstadoEnvio")] = emailTable.EncodedIntegerValue("gaia_EstadoEnvio", EmailDeliveryValues.Draft),
            [emailTable.Attribute("gaia_Prioridad")] = emailTable.EncodedIntegerValue("gaia_Prioridad", notification.Priority),
            [emailTable.Attribute("gaia_DisponibleDesde")] = notification.AvailableFrom,
            [emailTable.Attribute("gaia_IntentosRealizados")] = emailTable.EncodedIntegerValue("gaia_IntentosRealizados", 0),
            [emailTable.Attribute("gaia_MaximoIntentos")] = emailTable.EncodedIntegerValue("gaia_MaximoIntentos", 3),
            [templateLookup.NavigationProperty + "@odata.bind"] = $"/{templateTable.EntitySetName}({template.Id:D})",
            ["statecode"] = 0
        };

        Guid emailId;
        using (var response = await client.PostAsJsonAsync(emailTable.EntitySetName, payload, token))
        {
            await Ensure(response, "crear el correo en borrador", token);
            emailId = CreatedId(response, "correo saliente");
        }

        try
        {
            var order = 1;
            foreach (var attachment in notification.Attachments)
                await CreateAttachment(client, attachmentTable, emailTable, emailId, attachment, order++, token);

            await Patch(client, emailTable.EntitySetName, emailId, new()
            {
                [emailTable.Attribute("gaia_EstadoEnvio")] = emailTable.EncodedIntegerValue("gaia_EstadoEnvio", EmailDeliveryValues.Pending)
            }, "liberar el correo para envío", token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await TryMarkFailed(client, emailTable, emailId, exception);
            throw;
        }

        return new(emailId, notification.OperationId, false);
    }

    private static async Task<Template> ReadTemplate(HttpClient client, DataverseTableMetadata table, string code, CancellationToken token)
    {
        var fields = new TemplateFields(table);
        var rows = await DataverseJson.ReadAllAsync(client,
            $"{table.EntitySetName}?$select={fields.Select}&$filter=statecode eq 0 and {fields.Code} eq '{Escape(code)}' and {fields.Active} eq true", token);
        var now = DateTimeOffset.UtcNow;
        var active = rows.Where(row =>
        {
            var starts = DateTimeValue(row, fields.Starts);
            var ends = DateTimeValue(row, fields.Ends);
            return (!starts.HasValue || starts <= now) && (!ends.HasValue || ends >= now);
        }).ToArray();
        if (active.Length == 0) throw new InvalidOperationException($"No existe una plantilla activa y vigente con código {code}.");
        if (active.Length > 1) throw new InvalidOperationException($"Existe más de una plantilla activa y vigente con código {code}.");
        var row = active[0];
        return new(RequiredGuid(row, table.PrimaryIdAttribute), RequiredText(row, fields.Subject), RequiredText(row, fields.Html),
            Text(row, fields.Profile) ?? DefaultProfile, Bool(row, fields.PermitsAttachments), Bool(row, fields.RequiresAttachment),
            Text(row, fields.DefaultCc), Text(row, fields.DefaultBcc));
    }

    private static async Task CreateAttachment(HttpClient client, DataverseTableMetadata table, DataverseTableMetadata emailTable,
        Guid emailId, EmailAttachmentReference attachment, int order, CancellationToken token)
    {
        var emailLookup = table.Relationship("gaia_CorreoSaliente", emailTable.LogicalName);
        var payload = new Dictionary<string, object?>
        {
            [table.PrimaryNameAttribute] = attachment.FileName,
            [table.Attribute("gaia_Orden")] = table.EncodedIntegerValue("gaia_Orden", order),
            [table.Attribute("gaia_ModuloOrigen")] = attachment.Module,
            [table.Attribute("gaia_RegistroOrigenId")] = attachment.SourceRecordId,
            [table.Attribute("gaia_Proveedor")] = table.EncodedIntegerValue("gaia_Proveedor", EmailDeliveryValues.SharePointProvider),
            [table.Attribute("gaia_SiteId")] = attachment.SiteId,
            [table.Attribute("gaia_DriveId")] = attachment.DriveId,
            [table.Attribute("gaia_DriveItemId")] = attachment.DriveItemId,
            [table.Attribute("gaia_ETag")] = attachment.ETag,
            [table.Attribute("gaia_TipoMime")] = attachment.ContentType,
            [table.Attribute("gaia_TamanoBytes")] = attachment.Length,
            [table.Attribute("gaia_HashSha256")] = attachment.Sha256,
            [table.Attribute("gaia_Obligatorio")] = attachment.Required,
            [table.Attribute("gaia_ModoEntrega")] = table.EncodedIntegerValue("gaia_ModoEntrega", attachment.DeliveryMode),
            [table.Attribute("gaia_EstadoAdjunto")] = table.EncodedIntegerValue("gaia_EstadoAdjunto", EmailDeliveryValues.AttachmentAvailable),
            [emailLookup.NavigationProperty + "@odata.bind"] = $"/{emailTable.EntitySetName}({emailId:D})",
            ["statecode"] = 0
        };
        using var response = await client.PostAsJsonAsync(table.EntitySetName, payload, token);
        await Ensure(response, "registrar un adjunto del correo", token);
    }

    private static EmailRecipient[] ParseDefaultRecipients(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(address => new EmailRecipient(address)).ToArray();

    private static EmailRecipient[] Merge(IReadOnlyList<EmailRecipient> left, IReadOnlyList<EmailRecipient> right) =>
        left.Concat(right).DistinctBy(item => item.Email, StringComparer.OrdinalIgnoreCase).ToArray();

    private static async Task Patch(HttpClient client, string entitySet, Guid id, Dictionary<string, object?> payload,
        string operation, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"{entitySet}({id:D})") { Content = JsonContent.Create(payload) };
        request.Headers.TryAddWithoutValidation("If-Match", "*");
        using var response = await client.SendAsync(request, token);
        await Ensure(response, operation, token);
    }

    private static async Task TryMarkFailed(HttpClient client, DataverseTableMetadata table, Guid id, Exception exception)
    {
        try
        {
            await Patch(client, table.EntitySetName, id, new()
            {
                [table.Attribute("gaia_EstadoEnvio")] = table.EncodedIntegerValue("gaia_EstadoEnvio", EmailDeliveryValues.PermanentError),
                [table.Attribute("gaia_CodigoError")] = "OUTBOX_PREPARATION_FAILED",
                [table.Attribute("gaia_DetalleError")] = SafeError(exception.Message)
            }, "registrar el error del correo", CancellationToken.None);
        }
        catch { /* El error original conserva la prioridad; no se exponen respuestas de proveedor. */ }
    }

    private static async Task Ensure(HttpResponseMessage response, string operation, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(token);
        throw new InvalidOperationException($"Dataverse rechazó {operation} ({(int)response.StatusCode}): {Error(body)}");
    }

    private static string Error(string body)
    {
        try { using var json = JsonDocument.Parse(body); return SafeError(json.RootElement.GetProperty("error").GetProperty("message").GetString()); }
        catch (JsonException) { return "Respuesta no válida de Dataverse."; }
    }

    private static string SafeError(string? value)
    {
        var sanitized = Regex.Replace(value ?? "Error sin descripción.", "https?://\\S+", "[dirección omitida]", RegexOptions.IgnoreCase);
        return sanitized[..Math.Min(sanitized.Length, 2000)];
    }

    private static Guid CreatedId(HttpResponseMessage response, string resource)
    {
        var uri = response.Headers.TryGetValues("OData-EntityId", out var values) ? values.SingleOrDefault() : null;
        var match = Regex.Match(uri ?? string.Empty, @"\(([0-9a-f-]{36})\)$", RegexOptions.IgnoreCase);
        return match.Success ? Guid.Parse(match.Groups[1].Value) : throw new InvalidOperationException($"Dataverse no devolvió el identificador del {resource}.");
    }

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static string? Text(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string RequiredText(JsonElement row, string name) => Text(row, name) ?? throw new InvalidOperationException($"La plantilla no contiene {name}.");
    private static Guid RequiredGuid(JsonElement row, string name) => Guid.TryParse(Text(row, name), out var id) ? id : throw new InvalidOperationException($"Dataverse no devolvió {name}.");
    private static bool Bool(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static DateTimeOffset? DateTimeValue(JsonElement row, string name) => DateTimeOffset.TryParse(Text(row, name), out var value) ? value : null;

    private sealed record Template(Guid Id, string Subject, string Html, string Profile, bool PermitsAttachments,
        bool RequiresAttachment, string? DefaultCc, string? DefaultBcc);

    private sealed record TemplateFields(string Code, string Subject, string Html, string Profile, string PermitsAttachments,
        string RequiresAttachment, string DefaultCc, string DefaultBcc, string Active, string Starts, string Ends, string Select)
    {
        public TemplateFields(DataverseTableMetadata table) : this(
            table.Attribute("gaia_Codigo"), table.Attribute("gaia_AsuntoPlantilla"), table.Attribute("gaia_CuerpoHtml"),
            table.Attribute("gaia_PerfilEnvio"), table.Attribute("gaia_PermiteAdjuntos"), table.Attribute("gaia_RequiereAdjunto"),
            table.Attribute("gaia_CopiaPredeterminada"), table.Attribute("gaia_CopiaOcultaPredeterminada"),
            table.Attribute("gaia_Activa"), table.Attribute("gaia_InicioVigencia"), table.Attribute("gaia_FinVigencia"), string.Empty)
        {
            Select = string.Join(',', table.PrimaryIdAttribute, Code, Subject, Html, Profile, PermitsAttachments,
                RequiresAttachment, DefaultCc, DefaultBcc, Active, Starts, Ends);
        }
    }
}
