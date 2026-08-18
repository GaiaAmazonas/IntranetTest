using Gaia.Api.Infrastructure.Dataverse;
using Microsoft.Identity.Client;

namespace Gaia.ArchitectureTests;

public sealed class DataverseReauthenticationTests
{
    [Fact]
    public void DetectsUiRequiredExceptionThroughInnerException()
    {
        var exception = new InvalidOperationException("outer", new MsalUiRequiredException("user_null", "Login required"));

        Assert.True(DataverseReauthentication.IsRequired(exception));
    }

    [Fact]
    public void DoesNotConvertUnrelatedFailuresIntoAuthenticationErrors() =>
        Assert.False(DataverseReauthentication.IsRequired(new InvalidOperationException("Dataverse failure")));
}
