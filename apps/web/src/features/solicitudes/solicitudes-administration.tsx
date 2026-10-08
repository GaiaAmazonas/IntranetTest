"use client";
/* eslint-disable react-hooks/set-state-in-effect, react-hooks/exhaustive-deps -- Dataverse loaders synchronize local editor state after remote changes. */
import { type FormEvent, useEffect, useMemo, useState } from "react";
import {
  Activity,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  Clock3,
  Download,
  Eye,
  EyeOff,
  GitBranch,
  GripVertical,
  LayoutList,
  LoaderCircle,
  Pencil,
  Plus,
  Search,
  Settings2,
  Trash2,
  X,
} from "lucide-react";
import { AppHeader } from "@/components/app-header";
import { PersonPicker } from "@/components/person-picker";
import { useSecurity } from "@/components/security-context";
import { apiRequest } from "@/lib/api-client";
import { SolicitudesWorkflowManager } from "./solicitudes-workflow-manager";
import {
  exportSolicitudesRequests,
  type SolicitudesRequestExport,
} from "@/lib/exports/solicitudes-requests-export";
import { useFeedback } from "@/components/feedback";
import { ConfirmDialog } from "@/components/form-dialog";
import { OrganizationalUnitPicker } from "@/components/organizational-unit-picker";
type Responsible = { id: string; name: string; unitIds: string[] };
type Unit = {
  id: string;
  code: string;
  name: string;
  parentId: string | null;
  level: number;
};
type Service = {
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
type Form = {
  id: string;
  serviceId: string;
  version: number;
  status: number;
  title: string;
  instructions: string | null;
  publishedAt: string | null;
  fieldCount: number;
  isActive: boolean;
};
type FormOption = {
  id: string;
  code: string;
  label: string;
  isDefault: boolean;
  order: number;
};
type FormField = {
  id: string;
  code: string;
  label: string;
  dataType: number;
  controlType: number;
  helpText: string | null;
  placeholder: string | null;
  required: boolean;
  order: number;
  width: number;
  minimumLength: number | null;
  maximumLength: number | null;
  minimumValue: number | null;
  maximumValue: number | null;
  allowsMultiple: boolean;
  maximumFiles: number | null;
  allowedFileTypes: string | null;
  visible: boolean;
  options: FormOption[];
};
type Definition = { form: Form; fields: FormField[] };
type ServiceMetrics = {
  serviceId: string;
  totalRequests: number;
  openRequests: number;
  resolvedRequests: number;
  pendingClosureRequests: number;
  overdueRequests: number;
};
type Snapshot = {
  services: Service[];
  forms: Form[];
  responsibles: Responsible[];
  units: Unit[];
  metrics: ServiceMetrics[];
};
type Tab = "setup" | "workflow";
type ServiceForm = Omit<
  Service,
  "id" | "currentFormId" | "description" | "instructions"
> & { description: string; instructions: string };
type FieldDraft = {
  code: string;
  label: string;
  dataType: number;
  controlType: number;
  helpText: string;
  placeholder: string;
  required: boolean;
  order: number;
  width: number;
  minimumLength: number | null;
  maximumLength: number | null;
  minimumValue: number | null;
  maximumValue: number | null;
  allowsMultiple: boolean;
  maximumFiles: number | null;
  allowedFileTypes: string;
  visible: boolean;
  options: string;
};
const emptyService: ServiceForm = {
  code: "",
  name: "",
  description: "",
  instructions: "",
  businessDays: 2,
  allowsAttachments: true,
  maximumAttachments: 5,
  maximumFileMb: 20,
  visible: false,
  order: 0,
  responsibleId: "",
  unitId: "",
  // El estado técnico se mantiene activo; la disponibilidad para el usuario
  // final se controla exclusivamente mediante publicar/despublicar.
  isActive: true,
};
const emptyField: FieldDraft = {
  code: "",
  label: "",
  dataType: 299540040,
  controlType: 299540050,
  helpText: "",
  placeholder: "",
  required: false,
  order: 0,
  width: 12,
  minimumLength: null,
  maximumLength: null,
  minimumValue: null,
  maximumValue: null,
  allowsMultiple: false,
  maximumFiles: null,
  allowedFileTypes: "",
  visible: true,
  options: "",
};
export function SolicitudesAdministration() {
  const security = useSecurity(),
    { notify } = useFeedback();
  const [data, setData] = useState<Snapshot | null>(null),
    [selectedId, setSelectedId] = useState(""),
    [tab, setTab] = useState<Tab>("setup"),
    [error, setError] = useState(""),
    [saving, setSaving] = useState(false),
    [serviceDialog, setServiceDialog] = useState(false),
    [deleteService, setDeleteService] = useState<Service | null>(null),
    [editingService, setEditingService] = useState<Service | null>(null),
    [serviceForm, setServiceForm] = useState<ServiceForm>(emptyService);
  async function load() {
    try {
      setData(await apiRequest<Snapshot>("/api/solicitudes/administration"));
    } catch (reason) {
      setError(message(reason));
    }
  }
  useEffect(() => {
    void load();
  }, []);
  const selected =
    data?.services.find((item) => item.id === selectedId) ?? null;
  function enter(service: Service) {
    setSelectedId(service.id);
    setTab("setup");
    window.scrollTo({ top: 0, behavior: "smooth" });
  }
  function editService(service?: Service) {
    setEditingService(service ?? null);
    setServiceForm(
      service
        ? {
            ...service,
            description: service.description ?? "",
            instructions: service.instructions ?? "",
          }
        : emptyService,
    );
    setServiceDialog(true);
  }
  async function saveService(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    setError("");
    try {
      const payload = {
        ...serviceForm,
        code: editingService ? serviceForm.code : makeCode(serviceForm.name),
      };
      const result = await apiRequest<{ id?: string }>(
        `/api/solicitudes/administration/services${editingService ? `/${editingService.id}` : ""}`,
        {
          method: editingService ? "PUT" : "POST",
          body: JSON.stringify(payload),
        },
      );
      setServiceDialog(false);
      await load();
      notify({
        tone: "success",
        title: editingService ? "Servicio actualizado" : "Servicio creado",
        description: "La configuración quedó guardada correctamente.",
      });
      if (result?.id) {
        setSelectedId(result.id);
        setTab("setup");
      }
    } catch (reason) {
      const detail = message(reason);
      setError(detail);
      notify({
        tone: "error",
        title: "No fue posible guardar el servicio",
        description: detail,
      });
    } finally {
      setSaving(false);
    }
  }
  async function purgeService() {
    if (!deleteService) return;
    setSaving(true);
    setError("");
    try {
      const result = await apiRequest<{ deleted: number }>(`/development/maintenance/solicitudes-test-data/services/${deleteService.id}`, { method: "DELETE" });
      notify({ tone: "success", title: "Servicio y datos eliminados", description: `Se eliminaron físicamente ${result.deleted} registros relacionados con ${deleteService.name}.` });
      setDeleteService(null);
      setSelectedId("");
      await load();
    } catch (reason) {
      const detail = message(reason);
      notify({ tone: "error", title: "No fue posible eliminar los datos de prueba", description: detail });
    } finally {
      setSaving(false);
    }
  }
  const responsibles = useMemo(
    () =>
      data?.responsibles.filter(
        (person) =>
          person.unitIds.includes(serviceForm.unitId) ||
          person.id === serviceForm.responsibleId,
      ) ?? [],
    [data, serviceForm.unitId, serviceForm.responsibleId],
  );
  return (
    <main className="gaia-app-page min-h-screen bg-[var(--surface-page)]">
      <AppHeader title="Solicitudes · Servicios y flujos" />
      <div className="mx-auto max-w-[1440px] px-5 py-8 lg:px-8">
        {error && (
          <p
            className="mb-4 rounded-xl bg-[#fff0f0] p-3 text-sm text-[#9a384d]"
            role="alert"
          >
            {error}
          </p>
        )}
        {!selected ? (
          <ServiceCatalog
            canEdit={security.can("HD.CATALOGOS.ADMINISTRAR")}
            data={data}
            edit={editService}
            purge={(service) => setDeleteService(service)}
            enter={enter}
            create={() => editService()}
          />
        ) : (
          <ServiceWorkspace
            canEdit={security.can("HD.CATALOGOS.ADMINISTRAR")}
            data={data!}
            edit={() => editService(selected)}
            purge={() => setDeleteService(selected)}
            load={load}
            selected={selected}
            setSelectedId={setSelectedId}
            setTab={setTab}
            tab={tab}
          />
        )}
      </div>
      {serviceDialog && (
        <Modal
          title={editingService ? "Editar servicio" : "Crear servicio"}
          close={() => setServiceDialog(false)}
        >
          <form onSubmit={saveService}>
            <p className="text-sm text-[var(--gaia-ink-500)]">
              Al crear el servicio se preparan automáticamente Asunto y
              Descripción.
            </p>
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <Input compact label="Nombre" required>
                <input
                  required
                  value={serviceForm.name}
                  onChange={(e) =>
                    setServiceForm({ ...serviceForm, name: e.target.value })
                  }
                />
              </Input>
              <Input compact label="Unidad organizacional" required>
                <OrganizationalUnitPicker
                  onChange={(unitId) =>
                    setServiceForm({
                      ...serviceForm,
                      unitId,
                      responsibleId: "",
                    })
                  }
                  units={data?.units ?? []}
                  required
                  value={serviceForm.unitId}
                />
              </Input>
              <Input compact label="Responsable predeterminado" required>
                <PersonPicker
                  disabled={!serviceForm.unitId}
                  key={serviceForm.unitId}
                  people={responsibles}
                  units={data?.units ?? []}
                  required
                  value={serviceForm.responsibleId}
                  onChange={(responsibleId) =>
                    setServiceForm({
                      ...serviceForm,
                      responsibleId,
                    })
                  }
                />
              </Input>
              <Input compact label="Días hábiles" required>
                <input
                  min={1}
                  required
                  type="number"
                  value={serviceForm.businessDays}
                  onChange={(e) =>
                    setServiceForm({
                      ...serviceForm,
                      businessDays: Number(e.target.value),
                    })
                  }
                />
              </Input>
            </div>
            <div className="mt-3">
              <Input compact label="Descripción">
                <textarea
                  rows={2}
                  value={serviceForm.description}
                  onChange={(e) =>
                    setServiceForm({
                      ...serviceForm,
                      description: e.target.value,
                    })
                  }
                />
              </Input>
            </div>
            <Actions close={() => setServiceDialog(false)} saving={saving} />
          </form>
        </Modal>
      )}
      <ConfirmDialog
        confirmLabel="Eliminar servicio y todos sus datos"
        description={deleteService ? `Se eliminarán físicamente “${deleteService.name}”, todas sus solicitudes activas e inactivas, formularios, campos, flujos, etapas, conexiones, gestiones, respuestas, adjuntos e historial. Esta acción no se puede deshacer.` : ""}
        destructive
        loading={saving}
        onCancel={() => setDeleteService(null)}
        onConfirm={() => void purgeService()}
        open={Boolean(deleteService)}
        title="¿Eliminar todos los datos de prueba de este servicio?"
      />
    </main>
  );
}
function ServiceCatalog({
  data,
  canEdit,
  enter,
  edit,
  purge,
  create,
}: {
  data: Snapshot | null;
  canEdit: boolean;
  enter: (service: Service) => void;
  edit: (service: Service) => void;
  purge: (service: Service) => void;
  create: () => void;
}) {
  const [search, setSearch] = useState(""),
    [status, setStatus] = useState<"all" | "active" | "inactive">("all"),
    [visibility, setVisibility] = useState<"all" | "visible" | "hidden">("all"),
    [dashboardExpanded, setDashboardExpanded] = useState(false),
    [exporting, setExporting] = useState(false),
    [exportError, setExportError] = useState("");
  function toggleDashboard() {
    const next = !dashboardExpanded;
    setDashboardExpanded(next);
    window.localStorage.setItem(
      "gaia-solicitudes-services-dashboard-expanded",
      String(next),
    );
  }
  async function downloadReport() {
    setExporting(true);
    setExportError("");
    try {
      const rows = await apiRequest<SolicitudesRequestExport[]>(
        "/api/solicitudes/administration/export",
        { cache: "no-store" },
      );
      await exportSolicitudesRequests(rows);
    } catch (reason) {
      setExportError(message(reason));
    } finally {
      setExporting(false);
    }
  }
  const services = data?.services ?? [],
    metrics = data?.metrics ?? [],
    metric = (id: string) =>
      metrics.find((item) => item.serviceId === id) ?? {
        serviceId: id,
        totalRequests: 0,
        openRequests: 0,
        resolvedRequests: 0,
        pendingClosureRequests: 0,
        overdueRequests: 0,
      };
  const filtered = services.filter(
    (service) =>
      (status === "all" || (status === "active") === service.isActive) &&
      (visibility === "all" ||
        (visibility === "visible") === service.visible) &&
      `${service.name} ${service.code} ${service.description ?? ""}`
        .toLocaleLowerCase("es")
        .includes(search.trim().toLocaleLowerCase("es")),
  );
  const totals = metrics.reduce(
    (value, item) => ({
      requests: value.requests + item.totalRequests,
      open: value.open + item.openRequests,
      resolved: value.resolved + item.resolvedRequests,
      pendingClosure: value.pendingClosure + item.pendingClosureRequests,
      overdue: value.overdue + item.overdueRequests,
    }),
    { requests: 0, open: 0, resolved: 0, pendingClosure: 0, overdue: 0 },
  );
  const ranked = [...services]
      .sort((a, b) => metric(b.id).totalRequests - metric(a.id).totalRequests)
      .slice(0, 5),
    peak = Math.max(1, ...ranked.map((item) => metric(item.id).totalRequests));
  return (
    <>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-[10px] font-bold uppercase tracking-[.16em] text-[var(--brand-primary)]">
            Administración funcional
          </p>
          <h1 className="mt-2 text-3xl font-semibold">Servicios</h1>
          <p className="mt-2 text-sm text-[var(--gaia-ink-500)]">
            Panorama operativo y configuración del catálogo de atención.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            className="rounded-xl border border-[var(--brand-primary)] bg-white px-5 py-3 text-sm font-semibold text-[var(--brand-primary)] disabled:opacity-60"
            disabled={exporting}
            onClick={() => void downloadReport()}
          >
            {exporting ? (
              <LoaderCircle className="mr-2 inline animate-spin" size={16} />
            ) : (
              <Download className="mr-2 inline" size={16} />
            )}{" "}
            {exporting ? "Preparando Excel…" : "Exportar solicitudes"}
          </button>
          {canEdit && (
            <button
              className="rounded-xl bg-[var(--brand-primary)] px-5 py-3 text-sm font-semibold text-white shadow-sm"
              onClick={create}
            >
              <Plus className="mr-2 inline" size={16} />
              Nuevo servicio
            </button>
          )}
        </div>
      </header>
      {exportError && (
        <p
          className="mt-4 rounded-xl bg-[#fff0f0] p-3 text-sm text-[#9a384d]"
          role="alert"
        >
          {exportError}
        </p>
      )}
      <section className="mt-7 overflow-hidden rounded-2xl border border-[var(--gaia-line)] bg-[var(--surface-card)] shadow-sm">
        <header className="flex items-center justify-between gap-4 px-5 py-4">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[.16em] text-[var(--brand-primary)]">
              Resumen ejecutivo
            </p>
            <h2 className="mt-1 font-semibold">
              Indicadores y salud del catálogo
            </h2>
          </div>
          <button
            aria-expanded={dashboardExpanded}
            className="inline-flex items-center gap-2 rounded-xl border border-[var(--gaia-line)] bg-white px-3 py-2 text-xs font-semibold text-[var(--brand-primary)]"
            onClick={toggleDashboard}
          >
            {dashboardExpanded ? (
              <>
                <ChevronUp size={15} />
                Ocultar resumen
              </>
            ) : (
              <>
                <ChevronDown size={15} />
                Mostrar resumen
              </>
            )}
          </button>
        </header>
        {dashboardExpanded && (
          <div className="border-t border-[var(--gaia-line)] bg-[var(--surface-page)] p-4">
            <section
              aria-label="Resumen de servicios"
              className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4"
            >
              <Kpi
                icon={Activity}
                label="Servicios configurados"
                value={services.length}
                detail={`${services.filter((x) => x.isActive).length} activos · ${services.filter((x) => x.visible).length} visibles`}
              />
              <Kpi
                icon={LayoutList}
                label="Solicitudes recibidas"
                value={totals.requests}
                detail="Acumulado del catálogo"
              />
              <Kpi
                icon={Clock3}
                label="Solicitudes en atención"
                value={totals.open}
                detail={
                  totals.overdue
                    ? `${totals.overdue} fuera de plazo`
                    : "Sin vencimientos"
                }
                alert={totals.overdue > 0}
              />
              <Kpi
                icon={CheckCircle2}
                label="Solicitudes resueltas"
                value={totals.resolved}
                detail={
                  totals.pendingClosure
                    ? `${totals.pendingClosure} pendientes de cierre por el solicitante`
                    : totals.requests
                      ? `${Math.round((totals.resolved / totals.requests) * 100)}% del total recibido`
                      : "Aún sin solicitudes"
                }
              />
            </section>
            <section className="mt-4 grid gap-4 lg:grid-cols-[1.4fr_1fr]">
              <article className="rounded-2xl border border-[var(--gaia-line)] bg-[var(--surface-card)] p-5 shadow-sm">
                <div className="flex items-baseline justify-between">
                  <div>
                    <h2 className="font-semibold">Demanda por servicio</h2>
                    <p className="mt-1 text-xs text-[var(--gaia-ink-500)]">
                      Servicios con mayor volumen acumulado
                    </p>
                  </div>
                  <small className="text-[var(--gaia-ink-500)]">Top 5</small>
                </div>
                <div className="mt-5 space-y-4">
                  {ranked.map((service) => (
                    <div key={service.id}>
                      <div className="mb-1.5 flex justify-between gap-3 text-xs">
                        <span className="truncate font-semibold">
                          {service.name}
                        </span>
                        <strong>{metric(service.id).totalRequests}</strong>
                      </div>
                      <div className="h-2 overflow-hidden rounded-full bg-[var(--gaia-accent-soft)]">
                        <div
                          className="h-full rounded-full bg-[var(--brand-primary)]"
                          style={{
                            width: `${(metric(service.id).totalRequests / peak) * 100}%`,
                          }}
                        />
                      </div>
                    </div>
                  ))}
                  {!ranked.length && (
                    <p className="py-8 text-center text-sm text-[var(--gaia-ink-500)]">
                      Aún no hay servicios configurados.
                    </p>
                  )}
                </div>
              </article>
              <article className="rounded-2xl border border-[var(--gaia-line)] bg-[#123f48] p-5 text-white shadow-sm">
                <p className="text-[10px] font-bold uppercase tracking-[.16em] text-[#a8d7d2]">
                  Salud del catálogo
                </p>
                <h2 className="mt-2 text-xl font-semibold">
                  Disponibilidad para la intranet
                </h2>
                <p className="mt-2 text-sm text-white/70">
                  Revisa configuraciones que pueden impedir que un servicio sea
                  utilizado.
                </p>
                <div className="mt-5 grid grid-cols-2 gap-3">
                  <Health
                    value={
                      services.filter(
                        (x) => x.isActive && x.visible && x.currentFormId,
                      ).length
                    }
                    label="Listos para operar"
                  />
                  <Health
                    value={services.filter((x) => !x.isActive).length}
                    label="Inactivos"
                  />
                  <Health
                    value={
                      services.filter((x) => x.isActive && !x.visible).length
                    }
                    label="Ocultos"
                  />
                  <Health
                    value={
                      services.filter((x) => x.isActive && !x.currentFormId)
                        .length
                    }
                    label="Sin formulario vigente"
                  />
                </div>
              </article>
            </section>
          </div>
        )}
      </section>
      <section className="mt-5 overflow-hidden rounded-2xl border border-[var(--gaia-line)] bg-[var(--surface-card)] shadow-sm">
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--gaia-line)] p-5">
          <div>
            <h2 className="text-lg font-semibold">Catálogo de servicios</h2>
            <p className="text-xs text-[var(--gaia-ink-500)]">
              {filtered.length} de {services.length} servicios
            </p>
          </div>
          <div className="flex flex-1 flex-wrap justify-end gap-2">
            <label className="flex min-h-10 min-w-[240px] items-center gap-2 rounded-xl border border-[var(--gaia-line)] bg-white px-3 text-sm">
              <Search size={15} />
              <input
                className="w-full border-0 bg-transparent outline-none"
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Buscar servicio…"
                type="search"
                value={search}
              />
            </label>
            <select
              className="min-h-10 rounded-xl border border-[var(--gaia-line)] bg-white px-3 text-sm"
              onChange={(e) => setStatus(e.target.value as typeof status)}
              value={status}
            >
              <option value="all">Todos los estados</option>
              <option value="active">Activos</option>
              <option value="inactive">Inactivos</option>
            </select>
            <select
              className="min-h-10 rounded-xl border border-[var(--gaia-line)] bg-white px-3 text-sm"
              onChange={(e) =>
                setVisibility(e.target.value as typeof visibility)
              }
              value={visibility}
            >
              <option value="all">Toda visibilidad</option>
              <option value="visible">Visibles</option>
              <option value="hidden">Ocultos</option>
            </select>
          </div>
        </header>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[1120px] table-fixed text-left text-sm">
            <colgroup>
              <col className="w-[34%]" />
              <col className="w-[10%]" />
              <col className="w-[7%]" />
              <col className="w-[7%]" />
              <col className="w-[7%]" />
              <col className="w-[7%]" />
              <col className="w-[8%]" />
              <col className="w-[20%]" />
            </colgroup>
            <thead className="bg-[#e3f0f1] text-[10px] uppercase tracking-wider text-[var(--gaia-ink-500)]">
              <tr>
                <th className="p-4">Servicio</th>
                <th className="px-2 text-center">Estado</th>
                <th className="border-l border-[#cfdee0] px-2 text-center">
                  Solicitudes
                </th>
                <th className="px-2 text-center">Abiertas</th>
                <th className="px-2 text-center">Resueltas</th>
                <th className="border-r border-[#cfdee0] px-2 text-center">
                  Vencidas
                </th>
                <th className="px-2 text-center">Plazo</th>
                <th className="pr-4 text-center">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((service, index) => {
                const stats = metric(service.id);
                const published = service.isActive && service.visible && Boolean(service.currentFormId);
                return (
                  <tr
                    className={`border-t border-[var(--gaia-line)] transition hover:bg-[var(--gaia-accent-pale)] ${index % 2 ? "bg-[#fbfcfb]" : "bg-white"}`}
                    key={service.id}
                  >
                    <td className="p-4">
                      <div className="flex min-w-0 items-center gap-3">
                        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-[var(--gaia-accent-soft)] font-bold text-[var(--brand-primary)]">
                          {service.name.slice(0, 2).toUpperCase()}
                        </span>
                        <span className="min-w-0">
                          <strong className="block truncate">
                            {service.name}
                          </strong>
                          <small
                            className="block truncate text-[var(--gaia-ink-500)]"
                            title={`${service.code} · ${service.description || "Sin descripción"}`}
                          >
                            {service.code} ·{" "}
                            {service.description || "Sin descripción"}
                          </small>
                        </span>
                      </div>
                    </td>
                    <td className="px-2 text-center">
                      <span
                        className={`inline-block rounded-full px-2.5 py-1 text-[10px] font-bold ${published ? "bg-[#e2f3ec] text-[#17695b]" : service.isActive ? "bg-[#fff0cf] text-[#8a5b00]" : "bg-[#eef0ef] text-[#69736f]"}`}
                      >
                        {published ? "PUBLICADO" : service.isActive ? "BORRADOR" : "INACTIVO"}
                      </span>
                      <small className="mt-1 flex items-center justify-center gap-1 text-[var(--gaia-ink-500)]">
                        {service.visible ? (
                          <Eye size={12} />
                        ) : (
                          <EyeOff size={12} />
                        )}{" "}
                        {service.visible ? "Visible" : "Oculto"}
                      </small>
                    </td>
                    <td className="border-l border-[#e1e8e7] px-2 text-center font-semibold tabular-nums">
                      {stats.totalRequests}
                    </td>
                    <td className="px-2 text-center tabular-nums">
                      {stats.openRequests}
                    </td>
                    <td className="px-2 text-center tabular-nums">
                      {stats.resolvedRequests}
                    </td>
                    <td
                      className={`border-r border-[#e1e8e7] px-2 text-center tabular-nums ${stats.overdueRequests ? "font-bold text-[#b64a3f]" : ""}`}
                    >
                      {stats.overdueRequests}
                    </td>
                    <td className="px-2 text-center tabular-nums">
                      {service.businessDays} días
                    </td>
                    <td className="pr-4">
                      <div className="flex flex-wrap justify-center gap-2">
                        <button
                          className="inline-flex items-center rounded-lg bg-[var(--brand-primary)] px-3 py-2 text-xs font-semibold text-white"
                          onClick={() => enter(service)}
                          type="button"
                        >
                          Gestionar
                          <ArrowRight className="ml-1 inline" size={13} />
                        </button>
                        {canEdit && (
                          <button
                            aria-label={`Editar ${service.name}`}
                            className="grid size-9 place-items-center rounded-lg border border-[var(--gaia-line)] bg-white text-[var(--brand-primary)]"
                            onClick={() => edit(service)}
                            title="Editar servicio"
                            type="button"
                          >
                            <Pencil size={14} />
                          </button>
                        )}
                        {canEdit && (
                          <button
                            aria-label={`Eliminar datos de prueba de ${service.name}`}
                            className="grid size-9 place-items-center rounded-lg border border-[#d8a5aa] bg-white text-[#9a384d]"
                            onClick={() => purge(service)}
                            title="Eliminar servicio y todos sus datos de prueba"
                            type="button"
                          >
                            <Trash2 size={14} />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {!filtered.length && (
          <div className="grid min-h-40 place-items-center text-sm text-[var(--gaia-ink-500)]">
            No hay servicios que coincidan con los filtros.
          </div>
        )}
      </section>
    </>
  );
}
function Kpi({
  icon: Icon,
  label,
  value,
  detail,
  alert = false,
}: {
  icon: typeof Activity;
  label: string;
  value: number;
  detail: string;
  alert?: boolean;
}) {
  return (
    <article className="rounded-2xl border border-[var(--gaia-line)] bg-[var(--surface-card)] p-5 shadow-sm">
      <div className="flex items-start justify-between">
        <span
          className={`grid size-10 place-items-center rounded-xl ${alert ? "bg-[#fff0ec] text-[#b64a3f]" : "bg-[var(--gaia-accent-soft)] text-[var(--brand-primary)]"}`}
        >
          <Icon size={19} />
        </span>
        <strong className="text-3xl font-semibold">
          {value.toLocaleString("es-CO")}
        </strong>
      </div>
      <h2 className="mt-4 text-sm font-semibold">{label}</h2>
      <p
        className={`mt-1 text-xs ${alert ? "text-[#b64a3f]" : "text-[var(--gaia-ink-500)]"}`}
      >
        {detail}
      </p>
    </article>
  );
}
function Health({ value, label }: { value: number; label: string }) {
  return (
    <div className="rounded-xl bg-white/10 p-3">
      <strong className="text-2xl">{value}</strong>
      <small className="mt-1 block text-white/65">{label}</small>
    </div>
  );
}
function ServiceWorkspace({
  selected,
  data,
  tab,
  setTab,
  setSelectedId,
  canEdit,
  edit,
  purge,
  load,
}: {
  selected: Service;
  data: Snapshot;
  tab: Tab;
  setTab: (tab: Tab) => void;
  setSelectedId: (id: string) => void;
  canEdit: boolean;
  edit: () => void;
  purge: () => void;
  load: () => Promise<void>;
}) {
  const forms = data.forms.filter((item) => item.serviceId === selected.id && item.isActive);
  const configurationLocked = selected.visible;
  const draftForm = forms.find((item) => item.status === 299540030) ?? null;
  const formReady = Boolean(
    (draftForm && draftForm.fieldCount > 0) || selected.currentFormId,
  );
  return (
    <>
      <button
        className="mb-5 inline-flex items-center gap-2 text-sm font-semibold text-[var(--brand-primary)]"
        onClick={() => setSelectedId("")}
      >
        <ArrowLeft size={16} />
        Volver a servicios
      </button>
      <header className="rounded-3xl border border-[var(--gaia-line)] bg-[var(--surface-card)] p-6 shadow-sm">
        <div className="flex justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-widest text-[var(--brand-primary)]">
              Servicio seleccionado
            </p>
            <h1 className="mt-2 text-3xl font-semibold">{selected.name}</h1>
            <p className="mt-2 text-sm text-[var(--gaia-ink-500)]">
              {selected.description || "Configura la experiencia de atención."}
            </p>
          </div>
          <div className={`flex h-fit max-w-xl items-center gap-2 rounded-xl border px-3 py-2 text-sm ${selected.visible ? "border-[#63a998] bg-[#e7f6f1] text-[#175f53]" : "border-[#d59a3a] bg-[#fff7e7] text-[#80510e]"}`}>
            {selected.visible
              ? <CheckCircle2 className="shrink-0" size={18} />
              : <EyeOff className="shrink-0" size={18} />}
            <strong className="shrink-0 uppercase tracking-wide">
              {selected.visible ? "Publicado" : "No publicado"}
            </strong>
            <span className="text-xs font-medium">
              {selected.visible
                ? "Disponible en la intranet para recibir solicitudes."
                : selected.isActive
                  ? "Borrador activo; publícalo desde el paso final cuando esté listo."
                  : "Servicio inactivo y no disponible en la intranet."}
            </span>
          </div>
        </div>
        <nav className="mt-6 flex gap-1 overflow-x-auto border-b border-[var(--gaia-line)]">
          {(
            [
              {
                id: "setup",
                label: "Paso 1. Configuración y formulario",
                icon: Settings2,
              },
              {
                id: "workflow",
                label: "Paso 2. Flujo y publicación",
                icon: GitBranch,
              },
            ] as const
          ).map((item) => (
            <button
              className={`inline-flex items-center gap-2 border-b-2 px-4 py-3 text-sm font-semibold ${tab === item.id ? "border-[var(--brand-primary)] text-[var(--brand-primary)]" : "border-transparent text-[var(--gaia-ink-500)]"}`}
              key={item.id}
              onClick={() => setTab(item.id)}
            >
              <item.icon size={16} />
              {item.label}
            </button>
          ))}
        </nav>
      </header>
      <section className="mt-5">
        {tab === "setup" && (
          <div className="space-y-5">
            <Overview canEdit={canEdit} canUnpublish={canEdit && configurationLocked} edit={edit} purge={purge} unpublish={async()=>{if(!window.confirm("El servicio dejará de estar disponible para nuevas solicitudes. Las solicitudes existentes y las versiones publicadas se conservarán. ¿Deseas continuar?"))return;await apiRequest(`/api/solicitudes/administration/services/${selected.id}/unpublish`,{method:"POST",body:"{}"});await load();}} />
            <FormsWorkspace
              canEdit={canEdit}
              forms={forms}
              load={load}
              service={selected}
            />
          </div>
        )}
        {tab === "workflow" && (
          <SolicitudesWorkflowManager
            canEdit={canEdit}
            draftFormId={draftForm?.id ?? null}
            formReady={formReady}
            onPublished={load}
            people={data.responsibles}
            service={selected}
            serviceId={selected.id}
            serviceName={selected.name}
            units={data.units}
          />
        )}
      </section>
    </>
  );
}
function Overview({
  canEdit,
  canUnpublish,
  edit,
  purge,
  unpublish,
}: {
  canEdit: boolean;
  canUnpublish: boolean;
  edit: () => void;
  purge: () => void;
  unpublish: () => Promise<void>;
}) {
  const [unpublishing, setUnpublishing] = useState(false);
  const [unpublishError, setUnpublishError] = useState("");
  async function confirmUnpublish() {
    setUnpublishing(true);
    setUnpublishError("");
    try { await unpublish(); }
    catch (reason) { setUnpublishError(message(reason)); }
    finally { setUnpublishing(false); }
  }
  return (
    <article className="rounded-2xl border border-[var(--gaia-line)] bg-[var(--surface-card)] p-6">
      <div className="flex flex-wrap justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold">Configuración general</h2>
          <p className="text-sm text-[var(--gaia-ink-500)]">
            Información operativa y disponibilidad.
          </p>
        </div>
        {(canEdit || canUnpublish) && <div className="flex flex-wrap gap-2">
          {canUnpublish && <button className="rounded-xl border border-[#b9852f] px-4 py-2 text-sm font-semibold text-[#79520f] disabled:opacity-60" disabled={unpublishing} onClick={()=>void confirmUnpublish()}>
            {unpublishing ? "Despublicando…" : "Despublicar para editar"}
          </button>}
          {canEdit && <>
          <button className="rounded-xl border px-4 py-2 text-sm font-semibold" onClick={edit}>
            <Pencil className="mr-2 inline" size={15} />Editar configuración
          </button>
          <button className="rounded-xl border border-[#c96b72] px-4 py-2 text-sm font-semibold text-[#9a384d]" onClick={purge}>
            <Trash2 className="mr-2 inline" size={15} />Eliminar datos de prueba
          </button>
          </>}
        </div>}
      </div>
      {unpublishError && <p className="mt-4 rounded-xl bg-[#fff0f0] p-3 text-sm text-[#9a384d]" role="alert">{unpublishError}</p>}
    </article>
  );
}
function FormsWorkspace({
  service,
  forms,
  canEdit,
  load,
}: {
  service: Service;
  forms: Form[];
  canEdit: boolean;
  load: () => Promise<void>;
}) {
  const [selected, setSelected] = useState(
      forms.find((item) => item.status === 299540030)?.id ?? forms[0]?.id ?? "",
    ),
    [title, setTitle] = useState(`Formulario de ${service.name}`),
    [saving, setSaving] = useState(false),
    [error, setError] = useState("");
  useEffect(() => {
    if (!forms.some((item) => item.id === selected))
      setSelected(forms[0]?.id ?? "");
  }, [forms, selected]);
  async function create(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    try {
      const result = await apiRequest<{ id: string }>(
        "/api/solicitudes/administration/forms",
        {
          method: "POST",
          body: JSON.stringify({
            serviceId: service.id,
            title,
            instructions: null,
          }),
        },
      );
      await load();
      setSelected(result.id);
    } catch (reason) {
      setError(message(reason));
    } finally {
      setSaving(false);
    }
  }
  async function removeDraft() {
    if(!current||current.status!==299540030||!window.confirm(`¿Eliminar la versión ${current.version} en borrador? Esta acción no afecta versiones publicadas.`))return;
    setSaving(true);setError("");
    try{await apiRequest(`/api/solicitudes/administration/forms/${current.id}`,{method:"DELETE"});setSelected("");await load();}
    catch(reason){setError(message(reason));}
    finally{setSaving(false);}
  }
  const current = forms.find((item) => item.id === selected);
  return (
    <div className="grid gap-5 lg:grid-cols-[250px_1fr]">
      <aside className="rounded-2xl border bg-[var(--surface-card)] p-4">
        <h2 className="font-semibold">Versiones</h2>
        <div className="mt-3 space-y-2">
          {forms.map((form) => (
            <button
              className={`w-full rounded-xl border p-3 text-left ${selected === form.id ? "border-[var(--brand-primary)] bg-[var(--gaia-accent-pale)]" : "border-[var(--gaia-line)]"}`}
              key={form.id}
              onClick={() => setSelected(form.id)}
            >
              <strong className="block text-sm">Versión {form.version}</strong>
              <small>
                {form.status === 299540031
                  ? "Publicada"
                  : form.status === 299540032
                    ? "Retirada"
                    : "Borrador"}{" "}
                · {form.fieldCount} campos
              </small>
            </button>
          ))}
        </div>
        {canEdit && !forms.some((item) => item.status === 299540030) && (
          <form className="mt-4 border-t pt-4" onSubmit={create}>
            <input
              className="h-10 w-full rounded-lg border px-3 text-sm"
              onChange={(e) => setTitle(e.target.value)}
              required
              value={title}
            />
            <button
              className="mt-2 w-full rounded-lg bg-[var(--brand-primary)] px-3 py-2 text-xs font-semibold text-white"
              disabled={saving}
            >
              Nueva versión
            </button>
          </form>
        )}
      </aside>
      <article className="rounded-2xl border bg-[var(--surface-card)] p-5">
        {error && (
          <p className="mb-3 rounded-lg bg-[#fff0f0] p-2 text-sm text-[#9a384d]">
            {error}
          </p>
        )}
        {current ? (
          <>
            <div className="flex flex-wrap justify-between gap-3">
              <div>
                <h2 className="text-xl font-semibold">{current.title}</h2>
                <p className="text-sm text-[var(--gaia-ink-500)]">
                  Diseña lo que verá la persona al crear la solicitud.
                </p>
              </div>
              <span className="h-fit rounded-full bg-[var(--gaia-accent-soft)] px-3 py-1.5 text-xs font-semibold text-[var(--brand-primary)]">
                {current.status === 299540030
                  ? "Borrador guardado"
                  : current.status === 299540031
                    ? "Versión vigente"
                    : "Versión retirada"}
              </span>
              {canEdit&&current.status===299540030&&<button aria-label={`Eliminar versión ${current.version}`} className="h-fit rounded-xl border border-[#c96b72] px-3 py-2 text-xs font-semibold text-[#9a384d]" disabled={saving} onClick={()=>void removeDraft()} type="button"><Trash2 className="mr-1 inline" size={14}/>Eliminar borrador</button>}
            </div>
            {current.status === 299540030 && (
              <div className="mt-4 rounded-xl border border-[#c8ddd7] bg-[#f2f8f6] p-4 text-sm">
                <strong>
                  Este formulario aún no está visible para los usuarios.
                </strong>
                <p className="mt-1 text-[var(--gaia-ink-500)]">
                  Se publicará junto con el flujo y el servicio desde el paso 2.
                </p>
              </div>
            )}
            <FormDesigner
              canEdit={canEdit && current.status !== 299540032}
              form={current}
              changed={load}
            />
          </>
        ) : (
          <div className="grid min-h-64 place-items-center text-sm">
            Crea la primera versión.
          </div>
        )}
      </article>
    </div>
  );
}
function FormDesigner({
  form,
  canEdit,
  changed,
}: {
  form: Form;
  canEdit: boolean;
  changed: () => Promise<void>;
}) {
  const [data, setData] = useState<Definition | null>(null),
    [field, setField] = useState<FormField | null | undefined>(undefined),
    [draft, setDraft] = useState<FieldDraft>(emptyField),
    [preview, setPreview] = useState(false),
    [saving, setSaving] = useState(false),
    [error, setError] = useState(""),
    [draggedId, setDraggedId] = useState("");
  async function load() {
    setData(
      await apiRequest<Definition>(
        `/api/solicitudes/administration/forms/${form.id}`,
      ),
    );
  }
  useEffect(() => {
    void load();
  }, [form.id]);
  function edit(value?: FormField) {
    setField(value ?? null);
    setDraft(
      value
        ? {
            ...emptyField,
            ...value,
            helpText: value.helpText ?? "",
            placeholder: value.placeholder ?? "",
            allowedFileTypes: value.allowedFileTypes ?? "",
            options: value.options.map((option) => option.label).join("\n"),
          }
        : { ...emptyField, order: data?.fields.length ?? 0 },
    );
  }
  async function save(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    try {
      const options = draft.options
        .split("\n")
        .map((x) => x.trim())
        .filter(Boolean)
        .map((label, index) => ({
          id: field?.options[index]?.id ?? null,
          code: field?.options[index]?.code ?? makeCode(label),
          label,
          order: index,
          isDefault: index === 0,
          isActive: true,
        }));
      await apiRequest(
        `/api/solicitudes/administration/forms/${form.id}/fields${field ? `/${field.id}` : ""}`,
        {
          method: field ? "PUT" : "POST",
          body: JSON.stringify({
            ...draft,
            code: field ? draft.code : makeCode(draft.label),
            options,
          }),
        },
      );
      setField(undefined);
      await load();
      await changed();
    } catch (reason) {
      setError(message(reason));
    } finally {
      setSaving(false);
    }
  }
  async function remove(item: FormField) {
    if (
      !window.confirm(
        `¿Eliminar el campo “${item.label}”? Esta acción lo quitará de esta versión en borrador.`,
      )
    )
      return;
    setSaving(true);
    setError("");
    try {
      await apiRequest(
        `/api/solicitudes/administration/forms/${form.id}/fields/${item.id}`,
        { method: "DELETE" },
      );
      await load();
      await changed();
    } catch (reason) {
      setError(message(reason));
    } finally {
      setSaving(false);
    }
  }
  async function reorder(targetId: string) {
    if (!data || !draggedId || draggedId === targetId) return;
    const ordered = [...data.fields],
      from = ordered.findIndex((item) => item.id === draggedId),
      to = ordered.findIndex((item) => item.id === targetId);
    if (from < 0 || to < 0) return;
    const [moved] = ordered.splice(from, 1);
    ordered.splice(to, 0, moved);
    const normalized = ordered.map((item, index) => ({
      ...item,
      order: index,
    }));
    setData({ ...data, fields: normalized });
    setDraggedId("");
    setSaving(true);
    setError("");
    try {
      for (const item of normalized)
        await apiRequest(
          `/api/solicitudes/administration/forms/${form.id}/fields/${item.id}`,
          {
            method: "PUT",
            body: JSON.stringify({
              ...item,
              options: item.options.map((option) => ({
                ...option,
                isActive: true,
              })),
            }),
          },
        );
      await changed();
    } catch (reason) {
      setError(message(reason));
      await load();
    } finally {
      setSaving(false);
    }
  }
  return (
    <div className="mt-5">
      <div className="flex flex-wrap justify-end gap-2">
        <button
          className="inline-flex items-center gap-2 rounded-xl border px-4 py-2 text-sm font-semibold"
          onClick={() => setPreview(true)}
        >
          <Eye size={16} />
          Previsualizar formulario
        </button>
        {canEdit && (
          <button
            className="inline-flex items-center gap-2 rounded-xl bg-[var(--brand-primary)] px-4 py-2 text-sm font-semibold text-white"
            onClick={() => edit()}
          >
            <Plus size={16} />
            Agregar campo
          </button>
        )}
      </div>
      {error && (
        <p className="mt-3 rounded-lg bg-[#fff0f0] p-2 text-sm text-[#9a384d]">
          {error}
        </p>
      )}
      <div className="mt-4 space-y-3">
        {data?.fields.map((item, index) => (
          <FieldCanvas
            field={item}
            index={index}
            key={item.id}
            edit={() => edit(item)}
            remove={() => void remove(item)}
            canEdit={canEdit}
            dragging={draggedId === item.id}
            startDrag={() => setDraggedId(item.id)}
            drop={() => void reorder(item.id)}
          />
        ))}
      </div>
      {field !== undefined && (
        <Modal
          title={field ? "Editar campo" : "Agregar campo"}
          close={() => setField(undefined)}
        >
          <form onSubmit={save}>
            <div className="grid gap-4 sm:grid-cols-2">
              <Input label="Nombre del campo" required>
                <input
                  required
                  value={draft.label}
                  onChange={(e) =>
                    setDraft({ ...draft, label: e.target.value })
                  }
                />
              </Input>
              <Input label="Tipo de respuesta">
                <select
                  value={draft.controlType}
                  onChange={(e) =>
                    setDraft({
                      ...draft,
                      controlType: Number(e.target.value),
                      dataType: dataTypeFor(Number(e.target.value)),
                    })
                  }
                >
                  <option value={299540050}>Texto corto</option>
                  <option value={299540051}>Texto largo</option>
                  <option value={299540052}>Número</option>
                  <option value={299540053}>Correo</option>
                  <option value={299540056}>Fecha</option>
                  <option value={299540057}>Fecha y hora</option>
                  <option value={299540058}>Lista de opciones</option>
                </select>
              </Input>
              <Input label="Texto de ejemplo">
                <input
                  value={draft.placeholder}
                  onChange={(e) =>
                    setDraft({ ...draft, placeholder: e.target.value })
                  }
                />
              </Input>
              <Input label="Orden">
                <input
                  min={0}
                  type="number"
                  value={draft.order}
                  onChange={(e) =>
                    setDraft({ ...draft, order: Number(e.target.value) })
                  }
                />
              </Input>
            </div>
            <Input label="Ayuda para el usuario">
              <input
                value={draft.helpText}
                onChange={(e) =>
                  setDraft({ ...draft, helpText: e.target.value })
                }
              />
            </Input>
            {draft.controlType === 299540058 && (
              <Input label="Opciones (una por línea)">
                <textarea
                  required
                  rows={5}
                  value={draft.options}
                  onChange={(e) =>
                    setDraft({ ...draft, options: e.target.value })
                  }
                />
              </Input>
            )}
            <div className="mt-4 grid gap-3 sm:grid-cols-2">
              <Toggle
                checked={draft.required}
                label="Respuesta obligatoria"
                change={(required) => setDraft({ ...draft, required })}
              />
              <Toggle
                checked={draft.visible}
                label="Mostrar al solicitante"
                change={(visible) => setDraft({ ...draft, visible })}
              />
            </div>
            <Actions close={() => setField(undefined)} saving={saving} />
          </form>
        </Modal>
      )}
      {preview && (
        <PreviewModal close={() => setPreview(false)} definition={data} />
      )}
    </div>
  );
}
function FieldCanvas({
  field,
  index,
  edit,
  remove,
  canEdit,
  dragging,
  startDrag,
  drop,
}: {
  field: FormField;
  index: number;
  edit: () => void;
  remove: () => void;
  canEdit: boolean;
  dragging: boolean;
  startDrag: () => void;
  drop: () => void;
}) {
  return (
    <article
      draggable={canEdit}
      onDragStart={startDrag}
      onDragOver={(event) => {
        if (canEdit) event.preventDefault();
      }}
      onDrop={drop}
      className={`rounded-2xl border p-4 transition ${dragging ? "opacity-45 ring-2 ring-[var(--brand-primary)]" : ""} ${field.visible ? "bg-[var(--surface-page)]" : "border-dashed bg-[var(--surface-muted)] opacity-70"}`}
    >
      <div className="flex items-start gap-3">
        {canEdit && (
          <span
            aria-label="Arrastra para reordenar"
            className="mt-1 cursor-grab text-[var(--gaia-ink-500)]"
            title="Arrastra para reordenar"
          >
            <GripVertical size={17} />
          </span>
        )}
        <span className="grid size-8 shrink-0 place-items-center rounded-lg bg-[var(--gaia-accent-soft)] text-xs font-bold text-[var(--brand-primary)]">
          {index + 1}
        </span>
        <div className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-2">
            <strong>{field.label}</strong>
            {field.required && (
              <small className="rounded-full bg-[#fff0f0] px-2 py-0.5 text-[#9a384d]">
                Obligatorio
              </small>
            )}
            {!field.visible && (
              <small className="rounded-full bg-white px-2 py-0.5">
                Oculto
              </small>
            )}
          </span>
          {field.helpText && (
            <p className="mt-1 text-xs text-[var(--gaia-ink-500)]">
              {field.helpText}
            </p>
          )}
          <div className="pointer-events-none">
            <PreviewControl field={field} />
          </div>
        </div>
        {canEdit && (
          <span className="flex gap-2">
            <button
              aria-label={`Editar ${field.label}`}
              className="rounded-lg border bg-white p-2"
              onClick={edit}
            >
              <Pencil size={15} />
            </button>
            <button
              aria-label={`Eliminar ${field.label}`}
              className="rounded-lg border border-[#e3b8b2] bg-white p-2 text-[#a33e35]"
              disabled={dragging}
              onClick={remove}
              title="Eliminar campo"
            >
              <Trash2 size={15} />
            </button>
          </span>
        )}
      </div>
    </article>
  );
}
function PreviewModal({
  definition,
  close,
}: {
  definition: Definition | null;
  close: () => void;
}) {
  const fields = definition?.fields ?? [],
    hidden = fields.filter((field) => !field.visible).length;
  return (
    <div
      className="fixed inset-0 z-[80] grid place-items-center bg-black/45 p-4"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) close();
      }}
    >
      <section
        className="max-h-[92vh] w-full max-w-2xl overflow-auto rounded-3xl bg-white p-6 shadow-2xl"
        role="dialog"
        aria-modal="true"
      >
        <header className="flex justify-between">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-widest text-[var(--brand-primary)]">
              Vista previa de diseño
            </p>
            <h2 className="mt-1 text-2xl font-semibold">
              {definition?.form.title}
            </h2>
          </div>
          <button aria-label="Cerrar" onClick={close}>
            <X size={20} />
          </button>
        </header>
        {hidden > 0 && (
          <p className="mt-4 rounded-xl bg-[var(--surface-muted)] p-3 text-xs text-[var(--gaia-ink-500)]">
            Los campos marcados como “Oculto” se muestran atenuados únicamente
            para que puedas revisar el diseño. No aparecerán en el formulario
            del solicitante.
          </p>
        )}
        <div className="mt-6 space-y-4">
          {fields.map((field) => (
            <label
              className={`block rounded-xl text-sm font-semibold ${field.visible ? "" : "border border-dashed p-3 opacity-55"}`}
              key={field.id}
            >
              <span>
                {field.label}
                {field.required && " *"}
                {!field.visible && (
                  <small className="ml-2 rounded-full bg-[var(--surface-muted)] px-2 py-0.5 font-normal">
                    Oculto
                  </small>
                )}
              </span>
              <PreviewControl field={field} />
              {field.helpText && (
                <small className="block font-normal text-[var(--gaia-ink-500)]">
                  {field.helpText}
                </small>
              )}
            </label>
          ))}
        </div>
        {!fields.length && (
          <div className="mt-6 rounded-2xl border border-dashed p-8 text-center">
            <LayoutList className="mx-auto" />
            <strong className="mt-3 block">
              Aún no hay campos en el formulario
            </strong>
            <p className="text-sm text-[var(--gaia-ink-500)]">
              Agrega el primer campo para comenzar el diseño.
            </p>
          </div>
        )}
        <button
          className="mt-6 w-full rounded-xl bg-[var(--brand-primary)] p-3 font-semibold text-white"
          onClick={close}
        >
          Cerrar vista previa
        </button>
      </section>
    </div>
  );
}
function PreviewControl({ field }: { field: FormField }) {
  const style = "mt-2 min-h-11 w-full rounded-xl border bg-white px-3";
  if (field.controlType === 299540051)
    return (
      <textarea
        className={`${style} py-3`}
        placeholder={field.placeholder ?? ""}
        rows={3}
      />
    );
  if (field.controlType === 299540058)
    return (
      <select
        className={style}
        defaultValue={
          field.options.find((option) => option.isDefault)?.id ?? ""
        }
      >
        <option value="">
          {field.placeholder ||
            `Selecciona ${field.label.toLocaleLowerCase("es")}`}
        </option>
        {field.options.map((option) => (
          <option key={option.id} value={option.id}>
            {option.label}
          </option>
        ))}
      </select>
    );
  if (field.controlType === 299540059)
    return (
      <div className="mt-2 grid gap-2">
        {field.options.map((option) => (
          <label
            className="flex items-center gap-2 font-normal"
            key={option.id}
          >
            <input
              defaultChecked={option.isDefault}
              name={field.id}
              type="radio"
              value={option.id}
            />
            {option.label}
          </label>
        ))}
      </div>
    );
  if (field.controlType === 299540061)
    return (
      <div className="mt-2 grid gap-2">
        {field.options.map((option) => (
          <label
            className="flex items-center gap-2 font-normal"
            key={option.id}
          >
            <input
              defaultChecked={option.isDefault}
              type="checkbox"
              value={option.id}
            />
            {option.label}
          </label>
        ))}
      </div>
    );
  if (field.controlType === 299540060)
    return (
      <label className="mt-2 flex items-center gap-2 font-normal">
        <input type="checkbox" /> Sí
      </label>
    );
  if (field.controlType === 299540062)
    return (
      <input
        accept={field.allowedFileTypes ?? undefined}
        className={`${style} py-2`}
        multiple={field.allowsMultiple}
        type="file"
      />
    );
  return (
    <input
      className={style}
      placeholder={field.placeholder ?? ""}
      type={
        field.controlType === 299540052
          ? "number"
          : field.controlType === 299540053
            ? "email"
            : field.controlType === 299540054
              ? "tel"
              : field.controlType === 299540055
                ? "url"
                : field.controlType === 299540056
                  ? "date"
                  : field.controlType === 299540057
                    ? "datetime-local"
                    : "text"
      }
    />
  );
}
function Modal({
  title,
  close,
  children,
}: {
  title: string;
  close: () => void;
  children: React.ReactNode;
}) {
  return (
    <div className="fixed inset-0 z-[70] grid place-items-center bg-black/40 p-4">
      <section className="max-h-[92vh] w-full max-w-2xl overflow-auto rounded-3xl bg-white p-5 shadow-2xl sm:p-6">
        <header className="flex justify-between">
          <h2 className="text-xl font-semibold">{title}</h2>
          <button aria-label="Cerrar" onClick={close}>
            <X size={20} />
          </button>
        </header>
        {children}
      </section>
    </div>
  );
}
function Input({
  label,
  children,
  required = false,
  compact = false,
}: {
  label: string;
  children: React.ReactNode;
  required?: boolean;
  compact?: boolean;
}) {
  return (
    <label className={`${compact ? "" : "mt-4"} block text-xs font-semibold [&>input]:mt-2 [&>input]:min-h-11 [&>input]:w-full [&>input]:rounded-xl [&>input]:border [&>input]:px-3 [&>select]:mt-2 [&>select]:min-h-11 [&>select]:w-full [&>select]:rounded-xl [&>select]:border [&>select]:px-3 [&>textarea]:mt-2 [&>textarea]:w-full [&>textarea]:rounded-xl [&>textarea]:border [&>textarea]:p-3`}>
      {label}
      {required && (
        <span className="ml-1 text-red-600" aria-hidden="true">
          *
        </span>
      )}
      {children}
    </label>
  );
}
function Toggle({
  checked,
  label,
  change,
}: {
  checked: boolean;
  label: string;
  change: (value: boolean) => void;
}) {
  return (
    <label className="flex items-center gap-3 rounded-xl border p-2.5 text-sm font-semibold">
      <input
        checked={checked}
        onChange={(e) => change(e.target.checked)}
        type="checkbox"
      />
      {label}
    </label>
  );
}
function Actions({ close, saving }: { close: () => void; saving: boolean }) {
  return (
    <div className="mt-5 flex justify-end gap-3">
      <button
        className="rounded-xl border px-4 py-2 disabled:opacity-60"
        disabled={saving}
        onClick={close}
        type="button"
      >
        Cancelar
      </button>
      <button
        className="rounded-xl bg-[var(--brand-primary)] px-4 py-2 font-semibold text-white disabled:opacity-60"
        disabled={saving}
      >
        {saving ? "Guardando cambios…" : "Guardar cambios"}
      </button>
    </div>
  );
}
function makeCode(value: string) {
  return (
    value
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toUpperCase()
      .replace(/[^A-Z0-9]+/g, "_")
      .replace(/^_+|_+$/g, "")
      .slice(0, 80) || "CAMPO"
  );
}
function dataTypeFor(control: number) {
  return control === 299540052
    ? 299540042
    : control === 299540056
      ? 299540043
      : control === 299540057
        ? 299540044
        : control === 299540058
          ? 299540046
          : 299540040;
}
function message(reason: unknown) {
  return reason instanceof Error
    ? reason.message
    : "No fue posible completar la operación.";
}
