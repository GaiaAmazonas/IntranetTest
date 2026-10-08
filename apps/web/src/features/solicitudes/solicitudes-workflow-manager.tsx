"use client";
import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import dagre from "@dagrejs/dagre";
import {
  Background,
  BackgroundVariant,
  Controls,
  Handle,
  MarkerType,
  MiniMap,
  Position,
  ReactFlow,
  type Connection,
  type Edge,
  type Node,
  type NodeProps,
  type ReactFlowInstance,
  useEdgesState,
  useNodesState,
} from "@xyflow/react";
import {
  CheckCircle2,
  Circle,
  Copy,
  Eye,
  FileText,
  GitBranch,
  HelpCircle,
  LayoutGrid,
  Maximize2,
  Minimize2,
  Pencil,
  Plus,
  Send,
  ShieldCheck,
  Trash2,
  UserRoundCog,
  X,
} from "lucide-react";
import { apiRequest } from "@/lib/api-client";
import { SolicitudesStageFormDesigner, type StageForm } from "./solicitudes-dynamic-form";
import { useFeedback } from "@/components/feedback";
import { ConfirmDialog } from "@/components/form-dialog";
import { OrganizationalUnitPicker } from "@/components/organizational-unit-picker";
import { PersonPicker } from "@/components/person-picker";

type Unit = {
  id: string;
  code: string;
  name: string;
  parentId?: string | null;
  level?: number;
};
type Person = { id: string; name: string; unitIds: string[] };
type Summary = {
  id: string;
  serviceId: string;
  version: number;
  status: number;
  name: string;
  description: string | null;
  publishedAt: string | null;
  stepCount: number;
  routeCount: number;
};
type Step = {
  id: string;
  code: string;
  type: number;
  order: number;
  initial: boolean;
  final: boolean;
  reopeningEntry: boolean;
  assignmentStrategy: number;
  unitId: string | null;
  personId: string | null;
  activationRule: number;
  requiresDecision: boolean;
  requiresObservation: boolean;
  requiresFile: boolean;
  allowsRequesterReturn: boolean;
  targetDays: number | null;
  active: boolean;
  positionX: number | null;
  positionY: number | null;
};
type Route = {
  id: string;
  code: string;
  sourceStepId: string;
  targetStepId: string;
  requiredResult: number;
  order: number;
  active: boolean;
};
type Definition = {
  id: string;
  serviceId: string;
  version: number;
  status: number;
  steps: Step[];
  routes: Route[];
};
type PublishableService = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  instructions: string | null;
  businessDays: number;
  allowsAttachments: boolean;
  maximumAttachments: number;
  maximumFileMb: number;
  visible: boolean;
  order: number;
  responsibleId: string;
  unitId: string;
  currentFormId: string | null;
  isActive: boolean;
};
const blankStep = {
  code: "",
  type: 299540140,
  order: 10,
  initial: false,
  final: false,
  reopeningEntry: false,
  assignmentStrategy: 299540150,
  unitId: "",
  personId: "",
  activationRule: 299540160,
  requiresDecision: false,
  requiresObservation: false,
  requiresFile: false,
  allowsRequesterReturn: true,
  targetDays: null as number | null,
  active: true,
  positionX: null as number | null,
  positionY: null as number | null,
};
const blankRoute = {
  code: "",
  sourceStepId: "",
  targetStepId: "",
  requiredResult: 299540170,
  order: 10,
  active: true,
};

export function SolicitudesWorkflowManager({
  serviceId,
  serviceName,
  service,
  draftFormId,
  formReady,
  onPublished,
  units,
  people,
  canEdit,
}: {
  serviceId: string;
  serviceName: string;
  service: PublishableService;
  draftFormId: string | null;
  formReady: boolean;
  onPublished: () => Promise<void>;
  units: Unit[];
  people: Person[];
  canEdit: boolean;
}) {
  const { notify } = useFeedback();
  const [items, setItems] = useState<Summary[]>([]),
    [selected, setSelected] = useState(""),
    [definition, setDefinition] = useState<Definition | null>(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false),
    [draftName, setDraftName] = useState(""),
    [draftDescription, setDraftDescription] = useState(""),
    [creatingWorkflow, setCreatingWorkflow] = useState(false),
    [step, setStep] = useState<Step | typeof blankStep | null>(null),
    [assignmentStep, setAssignmentStep] = useState<Step | null>(null),
    [viewStep, setViewStep] = useState<Step | null>(null),
    [viewStepForm, setViewStepForm] = useState<StageForm | null>(null),
    [viewStepFormLoading, setViewStepFormLoading] = useState(false),
    [route, setRoute] = useState<Route | typeof blankRoute | null>(null),
    [formStep, setFormStep] = useState<Step | null>(null),
    [confirmPublish, setConfirmPublish] = useState(false),
    [workflow, setWorkflow] = useState<Summary | null>(null),
    [deleteWorkflowTarget, setDeleteWorkflowTarget] = useState<Summary | null>(null),
    [deleteStepTarget, setDeleteStepTarget] = useState<Step | null>(null),
    [deleteRouteTarget, setDeleteRouteTarget] = useState<Route | null>(null),
    [designerExpanded, setDesignerExpanded] = useState(false);
  async function load(preferred?: string) {
    const values = await apiRequest<Summary[]>(
      `/api/solicitudes/administration/services/${serviceId}/workflows`,
    );
    setItems(values);
    setSelected((current) => preferred || current || values[0]?.id || "");
  }
  function openPublishedStep(item: Step) {
    setViewStepForm(null);
    setViewStepFormLoading(true);
    setViewStep(item);
  }
  function closePublishedStep() {
    setViewStep(null);
    setViewStepForm(null);
    setViewStepFormLoading(false);
  }
  useEffect(() => {
    let live = true;
    apiRequest<Summary[]>(
      `/api/solicitudes/administration/services/${serviceId}/workflows`,
    )
      .then((workflows) => {
        if (live) {
          setItems(workflows);
          setSelected(workflows[0]?.id || "");
          if (!workflows.length) setDefinition(null);
        }
      })
      .catch((e) => {
        if (live) setError(message(e));
      });
    return () => {
      live = false;
    };
  }, [serviceId]);
  useEffect(() => {
    if (!selected) return;
    let live = true;
    apiRequest<Definition>(
      `/api/solicitudes/administration/workflows/${selected}`,
    )
      .then((x) => {
        if (live) setDefinition(x);
      })
      .catch((e) => {
        if (live) setError(message(e));
      });
    return () => {
      live = false;
    };
  }, [selected]);
  useEffect(() => {
    if (!viewStep) return;
    let live = true;
    apiRequest<StageForm | undefined>(`/api/solicitudes/administration/workflow-steps/${viewStep.id}/form`, { cache: "no-store" })
      .then((value) => { if (live) setViewStepForm(value ?? null); })
      .catch((reason) => { if (live) setError(message(reason)); })
      .finally(() => { if (live) setViewStepFormLoading(false); });
    return () => { live = false; };
  }, [viewStep]);
  async function createDraft(e: FormEvent) {
    e.preventDefault();
    await run(async () => {
      const value = await apiRequest<{ id: string }>(
        "/api/solicitudes/administration/workflows",
        {
          method: "POST",
          body: JSON.stringify({
            serviceId,
            name: draftName,
            description: draftDescription.trim() || null,
          }),
        },
      );
      setDraftName("");
      setDraftDescription("");
      setCreatingWorkflow(false);
      await load(value.id);
    });
  }
  function startEditableVersion() {
    const current = items.find((item) => item.id === definition?.id);
    setDraftName(current?.name || `Flujo de ${serviceName}`);
    setDraftDescription(current?.description || "");
    setCreatingWorkflow(true);
  }
  async function saveWorkflow(e:FormEvent){e.preventDefault();if(!workflow)return;await run(async()=>{await apiRequest(`/api/solicitudes/administration/workflows/${workflow.id}`,{method:"PUT",body:JSON.stringify({name:workflow.name,description:workflow.description})});setWorkflow(null);await load(workflow.id);setDefinition(await apiRequest<Definition>(`/api/solicitudes/administration/workflows/${workflow.id}`));notify({tone:"success",title:"Flujo actualizado",description:"El nombre y la descripción quedaron guardados."});});}
  async function deleteWorkflow(){if(!deleteWorkflowTarget)return;const target=deleteWorkflowTarget;await run(async()=>{await apiRequest(`/api/solicitudes/administration/workflows/${target.id}`,{method:"DELETE"});setDeleteWorkflowTarget(null);setDefinition(null);setSelected("");await load();notify({tone:"success",title:"Flujo eliminado",description:"El borrador y sus etapas dejaron de aparecer en la configuración."});});}
  async function saveStep(e: FormEvent) {
    e.preventDefault();
    if (!definition || !step) return;
    const updating = "id" in step;
    await run(async () => {
      const body = {
        ...step,
        type: step.final ? 299540144 : step.requiresDecision ? 299540142 : 299540140,
        unitId:
          step.assignmentStrategy === 299540150 ? step.unitId || null : null,
        personId:
          step.assignmentStrategy === 299540151 ? step.personId || null : null,
      };
      await apiRequest(
        `/api/solicitudes/administration/workflows/${definition.id}/steps${updating ? `/${step.id}` : ""}`,
        { method: updating ? "PUT" : "POST", body: JSON.stringify(body) },
      );
      setStep(null);
      setDefinition(
        await apiRequest<Definition>(
          `/api/solicitudes/administration/workflows/${definition.id}`,
        ),
      );
      await load();
      notify({
        tone: "success",
        title: updating ? "Etapa actualizada" : "Etapa creada",
        description: "El recorrido muestra la configuración guardada.",
      });
    });
  }
  async function savePublishedAssignment(e:FormEvent){e.preventDefault();if(!definition||!assignmentStep)return;await run(async()=>{await apiRequest(`/api/solicitudes/administration/workflows/${definition.id}/steps/${assignmentStep.id}/assignment`,{method:"PUT",body:JSON.stringify({assignmentStrategy:assignmentStep.assignmentStrategy,unitId:assignmentStep.assignmentStrategy===299540150?assignmentStep.unitId:null,personId:assignmentStep.assignmentStrategy===299540151?assignmentStep.personId:null})});setAssignmentStep(null);setDefinition(await apiRequest<Definition>(`/api/solicitudes/administration/workflows/${definition.id}`));notify({tone:"success",title:"Asignación actualizada",description:"Las gestiones activas se reasignaron y las que estaban tomadas volvieron a quedar disponibles."});});}
  async function saveRoute(e: FormEvent) {
    e.preventDefault();
    if (!definition || !route) return;
    const updating = "id" in route;
    const source=definition.steps.find(item=>item.id===route.sourceStepId),target=definition.steps.find(item=>item.id===route.targetStepId);
    const requiredResult=source?.requiresDecision
      ? [299540171,299540172].includes(route.requiredResult)?route.requiredResult:299540171
      : 299540170;
    const resultCode=requiredResult===299540171?"APROBADO":requiredResult===299540172?"RECHAZADO":"COMPLETADO";
    const normalized={...route,requiredResult,code:`${source?.code??"ETAPA"}_A_${target?.code??"ETAPA"}_${resultCode}`.slice(0,80),order:updating?route.order:(definition.routes.length+1)*10};
    const duplicate=definition.routes.find(item=>item.active&&(!updating||item.id!==route.id)&&item.sourceStepId===normalized.sourceStepId&&item.targetStepId===normalized.targetStepId&&item.requiredResult===normalized.requiredResult);
    if(duplicate){const detail="Ya existe una conexión con el mismo origen, destino y resultado.";setError(detail);notify({tone:"error",title:"Conexión duplicada",description:detail});return;}
    await run(async () => {
      await apiRequest(
        `/api/solicitudes/administration/workflows/${definition.id}/routes${updating ? `/${route.id}` : ""}`,
        { method: updating ? "PUT" : "POST", body: JSON.stringify(normalized) },
      );
      setRoute(null);
      setDefinition(
        await apiRequest<Definition>(
          `/api/solicitudes/administration/workflows/${definition.id}`,
        ),
      );
      await load();
      notify({
        tone: "success",
        title: updating ? "Conexión actualizada" : "Conexión creada",
        description: "La decisión ya aparece en el recorrido.",
      });
    });
  }
  async function deleteRoute(item:Route){if(!definition)return;await run(async()=>{await apiRequest(`/api/solicitudes/administration/workflows/${definition.id}/routes/${item.id}`,{method:"DELETE"});setDeleteRouteTarget(null);setDefinition(await apiRequest<Definition>(`/api/solicitudes/administration/workflows/${definition.id}`));await load(definition.id);notify({tone:"success",title:"Conexión eliminada",description:"La conexión fue eliminada definitivamente del borrador."});});}
  const saveStepPosition = useCallback(async (stepId:string,position:{x:number;y:number})=>{
    if(!definition||definition.status!==299540130)return;
    const rounded={positionX:Math.max(0,Math.round(position.x)),positionY:Math.max(0,Math.round(position.y))};
    try{
      await apiRequest(`/api/solicitudes/administration/workflows/${definition.id}/steps/${stepId}/position`,{method:"PUT",body:JSON.stringify(rounded)});
      setDefinition(current=>current?{...current,steps:current.steps.map(item=>item.id===stepId?{...item,...rounded}:item)}:current);
    }catch(value){setError(value instanceof Error?value.message:"No fue posible guardar la posición de la etapa.");}
  },[definition]);
  const saveStepPositions=useCallback(async (positions:Array<{id:string;x:number;y:number}>)=>{
    if(!definition||definition.status!==299540130)return;
    try{
      await Promise.all(positions.map(item=>apiRequest(`/api/solicitudes/administration/workflows/${definition.id}/steps/${item.id}/position`,{method:"PUT",body:JSON.stringify({positionX:Math.max(0,Math.round(item.x)),positionY:Math.max(0,Math.round(item.y))})})));
      const byId=new Map(positions.map(item=>[item.id,item]));setDefinition(current=>current?{...current,steps:current.steps.map(item=>{const position=byId.get(item.id);return position?{...item,positionX:Math.max(0,Math.round(position.x)),positionY:Math.max(0,Math.round(position.y))}:item})}:current);
      notify({tone:"success",title:"Diagrama organizado",description:"La distribución automática quedó guardada."});
    }catch(value){const detail=value instanceof Error?value.message:"No fue posible organizar el diagrama.";setError(detail);notify({tone:"error",title:"No fue posible organizar",description:detail});}
  },[definition,notify]);
  async function publish() {
    if (!definition) return;
    await run(async () => {
      if (definition.status === 299540130 && draftFormId) {
        await apiRequest(
          `/api/solicitudes/administration/forms/${draftFormId}/publish`,
          { method: "POST", body: "{}" },
        );
      }
      await apiRequest(
        `/api/solicitudes/administration/workflows/${definition.id}/publish`,
        { method: "POST", body: "{}" },
      );
      setConfirmPublish(false);
      await onPublished();
      await load(definition.id);
      setDefinition(
        await apiRequest<Definition>(
          `/api/solicitudes/administration/workflows/${definition.id}`,
        ),
      );
    });
  }
  async function duplicateStep(item: Step) {
    if (!definition) return;
    await run(async () => {
      await apiRequest(
        `/api/solicitudes/administration/workflows/${definition.id}/steps/${item.id}/duplicate`,
        { method: "POST", body: "{}" },
      );
      setDefinition(
        await apiRequest<Definition>(
          `/api/solicitudes/administration/workflows/${definition.id}`,
        ),
      );
      await load(definition.id);
      notify({
        tone: "success",
        title: "Etapa duplicada",
        description:
          "Se copiaron su configuración y formulario. Define sus conexiones antes de publicar.",
      });
    });
  }
  async function deleteStep(item: Step) {
    if (!definition) return;
    await run(async () => {
      await apiRequest(
        `/api/solicitudes/administration/workflows/${definition.id}/steps/${item.id}`,
        { method: "DELETE" },
      );
      setDefinition(
        await apiRequest<Definition>(
          `/api/solicitudes/administration/workflows/${definition.id}`,
        ),
      );
      await load(definition.id);
      setDeleteStepTarget(null);
      notify({
        tone: "success",
        title: "Etapa eliminada",
        description: "La etapa, su formulario y sus conexiones fueron eliminados definitivamente.",
      });
    });
  }
  async function run(action: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setError("");
    try {
      await action();
    } catch (e) {
      const detail = message(e);
      setError(detail);
      notify({
        tone: "error",
        title: "No fue posible completar la acción",
        description: detail,
      });
    } finally {
      setBusy(false);
    }
  }
  const draft = definition?.status === 299540130,
    routeSource = route && definition ? definition.steps.find(item=>item.id===route.sourceStepId) : null,
    routeNeedsDecision = Boolean(routeSource?.requiresDecision),
    canRepublish = Boolean(canEdit && !service.visible && definition?.status === 299540131),
    checks = definition ? publicationChecks(definition) : [],
    allChecks = [
      { label: "Servicio activo", ok: service.isActive },
      { label: "Formulario configurado", ok: formReady },
      ...checks,
    ],
    ready = allChecks.every((x) => x.ok);
  return (
    <section className="mt-6 rounded-2xl border border-[var(--gaia-line)] bg-[var(--surface-card)] p-5 shadow-sm">
      <header className="grid items-start gap-4 lg:grid-cols-[minmax(0,1fr)_auto]">
        <div className="flex min-w-0 gap-3">
          <span className="grid size-10 place-items-center rounded-xl bg-[var(--gaia-accent-soft)] text-[var(--brand-primary)]">
            <GitBranch size={18} />
          </span>
          <div>
            <small className="font-bold uppercase tracking-widest text-[var(--brand-primary)]">
              Paso 2 de 2 · Flujo y publicación
            </small>
            <h2 className="text-lg font-semibold">
              Diseña cómo se atenderá la solicitud
            </h2>
            <p className="text-xs text-[var(--gaia-ink-500)]">
              Crea las etapas, asigna responsables y conecta el recorrido de{" "}
              {serviceName}.
            </p>
          </div>
        </div>
        <div className="flex max-w-full flex-wrap items-center justify-end gap-2 lg:justify-self-end">
          {items.map((x) => (<div className={`flex items-stretch overflow-hidden rounded-xl border ${selected===x.id?"border-[var(--brand-primary)] bg-[var(--gaia-accent-pale)]":"border-[var(--gaia-line)] bg-white"}`} key={x.id}><button className="px-3 py-2 text-left text-xs" onClick={()=>setSelected(x.id)} type="button"><strong>{x.name} · v{x.version}</strong><span className="block text-[var(--gaia-ink-500)]">{status(x.status)} · {x.stepCount} pasos · {x.routeCount} rutas</span></button>{canEdit&&x.status===299540130&&<span className="flex items-center gap-1 border-l border-[var(--gaia-line)] px-1"><button aria-label={`Editar ${x.name}`} className="rounded-lg p-2 text-[var(--brand-primary)] hover:bg-white" onClick={()=>setWorkflow(x)} title="Editar flujo" type="button"><Pencil size={14}/></button><button aria-label={`Eliminar ${x.name}`} className="rounded-lg p-2 text-[#9a384d] hover:bg-[#fff0f0]" onClick={()=>setDeleteWorkflowTarget(x)} title="Eliminar flujo" type="button"><Trash2 size={14}/></button></span>}</div>))}
          {canEdit && !items.some((x) => x.status === 299540130) && <Action disabled={busy} onClick={items.length?startEditableVersion:()=>setCreatingWorkflow(true)} type="button"><Plus size={14}/>{items.length?"Crear versión editable":"Crear flujo"}</Action>}
        </div>
      </header>
      <div className="mt-5 grid gap-3 md:grid-cols-3">
        {[
          [
            "1",
            "Crea las etapas",
            "Define quién atiende y qué debe completar.",
          ],
          [
            "2",
            "Conecta el recorrido",
            "Indica qué sucede según cada resultado.",
          ],
          ["3", "Valida y publica", "Activa el flujo para solicitudes nuevas."],
        ].map(([number, title, copy]) => (
          <div
            className="rounded-xl border border-[var(--gaia-line)] bg-[var(--gaia-canvas)] p-3"
            key={number}
          >
            <span className="mb-2 inline-grid size-7 place-items-center rounded-full bg-[var(--gaia-accent-soft)] text-xs font-bold text-[var(--brand-primary)]">
              {number}
            </span>
            <strong className="ml-2 text-sm">{title}</strong>
            <p className="mt-1 text-xs text-[var(--gaia-ink-500)]">{copy}</p>
          </div>
        ))}
      </div>
      {error && (
        <p
          className="mt-4 rounded-xl bg-[#fff0f0] p-3 text-sm text-[#9a384d]"
          role="alert"
        >
          {error}
        </p>
      )}
      {!items.length && (
        <div className="mt-5 rounded-2xl border border-dashed border-[var(--gaia-line)] bg-[var(--gaia-canvas)] p-8 text-center">
          <GitBranch className="mx-auto text-[var(--brand-primary)]" />
          <h3 className="mt-3 font-semibold">
            Este servicio aún no tiene un flujo
          </h3>
          <p className="mt-1 text-sm text-[var(--gaia-ink-500)]">
            Crea uno para definir responsables, revisiones, decisiones y cierre.
            Mientras tanto se conserva la gestión tradicional.
          </p>
        </div>
      )}
      {definition && (
        <div className={`mt-5 grid gap-5 ${designerExpanded?"":"xl:grid-cols-[1fr_340px]"}`}>
          <div className="rounded-2xl border border-[var(--gaia-line)] p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="font-semibold">Recorrido de atención</h3>
                <p className="text-xs text-[var(--gaia-ink-500)]">
                  Crea etapas, organízalas y conéctalas directamente en el lienzo.
                </p>
              </div>
              {canEdit && draft && (
                <Action
                  onClick={() =>
                    setStep({
                      ...blankStep,
                      initial: definition.steps.length === 0,
                      order: (definition.steps.length + 1) * 10,
                    })
                  }
                >
                  <Plus size={14} />
                  Agregar etapa
                </Action>
              )}
            </div>
            <WorkflowDiagram canAdminister={canEdit} canEdit={Boolean(canEdit&&draft)} definition={definition} expanded={designerExpanded} onArrange={saveStepPositions} onConnect={(sourceStepId,targetStepId)=>setRoute({...blankRoute,sourceStepId,targetStepId,code:`${name(definition.steps,sourceStepId)}_A_${name(definition.steps,targetStepId)}`.slice(0,80),order:(definition.routes.length+1)*10})} onMoveStep={saveStepPosition} onNodeAction={(action,item)=>{if(action==="form")setFormStep(item);else if(action==="edit")setStep(item);else if(action==="duplicate")void duplicateStep(item);else if(action==="delete")setDeleteStepTarget(item);else if(action==="view")openPublishedStep(item);else if(action==="assignment")setAssignmentStep(item)}} onSelectRoute={item=>setRoute(item)} onToggleExpanded={()=>setDesignerExpanded(value=>!value)} people={people} published={definition.status===299540131} units={units}/>
          </div>
          <aside className="h-fit rounded-2xl border border-[var(--gaia-line)] bg-[var(--gaia-canvas)] p-4">
            <div className="flex items-center gap-2">
              <ShieldCheck className="text-[var(--brand-primary)]" size={20} />
              <h3 className="font-semibold">Preparación para publicar</h3>
            </div>
            <p className="mt-1 text-xs text-[var(--gaia-ink-500)]">
              Revisa lo necesario antes de activar esta versión.
            </p>
            <ul className={`mt-4 gap-3 ${designerExpanded ? "grid md:grid-cols-2 xl:grid-cols-3" : "space-y-3"}`}>
              {allChecks.map((x) => (
                <li className="flex gap-2 text-sm" key={x.label}>
                  {x.ok ? (
                    <CheckCircle2
                      className="shrink-0 text-[#317a4c]"
                      size={18}
                    />
                  ) : (
                    <Circle
                      className="shrink-0 text-[var(--gaia-ink-500)]"
                      size={18}
                    />
                  )}
                  <span>{x.label}</span>
                </li>
              ))}
            </ul>
            {canEdit && (draft || canRepublish) && (
              <Action
                className="mt-5 w-full justify-center"
                disabled={busy || !ready}
                onClick={() => setConfirmPublish(true)}
              >
                <Send size={14} />
                {canRepublish ? "Volver a publicar" : "Revisar y publicar"}
              </Action>
            )}
            {canRepublish && !items.some((item) => item.status === 299540130) && (
              <div className="mt-3 rounded-xl border border-[#c8ddd7] bg-white p-3 text-center">
                <p className="text-xs leading-5 text-[var(--gaia-ink-500)]">
                  ¿Necesitas cambiar etapas o conexiones? Crea una versión editable; se copiará este flujo completo y la versión publicada conservará su historial.
                </p>
                <button className="mt-3 inline-flex items-center gap-2 rounded-xl border border-[var(--brand-primary)] px-3 py-2 text-xs font-semibold text-[var(--brand-primary)]" disabled={busy} onClick={startEditableVersion} type="button"><Copy size={14}/>Crear versión editable</button>
              </div>
            )}
            {draft && !ready && (
              <p className="mt-2 text-center text-xs text-[var(--gaia-ink-500)]">
                Completa los puntos pendientes para publicar.
              </p>
            )}
            {!draft && !canRepublish && (
              <p className="mt-5 rounded-xl bg-[var(--gaia-accent-soft)] p-3 text-center text-xs font-semibold text-[var(--brand-primary)]">
                Esta versión está {status(definition.status).toLowerCase()}. Su
                configuración, etapas y formularios son de solo lectura.
              </p>
            )}
          </aside>
        </div>
      )}
      {step && definition && (
        <Dialog
          title={"id" in step ? "Editar etapa" : "Nueva etapa"}
          close={() => setStep(null)}
          submit={saveStep}
          busy={busy}
        >
          <p className="mt-1 text-sm text-[var(--gaia-ink-500)]">
            Define qué ocurre en esta parte del proceso y quién debe atenderla.
          </p>
          <StageGuide />
          <Grid>
            <Input
              help="Ej.: Revisión inicial o Aprobación de gerencia."
              label="Nombre de la etapa"
            >
              <input
                onChange={(e) =>
                  setStep({ ...step, code: workflowCode(e.target.value) })
                }
                placeholder="Ej. Revisión inicial"
                required
                value={friendly(step.code)}
              />
            </Input>
            <Input
              help={assignmentHelp(step.assignmentStrategy)}
              info={<ContextHelp content={helpTopics.assignment} />}
              label="¿Quién atenderá esta etapa?"
            >
              <select
                value={step.assignmentStrategy}
                onChange={(e) =>
                  setStep({
                    ...step,
                    assignmentStrategy: Number(e.target.value),
                    unitId: "",
                    personId: "",
                  })
                }
              >
                <option value={299540150}>Una unidad organizacional</option>
                <option value={299540151}>Una persona específica</option>
                <option value={299540152}>
                  El responsable actual de la solicitud
                </option>
              </select>
            </Input>
            {step.assignmentStrategy === 299540150 && (
              <Input
                help="Busca la unidad por código o nombre."
                label="Unidad responsable"
              >
                <OrganizationalUnitPicker
                  onChange={(unitId) => setStep({ ...step, unitId })}
                  required
                  units={units}
                  value={step.unitId ?? ""}
                />
              </Input>
            )}
            {step.assignmentStrategy === 299540151 && (
              <Input
                help="Busca la persona por nombre o unidad."
                label="Persona responsable"
              >
                <PersonPicker
                  onChange={(personId) => setStep({ ...step, personId })}
                  people={people}
                  required
                  units={units}
                  value={step.personId ?? ""}
                />
              </Input>
            )}
            {!step.initial && (
              <Input
                help="Cualquier conexión: caminos alternativos. Todas: trabajos paralelos."
                info={<ContextHelp content={helpTopics.activation} />}
                label="¿Cuándo se activa?"
              >
                <select
                  value={step.activationRule}
                  onChange={(e) =>
                    setStep({ ...step, activationRule: Number(e.target.value) })
                  }
                >
                  <option value={299540160}>
                    Cuando llegue cualquier conexión
                  </option>
                  <option value={299540161}>
                    Cuando se cumplan todas las conexiones
                  </option>
                </select>
              </Input>
            )}
            <Input
              help="Déjalo vacío si la etapa no tiene plazo propio."
              label="Tiempo objetivo (días hábiles)"
            >
              <input
                min={1}
                placeholder="Sin plazo específico"
                type="number"
                value={step.targetDays ?? ""}
                onChange={(e) =>
                  setStep({
                    ...step,
                    targetDays: e.target.value ? Number(e.target.value) : null,
                  })
                }
              />
            </Input>
          </Grid>
          <fieldset className="mt-5">
            <legend className="flex items-center gap-2 font-semibold">
              <StepNumber number="4" />
              Define el papel de la etapa{" "}
              <ContextHelp content={helpTopics.behavior} />
            </legend>
            <p className="mt-1 text-xs text-[var(--gaia-ink-500)]">
              Primero indica dónde participa dentro del recorrido.
            </p>
            <div className="mt-2 grid gap-2 sm:grid-cols-2">
              {stepOptions
                .filter((option) =>
                  ["initial", "final", "reopeningEntry"].includes(option.key),
                )
                .map((option) => (
                  <StageOption
                    key={option.key}
                    option={option}
                    step={step}
                    update={(checked) =>
                      setStep({ ...step, [option.key]: checked })
                    }
                  />
                ))}
            </div>
          </fieldset>
          {step.initial && step.final && (
            <div className="mt-4 rounded-xl border border-[var(--brand-primary)] bg-[var(--gaia-accent-pale)] p-4">
              <strong className="text-sm text-[var(--brand-primary)]">
                Flujo de una sola etapa
              </strong>
              <p className="mt-1 text-xs leading-5 text-[var(--gaia-ink-500)]">
                Esta etapa recibe la solicitud y también termina el proceso. No
                necesita conexiones. Es válida, por ejemplo, para una única
                aprobación de coordinación.
              </p>
            </div>
          )}
          <fieldset className="mt-5">
            <legend className="font-semibold">
              Define qué se exige para completarla
            </legend>
            <p className="mt-1 text-xs text-[var(--gaia-ink-500)]">
              Define las acciones disponibles y cuáles evidencias serán obligatorias.
            </p>
            <div className="mt-3">
              <strong className="text-sm">¿Cómo se completa esta etapa?</strong>
              <div className="mt-2 grid gap-2 sm:grid-cols-2">
                <label className={`flex cursor-pointer gap-3 rounded-xl border p-3 ${!step.requiresDecision ? "border-[var(--brand-primary)] bg-[var(--gaia-accent-pale)]" : "border-[var(--gaia-line)]"}`}>
                  <input checked={!step.requiresDecision} name="completion-mode" onChange={()=>setStep({...step,requiresDecision:false})} type="radio" />
                  <span><strong className="block text-sm">Completar</strong><small className="text-[var(--gaia-ink-500)]">Muestra una única acción para registrar que el trabajo terminó.</small></span>
                </label>
                <label className={`flex cursor-pointer gap-3 rounded-xl border p-3 ${step.requiresDecision ? "border-[var(--brand-primary)] bg-[var(--gaia-accent-pale)]" : "border-[var(--gaia-line)]"}`}>
                  <input checked={step.requiresDecision} name="completion-mode" onChange={()=>setStep({...step,requiresDecision:true})} type="radio" />
                  <span><strong className="block text-sm">Aprobar o rechazar</strong><small className="text-[var(--gaia-ink-500)]">Muestra dos acciones y permite dirigir el recorrido según la decisión.</small></span>
                </label>
              </div>
            </div>
            <div className="mt-2 grid gap-2 sm:grid-cols-2">
              {stepOptions
                .filter(
                  (option) =>
                    !["initial", "final", "reopeningEntry", "requiresDecision"].includes(
                      option.key,
                    ),
                )
                .map((option) => (
                  <StageOption
                    key={option.key}
                    option={option}
                    step={step}
                    update={(checked) =>
                      setStep({ ...step, [option.key]: checked })
                    }
                  />
                ))}
            </div>
          </fieldset>
          <p className="mt-4 rounded-xl bg-[var(--gaia-accent-pale)] p-3 text-xs text-[var(--gaia-ink-500)]">
            Después de guardar, puedes mover esta etapa libremente dentro del
            diseñador y conectarla con las demás.
          </p>
        </Dialog>
      )}
      {assignmentStep&&definition&&<Dialog busy={busy} close={()=>setAssignmentStep(null)} compact submit={savePublishedAssignment} title="Cambiar asignación de la etapa"><p className="mt-1 text-sm text-[var(--gaia-ink-500)]">Este cambio actualizará la etapa publicada y reasignará sus gestiones activas. Las gestiones que ya estaban tomadas volverán a quedar disponibles.</p><div className="mt-5 grid gap-5"><Input label="¿Quién atenderá esta etapa?"><select value={assignmentStep.assignmentStrategy} onChange={e=>setAssignmentStep({...assignmentStep,assignmentStrategy:Number(e.target.value),unitId:null,personId:null})}><option value={299540150}>Una unidad organizacional</option><option value={299540151}>Una persona específica</option><option value={299540152}>El responsable actual de la solicitud</option></select></Input>{assignmentStep.assignmentStrategy===299540150&&<Input label="Unidad responsable"><OrganizationalUnitPicker onChange={unitId=>setAssignmentStep({...assignmentStep,unitId})} required units={units} value={assignmentStep.unitId??""}/></Input>}{assignmentStep.assignmentStrategy===299540151&&<Input label="Persona responsable"><PersonPicker onChange={personId=>setAssignmentStep({...assignmentStep,personId})} people={people} required units={units} value={assignmentStep.personId??""}/></Input>}</div></Dialog>}
      {viewStep&&<div className="fixed inset-0 z-[85] grid place-items-center bg-black/40 p-4"><section className="max-h-[92vh] w-full max-w-3xl overflow-auto rounded-3xl bg-white p-6 shadow-2xl"><header className="flex items-start justify-between"><div><small className="font-bold uppercase tracking-widest text-[var(--brand-primary)]">Configuración publicada</small><h2 className="mt-1 text-2xl font-semibold">{friendly(viewStep.code)}</h2></div><button aria-label="Cerrar" className="rounded-full border p-2" onClick={closePublishedStep}><X size={18}/></button></header><dl className="mt-5 grid gap-3 sm:grid-cols-2"><Fact label="Tipo" value={stepType(viewStep.type)}/><Fact label="Asignación" value={assignmentDetail(viewStep,units,people)}/><Fact label="Activación" value={viewStep.activationRule===299540161?"Espera todas las conexiones":"Se activa con cualquier conexión"}/><Fact label="Plazo" value={viewStep.targetDays?`${viewStep.targetDays} días hábiles`:"Sin plazo propio"}/><Fact label="Participación" value={[viewStep.initial&&"Inicial",viewStep.final&&"Final",viewStep.reopeningEntry&&"Reapertura"].filter(Boolean).join(" · ")||"Intermedia"}/><Fact label="Requisitos" value={[viewStep.requiresDecision&&"Decisión",viewStep.requiresObservation&&"Observación",viewStep.requiresFile&&"Archivo",viewStep.allowsRequesterReturn&&"Devolución"].filter(Boolean).join(" · ")||"Sin requisitos adicionales"}/></dl><div className="mt-5 rounded-2xl border border-[var(--gaia-line)] p-4"><div><h3 className="font-semibold">Formulario de la etapa</h3>{viewStepForm?.instructions&&<p className="mt-1 text-xs text-[var(--gaia-ink-500)]">{viewStepForm.instructions}</p>}</div>{viewStepFormLoading?<p className="mt-4 text-sm text-[var(--gaia-ink-500)]">Cargando campos configurados…</p>:viewStepForm?.fields.length?<ol className="mt-4 space-y-2">{[...viewStepForm.fields].sort((a,b)=>a.order-b.order).map((field,index)=><li className="rounded-xl bg-[var(--gaia-canvas)] p-3" key={field.id}><div className="flex items-start gap-3"><span className="grid size-7 shrink-0 place-items-center rounded-full bg-[var(--gaia-accent-soft)] text-xs font-bold text-[var(--brand-primary)]">{index+1}</span><div className="min-w-0"><strong className="block text-sm">{field.label}</strong><span className="mt-1 block text-xs text-[var(--gaia-ink-500)]">{stageFieldType(field.dataType,field.controlType)} · {field.required?"Obligatorio":"Opcional"} · {field.visible?"Visible":"Oculto"} · {field.width===12?"Fila completa":`${field.width}/12 de fila`}</span>{field.helpText&&<span className="mt-1 block text-xs text-[var(--gaia-ink-500)]">{field.helpText}</span>}{field.options.length>0&&<span className="mt-1 block text-xs text-[var(--gaia-ink-500)]">Opciones: {[...field.options].sort((a,b)=>a.order-b.order).map(option=>option.label).join(" · ")}</span>}</div></div></li>)}</ol>:<p className="mt-4 rounded-xl border border-dashed p-4 text-center text-sm text-[var(--gaia-ink-500)]">Esta etapa no tiene campos configurados.</p>}</div><button className="mt-6 w-full rounded-xl bg-[var(--brand-primary)] px-4 py-2 font-semibold text-white" onClick={closePublishedStep}>Cerrar</button></section></div>}
      {workflow&&<Dialog busy={busy} close={()=>setWorkflow(null)} compact submit={saveWorkflow} title="Editar flujo"><p className="mt-1 text-sm text-[var(--gaia-ink-500)]">Actualiza la identificación del flujo en borrador.</p><div className="mt-6 grid gap-5"><Input label="Nombre del flujo"><input maxLength={200} onChange={event=>setWorkflow({...workflow,name:event.target.value})} required value={workflow.name}/></Input><Input label="Descripción"><textarea maxLength={1000} onChange={event=>setWorkflow({...workflow,description:event.target.value})} placeholder="Describe brevemente el propósito de este flujo" rows={4} value={workflow.description??""}/></Input></div></Dialog>}
      {creatingWorkflow&&<Dialog busy={busy} close={()=>setCreatingWorkflow(false)} compact submit={createDraft} title={items.length?"Crear nueva versión":"Crear flujo"}><p className="mt-1 text-sm text-[var(--gaia-ink-500)]">Define cómo identificarás este recorrido de atención.</p><div className="mt-6 grid gap-5"><Input label="Nombre del flujo"><input autoFocus id={`workflow-name-${serviceId}`} maxLength={200} onChange={event=>setDraftName(event.target.value)} placeholder="Ej. Atención estándar" required value={draftName}/></Input><Input label="Descripción"><textarea maxLength={1000} onChange={event=>setDraftDescription(event.target.value)} placeholder="Describe brevemente el propósito de este flujo" rows={4} value={draftDescription}/></Input></div></Dialog>}
      <ConfirmDialog confirmLabel="Eliminar flujo" description={deleteWorkflowTarget?`Se eliminará el borrador «${deleteWorkflowTarget.name}», junto con sus etapas y conexiones. Esta acción no afecta versiones publicadas.`:""} destructive loading={busy} onCancel={()=>setDeleteWorkflowTarget(null)} onConfirm={()=>void deleteWorkflow()} open={Boolean(deleteWorkflowTarget)} title="¿Eliminar este flujo en borrador?"/>
      <ConfirmDialog confirmLabel="Eliminar etapa definitivamente" description={deleteStepTarget?`Se eliminarán definitivamente la etapa «${friendly(deleteStepTarget.code)}», su formulario, sus campos, opciones y todas sus conexiones. Esta acción no se puede deshacer.`:""} destructive loading={busy} onCancel={()=>setDeleteStepTarget(null)} onConfirm={()=>{if(deleteStepTarget)void deleteStep(deleteStepTarget)}} open={Boolean(deleteStepTarget)} title="¿Eliminar definitivamente esta etapa?"/>
      <ConfirmDialog confirmLabel="Eliminar conexión definitivamente" description={deleteRouteTarget?`Se eliminará definitivamente la conexión «${friendly(name(definition?.steps??[],deleteRouteTarget.sourceStepId))} → ${friendly(name(definition?.steps??[],deleteRouteTarget.targetStepId))}». Esta acción no se puede deshacer.`:""} destructive loading={busy} onCancel={()=>setDeleteRouteTarget(null)} onConfirm={()=>{if(deleteRouteTarget)void deleteRoute(deleteRouteTarget)}} open={Boolean(deleteRouteTarget)} title="¿Eliminar definitivamente esta conexión?"/>
      {route && definition && (
        <Dialog
          title={"id" in route ? "Editar conexión" : "Nueva conexión"}
          close={() => setRoute(null)}
          submit={saveRoute}
          busy={busy}
          compact
        >
          {"id" in route&&draft&&<button className="mb-4 inline-flex items-center gap-2 rounded-xl border border-[#dba6a1] px-3 py-2 text-xs font-semibold text-[#9a384d] hover:bg-[#fff0f0]" onClick={()=>{setDeleteRouteTarget(route);setRoute(null)}} type="button"><Trash2 size={14}/>Eliminar conexión</button>}
          <Grid>
            <Input label="Desde">
              <select
                required
                value={route.sourceStepId}
                onChange={(e) => {
                  const source=definition.steps.find(item=>item.id===e.target.value);
                  setRoute({ ...route, sourceStepId: e.target.value, requiredResult: source?.requiresDecision ? 299540171 : 299540170 });
                }}
              >
                <option value="">Seleccionar</option>
                {definition.steps.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.code}
                  </option>
                ))}
              </select>
            </Input>
            <Input label="Hacia">
              <select
                required
                value={route.targetStepId}
                onChange={(e) =>
                  setRoute({ ...route, targetStepId: e.target.value })
                }
              >
                <option value="">Seleccionar</option>
                {definition.steps.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.code}
                  </option>
                ))}
              </select>
            </Input>
            {routeNeedsDecision && <Input help="La conexión se activará únicamente con esta decisión de la etapa anterior." label="Continuar cuando">
              <select value={[299540171,299540172].includes(route.requiredResult)?route.requiredResult:299540171} onChange={(e)=>setRoute({...route,requiredResult:Number(e.target.value)})}>
                <option value={299540171}>Se apruebe</option>
                <option value={299540172}>Se rechace</option>
              </select>
            </Input>}
          </Grid>
        </Dialog>
      )}
      {formStep && (
        <SolicitudesStageFormDesigner
          onClose={() => setFormStep(null)}
          readOnly={!canEdit || !draft || service.visible}
          stepId={formStep.id}
          stepName={friendly(formStep.code)}
        />
      )}
      {confirmPublish && definition && (
        <div className="fixed inset-0 z-[70] grid place-items-center bg-black/35 p-4">
          <div className="w-full max-w-lg rounded-3xl bg-[var(--surface-card)] p-6 shadow-2xl">
            <span className="grid size-11 place-items-center rounded-full bg-[var(--gaia-accent-soft)] text-[var(--brand-primary)]">
              <Send size={19} />
            </span>
            <h2 className="mt-4 text-xl font-semibold">
              {canRepublish ? "Volver a publicar el servicio" : `Publicar versión ${definition.version}`}
            </h2>
            <p className="mt-2 text-sm text-[var(--gaia-ink-500)]">
              {canRepublish
                ? "Se reactivará la versión publicada vigente sin crear copias ni modificar las solicitudes existentes. El servicio volverá a estar disponible para solicitudes nuevas."
                : "Se publicarán juntos la configuración, el formulario y este flujo. El servicio quedará disponible para solicitudes nuevas y las versiones publicadas se conservarán como historial."}
            </p>
            <div className="mt-5 flex justify-end gap-2">
              <button
                className="rounded-xl border px-4 py-2 text-sm"
                onClick={() => setConfirmPublish(false)}
              >
                Seguir editando
              </button>
              <Action disabled={busy} onClick={() => void publish()}>
                {busy ? "Publicando servicio…" : canRepublish ? "Volver a publicar" : "Publicar servicio completo"}
              </Action>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
function Dialog({
  title,
  close,
  submit,
  busy,
  compact=false,
  children,
}: {
  title: string;
  close: () => void;
  submit: (e: FormEvent) => void;
  busy: boolean;
  compact?: boolean;
  children: React.ReactNode;
}) {
  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-black/35 p-2 sm:p-4">
      <form
        className={`max-h-[94vh] overflow-x-hidden overflow-y-auto rounded-3xl bg-[var(--surface-card)] p-5 pb-0 shadow-2xl sm:p-6 sm:pb-0 ${compact?"w-[min(94vw,38rem)]":"w-[min(96vw,68rem)] lg:p-7 lg:pb-0"}`}
        onSubmit={submit}
      >
        <h2 className="text-2xl font-semibold">{title}</h2>
        <div className={compact?"pb-6":"pb-20"}>{children}</div>
        <div className={`sticky bottom-0 z-10 -mx-5 flex justify-end gap-2 border-t border-[var(--gaia-line)] bg-[var(--surface-card)] px-5 py-3 shadow-[0_-10px_24px_rgba(18,63,72,.08)] sm:-mx-6 sm:px-6 ${compact?"":"lg:-mx-7 lg:px-7"}`}>
          <button
            className="rounded-xl border px-4 py-2 disabled:opacity-60"
            disabled={busy}
            onClick={close}
            type="button"
          >
            Cancelar
          </button>
          <Action disabled={busy} type="submit">
            {busy ? "Guardando cambios…" : "Guardar cambios"}
          </Action>
        </div>
      </form>
    </div>
  );
}
function Grid({ children }: { children: React.ReactNode }) {
  return (
    <div className="mt-4 grid items-start gap-x-6 gap-y-4 md:grid-cols-2">{children}</div>
  );
}
function Input({
  label,
  help,
  info,
  children,
}: {
  label: string;
  help?: string;
  info?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="flex h-full flex-col text-xs font-semibold">
      <div className="flex items-center gap-1.5">
        <span>{label}</span>
        {info}
      </div>
      <div className="mt-2 [&>input]:h-11 [&>input]:w-full [&>input]:rounded-xl [&>input]:border [&>input]:border-[var(--gaia-line-strong)] [&>input]:px-3 [&>input]:outline-none focus-within:[&>input]:border-[var(--brand-primary)] [&>select]:h-11 [&>select]:w-full [&>select]:rounded-xl [&>select]:border [&>select]:px-3 [&>textarea]:w-full [&>textarea]:resize-y [&>textarea]:rounded-xl [&>textarea]:border [&>textarea]:border-[var(--gaia-line-strong)] [&>textarea]:px-3 [&>textarea]:py-3 [&>textarea]:font-normal [&>textarea]:outline-none focus-within:[&>textarea]:border-[var(--brand-primary)]">
        {children}
      </div>
      {help && (
        <small className="mt-1.5 block font-normal leading-4 text-[11px] text-[var(--gaia-ink-500)]">
          {help}
        </small>
      )}
    </div>
  );
}
function StageGuide() {
  return (
    <ol className="mt-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
      {[
        ["1", "Identifica", "Nombre de la etapa"],
        ["2", "Asigna", "Unidad o persona"],
        ["3", "Programa", "Activación y plazo"],
        ["4", "Condiciona", "Inicio, cierre y requisitos"],
      ].map(([number, title, help]) => (
        <li
          className="rounded-xl border border-[var(--gaia-line)] px-3 py-2.5"
          key={number}
        >
          <span className="flex items-center gap-2">
            <StepNumber number={number} />
            <strong className="text-sm">{title}</strong>
          </span>
          <small className="mt-1 block pl-8 text-[11px] leading-4 text-[var(--gaia-ink-500)]">
            {help}
          </small>
        </li>
      ))}
    </ol>
  );
}
function StepNumber({ number }: { number: string }) {
  return (
    <span className="inline-grid size-6 shrink-0 place-items-center rounded-full bg-[var(--gaia-accent-soft)] text-xs font-bold text-[var(--brand-primary)]">
      {number}
    </span>
  );
}
function StageOption({
  option,
  step,
  update,
}: {
  option: (typeof stepOptions)[number];
  step: Step | typeof blankStep;
  update: (checked: boolean) => void;
}) {
  return (
    <label className="flex gap-3 rounded-xl border border-[var(--gaia-line)] p-2.5">
      <input
        checked={Boolean(step[option.key])}
        className="mt-1"
        onChange={(event) => update(event.target.checked)}
        type="checkbox"
      />
      <span>
        <strong className="block text-sm">{option.label}</strong>
        <small className="block text-[11px] leading-4 text-[var(--gaia-ink-500)]">{option.help}</small>
      </span>
    </label>
  );
}
type HelpContent = {
  title: string;
  summary: string;
  example: string;
  recommendation: string;
};
function ContextHelp({ content }: { content: HelpContent }) {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button
        aria-label={`Ayuda sobre ${content.title}`}
        className="inline-grid size-6 place-items-center rounded-full text-[var(--brand-primary)] transition hover:bg-[var(--gaia-accent-soft)]"
        onClick={() => setOpen(true)}
        title="Ver explicación y ejemplo"
        type="button"
      >
        <HelpCircle size={16} />
      </button>
      {open && (
        <div
          className="fixed inset-0 z-[90] grid place-items-center bg-black/35 p-4"
          onMouseDown={(event) => {
            if (event.currentTarget === event.target) setOpen(false);
          }}
        >
          <article
            aria-modal="true"
            className="w-full max-w-lg rounded-3xl bg-[var(--surface-card)] p-6 text-left shadow-2xl"
            role="dialog"
          >
            <header className="flex items-start justify-between gap-3">
              <div>
                <small className="font-bold uppercase tracking-widest text-[var(--brand-primary)]">
                  Ayuda para configurar
                </small>
                <h3 className="mt-1 text-xl font-semibold">{content.title}</h3>
              </div>
              <button
                aria-label="Cerrar ayuda"
                className="grid size-9 place-items-center rounded-full border border-[var(--gaia-line)]"
                onClick={() => setOpen(false)}
                type="button"
              >
                <X size={17} />
              </button>
            </header>
            <p className="mt-4 text-sm font-normal leading-6 text-[var(--gaia-ink-500)]">
              {content.summary}
            </p>
            <div className="mt-4 rounded-2xl border border-[var(--gaia-line)] bg-[var(--gaia-canvas)] p-4">
              <strong className="text-sm">Ejemplo</strong>
              <pre className="mt-2 whitespace-pre-wrap font-sans text-sm font-normal leading-6">
                {content.example}
              </pre>
            </div>
            <div className="mt-4 rounded-xl bg-[var(--gaia-accent-pale)] p-3">
              <strong className="text-sm text-[var(--brand-primary)]">
                Recomendación
              </strong>
              <p className="mt-1 text-sm font-normal text-[var(--gaia-ink-500)]">
                {content.recommendation}
              </p>
            </div>
            <button
              className="mt-5 w-full rounded-xl bg-[var(--brand-primary)] px-4 py-2.5 text-sm font-semibold text-white"
              onClick={() => setOpen(false)}
              type="button"
            >
              Entendido
            </button>
          </article>
        </div>
      )}
    </>
  );
}
function Action({
  className = "",
  children,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      className={`inline-flex items-center gap-2 rounded-xl bg-[var(--brand-primary)] px-4 py-2 text-xs font-semibold text-white disabled:opacity-50 ${className}`}
      type={props.type ?? "button"}
      {...props}
    >
      {children}
    </button>
  );
}
function Badge({ children }: { children: React.ReactNode }) {
  return (
    <small className="rounded-full bg-[var(--gaia-accent-soft)] px-2 py-1 font-semibold text-[var(--brand-primary)]">
      {children}
    </small>
  );
}
function Fact({label,value}:{label:string;value:string}){return <div className="rounded-xl border border-[var(--gaia-line)] bg-[var(--surface-muted)] p-3"><dt className="text-[10px] font-bold uppercase tracking-wider text-[var(--gaia-ink-500)]">{label}</dt><dd className="mt-1 text-sm font-semibold">{value}</dd></div>}
type NodeAction="form"|"edit"|"duplicate"|"delete"|"view"|"assignment";
type DiagramNodeData={step:Step;index:number;assignment:string;canEdit:boolean;canAdminister:boolean;published:boolean;warnings:string[];onAction:(action:NodeAction,step:Step)=>void}&Record<string,unknown>;
type DiagramNode=Node<DiagramNodeData,"workflowStep">;
const diagramNodeTypes={workflowStep:WorkflowStepNode};
const diagramWidth=264,diagramHeight=174;
function NodeActionButton({children,label,onClick,tone="normal"}:{children:React.ReactNode;label:string;onClick:()=>void;tone?:"normal"|"danger"}){return <button aria-label={label} className={`nodrag nopan grid size-7 place-items-center rounded-lg border bg-white transition ${tone==="danger"?"border-[#e3b4af] text-[#9a384d] hover:bg-[#fff0f0]":"border-[var(--gaia-line)] text-[var(--brand-primary)] hover:bg-[var(--gaia-accent-pale)]"}`} onClick={event=>{event.stopPropagation();onClick()}} title={label} type="button">{children}</button>}
function WorkflowStepNode({data,selected}:NodeProps<DiagramNode>){const {step,index,assignment,canEdit,canAdminister,published,warnings,onAction}=data;return <article className={`relative h-[174px] w-[264px] rounded-2xl border bg-white p-4 shadow-sm transition ${warnings.length?"border-[#d99b3f]":selected?"border-[var(--brand-primary)] ring-2 ring-[var(--gaia-accent-soft)]":"border-[var(--gaia-line)]"}`}><Handle className="!size-3 !border-2 !border-white !bg-[#4d8b82]" isConnectable={canEdit} position={Position.Left} type="target"/><div className="flex items-start justify-between gap-2"><small className="font-bold uppercase tracking-[0.14em] text-[var(--brand-primary)]">{step.initial?"Inicio":step.final?"Final":`Etapa ${index+1}`}</small><span className="flex gap-1">{warnings.length>0&&<small className="rounded-full bg-[#fff3d8] px-2 py-1 font-bold text-[#8b5d12]" title={warnings.join("\n")}>{warnings.length} pendiente{warnings.length===1?"":"s"}</small>}{step.initial&&<Badge>Inicio</Badge>}{step.final&&<Badge>Final</Badge>}</span></div><strong className="mt-2 block truncate text-sm" title={friendly(step.code)}>{friendly(step.code)}</strong><span className="mt-1 block truncate text-[11px] text-[var(--gaia-ink-500)]" title={assignment}>{stepType(step.type)} · {assignment}</span><span className="mt-2 block text-[10px] font-medium text-[#58736c]">{step.activationRule===299540161?"Espera todas las entradas":"Se activa con cualquier entrada"}{step.targetDays?` · ${step.targetDays} días`:""}</span><div className="mt-3 flex items-center gap-1.5 border-t border-[var(--gaia-line)] pt-2"><NodeActionButton label={canEdit?"Configurar formulario":"Ver formulario"} onClick={()=>onAction("form",step)}><FileText size={14}/></NodeActionButton>{canEdit&&<><NodeActionButton label="Duplicar etapa" onClick={()=>onAction("duplicate",step)}><Copy size={14}/></NodeActionButton><NodeActionButton label="Editar etapa" onClick={()=>onAction("edit",step)}><Pencil size={14}/></NodeActionButton><NodeActionButton label="Eliminar etapa" onClick={()=>onAction("delete",step)} tone="danger"><Trash2 size={14}/></NodeActionButton></>}{!canEdit&&<NodeActionButton label="Ver configuración" onClick={()=>onAction("view",step)}><Eye size={14}/></NodeActionButton>}{published&&canAdminister&&<NodeActionButton label="Cambiar asignación" onClick={()=>onAction("assignment",step)}><UserRoundCog size={14}/></NodeActionButton>}</div><Handle className="!size-3 !border-2 !border-white !bg-[var(--brand-primary)]" isConnectable={canEdit} position={Position.Right} type="source"/></article>}
function automaticPositions(definition:Definition){const ordered=[...definition.steps].sort((a,b)=>a.order-b.order);const graph=new dagre.graphlib.Graph().setDefaultEdgeLabel(()=>({}));graph.setGraph({rankdir:"LR",ranksep:110,nodesep:55,marginx:35,marginy:35});ordered.forEach(step=>graph.setNode(step.id,{width:diagramWidth,height:diagramHeight}));definition.routes.filter(item=>item.active).forEach(route=>graph.setEdge(route.sourceStepId,route.targetStepId));dagre.layout(graph);return new Map(ordered.map((step,index)=>{const value=graph.node(step.id)??{x:index*(diagramWidth+110)+diagramWidth/2,y:diagramHeight/2};return[step.id,{x:Math.max(0,value.x-diagramWidth/2),y:Math.max(0,value.y-diagramHeight/2)}]}));}
function diagramStepWarnings(definition:Definition){const active=definition.steps.filter(item=>item.active),routes=definition.routes.filter(item=>item.active),reachable=new Set(active.filter(item=>item.initial).map(item=>item.id));let changed=true;while(changed){changed=false;for(const route of routes)if(reachable.has(route.sourceStepId)&&!reachable.has(route.targetStepId)){reachable.add(route.targetStepId);changed=true}}const warnings=new Map<string,string[]>();for(const step of active){const values:string[]=[];if(!step.final&&!routes.some(route=>route.sourceStepId===step.id))values.push("No tiene conexión de salida");if(!reachable.has(step.id))values.push("No es alcanzable desde una etapa inicial");if(step.assignmentStrategy===299540150&&!step.unitId)values.push("Falta la unidad responsable");if(step.assignmentStrategy===299540151&&!step.personId)values.push("Falta la persona responsable");if(values.length)warnings.set(step.id,values)}return warnings;}
function diagramElements(definition:Definition,units:Unit[],people:Person[],canEdit:boolean,canAdminister:boolean,published:boolean,onAction:(action:NodeAction,step:Step)=>void){
  const ordered=[...definition.steps].sort((a,b)=>a.order-b.order),automatic=automaticPositions(definition),warnings=diagramStepWarnings(definition);
  const positionOf=(step:Step)=>automatic.get(step.id)??{x:step.order*(diagramWidth+110),y:0};
  const visualOrder=new Map(ordered.filter(step=>!step.initial&&!step.final).sort((a,b)=>{const aPosition={x:a.positionX??positionOf(a).x,y:a.positionY??positionOf(a).y},bPosition={x:b.positionX??positionOf(b).x,y:b.positionY??positionOf(b).y},horizontalDistance=aPosition.x-bPosition.x;return Math.abs(horizontalDistance)<diagramWidth/2?aPosition.y-bPosition.y||a.order-b.order:horizontalDistance;}).map((step,index)=>[step.id,index+1]));
  const nodes:DiagramNode[]=ordered.map((step,index)=>{const calculated=positionOf(step);return{id:step.id,type:"workflowStep",position:{x:step.positionX??calculated.x,y:step.positionY??calculated.y},data:{step,index:visualOrder.get(step.id)??index,assignment:assignmentDetail(step,units,people),canEdit,canAdminister,published,warnings:warnings.get(step.id)??[],onAction},draggable:canEdit};});
  const parallel=new Map<string,number>();const edges:Edge[]=definition.routes.filter(item=>item.active).map(route=>{const key=`${route.sourceStepId}:${route.targetStepId}`,index=parallel.get(key)??0;parallel.set(key,index+1);return{id:route.id,source:route.sourceStepId,target:route.targetStepId,label:result(route.requiredResult),markerEnd:{type:MarkerType.ArrowClosed,color:"#386d65"},style:{stroke:"#386d65",strokeWidth:2},labelStyle:{fill:"#315e58",fontSize:10,fontWeight:700},labelBgStyle:{fill:"#f8fbfa",fillOpacity:.96},labelBgPadding:[6,4],labelBgBorderRadius:6,type:"default",pathOptions:{curvature:.22+(index*.14)},data:{route}}});return{nodes,edges};
}
function WorkflowDiagram({definition,canEdit,canAdminister,expanded,onArrange,onConnect,onMoveStep,onNodeAction,onSelectRoute,onToggleExpanded,people,published,units}:{definition:Definition;canEdit:boolean;canAdminister:boolean;expanded:boolean;onArrange:(positions:Array<{id:string;x:number;y:number}>)=>Promise<void>;onConnect:(source:string,target:string)=>void;onMoveStep:(stepId:string,position:{x:number;y:number})=>Promise<void>;onNodeAction:(action:NodeAction,step:Step)=>void;onSelectRoute:(route:Route)=>void;onToggleExpanded:()=>void;people:Person[];published:boolean;units:Unit[]}){
 const initial=useMemo(()=>diagramElements(definition,units,people,canEdit,canAdminister,published,onNodeAction),[definition,units,people,canEdit,canAdminister,published,onNodeAction]);const [nodes,setNodes,onNodesChange]=useNodesState<DiagramNode>(initial.nodes);const [edges,setEdges,onEdgesChange]=useEdgesState(initial.edges);
 const [instance,setInstance]=useState<ReactFlowInstance<DiagramNode,Edge>|null>(null);const warningCount=publicationChecks(definition).filter(item=>!item.ok).length;
 useEffect(()=>{const next=diagramElements(definition,units,people,canEdit,canAdminister,published,onNodeAction);setNodes(next.nodes);setEdges(next.edges)},[definition,units,people,canEdit,canAdminister,published,onNodeAction,setEdges,setNodes]);
 useEffect(()=>{window.setTimeout(()=>void instance?.fitView({padding:.18,maxZoom:1,duration:300}),120)},[expanded,instance]);
 const connect=useCallback((connection:Connection)=>{if(canEdit&&connection.source&&connection.target&&connection.source!==connection.target)onConnect(connection.source,connection.target)},[canEdit,onConnect]);
 const arrange=useCallback(()=>{if(!canEdit)return;const positions=automaticPositions(definition),arranged=nodes.map(node=>({...node,position:positions.get(node.id)??node.position}));setNodes(arranged);void onArrange(arranged.map(node=>({id:node.id,x:node.position.x,y:node.position.y})));window.setTimeout(()=>void instance?.fitView({padding:.22,maxZoom:1,duration:350}),120)},[canEdit,definition,nodes,onArrange,instance,setNodes]);
 return <section className="mt-4 overflow-hidden rounded-2xl border border-[#b9d8d0] bg-[#f3f9f7]"><header className="flex flex-wrap items-center justify-between gap-3 border-b border-[#cde1db] px-4 py-3"><div><strong className="text-sm">Diseñador visual</strong><p className="text-xs text-[var(--gaia-ink-500)]">{canEdit?"Mueve las etapas o arrastra desde un punto de salida hasta el destino para conectarlas.":"Vista gráfica de la versión publicada."}</p></div><div className="flex flex-wrap items-center gap-2">{warningCount>0&&<span className="rounded-full bg-[#fff3d8] px-3 py-1 text-[10px] font-bold text-[#8b5d12]">{warningCount} validación{warningCount===1?"":"es"} pendiente{warningCount===1?"":"s"}</span>}<span className="rounded-full bg-white px-3 py-1 text-[10px] font-bold">{definition.steps.length} etapas</span><span className="rounded-full bg-white px-3 py-1 text-[10px] font-bold">{definition.routes.length} conexiones</span>{canEdit&&<button className="inline-flex items-center gap-1.5 rounded-lg border border-[#b9d8d0] bg-white px-3 py-1.5 text-[10px] font-bold text-[var(--brand-primary)] hover:bg-[var(--gaia-accent-pale)]" onClick={arrange} type="button"><LayoutGrid size={13}/>Organizar automáticamente</button>}<button aria-pressed={expanded} className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--brand-primary)] bg-[var(--brand-primary)] px-3 py-1.5 text-[10px] font-bold text-white shadow-md transition hover:-translate-y-0.5 hover:shadow-lg" onClick={onToggleExpanded} type="button">{expanded?<Minimize2 size={13}/>:<Maximize2 size={13}/>} {expanded?"Restaurar vista":"Ampliar diseñador"}</button></div></header><div className={`${expanded?"h-[calc(100vh-10rem)] min-h-[720px]":"h-[560px]"} bg-[#f8fbfa] transition-[height] duration-300`}><ReactFlow colorMode="light" edges={edges} fitView fitViewOptions={{padding:.22,maxZoom:1}} maxZoom={1.6} minZoom={.35} nodeTypes={diagramNodeTypes} nodes={nodes} nodesConnectable={canEdit} nodesDraggable={canEdit} onConnect={connect} onEdgesChange={onEdgesChange} onEdgeClick={(_,edge)=>{const route=edge.data?.route as Route|undefined;if(route&&canEdit)onSelectRoute(route)}} onInit={setInstance} onNodeDragStop={(_,node)=>{if(canEdit)void onMoveStep(node.id,node.position)}} onNodesChange={onNodesChange} panOnDrag snapGrid={[16,16]} snapToGrid><Background color="#c9d9d4" gap={20} size={1.2} variant={BackgroundVariant.Dots}/><Controls position="bottom-left" showInteractive={false}/><MiniMap className="!border !border-[#cde1db] !bg-white" maskColor="rgba(232,241,238,.7)" nodeColor={node=>{const step=(node.data as DiagramNodeData).step;return step.initial?"#4d8b82":step.final?"#6f3873":"#8bb5aa"}} pannable zoomable/></ReactFlow></div></section>
}
const stepOptions = [
  {
    key: "initial",
    label: "Es la etapa inicial",
    help: "La solicitud comienza aquí. Marca varias etapas iniciales cuando distintas áreas deban trabajar en paralelo desde la radicación.",
  },
  {
    key: "final",
    label: "Finaliza el proceso",
    help: "Al completarla, el flujo puede cerrar la solicitud.",
  },
  {
    key: "reopeningEntry",
    label: "Es el punto de retorno al reabrir",
    help: "Solo se usa después de cerrar una solicitud: si se reabre, se crea una nueva gestión en esta etapa. El flujo puede tener un único punto de retorno.",
  },
  {
    key: "requiresDecision",
    label: "Requiere una decisión",
    help: "La persona debe elegir un resultado, por ejemplo aprobar o rechazar.",
  },
  {
    key: "requiresObservation",
    label: "Hacer obligatoria la observación",
    help: "El campo de observación siempre está disponible; al marcarlo no se podrá finalizar sin escribirla.",
  },
  {
    key: "requiresFile",
    label: "Exigir al menos un archivo adjunto",
    help: "No crea un campo nuevo. Obliga a adjuntar un archivo a la gestión, ya sea desde su formulario o desde la opción de adjuntos.",
  },
  {
    key: "allowsRequesterReturn",
    label: "Permitir pedir información al solicitante",
    help: "Agrega la acción «Pedir información». La etapa queda en espera y se reactiva cuando el solicitante responde.",
  },
] as const;
const helpTopics = {
  assignment: {
    title: "Responsable de la etapa",
    summary:
      "Define dónde aparecerá el trabajo cuando la etapa quede disponible.",
    example:
      "Unidad organizacional: cualquier persona autorizada de Tecnología puede tomarla.\nPersona específica: siempre la recibe la persona seleccionada.\nResponsable actual: continúa quien ya atiende la solicitud.",
    recommendation:
      "Prefiere una unidad organizacional para evitar que el proceso dependa de una sola persona ausente.",
  },
  activation: {
    title: "¿Cuándo se activa la etapa?",
    summary:
      "Sigue esta guía: primero identifica cuántas etapas pueden conducir hasta esta; después decide si son caminos alternativos o trabajos que deben terminarse juntos.",
    example:
      "1. UNA SOLA ENTRADA\nAprobación de coordinación → Finalizar\nElige «cualquier conexión». Con una sola entrada ambas reglas darían el mismo resultado.\n\n2. CAMINOS ALTERNATIVOS\nSoporte interno ─┐\nProveedor externo ─┴→ Cerrar solicitud\nElige «cualquier conexión»: basta uno de los caminos.\n\n3. TRABAJOS PARALELOS\nRevisión jurídica ─┐\nRevisión financiera ─┴→ Elaborar respuesta\nElige «todas»: deben terminar ambas áreas.",
    recommendation:
      "Si el proceso consiste únicamente en una aprobación, marca esa misma etapa como inicial y final: no necesita conexiones. Si tiene una etapa anterior, usa «cualquier conexión».",
  },
  behavior: {
    title: "Comportamiento de la etapa",
    summary:
      "Configúralo en orden: 1) decide si inicia o termina el recorrido; 2) elige si se completa o se aprueba/rechaza; 3) agrega solamente las evidencias obligatorias.",
    example:
      "FLUJO DE UNA SOLA APROBACIÓN\n1. Nombre: Aprobación de coordinación.\n2. Responsable: coordinación correspondiente.\n3. Marca Inicial + Final.\n4. Elige Aprobar o rechazar.\n5. No agregues conexiones.\n\nFLUJO SECUENCIAL\nRecepción: Inicial y Completar.\nAprobación: Aprobar o rechazar.\nComunicar solución: Final.\n\nFLUJO PARALELO\nMarca como Inicial cada revisión que debe comenzar al radicar. Conéctalas a una etapa final configurada para esperar todas las entradas.",
    recommendation:
      "Activa solo las condiciones necesarias. Demasiados requisitos pueden dificultar la atención o bloquear el recorrido.",
  },
} satisfies Record<string, HelpContent>;
const workflowCode = (value: string) =>
  value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toUpperCase()
    .trimStart()
    .replace(/[^A-Z0-9_-]+/g, "_")
    .replace(/_+/g, "_")
    .slice(0, 80);
const assignmentHelp = (value: number) =>
  (
    ({
      299540150:
        "La etapa quedará disponible para la unidad seleccionada.",
      299540151: "La etapa se asignará directamente a esta persona.",
      299540152: "Continúa quien ya atiende la solicitud.",
    }) as Record<number, string>
  )[value] ?? "Define quién recibirá esta actividad.";
function publicationChecks(flow: Definition) {
  const active = flow.steps.filter((x) => x.active),
    routes = flow.routes.filter((x) => x.active),
    ids = new Set(active.map((x) => x.id)),
    reachable = new Set(active.filter((x) => x.initial).map((x) => x.id));
  let changed = true;
  while (changed) {
    changed = false;
    for (const route of routes.filter((x) => reachable.has(x.sourceStepId)))
      if (!reachable.has(route.targetStepId)) {
        reachable.add(route.targetStepId);
        changed = true;
      }
  }
  return [
    {
      label: "Existe al menos una etapa inicial",
      ok: active.some((x) => x.initial),
    },
    {
      label: "Existe al menos una etapa final",
      ok: active.some((x) => x.final),
    },
    {
      label: "Cada etapa no final tiene una conexión de salida",
      ok: active
        .filter((x) => !x.final)
        .every((x) => routes.some((r) => r.sourceStepId === x.id)),
    },
    {
      label: "Todas las etapas tienen un responsable válido",
      ok: active.every(
        (x) =>
          x.assignmentStrategy === 299540152 ||
          (x.assignmentStrategy === 299540150 && !!x.unitId) ||
          (x.assignmentStrategy === 299540151 && !!x.personId),
      ),
    },
    {
      label: "Las conexiones pertenecen a este flujo",
      ok: routes.every(
        (x) =>
          ids.has(x.sourceStepId) &&
          ids.has(x.targetStepId) &&
          x.sourceStepId !== x.targetStepId,
      ),
    },
    {
      label: "No existen conexiones duplicadas",
      ok: new Set(routes.map(x=>`${x.sourceStepId}:${x.targetStepId}:${x.requiredResult}`)).size===routes.length,
    },
    {
      label: "Todas las etapas son alcanzables desde el inicio",
      ok: active.length > 0 && active.every((x) => reachable.has(x.id)),
    },
  ];
}
const friendly = (value: string) =>
  value
    .toLowerCase()
    .replace(/_/g, " ")
    .replace(/(^|\s)\p{L}/gu, (x) => x.toUpperCase());
const assignmentDetail = (step: Step, units: Unit[], people: Person[]) =>
  step.assignmentStrategy === 299540150
    ? `Bandeja: ${units.find((x) => x.id === step.unitId)?.name ?? "unidad pendiente"}`
    : step.assignmentStrategy === 299540151
      ? `Responsable: ${people.find((x) => x.id === step.personId)?.name ?? "persona pendiente"}`
      : "Responsable actual de la solicitud";
const message = (e: unknown) =>
  e instanceof Error ? e.message : "No fue posible completar la operación.";
const status = (x: number) =>
  x === 299540131 ? "Publicado" : x === 299540132 ? "Retirado" : "Borrador";
const stepType = (x: number) =>
  (
    ({
      299540140: "Gestión",
      299540141: "Revisión",
      299540142: "Aprobación",
      299540143: "Firma",
      299540144: "Respuesta final",
      299540145: "Automático",
    }) as Record<number, string>
  )[x] ?? "Paso";
const stageFieldType = (dataType: number, controlType: number) => {
  if (dataType === 299540047) return "Archivo";
  if (controlType === 299540058) return "Lista";
  if (controlType === 299540059) return "Selección única";
  if (controlType === 299540061) return "Casillas de selección";
  if (controlType === 299540051) return "Texto largo";
  if (dataType === 299540041 || dataType === 299540042) return "Número";
  if (dataType === 299540043) return "Fecha";
  if (dataType === 299540044) return "Fecha y hora";
  if (dataType === 299540045) return "Sí / No";
  return "Texto corto";
};
const result = (x: number) =>
  (
    ({
      299540170: "Completado",
      299540171: "Aprobado",
      299540172: "Rechazado",
      299540173: "Devuelto",
      299540174: "Requiere aprobación",
      299540175: "No aplica",
    }) as Record<number, string>
  )[x] ?? "Resultado";
const name = (steps: Step[], id: string) =>
  steps.find((x) => x.id === id)?.code ?? "Paso";
