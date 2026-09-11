using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

internal static class FileTransferStreams
{
    // Temporary, delete-on-close spool for non-seekable input, scanning and range retries.
    // Never a permanent storage provider; a bounded copy validates the actual length before remote writes.
    public static async Task<FileStream> PrepareAsync(Stream source, long expectedLength, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new FileStorageException(FileStorageError.InvalidLength);
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite, Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose | FileOptions.SequentialScan,
            BufferSize = 64 * 1024
        };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        var stream = new FileStream(Path.Combine(Path.GetTempPath(), $"gaia-transfer-{Guid.NewGuid():N}.tmp"), options);
        try
        {
            var buffer = new byte[64 * 1024];
            long count = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;
                if (read > expectedLength - count) throw new FileStorageException(FileStorageError.InvalidLength);
                await stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                count += read;
            }
            if (count != expectedLength) throw new FileStorageException(FileStorageError.InvalidLength);
            stream.Position = 0;
            return stream;
        }
        catch { await stream.DisposeAsync(); throw; }
    }
}

internal sealed class BorrowedStream(Stream source) : Stream
{
    public override bool CanRead => source.CanRead;
    public override bool CanSeek => source.CanSeek;
    public override bool CanWrite => false;
    public override long Length => source.Length;
    public override long Position { get => source.Position; set => source.Position = value; }
    public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => source.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => source.Seek(offset, origin);
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class ResponseOwnedStream(Stream source, HttpResponseMessage response) : Stream
{
    public override bool CanRead => source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => source.ReadAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing) { source.Dispose(); response.Dispose(); }
        base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        await source.DisposeAsync(); response.Dispose(); await base.DisposeAsync(); GC.SuppressFinalize(this);
    }
}
