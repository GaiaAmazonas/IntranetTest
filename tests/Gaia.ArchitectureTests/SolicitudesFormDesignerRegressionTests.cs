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
    public void WorkflowDesignerCanExpandInlineAndKeepsVersionInHeader()
    {
        var source=Read("solicitudes-workflow-manager.tsx");
        Assert.Contains("designerExpanded",source);
        Assert.Contains("Ampliar diseñador",source);
        Assert.Contains("Restaurar vista",source);
        Assert.Contains("h-[calc(100vh-10rem)]",source);
        Assert.Contains("lg:grid-cols-[minmax(0,1fr)_auto]",source);
        Assert.Contains("bg-[var(--brand-primary)]",source);
        Assert.Contains("designerExpanded ? \"grid md:grid-cols-2 xl:grid-cols-3\"",source);
        Assert.DoesNotContain("!designerExpanded&&<aside",source);
    }

    [Fact]
    public void UnpublishedServiceCanReactivateItsPublishedWorkflow()
    {
        var source=Read("solicitudes-workflow-manager.tsx");
        Assert.Contains("canRepublish",source);
        Assert.Contains("!service.visible && definition?.status === 299540131",source);
        Assert.Contains("Volver a publicar",source);
        Assert.Contains("sin crear copias ni modificar las solicitudes existentes",source);
        Assert.Contains("/publish",source);
    }

    [Fact]
    public void StageEditorUsesExplicitActionsInsteadOfRedundantActivityType()
    {
        var source=Read("solicitudes-workflow-manager.tsx");
        Assert.DoesNotContain("label=\"Tipo de actividad\"",source);
        Assert.Contains("¿Cómo se completa esta etapa?",source);
        Assert.Contains("Aprobar o rechazar",source);
        Assert.Contains("type: step.final ? 299540144 : step.requiresDecision ? 299540142 : 299540140",source);
        Assert.Contains("Hacer obligatoria la observación",source);
        Assert.Contains("No crea un campo nuevo",source);
        Assert.Contains("Es el punto de retorno al reabrir",source);
        Assert.Contains("allowsRequesterReturn: true",source);
    }

    [Fact]
    public void ServiceConfigurationShowsOneProminentPublicationStatusWithoutTechnicalCode()
    {
        var source=ReadDesigner();
        var workspace=Between(source,"function ServiceWorkspace","function Overview");
        var configuration=Between(source,"function Overview","function FormsWorkspace");
        Assert.Contains("Disponible en la intranet para recibir solicitudes",workspace);
        Assert.Contains("Borrador activo; publícalo desde el paso final",workspace);
        Assert.DoesNotContain("<Fact",configuration);
        Assert.DoesNotContain("<Fact",source);
    }

    [Fact]
    public void WorkflowStageAndRouteDeletionIsPhysicalAndCascadesDependencies()
    {
        var writer=ReadBackend("DataverseSolicitudesWorkflowExecutionWriter.cs");
        var routeDeletion=Between(writer,"public async Task DeleteRouteAsync","public async Task UpdateStepPositionAsync");
        Assert.Contains("await Delete(client,route.EntitySetName,routeId,token)",routeDeletion);
        Assert.DoesNotContain("await Patch",routeDeletion);

        var stages=ReadBackend("DataverseSolicitudesStageFormAdministration.cs");
        var stepDeletion=Between(stages,"public async Task DeleteStepAsync","private async Task<IReadOnlyList<string>> ValidateStageFormsAsync");
        Assert.Contains("foreach(var id in optionIds)await Delete",stepDeletion);
        Assert.Contains("foreach(var id in fieldIds)await Delete",stepDeletion);
        Assert.Contains("foreach(var id in formIds)await Delete",stepDeletion);
        Assert.Contains("foreach(var id in routeIds)await Delete",stepDeletion);
        Assert.Contains("await Delete(client,step.EntitySetName,stepId,token)",stepDeletion);
        Assert.DoesNotContain("await Patch",stepDeletion);
    }

    [Fact]
    public void NewWorkflowStartsWithInitialAndFinalStagesAndPeopleAreSearchable()
    {
        var backend=ReadBackend("DataverseSolicitudesWorkflowExecutionWriter.cs");
        Assert.Contains("REVISION_INICIAL",backend);
        Assert.Contains("CIERRE_FINAL",backend);
        Assert.Contains("REVISION_INICIAL_A_CIERRE",backend);

        var designer=Read("solicitudes-workflow-manager.tsx");
        Assert.Contains("<PersonPicker",designer);
        Assert.Contains("<OrganizationalUnitPicker",designer);
        var picker=ReadComponent("person-picker.tsx");
        Assert.Contains("Buscar persona por nombre o unidad",picker);
        Assert.Contains("role=\"listbox\"",picker);
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
    public void SolicitudesLetsTheApiHandleInteractiveReauthentication()
    {
        var endpoints=File.ReadAllText(Path.Combine(RepositoryRoot(),"src","Modules","Solicitudes",
            "Gaia.Modules.Solicitudes","SolicitudesEndpoints.cs"));
        Assert.Contains("RequiresInteractiveAuthentication(error)",endpoints);
        Assert.Contains("ExceptionDispatchInfo.Capture(error).Throw()",endpoints);
        Assert.Contains("MicrosoftIdentityWebChallengeUserException",endpoints);
        Assert.Contains("MsalUiRequiredException",endpoints);
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

    private static string ReadComponent(string file)
    {
        var root=RepositoryRoot();
        return File.ReadAllText(Path.Combine(root,"apps","web","src","components",file));
    }

    private static string ReadBackend(string file)
    {
        var root=RepositoryRoot();
        return File.ReadAllText(Path.Combine(root,"src","Gaia.Api","Infrastructure","Dataverse","Solicitudes",file));
    }

    private static string Between(string source,string start,string end)
    {
        var first=source.IndexOf(start,StringComparison.Ordinal);
        var last=source.IndexOf(end,first,StringComparison.Ordinal);
        Assert.True(first>=0&&last>first);
        return source[first..last];
    }

    private static string RepositoryRoot()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"Gaia.Platform.slnx")))root=root.Parent;
        return root?.FullName??throw new DirectoryNotFoundException();
    }
}
