using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gaia.BuildingBlocks.Notifications;

public static class EmailDeliveryValues
{
    public const int Draft = 299540000;
    public const int Pending = 299540001;
    public const int Processing = 299540002;
    public const int Sent = 299540003;
    public const int RetryableError = 299540004;
    public const int PermanentError = 299540005;
    public const int Cancelled = 299540006;

    public const int LowPriority = 299540000;
    public const int NormalPriority = 299540001;
    public const int HighPriority = 299540002;

    public const int SharePointProvider = 299540000;
    public const int AttachmentDelivery = 299540000;
    public const int LinkDelivery = 299540001;
    public const int AttachmentAvailable = 299540001;
}

public sealed record EmailRecipient(string Email, string? Name = null);

public sealed record EmailAttachmentReference(
    string FileName,
    string Module,
    string? SourceRecordId,
    string SiteId,
    string DriveId,
    string DriveItemId,
    string ContentType,
    long Length,
    string? ETag = null,
    string? Sha256 = null,
    bool Required = true,
    int DeliveryMode = EmailDeliveryValues.AttachmentDelivery);

public sealed record QueueEmailNotification(
    string OperationId,
    string? CorrelationId,
    string Module,
    string Event,
    string SourceRecordId,
    string TemplateCode,
    IReadOnlyList<EmailRecipient> To,
    IReadOnlyDictionary<string, string?> Variables,
    string? ReferenceNumber = null,
    string? SourceDescription = null,
    IReadOnlyList<EmailRecipient>? Cc = null,
    IReadOnlyList<EmailRecipient>? Bcc = null,
    IReadOnlyList<EmailAttachmentReference>? Attachments = null,
    int Priority = EmailDeliveryValues.NormalPriority,
    DateTimeOffset? AvailableFrom = null);

public sealed record QueuedEmailNotification(Guid Id, string OperationId, bool AlreadyExisted);

/// <summary>
/// Transactional notification outbox. Implementations create the row as Draft, add every
/// attachment reference and only then release it as Pending for Power Automate.
/// </summary>
public interface IEmailNotificationOutbox
{
    Task<QueuedEmailNotification> QueueAsync(QueueEmailNotification notification, CancellationToken cancellationToken);
}

public static partial class EmailNotificationRules
{
    private static readonly JsonSerializerOptions RecipientJsonOptions = new(JsonSerializerDefaults.Web);

    public static QueueEmailNotification Normalize(QueueEmailNotification value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var operationId = Required(value.OperationId, 100, "El identificador de operación");
        var module = Code(value.Module, 100, "El módulo");
        var eventCode = Code(value.Event, 150, "El evento");
        var sourceId = Required(value.SourceRecordId, 100, "El registro de origen");
        var template = Code(value.TemplateCode, 100, "La plantilla");
        var recipients = NormalizeRecipients(value.To, true);
        var attachments = (value.Attachments ?? []).Select(NormalizeAttachment).ToArray();
        if (value.Priority is < EmailDeliveryValues.LowPriority or > EmailDeliveryValues.HighPriority)
            throw new ArgumentException("La prioridad del correo no es válida.");

        var variables = value.Variables
            .ToDictionary(pair => Code(pair.Key, 100, "La variable"), pair => pair.Value?.Trim(), StringComparer.OrdinalIgnoreCase);
        return value with
        {
            OperationId = operationId,
            CorrelationId = Optional(value.CorrelationId, 100),
            Module = module,
            Event = eventCode,
            SourceRecordId = sourceId,
            TemplateCode = template,
            ReferenceNumber = Optional(value.ReferenceNumber, 100),
            SourceDescription = Optional(value.SourceDescription, 500),
            To = recipients,
            Cc = NormalizeRecipients(value.Cc ?? [], false),
            Bcc = NormalizeRecipients(value.Bcc ?? [], false),
            Variables = variables,
            Attachments = attachments
        };
    }

    public static string RenderSubject(string template, IReadOnlyDictionary<string, string?> variables) =>
        Render(template, variables, htmlEncode: false, maximumLength: 500);

    public static string RenderHtml(string template, IReadOnlyDictionary<string, string?> variables) =>
        Render(template, variables, htmlEncode: true, maximumLength: 1_048_576);

    public static string RecipientsJson(IReadOnlyList<EmailRecipient> recipients) =>
        JsonSerializer.Serialize(recipients.Select(item => new { email = item.Email, nombre = item.Name }), RecipientJsonOptions);

    public static string VariablesJson(IReadOnlyDictionary<string, string?> variables) =>
        JsonSerializer.Serialize(variables, RecipientJsonOptions);

    public static EmailRecipient[] NormalizeRecipientList(IReadOnlyList<EmailRecipient> recipients, bool required = false) =>
        NormalizeRecipients(recipients, required);

    private static string Render(string template, IReadOnlyDictionary<string, string?> variables, bool htmlEncode, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(template)) throw new InvalidOperationException("La plantilla de correo no tiene contenido.");
        var rendered = TokenPattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            if (!variables.TryGetValue(key, out var value))
                throw new InvalidOperationException($"La plantilla requiere la variable {key}.");
            return htmlEncode ? WebUtility.HtmlEncode(value ?? string.Empty) : value ?? string.Empty;
        });
        var unresolved = TokenPattern().Match(rendered);
        if (unresolved.Success) throw new InvalidOperationException($"No fue posible resolver {unresolved.Value}.");
        if (rendered.Length > maximumLength) throw new InvalidOperationException("El contenido renderizado supera la longitud permitida.");
        return rendered;
    }

    private static EmailRecipient[] NormalizeRecipients(IReadOnlyList<EmailRecipient> values, bool required)
    {
        var result = values.Select(item =>
        {
            var email = item.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email) || email.Length > 320 || !EmailPattern().IsMatch(email))
                throw new ArgumentException("El correo contiene un destinatario inválido.");
            return new EmailRecipient(email, Optional(item.Name, 200));
        }).DistinctBy(item => item.Email, StringComparer.OrdinalIgnoreCase).ToArray();
        if (required && result.Length == 0) throw new ArgumentException("El correo requiere al menos un destinatario.");
        return result;
    }

    private static EmailAttachmentReference NormalizeAttachment(EmailAttachmentReference item)
    {
        if (item.Length < 0) throw new ArgumentException("El tamaño del adjunto no es válido.");
        if (item.DeliveryMode is not (EmailDeliveryValues.AttachmentDelivery or EmailDeliveryValues.LinkDelivery))
            throw new ArgumentException("El modo de entrega del adjunto no es válido.");
        return item with
        {
            FileName = Required(item.FileName, 255, "El nombre del archivo"),
            Module = Code(item.Module, 100, "El módulo del adjunto"),
            SourceRecordId = Optional(item.SourceRecordId, 100),
            SiteId = Required(item.SiteId, 500, "El sitio de SharePoint"),
            DriveId = Required(item.DriveId, 500, "La biblioteca de SharePoint"),
            DriveItemId = Required(item.DriveItemId, 500, "El archivo de SharePoint"),
            ContentType = Required(item.ContentType, 150, "El tipo MIME"),
            ETag = Optional(item.ETag, 500),
            Sha256 = Optional(item.Sha256, 64)
        };
    }

    private static string Required(string? value, int maximumLength, string label) =>
        Optional(value, maximumLength) ?? throw new ArgumentException($"{label} es obligatorio.");

    private static string Code(string? value, int maximumLength, string label)
    {
        var normalized = Required(value, maximumLength, label).ToUpperInvariant();
        if (!CodePattern().IsMatch(normalized)) throw new ArgumentException($"{label} contiene caracteres no válidos.");
        return normalized;
    }

    private static string? Optional(string? value, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > maximumLength) throw new ArgumentException($"El valor supera {maximumLength} caracteres.");
        return normalized;
    }

    [GeneratedRegex("^[A-Z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
    [GeneratedRegex("\\{\\{\\s*([A-Za-z0-9_-]+)\\s*\\}\\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
