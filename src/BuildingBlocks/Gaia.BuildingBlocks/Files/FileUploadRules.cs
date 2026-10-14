namespace Gaia.BuildingBlocks.Files;

public static class FileUploadRules
{
    private const string Forbidden = "\\/:*?\"<>|#%";

    public static string ValidateAndCreateStoredName(FileUpload upload, long maximumBytes,
        IReadOnlySet<string> extensions, IReadOnlySet<string> contentTypes)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(extensions);
        ArgumentNullException.ThrowIfNull(contentTypes);
        ValidateScope(upload.Scope);
        if (upload.FileId == Guid.Empty) throw new FileStorageException(FileStorageError.InvalidLogicalPath);
        if (upload.UploadedAt == default) throw new FileStorageException(FileStorageError.InvalidLogicalPath);
        ValidateSegment(upload.OriginalName, FileStorageError.InvalidFileName);
        if (upload.Length <= 0) throw new FileStorageException(FileStorageError.InvalidLength);
        if (upload.Length > maximumBytes) throw new FileStorageException(FileStorageError.FileTooLarge);
        var extension = Path.GetExtension(upload.OriginalName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(upload.ContentType)
            || !extensions.Contains(extension) || !contentTypes.Contains(upload.ContentType.ToLowerInvariant()))
            throw new FileStorageException(FileStorageError.TypeNotAllowed);
        return $"{upload.FileId:N}{extension}";
    }

    public static void ValidateContentSignature(FileUpload upload, Stream content)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead || !content.CanSeek) throw new FileStorageException(FileStorageError.TypeNotAllowed);
        var original = content.Position;
        Span<byte> header = stackalloc byte[12];
        var read = content.Read(header);
        content.Position = original;
        var extension = Path.GetExtension(upload.OriginalName).ToLowerInvariant();
        var valid = extension switch
        {
            ".pdf" => StartsWith(header[..read], "%PDF-"u8),
            ".png" => StartsWith(header[..read], [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
            ".jpg" or ".jpeg" => StartsWith(header[..read], [0xff, 0xd8, 0xff]),
            ".docx" or ".xlsx" or ".pptx" => StartsWith(header[..read], [0x50, 0x4b, 0x03, 0x04]),
            ".mp4" or ".mov" => read >= 8 && header[4..8].SequenceEqual("ftyp"u8),
            ".webm" => StartsWith(header[..read], [0x1a, 0x45, 0xdf, 0xa3]),
            _ => false
        };
        if (!valid) throw new FileStorageException(FileStorageError.TypeNotAllowed);
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> signature) =>
        value.Length >= signature.Length && value[..signature.Length].SequenceEqual(signature);

    public static void ValidateScope(LogicalFileScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ValidateSegment(scope.Scope, FileStorageError.InvalidLogicalPath);
        if (scope.CorrelationId is not null) ValidateSegment(scope.CorrelationId, FileStorageError.InvalidLogicalPath);
    }

    public static string NormalizeRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new FileStorageException(FileStorageError.InvalidLogicalPath);
        var segments = root.Split('/');
        foreach (var segment in segments) ValidateSegment(segment, FileStorageError.InvalidLogicalPath);
        return string.Join('/', segments);
    }

    private static void ValidateSegment(string value, FileStorageError error)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 180 || value is "." or ".."
            || !value.Equals(value.Trim(), StringComparison.Ordinal) || value.EndsWith('.')
            || value.Any(character => char.IsControl(character) || Forbidden.Contains(character)))
            throw new FileStorageException(error);
    }
}
