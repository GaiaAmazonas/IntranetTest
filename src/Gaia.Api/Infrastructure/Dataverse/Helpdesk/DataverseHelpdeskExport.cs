using System.Text.Json;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed partial class DataverseHelpdeskManagementStore
{
    public async Task<IReadOnlyList<HelpdeskRequestExportRow>> ReadExportAsync(CancellationToken token)
    {
        var client=await clients.CreateAsync();
        var request=await DataverseMetadataResolver.TableAsync(client,"gaia_solicitud",token);
        var service=await DataverseMetadataResolver.TableAsync(client,"gaia_servicio",token);
        var state=await DataverseMetadataResolver.TableAsync(client,"gaia_estadosolicitud",token);
        var third=await DataverseMetadataResolver.TableAsync(client,"gaia_terceros",token);
        var unit=await DataverseMetadataResolver.TableAsync(client,"gaia_organizacion",token);
        var serviceNav=request.Relationship("gaia_Servicio",service.LogicalName).NavigationProperty;
        var stateNav=request.Relationship("gaia_EstadoActual",state.LogicalName).NavigationProperty;
        var requesterNav=request.Relationship("gaia_Solicitante",third.LogicalName).NavigationProperty;
        var requesterUnitNav=request.Relationship("gaia_UnidadSolicitante",unit.LogicalName).NavigationProperty;
        var responsibleNav=request.Relationship("gaia_ResponsableInterno",third.LogicalName).NavigationProperty;
        var responsibleUnitNav=request.Relationship("gaia_UnidadResponsable",unit.LogicalName).NavigationProperty;
        var subject=request.Attribute("gaia_Asunto");var description=request.Attribute("gaia_DescripcionInicial");
        var submitted=request.Attribute("gaia_FechaRadicacion");var firstManagement=request.Attribute("gaia_FechaPrimeraGestion");
        var due=request.Attribute("gaia_FechaLimiteActual");var closed=request.Attribute("gaia_FechaCierre");
        var managementDays=request.Attribute("gaia_DiasGestionReales");var metSla=request.Attribute("gaia_CumplioSLA");
        var solution=request.Attribute("gaia_ResumenSolucion");var final=state.Attribute("gaia_EsFinal");
        var select=string.Join(',',request.PrimaryIdAttribute,request.PrimaryNameAttribute,subject,description,submitted,
            firstManagement,due,closed,managementDays,metSla,solution);
        var expand=$"{serviceNav}($select={service.PrimaryNameAttribute}),{stateNav}($select={state.PrimaryNameAttribute},{final}),"+
            $"{requesterNav}($select={third.PrimaryNameAttribute}),{requesterUnitNav}($select={unit.PrimaryNameAttribute}),"+
            $"{responsibleNav}($select={third.PrimaryNameAttribute}),{responsibleUnitNav}($select={unit.PrimaryNameAttribute})";
        var rows=await DataverseJson.ReadAllAsync(client,$"{request.EntitySetName}?$select={select}&$expand={expand}&$filter=statecode eq 0&$orderby={submitted} desc",token);
        var now=DateTimeOffset.UtcNow;
        return rows.Select(row=>
        {
            var submittedAt=DateTimeValue(row,submitted);var closedAt=DateTimeValue(row,closed);
            var stateRow=NestedExport(row,stateNav);var isFinal=Bool(stateRow,final);
            var elapsedEnd=closedAt??now;var elapsed=submittedAt.HasValue?Math.Max(0,(int)Math.Floor((elapsedEnd-submittedAt.Value).TotalDays)):0;
            return new HelpdeskRequestExportRow(RequiredGuid(row,request.PrimaryIdAttribute),Text(row,request.PrimaryNameAttribute)??"",
                Text(row,subject)??"",Text(row,description),Text(NestedExport(row,serviceNav),service.PrimaryNameAttribute)??"Sin servicio",
                Text(NestedExport(row,requesterNav),third.PrimaryNameAttribute)??"Sin solicitante",
                Text(NestedExport(row,requesterUnitNav),unit.PrimaryNameAttribute),submittedAt,DateTimeValue(row,firstManagement),
                Text(NestedExport(row,responsibleNav),third.PrimaryNameAttribute),Text(NestedExport(row,responsibleUnitNav),unit.PrimaryNameAttribute),
                Text(stateRow,state.PrimaryNameAttribute)??"Sin estado",isFinal,Date(row,due),closedAt,
                DataverseJson.OptionalInt32(row,managementDays),elapsed,OptionalBool(row,metSla),Text(row,solution));
        }).ToArray();
    }

    private static JsonElement NestedExport(JsonElement row,string name)=>
        row.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.Object?value:EmptyExportObject;
    private static readonly JsonElement EmptyExportObject=JsonSerializer.SerializeToElement(new{});
    private static bool? OptionalBool(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind is JsonValueKind.True or JsonValueKind.False?value.GetBoolean():null;
}
