namespace Gaia.Modules.ThirdParties;

public sealed record ProfilePhotoContent(byte[] Bytes,string ContentType);
public sealed record ProfilePhotoResult(ProfilePhotoContent? Content,bool Unavailable=false,TimeSpan? RetryAfter=null)
{
    public static ProfilePhotoResult Missing { get; } = new ProfilePhotoResult(null, false, null);
    public static ProfilePhotoResult ServiceUnavailable(TimeSpan? retryAfter=null) => new ProfilePhotoResult(null, true, retryAfter);
}

public interface IProfilePhotoReader
{
    Task<ProfilePhotoResult> ReadCurrentUserAsync(string entraObjectId,int size,CancellationToken token);
    Task<ProfilePhotoResult> ReadPersonAsync(Guid personId,int size,CancellationToken token);
}

public static class ProfilePhotoSizes
{
    private static readonly HashSet<int> Allowed=[48,64,96,120,240];
    public static bool IsAllowed(int size) => Allowed.Contains(size);
}
