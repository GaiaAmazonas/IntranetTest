using System.Net.Http.Json;
using System.Text.Json;
using Gaia.Modules.Helpdesk;

namespace Gaia.Api.Infrastructure.Dataverse.Helpdesk;

internal sealed partial class DataverseHelpdeskWorkflowExecutionWriter
{
    public async Task<HelpdeskStageForm?> ReadStageFormAsync(Guid stepId,CancellationToken token)
    {
        var client=await clients.CreateAsync();
        var form=await DataverseMetadataResolver.TableAsync(client,"gaia_formulariopaso",token);
        var field=await DataverseMetadataResolver.TableAsync(client,"gaia_campoformulariopaso",token);
        var option=await DataverseMetadataResolver.TableAsync(client,"gaia_opcioncampoformulariopaso",token);
        var stepRelation=form.Relationship("gaia_PasoFlujo","gaia_pasoflujo");
        var forms=await DataverseJson.ReadAllAsync(client,$"{form.EntitySetName}?$select={form.PrimaryIdAttribute},{form.Attribute("gaia_Titulo")},{form.Attribute("gaia_Instrucciones")},_{stepRelation.ReferencingAttribute}_value,statecode&$filter=statecode eq 0 and _{stepRelation.ReferencingAttribute}_value eq {stepId:D}&$top=2",token);
        if(forms.Count==0)return null;
        if(forms.Count>1)throw new InvalidOperationException("La etapa tiene más de un formulario activo.");
        var row=forms[0];var formId=RequiredGuid(row,form.PrimaryIdAttribute);
        var formRelation=field.Relationship("gaia_FormularioPaso","gaia_formulariopaso");
        var fields=await DataverseJson.ReadAllAsync(client,$"{field.EntitySetName}?$select={FieldSelect(field)},_{formRelation.ReferencingAttribute}_value,statecode&$filter=statecode eq 0 and _{formRelation.ReferencingAttribute}_value eq {formId:D}",token);
        var ids=fields.Select(x=>RequiredGuid(x,field.PrimaryIdAttribute)).ToHashSet();
        var optionRelation=option.Relationship("gaia_CampoFormularioPaso","gaia_campoformulariopaso");
        var options=ids.Count==0?[]:await DataverseJson.ReadAllAsync(client,$"{option.EntitySetName}?$select={option.PrimaryIdAttribute},{option.PrimaryNameAttribute},{option.Attribute("gaia_Codigo")},{option.Attribute("gaia_Orden")},{option.Attribute("gaia_EsPredeterminada")},_{optionRelation.ReferencingAttribute}_value,statecode&$filter=statecode eq 0",token);
        var grouped=options.Where(x=>OptionalGuid(x,$"_{optionRelation.ReferencingAttribute}_value") is {} id&&ids.Contains(id)).GroupBy(x=>OptionalGuid(x,$"_{optionRelation.ReferencingAttribute}_value")!.Value).ToDictionary(x=>x.Key,x=>(IReadOnlyList<HelpdeskFormOption>)x.Select(o=>new HelpdeskFormOption(RequiredGuid(o,option.PrimaryIdAttribute),Text(o,option.Attribute("gaia_Codigo"))??"",Text(o,option.PrimaryNameAttribute)??"",Bool(o,option.Attribute("gaia_EsPredeterminada")),Int(o,option.Attribute("gaia_Orden")))).OrderBy(o=>o.Order).ToArray());
        var definitions=fields.Select(x=>MapField(x,field,grouped.GetValueOrDefault(RequiredGuid(x,field.PrimaryIdAttribute),[]))).OrderBy(x=>x.Order).ToArray();
        return new(formId,stepId,Text(row,form.Attribute("gaia_Titulo"))??"",Text(row,form.Attribute("gaia_Instrucciones")),definitions);
    }

    public async Task<Guid> SaveStageFormAsync(Guid stepId,SaveHelpdeskStageForm command,CancellationToken token)
    {
        var client=await clients.CreateAsync();await RequireDraftForStep(stepId,client,token);
        var form=await DataverseMetadataResolver.TableAsync(client,"gaia_formulariopaso",token);var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);var relation=form.Relationship("gaia_PasoFlujo","gaia_pasoflujo");
        var rows=await DataverseJson.ReadAllAsync(client,$"{form.EntitySetName}?$select={form.PrimaryIdAttribute}&$filter=_{relation.ReferencingAttribute}_value eq {stepId:D}&$top=2",token);if(rows.Count>1)throw new InvalidOperationException("La etapa tiene más de un formulario.");
        var payload=new Dictionary<string,object?>{{form.PrimaryNameAttribute,command.Title},{form.Attribute("gaia_Titulo"),command.Title},{form.Attribute("gaia_Instrucciones"),command.Instructions},{relation.NavigationProperty+"@odata.bind",$"/{step.EntitySetName}({stepId:D})"},{"statecode",0}};
        if(rows.Count==1){var id=RequiredGuid(rows[0],form.PrimaryIdAttribute);await Patch(client,form.EntitySetName,id,payload,token);return id;}
        return await Create(client,form.EntitySetName,payload,token);
    }

    public async Task<Guid> SaveStageFormFieldAsync(Guid stepId,Guid? fieldId,SaveHelpdeskFormField value,CancellationToken token)
    {
        var client=await clients.CreateAsync();await RequireDraftForStep(stepId,client,token);var current=await ReadStageFormAsync(stepId,token)??throw new InvalidOperationException("Configura primero el encabezado del formulario de la etapa.");
        var form=await DataverseMetadataResolver.TableAsync(client,"gaia_formulariopaso",token);var field=await DataverseMetadataResolver.TableAsync(client,"gaia_campoformulariopaso",token);var option=await DataverseMetadataResolver.TableAsync(client,"gaia_opcioncampoformulariopaso",token);
        if(fieldId.HasValue&&!current.Fields.Any(x=>x.Id==fieldId.Value))throw new ArgumentException("El campo no pertenece al formulario de esta etapa.");
        var payload=FieldPayload(field,value);payload[field.Relationship("gaia_FormularioPaso","gaia_formulariopaso").NavigationProperty+"@odata.bind"]=$"/{form.EntitySetName}({current.Id:D})";payload["statecode"]=0;
        var id=fieldId.HasValue?fieldId.Value:await Create(client,field.EntitySetName,payload,token);if(fieldId.HasValue)await Patch(client,field.EntitySetName,id,payload,token);
        var optionRelation=option.Relationship("gaia_CampoFormularioPaso","gaia_campoformulariopaso");var existing=await DataverseJson.ReadAllAsync(client,$"{option.EntitySetName}?$select={option.PrimaryIdAttribute},{option.Attribute("gaia_Codigo")},_{optionRelation.ReferencingAttribute}_value,statecode&$filter=_{optionRelation.ReferencingAttribute}_value eq {id:D}",token);var retained=new HashSet<Guid>();
        foreach(var item in value.Options){var match=item.Id.HasValue?existing.SingleOrDefault(x=>RequiredGuid(x,option.PrimaryIdAttribute)==item.Id):existing.SingleOrDefault(x=>string.Equals(Text(x,option.Attribute("gaia_Codigo")),item.Code,StringComparison.OrdinalIgnoreCase));var optionId=match.ValueKind==JsonValueKind.Undefined?(Guid?)null:RequiredGuid(match,option.PrimaryIdAttribute);var optionPayload=new Dictionary<string,object?>{{option.PrimaryNameAttribute,item.Label},{option.Attribute("gaia_Codigo"),item.Code},{option.Attribute("gaia_Orden"),item.Order},{option.Attribute("gaia_EsPredeterminada"),item.IsDefault},{optionRelation.NavigationProperty+"@odata.bind",$"/{field.EntitySetName}({id:D})"},{"statecode",item.IsActive?0:1}};var saved=optionId.HasValue?optionId.Value:await Create(client,option.EntitySetName,optionPayload,token);if(optionId.HasValue)await Patch(client,option.EntitySetName,saved,optionPayload,token);retained.Add(saved);}
        foreach(var old in existing.Where(x=>Int(x,"statecode")==0&&!retained.Contains(RequiredGuid(x,option.PrimaryIdAttribute))))await Patch(client,option.EntitySetName,RequiredGuid(old,option.PrimaryIdAttribute),new Dictionary<string,object?>{{"statecode",1}},token);
        return id;
    }

    private async Task<IReadOnlyList<string>> ValidateStageFormsAsync(Guid flowId,CancellationToken token)
    {
        var definition=await definitions.ReadAsync(flowId,token)??throw new KeyNotFoundException("El flujo no existe.");var errors=new List<string>();
        foreach(var step in definition.Steps.Where(x=>x.Active))
        {
            var form=await ReadStageFormAsync(step.Id,token);if(form is null)continue;
            if(string.IsNullOrWhiteSpace(form.Title))errors.Add($"El formulario de la etapa {step.Code} no tiene título.");
            var duplicate=form.Fields.GroupBy(x=>x.Code,StringComparer.OrdinalIgnoreCase).FirstOrDefault(x=>x.Count()>1);if(duplicate is not null)errors.Add($"El formulario de {step.Code} repite el código {duplicate.Key}.");
            foreach(var field in form.Fields.Where(x=>x.Visible))
            {if(field.DataType==299540046&&field.Options.Count==0)errors.Add($"El campo {field.Code} de {step.Code} no tiene opciones.");if(field.Required&&field.DataType==299540047&&field.MaximumFiles==0)errors.Add($"El campo de archivo {field.Code} no permite cargas.");}
        }
        return errors;
    }

    private async Task CloneWorkflowAsync(Guid sourceFlowId,Guid targetFlowId,CancellationToken token)
    {
        var source=await definitions.ReadAsync(sourceFlowId,token);if(source is null)return;var map=new Dictionary<Guid,Guid>();
        foreach(var step in source.Steps.OrderBy(x=>x.Order)){var created=await SaveStepAsync(targetFlowId,null,new(step.Code,step.Type,step.Order,step.Initial,step.Final,step.ReopeningEntry,step.AssignmentStrategy,step.UnitId,step.PersonId,step.ActivationRule,step.RequiresDecision,step.RequiresObservation,step.RequiresFile,step.AllowsRequesterReturn,step.TargetDays,step.Active),token);map[step.Id]=created;var form=await ReadStageFormAsync(step.Id,token);if(form is null)continue;await SaveStageFormAsync(created,new(form.Title,form.Instructions),token);foreach(var field in form.Fields)await SaveStageFormFieldAsync(created,null,ToSave(field),token);}
        foreach(var route in source.Routes.OrderBy(x=>x.Order))await SaveRouteAsync(targetFlowId,null,new(route.Code,map[route.SourceStepId],map[route.TargetStepId],route.RequiredResult,route.Order,route.Active),token);
    }

    private static SaveHelpdeskFormField ToSave(HelpdeskFormField x)=>new(x.Code,x.Label,x.DataType,x.ControlType,x.HelpText,x.Placeholder,x.Required,x.Order,x.Width,x.MinimumLength,x.MaximumLength,x.MinimumValue,x.MaximumValue,x.AllowsMultiple,x.MaximumFiles,x.AllowedFileTypes,x.Visible,x.Options.Select(o=>new SaveHelpdeskFormOption(null,o.Code,o.Label,o.Order,o.IsDefault)).ToArray(),x.ValidationPattern,x.ValidationMessage,x.DefaultValue,x.ConfigurationJson);
    private static async Task RequireDraftForStep(Guid stepId,HttpClient client,CancellationToken token){var step=await DataverseMetadataResolver.TableAsync(client,"gaia_pasoflujo",token);var relation=step.Relationship("gaia_FlujoGestion","gaia_flujogestion");var row=await DataverseMetadataResolver.ReadOneAsync(client,$"{step.EntitySetName}({stepId:D})?$select=_{relation.ReferencingAttribute}_value,statecode",token)??throw new KeyNotFoundException("La etapa no existe.");if(Int(row,"statecode")!=0)throw new InvalidOperationException("La etapa está inactiva.");await RequireDraft(RequiredGuid(row,$"_{relation.ReferencingAttribute}_value"),client,token);}
    private static string FieldSelect(DataverseTableMetadata f)=>string.Join(',',new[]{f.PrimaryIdAttribute,f.PrimaryNameAttribute,f.Attribute("gaia_Codigo"),f.Attribute("gaia_TipoDato"),f.Attribute("gaia_TipoControl"),f.Attribute("gaia_TextoAyuda"),f.Attribute("gaia_Placeholder"),f.Attribute("gaia_Requerido"),f.Attribute("gaia_Orden"),f.Attribute("gaia_AnchoColumnas"),f.Attribute("gaia_LongitudMinima"),f.Attribute("gaia_LongitudMaxima"),f.Attribute("gaia_ValorMinimo"),f.Attribute("gaia_ValorMaximo"),f.Attribute("gaia_PatronValidacion"),f.Attribute("gaia_MensajeValidacion"),f.Attribute("gaia_ValorPredeterminado"),f.Attribute("gaia_PermiteVarios"),f.Attribute("gaia_MaximoArchivos"),f.Attribute("gaia_TiposArchivoPermitidos"),f.Attribute("gaia_ConfiguracionJson"),f.Attribute("gaia_Visible")});
    private static HelpdeskFormField MapField(JsonElement x,DataverseTableMetadata f,IReadOnlyList<HelpdeskFormOption> options)=>new(RequiredGuid(x,f.PrimaryIdAttribute),Text(x,f.Attribute("gaia_Codigo"))??"",Text(x,f.PrimaryNameAttribute)??"",Int(x,f.Attribute("gaia_TipoDato")),Int(x,f.Attribute("gaia_TipoControl")),Text(x,f.Attribute("gaia_TextoAyuda")),Text(x,f.Attribute("gaia_Placeholder")),Bool(x,f.Attribute("gaia_Requerido")),Int(x,f.Attribute("gaia_Orden")),Int(x,f.Attribute("gaia_AnchoColumnas")),NullableInt(x,f.Attribute("gaia_LongitudMinima")),NullableInt(x,f.Attribute("gaia_LongitudMaxima")),Decimal(x,f.Attribute("gaia_ValorMinimo")),Decimal(x,f.Attribute("gaia_ValorMaximo")),Bool(x,f.Attribute("gaia_PermiteVarios")),NullableInt(x,f.Attribute("gaia_MaximoArchivos")),Text(x,f.Attribute("gaia_TiposArchivoPermitidos")),Bool(x,f.Attribute("gaia_Visible")),options,Text(x,f.Attribute("gaia_PatronValidacion")),Text(x,f.Attribute("gaia_MensajeValidacion")),Text(x,f.Attribute("gaia_ValorPredeterminado")),Text(x,f.Attribute("gaia_ConfiguracionJson")));
    private static Dictionary<string,object?> FieldPayload(DataverseTableMetadata f,SaveHelpdeskFormField v)=>new(){{f.PrimaryNameAttribute,v.Label},{f.Attribute("gaia_Codigo"),v.Code},{f.Attribute("gaia_TipoDato"),f.EncodedIntegerValue("gaia_TipoDato",v.DataType)},{f.Attribute("gaia_TipoControl"),f.EncodedIntegerValue("gaia_TipoControl",v.ControlType)},{f.Attribute("gaia_TextoAyuda"),v.HelpText},{f.Attribute("gaia_Placeholder"),v.Placeholder},{f.Attribute("gaia_Requerido"),v.Required},{f.Attribute("gaia_Orden"),v.Order},{f.Attribute("gaia_AnchoColumnas"),v.Width},{f.Attribute("gaia_LongitudMinima"),v.MinimumLength},{f.Attribute("gaia_LongitudMaxima"),v.MaximumLength},{f.Attribute("gaia_ValorMinimo"),v.MinimumValue},{f.Attribute("gaia_ValorMaximo"),v.MaximumValue},{f.Attribute("gaia_PatronValidacion"),v.ValidationPattern},{f.Attribute("gaia_MensajeValidacion"),v.ValidationMessage},{f.Attribute("gaia_ValorPredeterminado"),v.DefaultValue},{f.Attribute("gaia_PermiteVarios"),v.AllowsMultiple},{f.Attribute("gaia_MaximoArchivos"),v.MaximumFiles},{f.Attribute("gaia_TiposArchivoPermitidos"),v.AllowedFileTypes},{f.Attribute("gaia_ConfiguracionJson"),v.ConfigurationJson},{f.Attribute("gaia_Visible"),v.Visible}};
    private static decimal? Decimal(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var result)?result:null;
}
