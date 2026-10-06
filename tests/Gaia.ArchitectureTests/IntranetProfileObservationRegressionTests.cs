using System.Text.Json;
using Gaia.Modules.Solicitudes;
namespace Gaia.ArchitectureTests;
public sealed class IntranetProfileObservationRegressionTests
{
    private static readonly JsonSerializerOptions WebOptions=new(JsonSerializerDefaults.Web);
    [Fact]
    public void RequesterReturnIsNotSuppressedByAdministrativeAccess()
    {
        var source=Read("src","Gaia.Api","Infrastructure","Dataverse","Solicitudes","DataverseSolicitudesConversationStore.cs");
        Assert.Contains("if(requesterCanReply) {",source);
        Assert.DoesNotContain("if(requesterCanReply&&!managementAccess)",source);
        Assert.Contains("..transitions.Where(item=>item.Id!=previous.Id)",source);
    }
    [Fact]
    public void ReturnFlagIsExposedToTheWebClient()
    {
        var id=Guid.NewGuid();
        var json=JsonSerializer.Serialize(new SolicitudesTransition(id,id,"Radicada",true,false,false,false,true),WebOptions);
        using var document=JsonDocument.Parse(json);
        Assert.True(document.RootElement.GetProperty("isObservationReturn").GetBoolean());
    }
    [Fact]
    public void RequesterReceivesConfiguredTransitionsOutsideObservationFlow()
    {
        var source=Read("src","Gaia.Api","Infrastructure","Dataverse","Solicitudes","DataverseSolicitudesConversationStore.cs");
        Assert.Contains("if(isManager)availableTransitions.AddRange",source);
        Assert.Contains("if(isRequester)availableTransitions.AddRange",source);
        Assert.Contains("ReadTransitions(client,state,stateId,299540010,token)",source);
    }
    [Fact]
    public void IntranetUsesOnlyTheActiveWorkflowObservation()
    {
        var source=Read("apps","web","src","features","intranet","intranet-solicitudes.tsx");
        Assert.Contains("workflow?.managements.some(item=>item.status===299540193)&&<form",source);
        Assert.Contains("item.id===waiting.id?{...item,status:299540191}:item",source);
        Assert.DoesNotContain("function RequesterStateActions",source);
        Assert.DoesNotContain("Selecciona la siguiente acción",source);
        Assert.DoesNotContain("function RequesterWorkflow",source);
    }
    [Fact]
    public void ConversationLabelsTeamCommentsWithTheirOrganizationalArea()
    {
        var source=Read("src","Gaia.Api","Infrastructure","Dataverse","Solicitudes","DataverseSolicitudesConversationStore.cs");
        Assert.Contains("IOrganizationalAssignmentStore assignments",source);
        Assert.Contains("item.IsPrimary",source);
        Assert.Contains("item.OrganizationalUnitName",source);
        Assert.Contains("teamAreas.GetValueOrDefault(authorId,\"Equipo Gaia\")",source);
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
