namespace Gaia.ArchitectureTests;

public sealed class SolicitudesFormDesignerRegressionTests
{
    [Fact]
    public void PreviewControlsRemainInteractive()
    {
        var source=ReadDesigner();
        Assert.Contains("function PreviewControl",source);
        Assert.Contains("type=\"radio\"",source);
        Assert.Contains("type=\"checkbox\"",source);
        Assert.Contains("type=\"file\"",source);
        Assert.DoesNotContain("<select className={style} disabled",source);
        Assert.DoesNotContain("<input className={style} disabled",source);
    }

    [Fact]
    public void DragAndDropPersistsEveryNormalizedOrder()
    {
        var source=ReadDesigner();
        Assert.Contains("draggable={canEdit}",source);
        Assert.Contains("onDrop={drop}",source);
        Assert.Contains("const normalized = ordered.map",source);
        Assert.Contains("order: index",source);
        Assert.Contains("method: \"PUT\"",source);
        Assert.Contains("await changed()",source);
    }

    [Fact]
    public void StageFormPreviewIsInteractiveAndReorderingIsPersisted()
    {
        var source=Read("solicitudes-dynamic-form.tsx");
        Assert.Contains("setPreviewValues(current=>({...current,[id]:value}))",source);
        Assert.Contains("draggable={!readOnly&&!busy}",source);
        Assert.Contains("onDrop={()=>void reorder(field.id)}",source);
        Assert.Contains("order:(index+1)*10",source);
        Assert.Contains("method:\"PUT\"",source);
    }

    [Fact]
    public void WorkflowDesignerSupportsParallelInitialStagesAndDraftMaintenance()
    {
        var source=Read("solicitudes-workflow-manager.tsx");
        Assert.Contains("Existe al menos una etapa inicial",source);
        Assert.DoesNotContain("Existe una única etapa inicial",source);
        Assert.Contains("/duplicate",source);
        Assert.Contains("method: \"DELETE\"",source);

        var stageForm=Read("solicitudes-dynamic-form.tsx");
        Assert.Contains("form/fields/${field.id}",stageForm);
        Assert.Contains("method:\"DELETE\"",stageForm);
    }

    [Fact]
    public void NewWorkflowStartsWithInitialAndFinalStagesAndPeopleAreSearchable()
    {
        var backend=ReadBackend("DataverseSolicitudesWorkflowExecutionWriter.cs");
        Assert.Contains("REVISION_INICIAL",backend);
        Assert.Contains("CIERRE_FINAL",backend);
        Assert.Contains("REVISION_INICIAL_A_CIERRE",backend);

        var designer=Read("solicitudes-workflow-manager.tsx");
        Assert.Contains("function PersonAutocomplete",designer);
        Assert.Contains("Buscar persona por nombre o unidad",designer);
        Assert.Contains("<PersonAutocomplete",designer);
    }

    [Fact]
    public void ManagementSavesAnswersThenFilesBeforeCompletingStage()
    {
        var source=Read("solicitudes-management-form.tsx");
        var answers=source.IndexOf("/form/responses",StringComparison.Ordinal);
        var files=source.IndexOf("/attachments",StringComparison.Ordinal);
        var completion=source.IndexOf("await onCompleted",StringComparison.Ordinal);
        Assert.True(answers>=0&&files>answers&&completion>files);
        Assert.Contains("data.form?",source);
        Assert.Contains("Esta etapa no requiere un formulario adicional",source);
    }

    [Fact]
    public void BackendKeepsResponsesScopedToManagementExecutionAndDerivesFilesServerSide()
    {
        var execution=ReadBackend("DataverseSolicitudesStageFormExecution.cs");
        var completion=ReadBackend("DataverseSolicitudesWorkflowExecutionWriter.cs");
        Assert.Contains("_value eq {managementId:D}",execution);
        Assert.Contains("ValidateManagementStageFormAsync(command.ManagementId",completion);
        Assert.Contains("command=command with{HasRelatedFile=relatedFiles.Count>0}",completion);
    }

    private static string ReadDesigner()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"Gaia.Platform.slnx")))root=root.Parent;
        return File.ReadAllText(Path.Combine(root?.FullName??throw new DirectoryNotFoundException(),
            "apps","web","src","features","solicitudes","solicitudes-administration.tsx"));
    }

    private static string Read(string file)
    {
        var root=RepositoryRoot();
        return File.ReadAllText(Path.Combine(root,"apps","web","src","features","solicitudes",file));
    }

    private static string ReadBackend(string file)
    {
        var root=RepositoryRoot();
        return File.ReadAllText(Path.Combine(root,"src","Gaia.Api","Infrastructure","Dataverse","Solicitudes",file));
    }

    private static string RepositoryRoot()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"Gaia.Platform.slnx")))root=root.Parent;
        return root?.FullName??throw new DirectoryNotFoundException();
    }
}
