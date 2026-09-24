using System.Text.Json;
using Gaia.Modules.Helpdesk;
namespace Gaia.ArchitectureTests;
public sealed class IntranetProfileObservationRegressionTests
{
    private static readonly JsonSerializerOptions WebOptions=new(JsonSerializerDefaults.Web);
    [Fact]
    public void RequesterReturnIsNotSuppressedByAdministrativeAccess()
    {
        var source=Read("src","Gaia.Api","Infrastructure","Dataverse","Helpdesk","DataverseHelpdeskConversationStore.cs");
        Assert.Contains("if(requesterCanReply) {",source);
        Assert.DoesNotContain("if(requesterCanReply&&!managementAccess)",source);
        Assert.Contains("..transitions.Where(item=>item.Id!=previous.Id)",source);
    }
    [Fact]
    public void ReturnFlagIsExposedToTheWebClient()
    {
        var id=Guid.NewGuid();
        var json=JsonSerializer.Serialize(new HelpdeskTransition(id,id,"Radicada",true,false,false,false,true),WebOptions);
        using var document=JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("isObservationReturn").GetBoolean());
    }
    [Fact]
    public void RequesterReceivesConfiguredTransitionsOutsideObservationFlow()
    {
        var source=Read("src","Gaia.Api","Infrastructure","Dataverse","Helpdesk","DataverseHelpdeskConversationStore.cs");
        Assert.Contains("if(isManager)availableTransitions.AddRange",source);
        Assert.Contains("if(isRequester)availableTransitions.AddRange",source);
        Assert.Contains("ReadTransitions(client,state,stateId,299540010,token)",source);
    }
    [Fact]
    public void IntranetRendersRequesterStateActions()
    {
        var source=Read("apps","web","src","features","intranet","intranet-helpdesk.tsx");
        Assert.Contains("function RequesterStateActions",source);
        Assert.Contains("Cerrar solicitud",source);
        Assert.Contains("selected?.requestsRating",source);
    }
    [Fact]
    public void ProfileUsesOnlyTheAuthenticatedLinkedPerson()
    {
        var source=Read("src","Modules","ThirdParties","Gaia.Modules.ThirdParties","ThirdPartiesEndpoints.cs");
        Assert.Contains("profile.MapGet(\"/\", GetCurrentUserProfileAsync)",source);
        Assert.Contains("account.User.ThirdPartyId",source);
        Assert.Contains("reader.ReadPersonAsync(id,token)",source);
    }
    private static string Read(params string[] path)
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"Gaia.Platform.slnx")))root=root.Parent;
        return File.ReadAllText(Path.Combine(new[]{root?.FullName??throw new DirectoryNotFoundException()}.Concat(path).ToArray()));
    }
}
