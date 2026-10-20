namespace Gaia.Modules.Solicitudes;

public static class SolicitudesFilePolicy
{
    public const string DefaultAccept = ".pdf,.png,.jpg,.jpeg,.docx,.xlsx,.pptx";

    private static readonly Dictionary<string, IReadOnlySet<string>> Allowed =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf" },
            [".png"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/png" },
            [".jpg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg" },
            [".jpeg"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "image/jpeg" },
            [".docx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
            [".xlsx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
            [".pptx"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/vnd.openxmlformats-officedocument.presentationml.presentation" }
        };

    public static void Validate(string originalName, string contentType)
    {
        var extension = Path.GetExtension(originalName);
        if (string.IsNullOrWhiteSpace(originalName) || string.IsNullOrWhiteSpace(contentType)
            || !Allowed.TryGetValue(extension, out var mimeTypes) || !mimeTypes.Contains(contentType))
            throw new ArgumentException("El archivo debe ser PDF, PNG, JPG, Word, Excel o PowerPoint.");
    }

    public static string NormalizeAccept(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return DefaultAccept;
        var extensions = configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (extensions.Length == 0 || extensions.Any(value => !Allowed.ContainsKey(value)))
            throw new ArgumentException("Los tipos de archivo permitidos no son válidos.");
        return string.Join(',', extensions);
    }
}
