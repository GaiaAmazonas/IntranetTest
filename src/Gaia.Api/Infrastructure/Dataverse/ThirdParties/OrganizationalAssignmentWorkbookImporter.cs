using Gaia.Modules.Organization;
using Gaia.Modules.ThirdParties;

namespace Gaia.Api.Infrastructure.Dataverse.ThirdParties;

internal sealed class OrganizationalAssignmentWorkbookImporter(IThirdPartyReader thirdPartyReader,
    IOrganizationPositionStore positionStore, IOrganizationUnitReader unitReader,
    IOrganizationalAssignmentStore assignmentStore) : IOrganizationalAssignmentImporter
{
    public async Task<OrganizationalAssignmentImportValidation> ValidateAsync(Stream workbook, CancellationToken token)
    {
        var plan = await PlanAsync(workbook, token); return plan.Validation;
    }

    public async Task<OrganizationalAssignmentImportResult> ImportAsync(Stream workbook, CancellationToken token)
    {
        var plan = await PlanAsync(workbook, token);
        if (!plan.Validation.Valid) return new(plan.Validation, 0, 0, plan.Validation.Unchanged, 0);
        var created=0;var updated=0;var errors=0;
        foreach (var item in plan.Items)
        {
            var command = new OrganizationalAssignmentCommand(item.PartyId,item.PositionId,item.UnitId,null,null,true,null,true,"organizational-import");
            var result = item.ExistingId.HasValue ? await assignmentStore.UpdateAsync(item.ExistingId.Value,command,token) : await assignmentStore.CreateAsync(command,token);
            if(result.Status==OrganizationalAssignmentWriteStatus.Created)created++;else if(result.Status==OrganizationalAssignmentWriteStatus.Updated)updated++;else errors++;
        }
        return new(plan.Validation,created,updated,plan.Validation.Unchanged,errors);
    }

    private async Task<ImportPlan> PlanAsync(Stream workbook, CancellationToken token)
    {
        PersonnelWorkbook source; try{source=PersonnelWorkbookReader.Read(workbook,["Cargos por persona"]);}catch(Exception exception)when(exception is not OperationCanceledException){return Invalid(exception.Message);}
        var sheet=source.Required("Cargos por persona"); var issues=new List<AdministrativeImportIssue>();
        var parties=(await thirdPartyReader.ListAsync(null,token)).Where(x=>x.IsActive).GroupBy(x=>Document(x.DocumentNumber)).ToDictionary(x=>x.Key,x=>x.First());
        var positions=(await positionStore.ListAsync(token)).Where(x=>x.IsActive).GroupBy(x=>Key(x.Name)).ToDictionary(x=>x.Key,x=>x.First());
        var units=(await unitReader.ListAsync(new(null,true,null),token)).GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase).ToDictionary(x=>x.Key,x=>x.First(),StringComparer.OrdinalIgnoreCase);
        var current=(await assignmentStore.ListAsync(token)).Where(x=>x.IsActive).GroupBy(x=>x.ThirdPartyId).ToDictionary(x=>x.Key,x=>x.First());
        var items=new List<ImportItem>();var unchanged=0;
        foreach(var row in sheet.Rows)
        {
            var document=Document(Value(row,"Número de documento","Documento"));var positionName=Value(row,"Cargo");var unitCode=Value(row,"Unidad","Código unidad","Codigo unidad");var unitName=Value(row,"Nombre Unidad","Nombre de la unidad");
            if(!parties.TryGetValue(document,out var party)){issues.Add(new(row.Number,sheet.Name,"THIRD_PARTY_NOT_FOUND",document));continue;}
            if(!positions.TryGetValue(Key(positionName),out var position)){issues.Add(new(row.Number,sheet.Name,"POSITION_NOT_FOUND",positionName));continue;}
            if(!units.TryGetValue(unitCode,out var unit)){issues.Add(new(row.Number,sheet.Name,"UNIT_NOT_FOUND",$"{unitCode} · {unitName}"));continue;}
            if(unitName.Length>0&&Key(unit.Name)!=Key(unitName))issues.Add(new(row.Number,sheet.Name,"UNIT_NAME_MISMATCH",$"{unitCode}: Excel '{unitName}' / Dataverse '{unit.Name}'","warning"));
            current.TryGetValue(party.Id,out var existing); if(existing is not null&&existing.PositionId==position.Id&&existing.OrganizationalUnitId==unit.Id){unchanged++;continue;}
            items.Add(new(party.Id,position.Id,unit.Id,existing?.Id));
        }
        var validation=new OrganizationalAssignmentImportValidation(!issues.Any(x=>x.Severity=="error"),sheet.Rows.Count,items.Count(x=>!x.ExistingId.HasValue),items.Count(x=>x.ExistingId.HasValue),unchanged,issues);
        return new(validation,items);
    }
    private static string Value(WorkbookRow row,params string[] names){foreach(var name in names){var value=row.Get(name);if(value.Length>0)return value;}return "";}
    private static string Document(string value)=>PersonnelWorkbookReader.Normalize(value).Replace(".0","",StringComparison.Ordinal);
    private static string Key(string value)=>PersonnelWorkbookReader.Key(value);
    private static ImportPlan Invalid(string detail)=>new(new(false,0,0,0,0,[new(0,"Libro","INVALID_WORKBOOK",detail)]),[]);
    private sealed record ImportItem(Guid PartyId,Guid PositionId,Guid UnitId,Guid? ExistingId);
    private sealed record ImportPlan(OrganizationalAssignmentImportValidation Validation,IReadOnlyList<ImportItem> Items);
}
