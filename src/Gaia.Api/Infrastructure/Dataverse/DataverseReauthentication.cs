using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace Gaia.Api.Infrastructure.Dataverse;

internal static class DataverseReauthentication
{
    internal static bool IsRequired(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is MicrosoftIdentityWebChallengeUserException or MsalUiRequiredException)
                return true;
        }

        return false;
    }
}
