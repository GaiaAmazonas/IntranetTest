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
}
