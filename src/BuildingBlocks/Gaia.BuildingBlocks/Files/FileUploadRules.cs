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
