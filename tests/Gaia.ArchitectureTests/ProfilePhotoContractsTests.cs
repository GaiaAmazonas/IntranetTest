using Gaia.Modules.ThirdParties;

namespace Gaia.ArchitectureTests;

public sealed class ProfilePhotoContractsTests
{
    [Theory]
    [InlineData(48)]
    [InlineData(64)]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(240)]
    public void AllowsOnlyTheConfiguredGraphSizes(int size) => Assert.True(ProfilePhotoSizes.IsAllowed(size));

    [Theory]
    [InlineData(0)]
    [InlineData(97)]
    [InlineData(512)]
    public void RejectsUnexpectedGraphSizes(int size) => Assert.False(ProfilePhotoSizes.IsAllowed(size));

    [Fact]
    public void MissingAndUnavailableAreDifferentSafeOutcomes()
    {
        Assert.Null(ProfilePhotoResult.Missing.Content);
        Assert.False(ProfilePhotoResult.Missing.Unavailable);
        Assert.True(ProfilePhotoResult.ServiceUnavailable().Unavailable);
    }

    [Fact]
    public void MissingPhotoUsesAnEmptySuccessfulResponseInsteadOfAConsoleError()
    {
        var source = Read("src", "Modules", "ThirdParties", "Gaia.Modules.ThirdParties", "ThirdPartiesEndpoints.cs");
        var missingBranch = source[(source.IndexOf("if (result.Content is null)", StringComparison.Ordinal))..];
        missingBranch = missingBranch[..missingBranch.IndexOf("context.Response.Headers.CacheControl = \"private, max-age=21600\"", StringComparison.Ordinal)];

        Assert.Contains("return Results.NoContent();", missingBranch);
        Assert.DoesNotContain("return Results.NotFound();", missingBranch);
    }

    private static string Read(params string[] path)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gaia.Platform.slnx"))) root = root.Parent;
        return File.ReadAllText(Path.Combine(new[] { root?.FullName ?? throw new DirectoryNotFoundException() }.Concat(path).ToArray()));
    }
}
