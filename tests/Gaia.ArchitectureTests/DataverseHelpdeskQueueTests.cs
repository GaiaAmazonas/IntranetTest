using System.Text.Json;
using Gaia.Api.Infrastructure.Dataverse;
using Gaia.Api.Infrastructure.Dataverse.Helpdesk;
using Gaia.Modules.Helpdesk;

namespace Gaia.ArchitectureTests;

public sealed class DataverseHelpdeskQueueTests
{
    [Fact]
    public void QueryFiltersAndOrdersInsideDataverseWithoutSkip()
    {
        var serviceId=Guid.NewGuid();
        var query=DataverseHelpdeskManagementStore.BuildQueueQuery(
            new("O'Hara",serviceId,null,null,true,2,10),Request(),Service(),State(),Third(),Unit(),new DateOnly(2026,9,7));

        var decoded=Uri.UnescapeDataString(query);
        Assert.Contains($"_gaia_servicio_value eq {serviceId:D}",decoded);
        Assert.Contains("contains(gaia_asunto,'O''Hara')",decoded);
        Assert.Contains("gaia_fechalimiteactual lt 2026-09-07",decoded);
        Assert.Contains("$orderby=gaia_fecharadicacion desc,gaia_solicitudid desc",decoded);
        Assert.Contains("$expand=gaia_Servicio",decoded);
        Assert.Contains("$count=true",decoded);
        Assert.DoesNotContain("$skip",decoded);
    }

    [Theory]
    [InlineData("{\"@odata.count\":37,\"value\":[]}",37)]
    [InlineData("{\"value\":[]}",null)]
    [InlineData("{\"@odata.count\":5000,\"@Microsoft.Dynamics.CRM.totalrecordcountlimitexceeded\":true,\"value\":[]}",null)]
    public void TotalIsOnlyExposedWhenReliable(string json,int? expected)
    {
        using var document=JsonDocument.Parse(json);
        Assert.Equal(expected,DataverseHelpdeskManagementStore.ReliableQueueTotal(document.RootElement));
    }

    private static DataverseTableMetadata Request()=>Metadata("gaia_solicitud","gaia_solicituds","gaia_solicitudid","gaia_numero",
        new(){["gaia_Asunto"]="gaia_asunto",["gaia_FechaRadicacion"]="gaia_fecharadicacion",["gaia_FechaLimiteActual"]="gaia_fechalimiteactual",["gaia_Servicio"]="gaia_servicio",["gaia_EstadoActual"]="gaia_estadoactual",["gaia_Solicitante"]="gaia_solicitante",["gaia_ResponsableInterno"]="gaia_responsableinterno",["gaia_UnidadResponsable"]="gaia_unidadresponsable"},
        [new("gaia_servicio","gaia_Servicio","gaia_servicio"),new("gaia_estadoactual","gaia_EstadoActual","gaia_estadosolicitud"),new("gaia_solicitante","gaia_Solicitante","gaia_terceros"),new("gaia_responsableinterno","gaia_ResponsableInterno","gaia_terceros"),new("gaia_unidadresponsable","gaia_UnidadResponsable","gaia_organizacion")]);
    private static DataverseTableMetadata Service()=>Metadata("gaia_servicio","gaia_servicios","gaia_servicioid","gaia_nombre",[],[]);
    private static DataverseTableMetadata State()=>Metadata("gaia_estadosolicitud","gaia_estadosolicituds","gaia_estadosolicitudid","gaia_nombre",new(){["gaia_Color"]="gaia_color",["gaia_EsFinal"]="gaia_esfinal"},[]);
    private static DataverseTableMetadata Third()=>Metadata("gaia_terceros","gaia_terceros","gaia_tercerosid","gaia_nombre",[],[]);
    private static DataverseTableMetadata Unit()=>Metadata("gaia_organizacion","gaia_organizacions","gaia_organizacionid","gaia_nombre",[],[]);
    private static DataverseTableMetadata Metadata(string logical,string set,string id,string name,Dictionary<string,string> attributes,DataverseRelationship[] relationships)
        =>new(logical,set,id,name,attributes,new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase),relationships);
}
