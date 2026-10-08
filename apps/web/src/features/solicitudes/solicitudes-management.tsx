"use client";

import {
  useEffect,
  useState,
  useSyncExternalStore,
  type FormEvent,
} from "react";
import { AppHeader } from "@/components/app-header";
import { ConfirmDialog } from "@/components/form-dialog";
import { useFeedback } from "@/components/feedback";
import { useSecurity } from "@/components/security-context";
import { apiRequest } from "@/lib/api-client";
import {
  AlertTriangle,
  ArrowRightCircle,
  CircleHelp,
  FileDown,
  Inbox,
  MessageSquareText,
  LoaderCircle,
  Paperclip,
  RefreshCw,
  Search,
  Trash2,
  UserRoundCog,
  X,
} from "lucide-react";

type Option = {
  id: string;
  name: string;
  code: string | null;
  unitIds?: string[] | null;
};
type Catalog = {
  services: Option[];
  states: Option[];
  responsibles: Option[];
  units: Option[];
};
type Item = {
  id: string;
  number: string;
  subject: string;
  service: string;
  status: string;
  statusColor: string | null;
  requester: string;
  responsible: string | null;
  unit: string | null;
  submittedAt: string | null;
  dueDate: string | null;
  isOverdue: boolean;
  canDelete?: boolean;
};
import {
  SolicitudesQueue,
  pagination,
  queueSearch,
  readQueueQuery,
  type QueueQuery,
  type QueuePage,
  type QueueView,
} from "./solicitudes-queue";
import { SolicitudesManagementFormDialog } from "./solicitudes-management-form";
import { downloadClosurePdf } from "./solicitudes-closure-pdf";
type Comment = {
  id: string;
  content: string;
  publishedAt: string;
  isInternal: boolean;
  isMine: boolean;
  authorRole: string;
};
type Transition = {
  id: string;
  targetStateId: string;
  targetState: string;
  requiresComment: boolean;
  requiresReason: boolean;
  requiresSolution: boolean;
  requestsRating: boolean;
};
type HistoryEvent = {
  id: string;
  title: string;
  detail: string | null;
  occurredAt: string;
  actor: string;
  movement: number;
  origin: number;
  visibleToRequester: boolean;
};
type Detail = {
  id: string;
  number: string;
  subject: string;
  description: string;
  service: string;
  status: string;
  statusColor: string | null;
  submittedAt: string | null;
  dueDate: string | null;
  allowsRequesterComments: boolean;
  isManager: boolean;
  comments: Comment[];
  transitions: Transition[];
  history: HistoryEvent[] | null;
};
type Attachment = {
  id: string;
  managementId: string | null;
  file: { originalName: string | null; storedName: string; length: number };
  uploadedAt: string;
};
type WorkflowAnswer = {
  fieldId: string;
  label: string;
  value: string | null;
  options: string[];
};
type WorkflowDestination = {
  stepId: string;
  code: string;
  name: string;
  final: boolean;
};
type WorkflowNextAction = {
  result: number;
  destinations: WorkflowDestination[];
};
type WorkflowItem = {
  id: string;
  stepId: string;
  stepCode: string;
  execution: number;
  status: number;
  result: number | null;
  observation: string | null;
  unitId: string | null;
  responsibleId: string | null;
  availableAt: string | null;
  completedAt: string | null;
  requiresDecision: boolean;
  requiresObservation: boolean;
  requiresFile: boolean;
  allowsRequesterReturn: boolean;
  final: boolean;
  targetDays: number | null;
  targetDueDate: string | null;
  unitName: string | null;
  responsibleName: string | null;
  formTitle: string | null;
  answers: WorkflowAnswer[];
  nextActions: WorkflowNextAction[];
  canTake: boolean;
  canManage: boolean;
};
type Workflow = {
  instanceId: string;
  flowId: string;
  version: number;
  status: number;
  managements: WorkflowItem[];
};
const quickQueueViews: QueueView[] = [
  "mine",
  "waiting",
  "tracking",
  "resolved",
];

export function SolicitudesManagement() {
  const security = useSecurity(),
    { notify } = useFeedback(),
    [catalog, setCatalog] = useState<Catalog | null>(null),
    [error, setError] = useState("");
  const [search, setSearch] = useState(""),
    [selected, setSelected] = useState<Item | null>(null),
    [responsibleId, setResponsibleId] = useState(""),
    [unitId, setUnitId] = useState(""),
    [reason, setReason] = useState(""),
    [saving, setSaving] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<Item | null>(null);
  const [detail, setDetail] = useState<Detail | null>(null),
    [workflow, setWorkflow] = useState<Workflow | null>(null),
    [attachments, setAttachments] = useState<Attachment[]>([]),
    [comment, setComment] = useState(""),
    [internal, setInternal] = useState(false),
    [transitionId, setTransitionId] = useState(""),
    [solution, setSolution] = useState(""),
    [rating, setRating] = useState(""),
    [auditOpen, setAuditOpen] = useState(false),
    [stageAssignmentOpen, setStageAssignmentOpen] = useState(false),
    [openingRequestId, setOpeningRequestId] = useState<string | null>(null);
  const [queueCountsLoading, setQueueCountsLoading] = useState(true),
    [queueCountsRevision, setQueueCountsRevision] = useState(0),
    [queueHelpOpen, setQueueHelpOpen] = useState(false),
    [viewTotals, setViewTotals] = useState<Partial<Record<QueueView, number>>>({}),
    [completion, setCompletion] = useState<{
      item: WorkflowItem;
      result: number;
    } | null>(null),
    [takingManagementId, setTakingManagementId] = useState<string | null>(null),
    [uploadingManagementId, setUploadingManagementId] = useState<string | null>(
      null,
    );
  const [queue] = useState(
    () =>
      new SolicitudesQueue((path, options) =>
        apiRequest<QueuePage>(path, options),
      ),
  );
  const snapshot = useSyncExternalStore(
    queue.subscribe,
    queue.getSnapshot,
    queue.getSnapshot,
  );
  const { data, loading, query } = snapshot,
    { serviceId, overdue, view } = query,
    controls = pagination(snapshot);
  function navigate(next: QueueQuery) {
    window.history.pushState(null, "", `?${queueSearch(next)}`);
    void queue.show(next);
  }
  function filter(name: "serviceId" | "overdue", value: string) {
    queue.cancelSearch();
    navigate({ ...query, search: search.trim(), [name]: value, page: 1 });
  }
  async function load() {
    await queue.afterMutation();
  }
  useEffect(() => {
    const restore = () => {
      queue.cancelSearch();
      const next = readQueueQuery(window.location.search);
      setSearch(next.search);
      void queue.show(next);
    };
    restore();
    window.addEventListener("popstate", restore);
    return () => {
      window.removeEventListener("popstate", restore);
      queue.dispose();
    };
  }, [queue]);
  useEffect(() => {
    let active = true;
    apiRequest<Catalog>("/api/solicitudes/management/catalog")
      .then((value) => {
        if (active) setCatalog(value);
      })
      .catch((value) => {
        if (active)
          setError(
            value instanceof Error
              ? value.message
              : "No fue posible cargar los filtros.",
          );
      });
    return () => {
      active = false;
    };
  }, []);
  useEffect(() => {
    let active = true;
    Promise.all(
      quickQueueViews.map(
        async (view) => {
          const result = await apiRequest<QueuePage>(
            `/api/solicitudes/management/queue?view=${view}&page=1&pageSize=1&sort=submitted-desc`,
            { cache: "no-store" },
          );
          return [view, Math.max(0, result.totalCount ?? result.total)] as const;
        },
      ),
    )
      .then((entries) => {
        if (active) setViewTotals(Object.fromEntries(entries));
      })
      .catch((value) => {
        if (active)
          setError(
            value instanceof Error
              ? value.message
              : "No fue posible cargar el panorama de gestiones.",
          );
      })
      .finally(() => {
        if (active) setQueueCountsLoading(false);
      });
    return () => {
      active = false;
    };
  }, [queueCountsRevision]);
  async function reassign(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    setSaving(true);
    setError("");
    try {
      await apiRequest(
        `/api/solicitudes/management/requests/${selected.id}/assignment`,
        {
          method: "PUT",
          body: JSON.stringify({
            responsibleId,
            unitId: unitId || null,
            reason,
          }),
        },
      );
      setSelected(null);
      setResponsibleId("");
      setUnitId("");
      setReason("");
      await load();
      notify({
        tone: "success",
        title: "Solicitud reasignada",
        description: "El nuevo responsable quedó registrado en el historial.",
      });
    } catch (value) {
      const description =
        value instanceof Error
          ? value.message
          : "No fue posible reasignar la solicitud.";
      setError(description);
      notify({ tone: "error", title: "No se pudo reasignar", description });
    } finally {
      setSaving(false);
    }
  }
  async function deleteResolved() {
    if (!deleteTarget || saving) return;
    setSaving(true);
    setError("");
    try {
      await apiRequest(
        `/api/solicitudes/management/requests/${deleteTarget.id}`,
        { method: "DELETE" },
      );
      const number = deleteTarget.number;
      setDeleteTarget(null);
      if (detail?.id === deleteTarget.id) {
        setDetail(null);
        setWorkflow(null);
      }
      await load();
      notify({
        tone: "success",
        title: "Solicitud eliminada",
        description: `${number} fue retirada de las bandejas. Su trazabilidad se conserva en Dataverse.`,
      });
    } catch (value) {
      const description =
        value instanceof Error
          ? value.message
          : "No fue posible eliminar la solicitud.";
      setError(description);
      notify({ tone: "error", title: "No se pudo eliminar", description });
    } finally {
      setSaving(false);
    }
  }
  async function manage(id: string) {
    const foreground = !detail || detail.id !== id;
    if (foreground) setOpeningRequestId(id);
    setError("");
    setAuditOpen(false);
    setStageAssignmentOpen(false);
    try {
      const [request, fileRows, workflowData] = await Promise.all([
        apiRequest<Detail>(`/api/solicitudes/requests/${id}`, {
          cache: "no-store",
        }),
        apiRequest<Attachment[]>(
          `/api/solicitudes/requests/${id}/attachments`,
          { cache: "no-store" },
        ).catch(() => []),
        apiRequest<Workflow>(`/api/solicitudes/requests/${id}/workflow`, {
          cache: "no-store",
        }).catch(() => null),
      ]);
      setDetail({ ...request, transitions: [] });
      setAttachments(fileRows);
      setWorkflow(workflowData);
    } catch (value) {
      setError(
        value instanceof Error
          ? value.message
          : "No fue posible abrir la solicitud.",
      );
    } finally {
      if (foreground) setOpeningRequestId(null);
    }
  }
  async function addComment(event: FormEvent) {
    event.preventDefault();
    if (!detail) return;
    setSaving(true);
    try {
      await apiRequest(`/api/solicitudes/requests/${detail.id}/comments`, {
        method: "POST",
        body: JSON.stringify({ content: comment, internal }),
      });
      setComment("");
      setInternal(false);
      await manage(detail.id);
      await load();
      notify({
        tone: "success",
        title: internal ? "Nota interna publicada" : "Comentario publicado",
        description: internal
          ? "Solo el equipo administrativo podrá verla."
          : "La conversación quedó actualizada.",
      });
    } catch (value) {
      const description =
        value instanceof Error
          ? value.message
          : "No fue posible publicar el comentario.";
      setError(description);
      notify({ tone: "error", title: "No se pudo publicar", description });
    } finally {
      setSaving(false);
    }
  }
  async function transition(event: FormEvent) {
    event.preventDefault();
    if (!detail || !transitionId || saving) return;
    setError("");
    const selectedTransition = detail.transitions.find(
      (item) => item.id === transitionId,
    );
    if (
      (selectedTransition?.requiresComment ||
        selectedTransition?.requiresReason) &&
      !reason.trim()
    ) {
      const description =
        "Escribe el motivo u observación antes de aplicar este cambio de estado.";
      setError(description);
      notify({
        tone: "warning",
        title: "Falta información para continuar",
        description,
      });
      return;
    }
    setSaving(true);
    try {
      const updated = await apiRequest<Detail>(
        `/api/solicitudes/requests/${detail.id}/transitions`,
        {
          method: "POST",
          body: JSON.stringify({
            transitionId,
            comment: selectedTransition?.requiresComment
              ? reason
              : comment || null,
            reason: reason || null,
            solution: solution || null,
            rating:
              selectedTransition?.requestsRating && rating
                ? Number(rating)
                : null,
            ratingComment: null,
          }),
        },
      );
      setDetail(updated);
      setTransitionId("");
      setComment("");
      setReason("");
      setSolution("");
      setRating("");
      await load();
      notify({
        tone: "success",
        title: "Estado actualizado",
        description: `La solicitud ahora se encuentra en ${updated.status}.`,
      });
    } catch (value) {
      const description =
        value instanceof Error
          ? value.message
          : "No fue posible cambiar el estado.";
      setError(description);
      notify({
        tone: "error",
        title: "No se pudo cambiar el estado",
        description,
      });
    } finally {
      setSaving(false);
    }
  }
  async function completeManagement(item: WorkflowItem, result: number) {
    if (detail) setCompletion({ item, result });
  }
  async function finishManagement(
    observation: string | null,
    uploadedInForm = false,
  ) {
    if (!detail || !completion) return;
    setSaving(true);
    setError("");
    try {
      const hasRelatedFile =
        uploadedInForm ||
        attachments.some((item) => item.managementId === completion.item.id);
      await apiRequest(
        `/api/solicitudes/management/workflows/${completion.item.id}/complete`,
        {
          method: "POST",
          body: JSON.stringify({
            managementId: completion.item.id,
            result: completion.result,
            observation,
            hasRelatedFile,
            operationId: crypto.randomUUID(),
          }),
        },
      );
      await manage(detail.id);
      await load();
      setQueueCountsLoading(true);
      setQueueCountsRevision((value) => value + 1);
      setCompletion(null);
    } catch (value) {
      setError(
        value instanceof Error
          ? value.message
          : "No fue posible completar la gestión.",
      );
      throw value;
    } finally {
      setSaving(false);
    }
  }
  async function takeManagement(item: WorkflowItem) {
    if (!detail) return;
    setTakingManagementId(item.id);
    setSaving(true);
    setError("");
    try {
      await apiRequest(
        `/api/solicitudes/management/workflows/${item.id}/take`,
        { method: "POST", body: "{}" },
      );
      await manage(detail.id);
      await load();
      setQueueCountsLoading(true);
      setQueueCountsRevision((value) => value + 1);
      notify({
        tone: "success",
        title: "Gestión asignada",
        description: "La etapa ya aparece como una gestión a tu cargo.",
      });
    } catch (value) {
      const description =
        value instanceof Error
          ? value.message
          : "No fue posible tomar la gestión.";
      setError(description);
      notify({
        tone: "error",
        title: "No se pudo tomar la gestión",
        description,
      });
    } finally {
      setTakingManagementId(null);
      setSaving(false);
    }
  }
  async function resumeManagement(item: WorkflowItem) {
    if (!detail) return;
    setSaving(true);
    setError("");
    try {
      await apiRequest(
        `/api/solicitudes/management/workflows/${item.id}/resume`,
        { method: "POST", body: "{}" },
      );
      await manage(detail.id);
      await load();
      setQueueCountsLoading(true);
      setQueueCountsRevision((value) => value + 1);
      notify({
        tone: "success",
        title: "Gestión reactivada",
        description:
          "Ya puedes aprobar, rechazar o volver a solicitar información.",
      });
    } catch (value) {
      const description =
        value instanceof Error
          ? value.message
          : "No fue posible reactivar la gestión.";
      setError(description);
      notify({
        tone: "error",
        title: "No se pudo reactivar la gestión",
        description,
      });
    } finally {
      setSaving(false);
    }
  }
  async function uploadManagementFile(item: WorkflowItem, file: File) {
    if (!detail) return;
    setUploadingManagementId(item.id);
    setSaving(true);
    setError("");
    try {
      const body = new FormData();
      body.append("file", file);
      body.append("visibility", "internal");
      body.append("managementId", item.id);
      await apiRequest(`/api/solicitudes/requests/${detail.id}/attachments`, {
        method: "POST",
        body,
      });
      await manage(detail.id);
      notify({
        tone: "success",
        title: "Archivo adjuntado",
        description: `${file.name} quedó asociado a esta etapa.`,
      });
    } catch (value) {
      const description =
        value instanceof Error
          ? value.message
          : "No fue posible adjuntar el archivo a la gestión.";
      setError(description);
      notify({
        tone: "error",
        title: "No se pudo adjuntar el archivo",
        description,
      });
    } finally {
      setUploadingManagementId(null);
      setSaving(false);
    }
  }
  async function reassignStage(
    managementId: string,
    responsibleId: string,
    reason: string,
  ) {
    if (!detail) return;
    setSaving(true);
    setError("");
    try {
      await apiRequest(
        `/api/solicitudes/management/workflows/${managementId}/assignment`,
        {
          method: "PUT",
          body: JSON.stringify({
            responsibleId: responsibleId || null,
            reason,
          }),
        },
      );
      setStageAssignmentOpen(false);
      await manage(detail.id);
      setQueueCountsLoading(true);
      setQueueCountsRevision((value) => value + 1);
      notify({
        tone: "success",
        title: "Etapa reasignada",
        description:
          "La persona responsable y la trazabilidad de la gestión quedaron actualizadas.",
      });
    } catch (value) {
      const description =
        value instanceof Error
          ? value.message
          : "No fue posible reasignar la etapa.";
      setError(description);
      notify({
        tone: "error",
        title: "No se pudo reasignar la etapa",
        description,
      });
      throw value;
    } finally {
      setSaving(false);
    }
  }
  return (
    <main className="gaia-app-page min-h-screen bg-[#f3f6f3] text-[var(--gaia-ink-900)]">
      <AppHeader title="Solicitudes · Solicitudes" />
      <div className="mx-auto max-w-[1500px] px-5 py-8 lg:px-8">
        <section className="overflow-hidden rounded-2xl border border-[var(--gaia-line)] bg-white shadow-sm">
          <header className="flex flex-wrap items-center justify-between gap-3 px-4 py-4 sm:px-5">
            <div>
              <p className="text-[10px] font-bold uppercase tracking-[.14em] text-[var(--brand-primary)]">
                Gestión operativa
              </p>
              <h1 className="mt-1 text-2xl font-semibold tracking-tight">
                Bandeja de solicitudes
              </h1>
              <p className="mt-1 text-xs text-[var(--gaia-ink-500)]">
                Localiza, prioriza y gestiona solicitudes desde un único listado.
              </p>
            </div>
            <button
              className="flex items-center gap-2 rounded-lg border border-[var(--gaia-line)] px-3 py-2 text-xs font-semibold transition hover:bg-[var(--surface-muted)] disabled:opacity-50"
              disabled={loading || queueCountsLoading}
              onClick={() => {
                void queue.refresh();
                setQueueCountsLoading(true);
                setQueueCountsRevision((value) => value + 1);
                if (detail) void manage(detail.id);
              }}
              type="button"
            >
              <RefreshCw className={loading || queueCountsLoading ? "animate-spin" : ""} size={14} />
              Actualizar
            </button>
          </header>

          <nav className="flex gap-1 overflow-x-auto border-y border-[var(--gaia-line)] px-4 py-2" aria-label="Filtros rápidos de solicitudes">
            {([
              ["mine", "Mis pendientes", viewTotals.mine],
              ["waiting", "Esperando respuesta", viewTotals.waiting],
              ["tracking", "En gestión", viewTotals.tracking],
              ["resolved", "Cerradas", viewTotals.resolved],
            ] as [QueueView, string, number | undefined][]).map(([value, label, count]) => (
              <button
                aria-pressed={view === value}
                className={`flex shrink-0 items-center gap-2 rounded-full px-3 py-2 text-xs font-semibold transition ${view === value ? "bg-[var(--brand-primary)] text-white" : "text-[var(--gaia-ink-700)] hover:bg-[var(--surface-muted)]"}`}
                key={value}
                onClick={() => navigate({ ...query, view: value, page: 1 })}
                type="button"
              >
                {label}
                {count !== undefined && (
                  <span className={`rounded-full px-1.5 py-0.5 text-[10px] ${view === value ? "bg-white/20" : "bg-[var(--surface-muted)]"}`}>
                    {count}
                  </span>
                )}
              </button>
            ))}
            <button
              aria-expanded={queueHelpOpen}
              aria-controls="queue-help"
              className="ml-auto flex shrink-0 items-center gap-1.5 rounded-full border border-[var(--gaia-line)] px-3 py-2 text-xs font-semibold text-[var(--brand-primary)] transition hover:bg-[var(--gaia-accent-pale)]"
              onClick={() => setQueueHelpOpen((open) => !open)}
              type="button"
            >
              <CircleHelp size={15} />
              ¿Qué significa cada bandeja?
            </button>
          </nav>

          {queueHelpOpen && (
            <aside
              className="border-b border-[var(--gaia-line)] bg-[var(--gaia-accent-pale)] px-4 py-3 sm:px-5"
              id="queue-help"
            >
              <div className="grid gap-2 text-xs md:grid-cols-2 xl:grid-cols-4">
                {queueHelpItems.map((item) => (
                  <div className="rounded-xl border border-white/80 bg-white/75 p-3" key={item.title}>
                    <strong className="text-[var(--gaia-ink-900)]">{item.title}</strong>
                    <p className="mt-1 leading-relaxed text-[var(--gaia-ink-600)]">{item.description}</p>
                  </div>
                ))}
              </div>
              <p className="mt-2 text-[11px] text-[var(--gaia-ink-500)]">
                Las solicitudes cambian de bandeja automáticamente según tu participación y el avance real del flujo.
              </p>
            </aside>
          )}

          <form
            className="p-4 sm:px-5"
            onSubmit={(event) => {
              event.preventDefault();
              void queue.refresh();
            }}
          >
            <div className="grid gap-2 md:grid-cols-[minmax(260px,1fr)_minmax(220px,360px)_minmax(160px,220px)]">
              <label className="flex min-h-10 items-center gap-2 rounded-xl border border-[var(--gaia-line)] px-3">
                <Search size={16} />
                <input
                  className="w-full outline-none"
                  aria-label="Buscar solicitudes"
                  maxLength={100}
                  onChange={(event) => {
                    setSearch(event.target.value);
                    queue.search(event.target.value, navigate);
                  }}
                  placeholder="Buscar número, asunto o solicitante"
                  value={search}
                />
              </label>
              <select className="min-h-10 rounded-xl border border-[var(--gaia-line)] px-3 text-sm" aria-label="Servicio" onChange={(event) => filter("serviceId", event.target.value)} value={serviceId}>
                <option value="">Todos los servicios</option>
                {catalog?.services.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
              </select>
              <select className="min-h-10 rounded-xl border border-[var(--gaia-line)] px-3 text-sm" aria-label="Plazo" onChange={(event) => filter("overdue", event.target.value)} value={overdue}>
                <option value="">Todos los plazos</option>
                <option value="true">Vencidas</option>
                <option value="false">En plazo</option>
              </select>
            </div>
            {(search || serviceId || overdue) && (
              <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-[var(--gaia-ink-500)]">
                <span>Filtros activos</span>
                {search && <span className="rounded-full bg-[var(--surface-muted)] px-2.5 py-1">Búsqueda: {search}</span>}
                {serviceId && <span className="rounded-full bg-[var(--surface-muted)] px-2.5 py-1">{catalog?.services.find((item) => item.id === serviceId)?.name}</span>}
                {overdue && <span className="rounded-full bg-[var(--surface-muted)] px-2.5 py-1">{overdue === "true" ? "Vencidas" : "En plazo"}</span>}
                <button className="font-semibold text-[var(--brand-primary)]" onClick={() => { setSearch(""); navigate({ search: "", serviceId: "", stateId: "", overdue: "", view, page: 1 }); }} type="button">Limpiar</button>
              </div>
            )}
          </form>
        </section>
        {(error || snapshot.error) && (
          <p
            role="alert"
            className="mt-4 rounded-xl bg-[#fff0f0] p-3 text-sm text-[#9a384d]"
          >
            {error || snapshot.error}
          </p>
        )}
        {openingRequestId && (
          <div
            className="fixed inset-0 z-[69] grid place-items-center bg-[#eef3ef]/95 p-4"
            role="status"
          >
            <div className="flex items-center gap-3 rounded-2xl border border-[var(--gaia-line)] bg-white px-6 py-5 shadow-xl">
              <LoaderCircle
                className="animate-spin text-[var(--brand-primary)]"
                size={22}
              />
              <span>
                <strong className="block text-sm">
                  Abriendo el expediente
                </strong>
                <small className="text-[var(--gaia-ink-500)]">
                  Estamos reuniendo la solicitud, su flujo y sus archivos…
                </small>
              </span>
            </div>
          </div>
        )}
        {detail && workflow && (
          <WorkflowPanel
            attachments={attachments}
            busy={saving}
            comment={comment}
            complete={completeManagement}
            detail={detail}
            internal={internal}
            onClose={() => {
              setAuditOpen(false);
              setDetail(null);
              setWorkflow(null);
            }}
            onComment={addComment}
            onTransition={transition}
            reason={reason}
            resume={resumeManagement}
            setComment={setComment}
            setInternal={setInternal}
            setReason={setReason}
            setSolution={setSolution}
            setTransitionId={setTransitionId}
            solution={solution}
            take={takeManagement}
            takingManagementId={takingManagementId}
            transitionId={transitionId}
            upload={uploadManagementFile}
            uploadingManagementId={uploadingManagementId}
            workflow={workflow}
          />
        )}
        {detail && workflow && (
          <>
            <div className="fixed bottom-6 right-6 z-[80] flex flex-wrap justify-end gap-2">
              {security.can("HD.SOLICITUDES.REASIGNAR") &&
                workflow.managements.some((item) =>
                  [299540191, 299540192].includes(item.status),
                ) && (
                  <button
                    className="rounded-full border border-[#245f58] bg-white px-5 py-3 text-sm font-semibold text-[#245f58] shadow-xl transition hover:-translate-y-0.5 hover:shadow-2xl"
                    onClick={() => setStageAssignmentOpen(true)}
                    type="button"
                  >
                    Reasignar etapa
                  </button>
                )}
              <button
                className="rounded-full bg-[#245f58] px-5 py-3 text-sm font-semibold text-white shadow-xl transition hover:-translate-y-0.5 hover:shadow-2xl"
                onClick={() => setAuditOpen(true)}
                type="button"
              >
                Ver actividad · {detail.history?.length ?? 0}
              </button>
            </div>
            {auditOpen && (
              <AuditDrawer close={() => setAuditOpen(false)} detail={detail} />
            )}{" "}
            {stageAssignmentOpen && (
              <StageAssignmentDialog
                busy={saving}
                catalog={catalog}
                close={() => setStageAssignmentOpen(false)}
                submit={reassignStage}
                workflow={workflow}
              />
            )}
          </>
        )}
        <section
          aria-busy={loading}
          className="mt-4 overflow-hidden rounded-2xl border border-[var(--gaia-line)] bg-white shadow-sm"
        >
          <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--gaia-line)] px-4 py-3 sm:px-5">
            <div>
              <h2 className="text-sm font-semibold">{queueViewLabel(view)}</h2>
              <p className="mt-0.5 text-[11px] text-[var(--gaia-ink-500)]">
                {queueViewDescription(view)}
              </p>
            </div>
            <span className="text-xs text-[var(--gaia-ink-500)]">{controls.label}</span>
          </header>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[900px] table-fixed text-left text-xs">
              <thead className="bg-[#edf3ef] text-[10px] uppercase tracking-wider text-[var(--gaia-ink-500)]">
                <tr>
                  <th className="w-[29%] px-5 py-3">Solicitud</th>
                  <th className="w-[17%] px-3 py-3">Servicio</th>
                  <th className="w-[23%] px-3 py-3">Atención actual</th>
                  <th className="w-[15%] px-3 py-3 text-center">Estado y plazo</th>
                  <th className="w-[16%] px-5 py-3 text-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {data?.items.map((item) => (
                  <tr
                    className={`border-t border-[var(--gaia-line)] align-top transition hover:bg-[var(--gaia-accent-pale)] ${item.isOverdue ? "border-l-4 border-l-[#b64a3f]" : ""}`}
                    key={item.id}
                  >
                    <td className="px-5 py-4">
                      <div className="flex flex-wrap items-center gap-2">
                        <strong className="text-sm text-[var(--gaia-ink-900)]">{item.number}</strong>
                      </div>
                      {item.subject && <span className="mt-1.5 block truncate text-sm text-[var(--gaia-ink-700)]" title={item.subject}>{item.subject}</span>}
                      <span className="mt-1 block truncate text-[11px] text-[var(--gaia-ink-500)]">
                        Solicitante: {item.requester}
                        {item.submittedAt ? ` · ${formatWorkflowDate(item.submittedAt)}` : ""}
                      </span>
                    </td>
                    <td className="px-3 py-4">
                      <span className="font-medium text-[var(--gaia-ink-700)]">{item.service}</span>
                    </td>
                    <td className="px-3 py-4">
                      {item.responsible || item.unit ? (
                        <div className="flex min-w-0 flex-col">
                          <strong className="truncate text-[#174f49]" title={item.responsible || "Disponible para el área"}>
                            {item.responsible || "Disponible para el área"}
                          </strong>
                          <small className="mt-1 truncate text-[#317c70]" title={item.unit || "Área sin identificar"}>
                            {item.unit || "Área sin identificar"}
                          </small>
                        </div>
                      ) : (
                        <span className="text-[var(--gaia-ink-500)]">
                          Sin gestión activa
                        </span>
                      )}
                    </td>
                    <td className="px-3 py-4 text-center">
                      <span
                        className="inline-flex rounded-full px-2.5 py-1 font-semibold"
                        style={{
                          background: `${item.statusColor || "#64748b"}18`,
                          color: item.statusColor || "#64748b",
                        }}
                      >
                        {item.status}
                      </span>
                      <span className={`mt-2 flex items-center justify-center gap-1 text-[11px] ${item.isOverdue ? "font-bold text-[#b64a3f]" : "text-[var(--gaia-ink-500)]"}`}>
                        {item.isOverdue && <AlertTriangle size={13} />}
                        {item.dueDate ? formatDateOnly(item.dueDate) : "Sin fecha límite"}
                      </span>
                    </td>
                    <td className="px-5 py-4 text-right">
                      <div className="flex flex-wrap justify-end gap-1.5">
                        <button
                          className="inline-flex items-center gap-1.5 rounded-lg bg-[var(--brand-primary)] px-3 py-2 font-semibold text-white"
                          onClick={() => void manage(item.id)}
                          type="button"
                        >
                          <ArrowRightCircle size={14} />
                          Gestionar
                        </button>
                        {security.can("HD.SOLICITUDES.REASIGNAR") && (
                          <button
                            aria-label={`Reasignar ${item.number}`}
                            className="inline-flex items-center gap-1.5 rounded-lg border border-[var(--gaia-line)] px-2.5 py-2 font-semibold text-[var(--brand-primary)]"
                            onClick={() => {
                              setSelected(item);
                              setResponsibleId("");
                              setUnitId("");
                            }}
                            title="Reasignar solicitud"
                            type="button"
                          >
                            <UserRoundCog size={14} />
                          </button>
                        )}
                        {view === "resolved" && item.canDelete && security.can("HD.SOLICITUDES.REASIGNAR") && (
                          <button
                            aria-label={`Retirar ${item.number} de la bandeja`}
                            className="inline-flex items-center rounded-lg border border-[#d9a7af] px-2.5 py-2 text-[#96394b] transition hover:bg-[#fff0f0]"
                            disabled={saving}
                            onClick={() => setDeleteTarget(item)}
                            title="Retirar solicitud resuelta de la bandeja"
                            type="button"
                          >
                            <Trash2 size={14} />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!loading && !snapshot.error && !data?.items.length && (
            <div className="grid min-h-48 place-items-center text-center text-sm text-[var(--gaia-ink-500)]">
              <span>
                <Inbox className="mx-auto mb-3" />
                {queueEmptyMessage(view)}
              </span>
            </div>
          )}
          {loading && (
            <p
              role="status"
              className="p-8 text-center text-sm text-[var(--gaia-ink-500)]"
            >
              Cargando solicitudes…
            </p>
          )}
          <nav
            aria-label="Paginación de solicitudes"
            className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--gaia-line)] p-4 text-xs"
          >
            <span aria-live="polite">Página {query.page}</span>
            <div className="flex flex-wrap items-center gap-2">
              <button
                className="rounded-lg border px-3 py-2 disabled:opacity-40"
                disabled={!controls.previous}
                onClick={() => navigate({ ...query, page: 1 })}
              >
                Primera
              </button>
              <button
                className="rounded-lg border px-3 py-2 disabled:opacity-40"
                disabled={!controls.previous}
                onClick={() => navigate({ ...query, page: query.page - 1 })}
              >
                Anterior
              </button>
              <strong aria-current="page">Página {query.page}</strong>
              <button
                className="rounded-lg border px-3 py-2 disabled:opacity-40"
                disabled={!controls.next}
                onClick={() => navigate({ ...query, page: query.page + 1 })}
              >
                Siguiente
              </button>
              {controls.last !== null && (
                <button
                  className="rounded-lg border px-3 py-2 disabled:opacity-40"
                  disabled={loading || query.page >= controls.last}
                  onClick={() => navigate({ ...query, page: controls.last! })}
                >
                  Última
                </button>
              )}
            </div>
          </nav>
        </section>
      </div>
      <ConfirmDialog
        confirmLabel="Sí, eliminar solicitud"
        description={
          deleteTarget
            ? `${deleteTarget.number} está resuelta y será retirada de las bandejas operativas. Se conservarán sus gestiones, archivos e historial para auditoría.`
            : ""
        }
        destructive
        loading={saving}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => void deleteResolved()}
        open={Boolean(deleteTarget)}
        title="¿Eliminar esta solicitud resuelta?"
      />
      {detail && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-black/35 p-4">
          <section className="max-h-[92vh] w-full max-w-4xl overflow-auto rounded-3xl bg-[var(--surface-card)] p-6 shadow-2xl">
            <header className="flex items-start justify-between gap-4">
              <div>
                <p className="text-xs font-bold uppercase tracking-wider text-[var(--brand-primary)]">
                  {detail.number} · {detail.service}
                </p>
                <h2 className="mt-1 text-2xl font-semibold">
                  {detail.subject}
                </h2>
                <span
                  className="mt-2 inline-block rounded-full px-3 py-1 text-xs font-semibold"
                  style={{
                    background: `${detail.statusColor || "#64748b"}18`,
                    color: detail.statusColor || "#64748b",
                  }}
                >
                  {detail.status}
                </span>
              </div>
              <button
                aria-label="Cerrar gestión"
                className="rounded-full border border-[var(--gaia-line)] p-2"
                onClick={() => setDetail(null)}
                type="button"
              >
                <X size={18} />
              </button>
            </header>
            <div className="mt-5 grid gap-4 md:grid-cols-[1fr_300px]">
              <div>
                <h3 className="text-sm font-semibold">Descripción</h3>
                <p className="mt-2 whitespace-pre-wrap rounded-xl bg-[var(--surface-muted)] p-4 text-sm">
                  {detail.description}
                </p>
                <h3 className="mt-5 flex items-center gap-2 text-sm font-semibold">
                  <MessageSquareText size={16} />
                  Conversación
                </h3>
                <div className="mt-2 space-y-2">
                  {detail.comments.map((item) => (
                    <article
                      className={`rounded-xl p-3 text-sm ${item.isMine ? "ml-8 bg-[var(--gaia-accent-soft)]" : "mr-8 bg-[var(--surface-muted)]"}`}
                      key={item.id}
                    >
                      <small className="text-[var(--gaia-ink-500)]">
                        {item.authorRole}
                        {item.isInternal ? " · Nota interna" : ""} ·{" "}
                        {new Date(item.publishedAt).toLocaleString("es-CO")}
                      </small>
                      <p className="mt-1 whitespace-pre-wrap">{item.content}</p>
                    </article>
                  ))}
                  {!detail.comments.length && (
                    <p className="rounded-xl border border-dashed p-3 text-sm text-[var(--gaia-ink-500)]">
                      Aún no hay comentarios.
                    </p>
                  )}
                </div>
                {detail.allowsRequesterComments && (
                  <form className="mt-3" onSubmit={addComment}>
                    <textarea
                      className="w-full rounded-xl border border-[var(--gaia-line)] bg-[var(--surface-card)] p-3 text-sm"
                      minLength={2}
                      onChange={(event) => setComment(event.target.value)}
                      placeholder="Escribe una respuesta o actualización"
                      required
                      rows={3}
                      value={comment}
                    />
                    <div className="mt-2 flex items-center justify-between">
                      <label className="text-xs">
                        <input
                          checked={internal}
                          className="mr-1"
                          onChange={(event) =>
                            setInternal(event.target.checked)
                          }
                          type="checkbox"
                        />
                        Nota interna
                      </label>
                      <button
                        className="rounded-lg bg-[var(--brand-primary)] px-4 py-2 text-xs font-semibold text-white"
                        disabled={saving}
                      >
                        Publicar comentario
                      </button>
                    </div>
                  </form>
                )}
              </div>
              <aside>
                <div className="rounded-xl border border-[var(--gaia-line)] p-4 text-sm">
                  <p>
                    <small className="block text-[var(--gaia-ink-500)]">
                      Fecha límite
                    </small>
                    <strong>{detail.dueDate || "Por definir"}</strong>
                  </p>
                  <p className="mt-3">
                    <small className="block text-[var(--gaia-ink-500)]">
                      Radicación
                    </small>
                    <strong>
                      {detail.submittedAt
                        ? new Date(detail.submittedAt).toLocaleString("es-CO")
                        : "Sin fecha"}
                    </strong>
                  </p>
                </div>
                <h3 className="mt-4 flex items-center gap-2 text-sm font-semibold">
                  <Paperclip size={15} />
                  Adjuntos
                </h3>
                <div className="mt-2 space-y-2">
                  {attachments.map((item) => (
                    <a
                      className="block rounded-lg border border-[var(--gaia-line)] p-2 text-xs text-[var(--brand-primary)]"
                      href={`${process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168"}/api/solicitudes/attachments/${item.id}/content`}
                      key={item.id}
                    >
                      {item.file.originalName || item.file.storedName} ·{" "}
                      {Math.ceil(item.file.length / 1024)} KB
                    </a>
                  ))}
                  {!attachments.length && (
                    <p className="text-xs text-[var(--gaia-ink-500)]">
                      Sin adjuntos.
                    </p>
                  )}
                </div>
                {detail.transitions.length > 0 && (
                  <form
                    className="mt-5 rounded-xl bg-[var(--gaia-accent-pale)] p-4"
                    onSubmit={transition}
                  >
                    <h3 className="text-sm font-semibold">Cambiar estado</h3>
                    <select
                      className="mt-3 min-h-10 w-full rounded-lg border border-[var(--gaia-line)] bg-[var(--surface-card)] px-2"
                      onChange={(event) => setTransitionId(event.target.value)}
                      required
                      value={transitionId}
                    >
                      <option value="">Selecciona el nuevo estado</option>
                      {detail.transitions.map((item) => (
                        <option key={item.id} value={item.id}>
                          {item.targetState}
                        </option>
                      ))}
                    </select>
                    <textarea
                      className="mt-2 w-full rounded-lg border border-[var(--gaia-line)] bg-[var(--surface-card)] p-2 text-xs"
                      onChange={(event) => setReason(event.target.value)}
                      placeholder="Motivo u observación"
                      rows={2}
                      value={reason}
                    />
                    {detail.transitions.find((item) => item.id === transitionId)
                      ?.requiresSolution && (
                      <textarea
                        className="mt-2 w-full rounded-lg border border-[var(--gaia-line)] bg-[var(--surface-card)] p-2 text-xs"
                        onChange={(event) => setSolution(event.target.value)}
                        placeholder="Solución aplicada"
                        required
                        rows={2}
                        value={solution}
                      />
                    )}
                    {saving && (
                      <p
                        className="mt-3 flex items-center justify-center gap-2 text-xs font-semibold text-[var(--brand-primary)]"
                        role="status"
                      >
                        <LoaderCircle className="animate-spin" size={15} />
                        Estamos guardando el cambio…
                      </p>
                    )}
                    <button
                      className="mt-2 flex w-full items-center justify-center gap-2 rounded-lg bg-[var(--brand-primary)] px-3 py-2 text-xs font-semibold text-white disabled:opacity-60"
                      disabled={saving}
                    >
                      {saving ? (
                        <>
                          <LoaderCircle className="animate-spin" size={15} />
                          Guardando cambio…
                        </>
                      ) : (
                        "Aplicar cambio"
                      )}
                    </button>
                  </form>
                )}
              </aside>
            </div>
          </section>
        </div>
      )}
      {selected && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-black/35 p-4">
          <form
            className="w-full max-w-lg rounded-3xl bg-[var(--surface-card)] p-6 shadow-2xl"
            onSubmit={reassign}
          >
            <h2 className="text-xl font-semibold">
              Reasignar {selected.number}
            </h2>
            <p className="mt-1 text-xs text-[var(--gaia-ink-500)]">
              El cambio quedará registrado en el historial funcional.
            </p>
            <label className="mt-5 block text-xs font-semibold">
              Responsable
              <select
                className="mt-2 min-h-11 w-full rounded-xl border border-[var(--gaia-line)] bg-[var(--surface-card)] px-3"
                onChange={(e) => setResponsibleId(e.target.value)}
                required
                value={responsibleId}
              >
                <option value="">Seleccionar</option>
                {catalog?.responsibles.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="mt-4 block text-xs font-semibold">
              Unidad
              <select
                className="mt-2 min-h-11 w-full rounded-xl border border-[var(--gaia-line)] bg-[var(--surface-card)] px-3"
                onChange={(e) => setUnitId(e.target.value)}
                value={unitId}
              >
                <option value="">Sin unidad</option>
                {catalog?.units.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="mt-4 block text-xs font-semibold">
              Motivo
              <textarea
                className="mt-2 w-full rounded-xl border border-[var(--gaia-line)] bg-[var(--surface-card)] p-3"
                maxLength={500}
                minLength={5}
                onChange={(e) => setReason(e.target.value)}
                required
                rows={3}
                value={reason}
              />
            </label>
            <div className="mt-5 flex justify-end gap-3">
              <button
                className="rounded-xl border px-4 py-2"
                onClick={() => setSelected(null)}
                type="button"
              >
                Cancelar
              </button>
              <button
                className="rounded-xl bg-[var(--brand-primary)] px-4 py-2 font-semibold text-white"
                disabled={saving}
                type="submit"
              >
                {saving ? "Guardando…" : "Confirmar"}
              </button>
            </div>
          </form>
        </div>
      )}
      {completion && detail && (
        <SolicitudesManagementFormDialog
          finalStep={completion.item.final}
          managementId={completion.item.id}
          onCancel={() => setCompletion(null)}
          onCompleted={finishManagement}
          requestId={detail.id}
          requiresObservation={completion.item.requiresObservation}
          result={completion.result}
        />
      )}
    </main>
  );
}

function StageAssignmentDialog({
  workflow,
  catalog,
  busy,
  close,
  submit,
}: {
  workflow: Workflow;
  catalog: Catalog | null;
  busy: boolean;
  close: () => void;
  submit: (
    managementId: string,
    responsibleId: string,
    reason: string,
  ) => Promise<void>;
}) {
  const active = workflow.managements.filter((item) =>
      [299540191, 299540192].includes(item.status),
    ),
    [managementId, setManagementId] = useState(active[0]?.id ?? ""),
    [responsibleId, setResponsibleId] = useState(""),
    [reason, setReason] = useState(""),
    [personSearch, setPersonSearch] = useState(""),
    [localError, setLocalError] = useState("");
  const selected = active.find((item) => item.id === managementId);
  const people = (catalog?.responsibles ?? []).filter(
    (person) =>
      (!selected?.unitId ||
        person.unitIds?.includes(selected.unitId) ||
        person.id === selected.responsibleId) &&
      person.name
        .toLocaleLowerCase("es")
        .includes(personSearch.trim().toLocaleLowerCase("es")),
  );
  async function save(event: FormEvent) {
    event.preventDefault();
    setLocalError("");
    try {
      await submit(managementId, responsibleId, reason);
    } catch (value) {
      setLocalError(
        value instanceof Error
          ? value.message
          : "No fue posible reasignar la etapa.",
      );
    }
  }
  return (
    <div
      className="fixed inset-0 z-[88] grid place-items-center bg-black/45 p-3"
      role="dialog"
      aria-modal="true"
      aria-label="Reasignar etapa"
    >
      <form
        className="max-h-[94vh] w-full max-w-xl overflow-auto rounded-3xl bg-[var(--surface-card)] p-6 shadow-2xl"
        onSubmit={save}
      >
        <header className="flex items-start justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[.14em] text-[var(--brand-primary)]">
              Distribución de trabajo
            </p>
            <h2 className="mt-1 text-2xl font-semibold">
              Reasignar una etapa activa
            </h2>
            <p className="mt-1 text-sm text-[var(--gaia-ink-500)]">
              Este cambio afecta únicamente la etapa seleccionada, no el
              responsable general de la solicitud.
            </p>
          </div>
          <button
            aria-label="Cerrar"
            className="rounded-full border p-2"
            disabled={busy}
            onClick={close}
            type="button"
          >
            <X size={18} />
          </button>
        </header>
        {localError && (
          <p
            className="mt-4 rounded-xl bg-[#fff0f0] p-3 text-sm text-[#9a384d]"
            role="alert"
          >
            {localError}
          </p>
        )}
        <label className="mt-5 block text-sm font-semibold">
          Etapa activa
          <select
            className="mt-2 min-h-12 w-full rounded-xl border border-[var(--gaia-line)] bg-white px-3"
            disabled={busy}
            onChange={(event) => {
              setManagementId(event.target.value);
              setResponsibleId("");
              setPersonSearch("");
            }}
            required
            value={managementId}
          >
            {active.map((item) => (
              <option key={item.id} value={item.id}>
                {item.formTitle || friendlyStep(item.stepCode)} ·{" "}
                {item.unitName || "Sin unidad"}
              </option>
            ))}
          </select>
        </label>
        <div className="mt-4 rounded-xl bg-[var(--surface-muted)] p-3 text-xs text-[var(--gaia-ink-500)]">
          <strong className="block text-[var(--gaia-ink-900)]">
            Asignación actual
          </strong>
          {selected?.responsibleName || "Disponible para la unidad"}
          {selected?.unitName && ` · ${selected.unitName}`}
        </div>
        <label className="mt-4 block text-sm font-semibold">
          Buscar persona
          <input
            className="mt-2 min-h-11 w-full rounded-xl border border-[var(--gaia-line)] px-3 font-normal"
            disabled={busy}
            onChange={(event) => setPersonSearch(event.target.value)}
            placeholder="Escribe el nombre"
            value={personSearch}
          />
          <small className="mt-1 block font-normal text-[var(--gaia-ink-500)]">
            Solo se muestran integrantes con asignación vigente en{" "}
            {selected?.unitName || "la unidad responsable"}.
          </small>
        </label>
        <label className="mt-4 block text-sm font-semibold">
          Nueva asignación
          <select
            className="mt-2 min-h-12 w-full rounded-xl border border-[var(--gaia-line)] bg-white px-3"
            disabled={busy}
            onChange={(event) => setResponsibleId(event.target.value)}
            value={responsibleId}
          >
            <option value="">
              Dejar disponible para la unidad responsable
            </option>
            {people.map((person) => (
              <option key={person.id} value={person.id}>
                {person.name}
              </option>
            ))}
          </select>
          {personSearch && people.length === 0 && (
            <small className="mt-1 block font-normal text-[#9a384d]">
              No se encontraron integrantes vigentes con ese nombre.
            </small>
          )}
          <small className="mt-1 block font-normal text-[var(--gaia-ink-500)]">
            Si no seleccionas una persona, cualquier integrante autorizado de la
            unidad podrá tomarla.
          </small>
        </label>
        <label className="mt-4 block text-sm font-semibold">
          Motivo del cambio
          <textarea
            className="mt-2 w-full rounded-xl border border-[var(--gaia-line)] p-3"
            disabled={busy}
            maxLength={500}
            minLength={5}
            onChange={(event) => setReason(event.target.value)}
            placeholder="Explica brevemente por qué se redistribuye esta etapa"
            required
            rows={3}
            value={reason}
          />
        </label>
        <div className="sticky bottom-0 -mx-6 -mb-6 mt-6 flex flex-wrap justify-end gap-2 border-t bg-[var(--surface-card)] px-6 py-4">
          <button
            className="rounded-xl border px-4 py-2 transition hover:bg-[var(--surface-muted)]"
            disabled={busy}
            onClick={close}
            type="button"
          >
            Cancelar
          </button>
          <button
            className="flex min-w-44 items-center justify-center gap-2 rounded-xl bg-[var(--brand-primary)] px-5 py-2 font-semibold text-white disabled:opacity-60"
            disabled={busy || !managementId}
          >
            {busy ? (
              <>
                <LoaderCircle className="animate-spin" size={16} />
                Guardando cambio…
              </>
            ) : (
              "Confirmar reasignación"
            )}
          </button>
        </div>
      </form>
    </div>
  );
}

function AuditDrawer({ detail, close }: { detail: Detail; close: () => void }) {
  const events = [...(detail.history ?? [])].sort(
    (a, b) =>
      new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime(),
  );
  return (
    <div
      className="fixed inset-0 z-[85] flex justify-end bg-black/35"
      role="dialog"
      aria-modal="true"
      aria-label="Actividad de la solicitud"
    >
      <button
        aria-label="Cerrar actividad"
        className="absolute inset-0 cursor-default"
        onClick={close}
        type="button"
      />
      <aside className="relative h-full w-full max-w-md overflow-auto bg-[var(--surface-card)] p-5 shadow-2xl sm:p-6">
        <header className="flex items-start justify-between gap-4 border-b border-[var(--gaia-line)] pb-4">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[.14em] text-[var(--brand-primary)]">
              Trazabilidad del expediente
            </p>
            <h2 className="mt-1 text-2xl font-semibold">
              Actividad de {detail.number}
            </h2>
            <p className="mt-1 text-sm text-[var(--gaia-ink-500)]">
              Decisiones, asignaciones, estados, archivos y comunicaciones en
              orden cronológico.
            </p>
          </div>
          <button
            aria-label="Cerrar"
            className="rounded-full border p-2 transition hover:bg-[var(--surface-muted)]"
            onClick={close}
            type="button"
          >
            <X size={19} />
          </button>
        </header>
        <div className="mt-5">
          {events.map((event, index) => (
            <article
              className="relative grid grid-cols-[16px_1fr] gap-2 pb-4"
              key={event.id}
            >
              <div className="relative">
                <span className="relative z-10 mt-1 block size-3 rounded-full bg-[var(--brand-primary)] ring-4 ring-[var(--gaia-accent-soft)]" />
                {index < events.length - 1 && (
                  <span className="absolute left-[5px] top-4 h-[calc(100%+4px)] w-px bg-[var(--gaia-line)]" />
                )}
              </div>
              <div className="rounded-xl border border-[var(--gaia-line)] bg-white px-3 py-3">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <strong className="text-sm">
                    {event.title || historyMovement(event.movement)}
                  </strong>
                  <time className="text-[10px] text-[var(--gaia-ink-500)]">
                    {formatWorkflowDate(event.occurredAt)}
                  </time>
                </div>
                {event.detail && (
                  <p className="mt-2 whitespace-pre-wrap text-sm leading-5 text-[var(--gaia-ink-500)]">
                    {event.detail}
                  </p>
                )}
                <small className="mt-2 block truncate text-[10px] text-[var(--gaia-ink-500)]">
                  {event.actor || "Sistema"}
                </small>
              </div>
            </article>
          ))}
          {!events.length && (
            <Empty text="Todavía no existen movimientos registrados para esta solicitud." />
          )}
        </div>
      </aside>
    </div>
  );
}

function historyMovement(value: number) {
  return (
    (
      {
        299540103: "Estado actualizado",
        299540104: "Responsable reasignado",
        299540105: "Gestión tomada",
        299540106: "Comentario publicado",
        299540107: "Archivo agregado",
        299540108: "Archivo inactivado",
        299540202: "Flujo reactivado",
        299540205: "Gestión tomada",
        299540206: "Gestión reasignada",
        299540208: "Etapa completada",
        299540209: "Etapa aprobada",
        299540210: "Etapa rechazada",
        299540211: "Información solicitada",
        299540212: "Espera del solicitante",
        299540213: "Gestión reanudada",
      } as Record<number, string>
    )[value] ?? "Actividad registrada"
  );
}

type WorkflowPanelProps = {
  workflow: Workflow;
  detail: Detail;
  attachments: Attachment[];
  busy: boolean;
  comment: string;
  internal: boolean;
  transitionId: string;
  reason: string;
  solution: string;
  complete: (item: WorkflowItem, result: number) => Promise<void>;
  take: (item: WorkflowItem) => Promise<void>;
  resume: (item: WorkflowItem) => Promise<void>;
  upload: (item: WorkflowItem, file: File) => Promise<void>;
  uploadingManagementId: string | null;
  takingManagementId: string | null;
  onClose: () => void;
  onComment: (event: FormEvent) => Promise<void>;
  onTransition: (event: FormEvent) => Promise<void>;
  setComment: (value: string) => void;
  setInternal: (value: boolean) => void;
  setTransitionId: (value: string) => void;
  setReason: (value: string) => void;
  setSolution: (value: string) => void;
};

function WorkflowPanel(props: WorkflowPanelProps) {
  const {
    workflow,
    detail,
    attachments,
    busy,
    comment,
    internal,
    complete,
    take,
    resume,
    upload,
    uploadingManagementId,
    takingManagementId,
    onClose,
    onComment,
    setComment,
    setInternal,
  } = props;
  const [supportView, setSupportView] = useState<
    "conversation" | "files" | "history"
  >("conversation");
  const current = workflow.managements.filter((item) =>
    [299540191, 299540192, 299540193].includes(item.status),
  );
  const previous = workflow.managements.filter((item) =>
    [299540194, 299540195].includes(item.status),
  );
  const hasAvailable = current.some((item) => item.canTake),
    hasInProgress = current.some((item) => item.canManage),
    hasWaiting = current.some((item) => item.status === 299540193),
    hasExternal = current.some(
      (item) => !item.canTake && !item.canManage && item.status !== 299540193,
    );
  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-black/45 p-2 backdrop-blur-[2px] sm:p-5">
      <section className="flex h-[min(96vh,980px)] w-full max-w-[1180px] flex-col overflow-hidden rounded-[28px] border border-[var(--gaia-line)] bg-[#f6f8f7] shadow-2xl">
        <header className="z-10 flex flex-wrap items-center justify-between gap-4 border-b border-[var(--gaia-line)] bg-white px-5 py-4 sm:px-8">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-bold uppercase tracking-[.13em] text-[var(--brand-primary)]">
                {detail.number}
              </span>
              <span
                className="rounded-full px-2.5 py-1 text-[11px] font-semibold"
                style={{
                  background: `${detail.statusColor || "#64748b"}18`,
                  color: detail.statusColor || "#64748b",
                }}
              >
                {detail.status}
              </span>
            </div>
            <h2 className="mt-1 truncate text-lg font-semibold sm:text-xl">
              {detail.service} · Flujo v{workflow.version}
            </h2>
          </div>
          <div className="flex items-center gap-2">
            {workflow.status === 299540182 && (
              <button
                className="inline-flex items-center gap-2 rounded-xl border border-[var(--brand-primary)] px-3 py-2 text-xs font-semibold text-[var(--brand-primary)] transition hover:bg-[var(--gaia-accent-pale)]"
                onClick={() =>
                  downloadClosurePdf({
                    ...detail,
                    managements: workflow.managements,
                  })
                }
                type="button"
              >
                <FileDown size={16} />
                <span className="hidden sm:inline">Descargar constancia</span>
              </button>
            )}
            <button
              aria-label="Cerrar expediente"
              className="rounded-full border border-[var(--gaia-line)] p-2.5 transition hover:bg-[var(--surface-muted)]"
              onClick={onClose}
              type="button"
            >
              <X size={20} />
            </button>
          </div>
        </header>

        <div className="flex-1 overflow-auto">
          <main className="mx-auto max-w-[980px] space-y-5 px-4 py-5 sm:px-7 sm:py-7">
            <section className="overflow-hidden rounded-2xl border border-[var(--gaia-line)] bg-white">
              <div className="p-5 sm:p-6">
                <p className="text-[10px] font-bold uppercase tracking-[.14em] text-[var(--brand-primary)]">
                  Solicitud
                </p>
                <h3 className="mt-2 text-xl font-semibold leading-tight sm:text-2xl">
                  {detail.subject}
                </h3>
                <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-[var(--gaia-ink-700)]">
                  {detail.description}
                </p>
              </div>
              <div className="grid border-t border-[var(--gaia-line)] bg-[#fafcfb] sm:grid-cols-3 sm:divide-x sm:divide-[var(--gaia-line)]">
                <div className="px-5 py-3.5">
                  <small className="block text-[var(--gaia-ink-500)]">Radicada</small>
                  <strong className="text-sm">
                    {detail.submittedAt
                      ? formatWorkflowDate(detail.submittedAt)
                      : "Sin fecha"}
                  </strong>
                </div>
                <div className="border-t border-[var(--gaia-line)] px-5 py-3.5 sm:border-t-0">
                  <small className="block text-[var(--gaia-ink-500)]">Fecha límite</small>
                  <strong className="text-sm">{detail.dueDate || "Por definir"}</strong>
                </div>
                <div className="border-t border-[var(--gaia-line)] px-5 py-3.5 sm:border-t-0">
                  <small className="block text-[var(--gaia-ink-500)]">Recorrido</small>
                  <strong className="text-sm">
                    {previous.length} finalizadas · {current.length} activas
                  </strong>
                </div>
              </div>
            </section>

            <section className="rounded-2xl border-2 border-[#a9cfc7] bg-white p-5 shadow-sm sm:p-6">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="grid size-7 place-items-center rounded-full bg-[var(--brand-primary)] text-xs font-bold text-white">
                      1
                    </span>
                    <p className="text-[10px] font-bold uppercase tracking-[.14em] text-[var(--brand-primary)]">
                      Gestión actual
                    </p>
                  </div>
                  <h3 className="mt-2 text-xl font-semibold">Qué debes hacer ahora</h3>
                  <p className="mt-1 max-w-3xl text-sm leading-6 text-[var(--gaia-ink-500)]">
                    {hasInProgress
                      ? "La gestión está a tu cargo. Revisa la etapa y selecciona la acción necesaria para continuar el recorrido."
                      : hasAvailable
                        ? "Esta etapa está disponible para tu equipo. Tómala para habilitar sus acciones y formulario."
                        : hasWaiting
                          ? "La etapa está esperando información del solicitante. Podrás continuar cuando responda."
                          : hasExternal
                            ? "La etapa está asignada a otra unidad. Puedes consultar su avance, pero ese equipo debe gestionarla."
                            : "Consulta la etapa vigente y su estado actual."}
                  </p>
                </div>
                <span className={`rounded-full px-3 py-1.5 text-xs font-semibold ${hasInProgress || hasAvailable ? "bg-[#e2f3ec] text-[#17695b]" : "bg-[var(--surface-muted)] text-[var(--gaia-ink-500)]"}`}>
                  {hasInProgress
                    ? "Acción requerida"
                    : hasAvailable
                      ? "Disponible para tomar"
                      : "Solo consulta"}
                </span>
              </div>
              <div className="mt-5 space-y-4">
                {current.map((item) => (
                  <ManagementCard
                    attachments={attachments}
                    busy={busy}
                    complete={complete}
                    item={item}
                    key={item.id}
                    resume={resume}
                    take={take}
                    taking={takingManagementId === item.id}
                    upload={upload}
                    uploading={uploadingManagementId === item.id}
                  />
                ))}
                {!current.length && (
                  <Empty text="Esta solicitud no tiene gestiones activas en este momento." />
                )}
              </div>
            </section>

            <section className="overflow-hidden rounded-2xl border border-[var(--gaia-line)] bg-white">
              <div className="border-b border-[var(--gaia-line)] px-4 pt-4 sm:px-6">
                <p className="px-1 text-[10px] font-bold uppercase tracking-[.14em] text-[var(--brand-primary)]">
                  Consulta del expediente
                </p>
                <nav className="mt-2 flex gap-1 overflow-x-auto" aria-label="Contenido del expediente">
                  {([
                    ["conversation", "Conversación", detail.comments.length],
                    ["files", "Archivos", attachments.length],
                    ["history", "Historial", previous.length],
                  ] as const).map(([value, label, count]) => (
                    <button
                      aria-current={supportView === value ? "page" : undefined}
                      className={`whitespace-nowrap border-b-2 px-3 py-3 text-sm font-semibold transition ${supportView === value ? "border-[var(--brand-primary)] text-[var(--brand-primary)]" : "border-transparent text-[var(--gaia-ink-500)] hover:text-[var(--gaia-ink-900)]"}`}
                      key={value}
                      onClick={() => setSupportView(value)}
                      type="button"
                    >
                      {label}
                      <span className="ml-2 rounded-full bg-[var(--surface-muted)] px-2 py-0.5 text-[10px]">
                        {count}
                      </span>
                    </button>
                  ))}
                </nav>
              </div>

              <div className="p-4 sm:p-6">
                {supportView === "conversation" && (
                  <div>
                    <div className="max-h-72 space-y-2 overflow-auto rounded-xl bg-[var(--surface-muted)] p-3">
                      {detail.comments.map((item) => (
                        <article
                          className={`rounded-xl p-3 text-sm ${item.isMine ? "ml-5 bg-[var(--gaia-accent-soft)]" : "mr-5 bg-white"}`}
                          key={item.id}
                        >
                          <small className="text-[var(--gaia-ink-500)]">
                            {item.authorRole}
                            {item.isInternal ? " · Nota interna" : ""} ·{" "}
                            {formatWorkflowDate(item.publishedAt)}
                          </small>
                          <p className="mt-1 whitespace-pre-wrap">{item.content}</p>
                        </article>
                      ))}
                      {!detail.comments.length && (
                        <p className="p-5 text-center text-sm text-[var(--gaia-ink-500)]">
                          Aún no hay mensajes en esta solicitud.
                        </p>
                      )}
                    </div>
                    {detail.allowsRequesterComments && (
                      <form className="mt-4" onSubmit={onComment}>
                        <label className="text-sm font-semibold" htmlFor="workflow-comment">
                          Publicar una actualización
                        </label>
                        <textarea
                          className="mt-2 w-full rounded-xl border border-[var(--gaia-line)] p-3 text-sm"
                          id="workflow-comment"
                          minLength={2}
                          onChange={(event) => setComment(event.target.value)}
                          placeholder="Escribe una respuesta o una nota para el equipo"
                          required
                          rows={3}
                          value={comment}
                        />
                        <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
                          <label className="text-xs text-[var(--gaia-ink-700)]">
                            <input
                              checked={internal}
                              className="mr-2"
                              onChange={(event) => setInternal(event.target.checked)}
                              type="checkbox"
                            />
                            Solo visible para el equipo interno
                          </label>
                          <button
                            className="rounded-lg bg-[var(--brand-primary)] px-5 py-2.5 text-xs font-semibold text-white disabled:opacity-60"
                            disabled={busy}
                          >
                            {busy ? "Publicando…" : internal ? "Publicar nota interna" : "Publicar comentario"}
                          </button>
                        </div>
                      </form>
                    )}
                  </div>
                )}

                {supportView === "files" && (
                  <div>
                    <div className="grid gap-3 sm:grid-cols-2">
                      {attachments.map((item) => (
                        <a
                          className="flex items-center gap-3 rounded-xl border border-[var(--gaia-line)] p-4 transition hover:bg-[var(--gaia-accent-pale)]"
                          href={`${process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168"}/api/solicitudes/attachments/${item.id}/content`}
                          key={item.id}
                        >
                          <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-[var(--gaia-accent-soft)] text-[var(--brand-primary)]">
                            <Paperclip size={17} />
                          </span>
                          <span className="min-w-0">
                            <strong className="block truncate text-sm text-[var(--brand-primary)]">
                              {item.file.originalName || item.file.storedName}
                            </strong>
                            <small className="text-[var(--gaia-ink-500)]">
                              {Math.ceil(item.file.length / 1024)} KB · {item.managementId ? "Gestión" : "Solicitud"}
                            </small>
                          </span>
                        </a>
                      ))}
                    </div>
                    {!attachments.length && (
                      <Empty text="Esta solicitud no tiene archivos adjuntos." />
                    )}
                  </div>
                )}

                {supportView === "history" && (
                  <div className="space-y-3">
                    {[...previous].reverse().map((item, index) => (
                      <article className="relative rounded-xl border border-[var(--gaia-line)] p-4 pl-14" key={item.id}>
                        <span className="absolute left-4 top-4 grid size-7 place-items-center rounded-full bg-[var(--surface-muted)] text-xs font-bold text-[var(--brand-primary)]">
                          {previous.length - index}
                        </span>
                        <strong className="text-sm">
                          {item.formTitle || friendlyStep(item.stepCode)}
                        </strong>
                        <small className="mt-1 block text-[var(--gaia-ink-500)]">
                          {workflowResult(item.result ?? 299540170)}
                          {item.completedAt ? ` · ${formatWorkflowDate(item.completedAt)}` : ""}
                        </small>
                        {item.observation && (
                          <p className="mt-2 whitespace-pre-wrap text-sm text-[var(--gaia-ink-700)]">
                            {item.observation}
                          </p>
                        )}
                      </article>
                    ))}
                    {!previous.length && (
                      <Empty text="Todavía no existen gestiones anteriores." />
                    )}
                  </div>
                )}
              </div>
            </section>
          </main>
        </div>
      </section>
    </div>
  );
}

type WorkspaceTab =
  | "summary"
  | "current"
  | "history"
  | "conversation"
  | "files";
function LegacyWorkflowPanel({
  workflow,
  detail,
  attachments,
  busy,
  comment,
  internal,
  transitionId,
  reason,
  solution,
  complete,
  take,
  upload,
  onClose,
  onComment,
  onTransition,
  setComment,
  setInternal,
  setTransitionId,
  setReason,
  setSolution,
}: {
  workflow: Workflow;
  detail: Detail;
  attachments: Attachment[];
  busy: boolean;
  comment: string;
  internal: boolean;
  transitionId: string;
  reason: string;
  solution: string;
  complete: (item: WorkflowItem, result: number) => Promise<void>;
  take: (item: WorkflowItem) => Promise<void>;
  upload: (item: WorkflowItem, file: File) => Promise<void>;
  onClose: () => void;
  onComment: (event: FormEvent) => Promise<void>;
  onTransition: (event: FormEvent) => Promise<void>;
  setComment: (value: string) => void;
  setInternal: (value: boolean) => void;
  setTransitionId: (value: string) => void;
  setReason: (value: string) => void;
  setSolution: (value: string) => void;
}) {
  const [tab, setTab] = useState<WorkspaceTab>("current");
  const current = workflow.managements.filter((item) =>
    [299540191, 299540192, 299540193].includes(item.status),
  );
  const previous = workflow.managements.filter((item) =>
    [299540194, 299540195].includes(item.status),
  );
  const tabs: [WorkspaceTab, string, number?][] = [
    ["summary", "Resumen"],
    ["current", "Gestión actual", current.length],
    ["history", "Recorrido", previous.length],
    ["conversation", "Conversación", detail.comments.length],
    ["files", "Archivos", attachments.length],
  ];
  return (
    <div className="fixed inset-0 z-[70] bg-[#eef3ef] p-2 sm:p-4">
      <section className="mx-auto flex h-full max-w-[1500px] flex-col overflow-hidden rounded-3xl border border-[var(--gaia-line)] bg-[var(--surface-card)] shadow-2xl">
        <header className="flex flex-wrap items-start justify-between gap-4 border-b border-[var(--gaia-line)] px-5 py-4 sm:px-7">
          <div>
            <p className="text-xs font-bold uppercase tracking-wider text-[var(--brand-primary)]">
              {detail.number} · {detail.service} · Flujo v{workflow.version}
            </p>
            <h2 className="mt-1 text-2xl font-semibold">{detail.subject}</h2>
            <div className="mt-2 flex flex-wrap gap-2">
              <span
                className="rounded-full px-3 py-1 text-xs font-semibold"
                style={{
                  background: `${detail.statusColor || "#64748b"}18`,
                  color: detail.statusColor || "#64748b",
                }}
              >
                {detail.status}
              </span>
              <span className="rounded-full bg-[var(--gaia-accent-soft)] px-3 py-1 text-xs font-semibold">
                {current.length}{" "}
                {current.length === 1 ? "gestión activa" : "gestiones activas"}
              </span>
            </div>
          </div>
          <button
            aria-label="Cerrar expediente"
            className="rounded-full border border-[var(--gaia-line)] p-2"
            onClick={onClose}
            type="button"
          >
            <X size={20} />
          </button>
        </header>
        <nav
          aria-label="Secciones del expediente"
          className="flex gap-1 overflow-x-auto border-b border-[var(--gaia-line)] px-4 py-2 sm:px-7"
        >
          {tabs.map(([value, label, count]) => (
            <button
              aria-current={tab === value ? "page" : undefined}
              className={`whitespace-nowrap rounded-xl px-4 py-2 text-sm font-semibold ${tab === value ? "bg-[var(--brand-primary)] text-white" : "hover:bg-[var(--gaia-accent-pale)]"}`}
              key={value}
              onClick={() => setTab(value)}
              type="button"
            >
              {label}
              {count !== undefined && (
                <span className="ml-2 rounded-full bg-white/20 px-2 py-0.5 text-[10px]">
                  {count}
                </span>
              )}
            </button>
          ))}
        </nav>
        <div className="flex-1 overflow-auto p-5 sm:p-7">
          {tab === "summary" && (
            <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_320px]">
              <section>
                <h3 className="text-lg font-semibold">
                  Información de la solicitud
                </h3>
                <p className="mt-3 whitespace-pre-wrap rounded-2xl bg-[var(--surface-muted)] p-5 text-sm leading-6">
                  {detail.description}
                </p>
                <div className="mt-5 grid gap-3 sm:grid-cols-3">
                  <Summary
                    label="Radicación"
                    value={
                      detail.submittedAt
                        ? formatWorkflowDate(detail.submittedAt)
                        : "Sin fecha"
                    }
                  />
                  <Summary
                    label="Fecha límite"
                    value={detail.dueDate || "Por definir"}
                  />
                  <Summary
                    label="Etapas registradas"
                    value={String(workflow.managements.length)}
                  />
                </div>
              </section>
              <aside>
                <h3 className="text-lg font-semibold">Estado administrativo</h3>
                <p className="mt-3 rounded-2xl border border-[var(--gaia-line)] p-4 text-sm">
                  La solicitud conserva todas sus gestiones. Las etapas
                  terminadas son de solo lectura y las activas aparecen en
                  Gestión actual.
                </p>
                {detail.transitions.length > 0 && (
                  <form
                    className="mt-4 rounded-2xl bg-[var(--gaia-accent-pale)] p-4"
                    onSubmit={onTransition}
                  >
                    <h4 className="font-semibold">
                      Cambio administrativo de estado
                    </h4>
                    <select
                      className="mt-3 min-h-10 w-full rounded-lg border bg-white px-2"
                      onChange={(event) => setTransitionId(event.target.value)}
                      required
                      value={transitionId}
                    >
                      <option value="">Selecciona el nuevo estado</option>
                      {detail.transitions.map((item) => (
                        <option key={item.id} value={item.id}>
                          {item.targetState}
                        </option>
                      ))}
                    </select>
                    <textarea
                      className="mt-2 w-full rounded-lg border bg-white p-2 text-xs"
                      onChange={(event) => setReason(event.target.value)}
                      placeholder="Motivo u observación"
                      rows={3}
                      value={reason}
                    />
                    {detail.transitions.find((item) => item.id === transitionId)
                      ?.requiresSolution && (
                      <textarea
                        className="mt-2 w-full rounded-lg border bg-white p-2 text-xs"
                        onChange={(event) => setSolution(event.target.value)}
                        placeholder="Solución aplicada"
                        required
                        rows={3}
                        value={solution}
                      />
                    )}
                    <button
                      className="mt-2 w-full rounded-lg bg-[var(--brand-primary)] px-3 py-2 text-xs font-semibold text-white disabled:opacity-60"
                      disabled={busy}
                    >
                      {busy ? "Guardando cambio…" : "Aplicar cambio"}
                    </button>
                  </form>
                )}
              </aside>
            </div>
          )}
          {tab === "current" && (
            <section>
              <div className="mb-4">
                <h3 className="text-lg font-semibold">
                  Qué debes gestionar ahora
                </h3>
                <p className="text-sm text-[var(--gaia-ink-500)]">
                  Cada tarjeta corresponde a una etapa activa. Completarla
                  ejecutará las conexiones configuradas.
                </p>
              </div>
              <div className="grid gap-4 xl:grid-cols-2">
                {current.map((item) => (
                  <ManagementCard
                    attachments={attachments}
                    busy={busy}
                    complete={complete}
                    item={item}
                    key={item.id}
                    take={take}
                    upload={upload}
                  />
                ))}
              </div>
              {!current.length && (
                <Empty text="Esta solicitud no tiene gestiones activas en este momento." />
              )}
            </section>
          )}
          {tab === "history" && (
            <section>
              <div className="mb-4">
                <h3 className="text-lg font-semibold">Recorrido de atención</h3>
                <p className="text-sm text-[var(--gaia-ink-500)]">
                  Aquí se conserva lo realizado por las unidades anteriores.
                </p>
              </div>
              <div className="space-y-4">
                {previous.map((item, index) => (
                  <ManagementCard
                    attachments={attachments}
                    busy={busy}
                    complete={complete}
                    index={index}
                    item={item}
                    key={item.id}
                    take={take}
                    upload={upload}
                  />
                ))}
              </div>
              {!previous.length && (
                <Empty text="Todavía no existen etapas completadas." />
              )}
            </section>
          )}
          {tab === "conversation" && (
            <section className="mx-auto max-w-4xl">
              <h3 className="flex items-center gap-2 text-lg font-semibold">
                <MessageSquareText size={19} />
                Conversación y notas internas
              </h3>
              <div className="mt-4 space-y-3">
                {detail.comments.map((item) => (
                  <article
                    className={`rounded-2xl p-4 text-sm ${item.isMine ? "ml-8 bg-[var(--gaia-accent-soft)]" : "mr-8 bg-[var(--surface-muted)]"}`}
                    key={item.id}
                  >
                    <small className="text-[var(--gaia-ink-500)]">
                      {item.authorRole}
                      {item.isInternal ? " · Nota interna" : ""} ·{" "}
                      {formatWorkflowDate(item.publishedAt)}
                    </small>
                    <p className="mt-2 whitespace-pre-wrap">{item.content}</p>
                  </article>
                ))}
                {!detail.comments.length && (
                  <Empty text="Aún no hay comentarios en la solicitud." />
                )}
              </div>
              {detail.allowsRequesterComments && (
                <form
                  className="mt-5 rounded-2xl border border-[var(--gaia-line)] p-4"
                  onSubmit={onComment}
                >
                  <textarea
                    className="w-full rounded-xl border p-3 text-sm"
                    minLength={2}
                    onChange={(event) => setComment(event.target.value)}
                    placeholder="Escribe una respuesta o actualización"
                    required
                    rows={4}
                    value={comment}
                  />
                  <div className="mt-3 flex items-center justify-between">
                    <label className="text-xs">
                      <input
                        checked={internal}
                        className="mr-2"
                        onChange={(event) => setInternal(event.target.checked)}
                        type="checkbox"
                      />
                      Publicar como nota interna
                    </label>
                    <button
                      className="rounded-lg bg-[var(--brand-primary)] px-4 py-2 text-xs font-semibold text-white"
                      disabled={busy}
                    >
                      Publicar comentario
                    </button>
                  </div>
                </form>
              )}
            </section>
          )}
          {tab === "files" && (
            <section>
              <h3 className="flex items-center gap-2 text-lg font-semibold">
                <Paperclip size={19} />
                Archivos del expediente
              </h3>
              <p className="mt-1 text-sm text-[var(--gaia-ink-500)]">
                Los archivos asociados a una etapa también aparecen dentro de su
                gestión.
              </p>
              <div className="mt-5 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                {attachments.map((item) => (
                  <a
                    className="rounded-2xl border border-[var(--gaia-line)] p-4 hover:bg-[var(--gaia-accent-pale)]"
                    href={`${process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168"}/api/solicitudes/attachments/${item.id}/content`}
                    key={item.id}
                  >
                    <strong className="block truncate text-sm text-[var(--brand-primary)]">
                      {item.file.originalName || item.file.storedName}
                    </strong>
                    <small className="mt-1 block text-[var(--gaia-ink-500)]">
                      {Math.ceil(item.file.length / 1024)} KB ·{" "}
                      {item.managementId
                        ? "Archivo de gestión"
                        : "Archivo general"}
                    </small>
                  </a>
                ))}
              </div>
              {!attachments.length && (
                <Empty text="Esta solicitud no tiene archivos adjuntos." />
              )}
            </section>
          )}
        </div>
      </section>
    </div>
  );
}
void LegacyWorkflowPanel;
function ManagementCard({
  item,
  attachments,
  busy,
  index,
  complete,
  take,
  taking = false,
  resume,
  upload,
  uploading = false,
}: {
  item: WorkflowItem;
  attachments: Attachment[];
  busy: boolean;
  index?: number;
  complete: (item: WorkflowItem, result: number) => Promise<void>;
  take: (item: WorkflowItem) => Promise<void>;
  taking?: boolean;
  resume?: (item: WorkflowItem) => Promise<void>;
  upload: (item: WorkflowItem, file: File) => Promise<void>;
  uploading?: boolean;
}) {
  const actionable = item.canTake || item.canManage,
    needsTake = item.canTake,
    canComplete = item.canManage,
    files = attachments.filter((file) => file.managementId === item.id);
  return (
    <article
      className={`relative rounded-2xl border p-4 shadow-sm ${actionable ? "border-[var(--brand-primary)] bg-[var(--gaia-accent-pale)]" : "border-[var(--gaia-line)] bg-white"}`}
    >
      {index !== undefined && (
        <span className="absolute -left-3 top-4 grid size-7 place-items-center rounded-full bg-[var(--brand-primary)] text-xs font-bold text-white">
          {index + 1}
        </span>
      )}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <strong className="text-base">
            {item.formTitle || friendlyStep(item.stepCode)}
          </strong>
          <small className="block text-[var(--gaia-ink-500)]">
            {item.stepCode} · Ejecución {item.execution} ·{" "}
            {workflowStatus(item.status)}
          </small>
          {(item.responsibleName || item.unitName) && (
            <div className="mt-2 inline-flex flex-col rounded-xl border border-[#afd4c8] bg-[#e8f6f1] px-3 py-2">
              <small className="text-[9px] font-bold uppercase tracking-wider text-[#317c70]">
                Área que atiende
              </small>
              <strong className="text-xs text-[#174f49]">
                {item.unitName || "Área sin identificar"}
              </strong>
              <small className="mt-0.5 text-[10px] text-[#317c70]">
                {item.responsibleName || "Disponible para integrantes del área"}
              </small>
            </div>
          )}
          {item.result && (
            <small className="mt-1 block font-semibold text-[var(--brand-primary)]">
              Resultado: {workflowResult(item.result)}
            </small>
          )}
          {item.completedAt && (
            <small className="block text-[var(--gaia-ink-500)]">
              Finalizada {formatWorkflowDate(item.completedAt)}
            </small>
          )}
        </div>
        {item.status === 299540193 && resume ? (
          <button
            className="rounded-lg bg-[var(--brand-primary)] px-3 py-2 text-xs font-semibold text-white transition hover:brightness-95 disabled:opacity-60"
            disabled={busy}
            onClick={() => void resume(item)}
          >
            {busy ? "Reactivando…" : "Continuar gestión"}
          </button>
        ) : (
          actionable && (
            <div className="flex flex-wrap gap-2">
              {needsTake && (
                <button
                  className="flex items-center gap-2 rounded-lg bg-[#245f58] px-3 py-2 text-xs font-semibold text-white transition hover:brightness-95"
                  disabled={busy}
                  onClick={() => void take(item)}
                >
                  {taking && <LoaderCircle className="animate-spin" size={14} />}{" "}
                  {taking ? "Tomando gestión…" : "Tomar gestión"}
                </button>
              )}
              {canComplete && (
                <>
                  <button
                    className="rounded-lg bg-[var(--brand-primary)] px-3 py-2 text-xs font-semibold text-white transition hover:brightness-95"
                    disabled={busy}
                    onClick={() =>
                      void complete(
                        item,
                        item.requiresDecision ? 299540171 : 299540170,
                      )
                    }
                  >
                    {item.final
                      ? item.requiresDecision
                        ? "Cerrar solicitud como aprobada"
                        : "Cerrar solicitud"
                      : item.requiresDecision
                        ? "Aprobar"
                        : "Completar"}
                  </button>
                  {item.requiresDecision && (
                    <button
                      className="rounded-lg border border-[#9a384d] px-3 py-2 text-xs text-[#9a384d] transition hover:bg-[#fff0f0]"
                      disabled={busy}
                      onClick={() => void complete(item, 299540172)}
                    >
                      {item.final ? "Cerrar solicitud como rechazada" : "Rechazar"}
                    </button>
                  )}
                  {item.allowsRequesterReturn && (
                    <button
                      className="rounded-lg border px-3 py-2 text-xs transition hover:bg-white"
                      disabled={busy}
                      onClick={() => void complete(item, 299540173)}
                    >
                      Pedir información
                    </button>
                  )}
                </>
              )}
            </div>
          )
        )}
      </div>
      <ManagementSla item={item} />
      {item.answers.length > 0 && (
        <div className="mt-4 rounded-xl border bg-white/80 p-3">
          <strong className="text-[10px] uppercase tracking-wider text-[var(--gaia-ink-500)]">
            Información registrada
          </strong>
          <dl className="mt-2 grid gap-3 sm:grid-cols-2">
            {item.answers.map((answer) => (
              <div key={answer.fieldId}>
                <dt className="text-[10px] font-semibold text-[var(--gaia-ink-500)]">
                  {answer.label}
                </dt>
                <dd className="text-sm">
                  {answer.options.length
                    ? answer.options.join(", ")
                    : answer.value || "Sin respuesta"}
                </dd>
              </div>
            ))}
          </dl>
        </div>
      )}
      {item.observation && (
        <div className="mt-3 rounded-xl bg-white/80 p-3 text-sm">
          <strong className="text-[10px] uppercase tracking-wider text-[var(--gaia-ink-500)]">
            Observación registrada
          </strong>
          <p className="mt-1 whitespace-pre-wrap">{item.observation}</p>
        </div>
      )}
      {files.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-2">
          {files.map((file) => (
            <a
              className="rounded-lg border bg-white px-3 py-2 text-xs text-[var(--brand-primary)]"
              href={`${process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168"}/api/solicitudes/attachments/${file.id}/content`}
              key={file.id}
            >
              {file.file.originalName || file.file.storedName}
            </a>
          ))}
        </div>
      )}
      {canComplete && (
        <>
          <div className="mt-3 flex flex-wrap gap-2 text-[10px] text-[var(--gaia-ink-500)]">
            {item.requiresDecision && (
              <span className="rounded-full bg-white px-2 py-1">
                Requiere decisión
              </span>
            )}
            {item.requiresObservation && (
              <span className="rounded-full bg-white px-2 py-1">
                Requiere observación
              </span>
            )}
            {item.requiresFile && (
              <span className="rounded-full bg-white px-2 py-1">
                Requiere archivo
              </span>
            )}
          </div>
          <label
            aria-live="polite"
            className={`mt-3 flex min-h-11 items-center justify-center gap-2 rounded-lg border border-dashed bg-white p-3 text-center text-xs font-semibold text-[var(--brand-primary)] transition ${uploading ? "cursor-wait border-solid bg-[var(--gaia-accent-pale)]" : "cursor-pointer hover:bg-[var(--surface-muted)]"}`}
          >
            {uploading ? (
              <>
                <LoaderCircle className="animate-spin" size={16} />
                Cargando archivo…
              </>
            ) : (
              "Adjuntar a esta gestión"
            )}
            <input
              className="hidden"
              disabled={busy}
              onChange={(event) => {
                const file = event.target.files?.[0];
                if (file) void upload(item, file);
              }}
              type="file"
            />
          </label>
        </>
      )}
    </article>
  );
}

function ManagementSla({ item }: { item: WorkflowItem }) {
  if (!item.availableAt) return null;
  const due = item.targetDueDate
      ? new Date(`${item.targetDueDate}T23:59:59`)
      : null,
    completed = item.completedAt ? new Date(item.completedAt) : null,
    comparison = completed ?? new Date(),
    overdue = Boolean(due && comparison > due);
  const label = !due
    ? `Disponible desde ${formatWorkflowDate(item.availableAt)}`
    : completed
      ? overdue
        ? `Finalizada fuera de la meta · vencía ${formatDateOnly(item.targetDueDate!)}`
        : `Finalizada dentro de la meta · vencía ${formatDateOnly(item.targetDueDate!)}`
      : overdue
        ? `Meta vencida · debía atenderse antes del ${formatDateOnly(item.targetDueDate!)}`
        : `Atender antes del ${formatDateOnly(item.targetDueDate!)}`;
  return (
    <div
      className={`mt-3 flex flex-wrap items-center justify-between gap-2 rounded-xl border px-3 py-2 text-xs ${overdue ? "border-[#e7b7b1] bg-[#fff3f1] text-[#9a384d]" : "border-[#cfe2da] bg-white/80 text-[#245f58]"}`}
    >
      <strong>{overdue ? "Atención requerida" : "Tiempo de atención"}</strong>
      <span>{label}</span>
      {item.targetDays !== null && (
        <span className="w-full text-[10px] opacity-75">
          Meta institucional: {item.targetDays}{" "}
          {item.targetDays === 1 ? "día hábil" : "días hábiles"}; incluye el
          calendario de días no laborables.
        </span>
      )}
    </div>
  );
}

function formatDateOnly(value: string) {
  return new Intl.DateTimeFormat("es-CO", {
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(new Date(`${value}T12:00:00`));
}
function queueViewLabel(view: QueueView) {
  return {
    mine: "Mis pendientes",
    waiting: "Solicitudes esperando respuesta",
    tracking: "Solicitudes en gestión",
    resolved: "Solicitudes cerradas",
  }[view];
}
function queueViewDescription(view: QueueView) {
  return {
    mine: "Están asignadas a ti o disponibles sin responsable dentro de tu unidad organizacional.",
    waiting: "Solicitaste información o una corrección y aún no han respondido.",
    tracking: "Ya completaste tu intervención, pero el proceso continúa en otras etapas.",
    resolved: "Procesos finalizados en los que participaste efectivamente.",
  }[view];
}
function queueEmptyMessage(view: QueueView) {
  return {
    mine: "No tienes gestiones pendientes.",
    waiting: "No estás esperando respuestas en este momento.",
    tracking: "No tienes solicitudes activas en seguimiento.",
    resolved: "Aún no tienes solicitudes cerradas con participación registrada.",
  }[view];
}
const queueHelpItems = [
  { title: "Mis pendientes", description: "Solicitudes asignadas a ti y gestiones sin responsable que pertenecen exclusivamente a tu unidad organizacional." },
  { title: "Esperando respuesta", description: "Casos que devolviste o sobre los que pediste información y aún no han sido atendidos." },
  { title: "En gestión", description: "Ya terminaste tu etapa, pero otras personas o áreas continúan con el proceso." },
  { title: "Cerradas", description: "Solicitudes ya finalizadas en las que realizaste al menos una gestión efectiva." },
];
function Summary({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-[var(--gaia-line)] p-4">
      <small className="block text-[var(--gaia-ink-500)]">{label}</small>
      <strong className="mt-1 block text-sm">{value}</strong>
    </div>
  );
}
function Empty({ text }: { text: string }) {
  return (
    <p className="mt-5 rounded-2xl border border-dashed p-8 text-center text-sm text-[var(--gaia-ink-500)]">
      {text}
    </p>
  );
}
function workflowStatus(value: number) {
  return (
    (
      {
        299540190: "Bloqueada",
        299540191: "Disponible",
        299540192: "En curso",
        299540193: "Espera al solicitante",
        299540194: "Completada",
        299540195: "Cancelada",
      } as Record<number, string>
    )[value] ?? "Estado"
  );
}
function workflowResult(value: number) {
  return (
    (
      {
        299540170: "Completado",
        299540171: "Aprobado",
        299540172: "Rechazado",
        299540173: "Devuelto al solicitante",
        299540174: "Requiere aprobación",
        299540175: "No aplica",
      } as Record<number, string>
    )[value] ?? "Resultado registrado"
  );
}
function formatWorkflowDate(value: string) {
  return new Intl.DateTimeFormat("es-CO", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}
function friendlyStep(value: string) {
  return value
    .toLocaleLowerCase("es")
    .replaceAll("_", " ")
    .replace(/(^|\s)\S/g, (letter) => letter.toLocaleUpperCase("es"));
}
