"use client";

/* eslint-disable react-hooks/set-state-in-effect -- la versión controla la lectura remota. */
import { ConfirmDialog, FormDialog } from "@/components/form-dialog";
import { useSecurity } from "@/components/security-context";
import { apiRequest } from "@/lib/api-client";
import { ChevronLeft, ChevronRight, Info, Plus, Search, Trash2, UserMinus, UsersRound } from "lucide-react";
import { type FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import type { Overview, TrainingVersion } from "./training-administration";
import { TrainingUnitSelect } from "./training-unit-select";

type Rule = { id: string; type: number; mode: number; unitId: string | null; unit: string | null; personId: string | null; person: string | null; includeSubunits: boolean; reason: string | null };
type Person = { id: string; name: string; unitId: string | null; unit: string | null; isExcluded: boolean; exclusionReason: string | null };
type Audience = { rules: Rule[]; preview: Person[]; includedCount: number; excludedCount: number };
type Form = { type: number; mode: number; unitId: string; personId: string; includeSubunits: boolean; reason: string };
const blank: Form = { type: 299541001, mode: 299541010, unitId: "", personId: "", includeSubunits: true, reason: "" };

export function TrainingAudienceManager({ data }: { data: Overview }) {
  const can = useSecurity().can("CAP.AUDIENCIAS.ADMINISTRAR");
  const [versionId, setVersionId] = useState(data.versionItems[0]?.id ?? "");
  const [audience, setAudience] = useState<Audience | null>(null);
  const [form, setForm] = useState<Form>(blank);
  const [personText, setPersonText] = useState("");
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<"all" | "included" | "excluded">("all");
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [removeId, setRemoveId] = useState("");
  const [exclude, setExclude] = useState<Person | null>(null);
  const [include, setInclude] = useState<Person | null>(null);
  const [reason, setReason] = useState("");
  const version = data.versionItems.find((item) => item.id === versionId);
  const editable = can && version?.status === 299540000;
  const pageSize = 10;
  const people = useMemo(() => {
    const term = search.trim().toLocaleLowerCase("es");
    return audience?.preview.filter((item) => (statusFilter === "all" || (statusFilter === "excluded") === item.isExcluded) && (!term || `${item.name} ${item.unit ?? ""}`.toLocaleLowerCase("es").includes(term))) ?? [];
  }, [audience, search, statusFilter]);
  const pages = Math.max(1, Math.ceil(people.length / pageSize));
  const current = Math.min(page, pages);
  const visible = people.slice((current - 1) * pageSize, current * pageSize);

  const load = useCallback(async (id: string) => {
    if (!id) { setAudience(null); return; }
    setLoading(true);
    try {
      setAudience(await apiRequest<Audience>(`/api/training/administration/versions/${id}/audience`));
      setError("");
    } catch (value) { setError(message(value, "No fue posible calcular los participantes.")); }
    finally { setLoading(false); }
  }, []);

  useEffect(() => {
    if (!data.versionItems.some((item) => item.id === versionId)) setVersionId(data.versionItems[0]?.id ?? "");
  }, [data.versionItems, versionId]);
  useEffect(() => { void load(versionId); setPage(1); }, [load, versionId]);

  async function save(event: FormEvent) {
    event.preventDefault();
    if (saving) return;
    setSaving(true); setError("");
    try {
      await apiRequest(`/api/training/administration/versions/${versionId}/audience`, {
        method: "POST",
        body: JSON.stringify({ ...form, unitId: form.type === 299541001 ? form.unitId : null, personId: form.type === 299541002 ? form.personId : null }),
      });
      setForm(blank); setPersonText("");
      await load(versionId);
    } catch (value) { setError(message(value, "No fue posible agregar el grupo.")); }
    finally { setSaving(false); }
  }

  async function remove() {
    if (!removeId) return;
    setSaving(true);
    try {
      await apiRequest(`/api/training/administration/versions/${versionId}/audience/${removeId}`, { method: "DELETE" });
      setRemoveId(""); await load(versionId);
    } catch (value) { setError(message(value, "No fue posible retirar el criterio.")); }
    finally { setSaving(false); }
  }

  async function excludePerson(event: FormEvent) {
    event.preventDefault();
    if (!exclude || saving) return;
    setSaving(true);
    try {
      await apiRequest(`/api/training/administration/versions/${versionId}/audience`, {
        method: "POST",
        body: JSON.stringify({ type: 299541002, mode: 299541011, unitId: null, personId: exclude.id, includeSubunits: false, reason }),
      });
      setExclude(null); setReason(""); await load(versionId);
    } catch (value) {
      const detail = message(value, "No fue posible excluir a la persona.");
      setError(detail);
      window.dispatchEvent(new CustomEvent("gaia:form-error", { detail }));
    } finally { setSaving(false); }
  }

  async function includePerson() {
    if (!include || saving) return;
    const exclusion = audience?.rules.find((rule) => rule.mode === 299541011 && rule.type === 299541002 && rule.personId === include.id);
    if (!exclusion) { setError("No se encontró la excepción individual que excluye a esta persona."); setInclude(null); return; }
    setSaving(true);
    try {
      await apiRequest(`/api/training/administration/versions/${versionId}/audience/${exclusion.id}`, { method: "DELETE" });
      setInclude(null); await load(versionId);
    } catch (value) { setError(message(value, "No fue posible incluir nuevamente a la persona.")); }
    finally { setSaving(false); }
  }

  return <>
    <section className="rounded-3xl border bg-[var(--surface-card)] p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div><h2 className="text-xl font-semibold">Participantes de la capacitación</h2><p className="mt-1 text-xs text-[var(--gaia-ink-500)]">Define grupos de personas y administra excepciones antes de publicar.</p></div>
        <VersionSelect versions={data.versionItems} value={versionId} set={setVersionId} />
      </div>
      {version && <div className="mt-4 flex gap-3 rounded-2xl bg-[var(--gaia-accent-pale)] p-4 text-sm"><Info className="shrink-0 text-[var(--brand-primary)]" size={18} /><p><strong>Participantes {version.dynamicAudience ? "dinámicos" : "fijos al publicar"}.</strong> {version.dynamicAudience ? "El sistema recalculará quién cumple los criterios." : "La lista calculada será la base de las asignaciones al publicar."}</p></div>}
      {error && <p className="mt-4 rounded-xl bg-[#fff0f0] p-3 text-sm text-[#9a384d]">{error}</p>}

      {editable && <form className="mt-5 rounded-2xl bg-[var(--gaia-surface-subtle)] p-4" onSubmit={save}>
        <h3 className="font-semibold">Agregar grupo o excepción</h3>
        <p className="mt-1 text-xs text-[var(--gaia-ink-500)]">Incluye toda Gaia, una unidad o una persona. Usa excluir para registrar excepciones justificadas.</p>
        <div className="mt-4 grid gap-4 md:grid-cols-2">
          <Field label="Operación" help="Incluye personas en la capacitación o las excluye del alcance."><select value={form.mode} onChange={(event) => setForm({ ...form, mode: Number(event.target.value) })}><option value="299541010">Incluir participantes</option><option value="299541011">Excluir participantes</option></select></Field>
          <Field label="Grupo que deseas seleccionar" help="Puedes seleccionar toda la organización, una unidad o una persona."><select value={form.type} onChange={(event) => { setForm({ ...form, type: Number(event.target.value), unitId: "", personId: "" }); setPersonText(""); }}><option value="299541000">Toda la organización</option><option value="299541001">Unidad organizacional</option><option value="299541002">Persona específica</option></select></Field>
          {form.type === 299541001 && <Field label="Unidad organizacional" help="Escribe código o nombre. La lista conserva el árbol y está ordenada por código."><TrainingUnitSelect units={data.units} value={form.unitId} onChange={(unitId) => setForm({ ...form, unitId })} /></Field>}
          {form.type === 299541002 && <Field label="Persona" help="Escribe el nombre y selecciona una coincidencia."><input list="audience-people" required placeholder="Buscar persona" value={personText} onChange={(event) => { setPersonText(event.target.value); const found = data.responsibles.find((item) => item.name === event.target.value); setForm({ ...form, personId: found?.id ?? "" }); }} /><datalist id="audience-people">{data.responsibles.map((item) => <option key={item.id} value={item.name} />)}</datalist></Field>}
          <Field label="Motivo u observación" help="Deja documentada la razón del grupo o excepción."><input maxLength={500} value={form.reason} onChange={(event) => setForm({ ...form, reason: event.target.value })} /></Field>
        </div>
        {form.type === 299541001 && <label className="mt-3 flex gap-3 rounded-xl border bg-white p-3 text-sm"><input checked={form.includeSubunits} onChange={(event) => setForm({ ...form, includeSubunits: event.target.checked })} type="checkbox" /><span><strong className="block">Incluir unidades subordinadas</strong><small>También incorpora personas de las unidades dependientes.</small></span></label>}
        <button className="gaia-button gaia-button-primary mt-4" disabled={saving}><Plus size={16} />{saving ? "Calculando…" : "Agregar grupo"}</button>
      </form>}

      <div className="mt-6 overflow-hidden rounded-2xl border border-[var(--gaia-line)] bg-[var(--surface-card)]">
        <header className="flex flex-wrap items-end justify-between gap-4 border-b border-[var(--gaia-line)] p-5">
          <div><h3 className="font-semibold">Participantes configurados</h3><p className="mt-1 text-xs text-[var(--gaia-ink-500)]">{audience?.includedCount ?? 0} personas incluidas · {audience?.excludedCount ?? 0} excluidas</p></div>
          <div className="flex w-full flex-wrap gap-2 sm:w-auto"><select aria-label="Filtrar participantes por estado" className="min-h-11 rounded-xl border border-[var(--gaia-line)] bg-white px-3 text-sm font-semibold" value={statusFilter} onChange={(event) => { setStatusFilter(event.target.value as typeof statusFilter); setPage(1); }}><option value="all">Todos</option><option value="included">Incluidos</option><option value="excluded">Excluidos</option></select><label className="flex min-h-11 min-w-0 flex-1 items-center gap-2 rounded-xl border border-[var(--gaia-line)] bg-white px-3 transition focus-within:border-[var(--brand-primary)] focus-within:ring-2 focus-within:ring-[var(--gaia-accent-pale)] sm:w-72"><Search className="text-[var(--gaia-ink-500)]" size={16} /><input className="min-w-0 flex-1 bg-transparent text-sm outline-none" placeholder="Buscar persona o unidad" value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} /></label></div>
        </header>
        <div className="border-b border-[var(--gaia-line)] bg-[var(--gaia-surface-subtle)] px-5 py-4">
          <p className="mb-3 text-[10px] font-bold uppercase tracking-[.12em] text-[var(--gaia-ink-500)]">Alcance y excepciones</p>
          <div className="flex flex-wrap gap-2">{audience?.rules.map((rule) => <span className={`inline-flex max-w-full items-center gap-2 rounded-full border px-3 py-2 text-xs ${rule.mode === 299541010 ? "border-[#cfe5d6] bg-[#edf7ef] text-[#286846]" : "border-[#ecc9c4] bg-[#fff3f1] text-[#a23a32]"}`} key={rule.id}><b>{rule.mode === 299541010 ? "Incluye" : "Excluye"}</b><span className="truncate">{target(rule)}</span>{rule.reason && <span className="hidden text-[var(--gaia-ink-500)] lg:inline">· {rule.reason}</span>}{editable && <button aria-label={`Retirar ${target(rule)}`} className="grid size-6 place-items-center rounded-full transition hover:bg-black/10" onClick={() => setRemoveId(rule.id)} type="button"><Trash2 size={13} /></button>}</span>)}{!audience?.rules.length && <span className="text-xs text-[var(--gaia-ink-500)]">Todavía no has definido grupos o excepciones.</span>}</div>
        </div>
        <div className="overflow-x-auto">{loading ? <div className="grid min-h-40 place-items-center text-sm text-[var(--gaia-ink-500)]">Calculando participantes…</div> : <table className="gaia-data-table w-full min-w-[720px] border-collapse text-sm"><thead className="bg-[var(--gaia-accent-pale)] text-[11px] uppercase tracking-wide text-[var(--gaia-ink-500)]"><tr><th className="px-5 py-3">Persona</th><th className="px-4 py-3">Unidad organizacional</th><th className="px-4 py-3">Estado</th><th className="px-5 py-3">Acciones</th></tr></thead><tbody>{visible.map((person) => <tr className={`border-t border-[var(--gaia-line)] transition hover:bg-[var(--gaia-surface-subtle)] ${person.isExcluded ? "bg-[#fff8f6]" : ""}`} key={person.id}><td className="px-5 py-4 font-semibold text-[var(--gaia-ink-900)]">{person.name}{person.exclusionReason && <small className="mt-1 block font-normal text-[var(--gaia-ink-500)]">Motivo: {person.exclusionReason}</small>}</td><td className="px-4 py-4 text-[var(--gaia-ink-700)]">{person.unit || "Sin unidad activa"}</td><td className="px-4 py-4"><span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-bold ${person.isExcluded ? "bg-[#fde7e3] text-[#a23a32]" : "bg-[#e7f5eb] text-[#286846]"}`}>{person.isExcluded ? "Excluida" : "Incluida"}</span></td><td className="px-5 py-4">{editable && (person.isExcluded ? <button className="gaia-button gaia-button-primary !min-h-9 !px-3 !text-xs" onClick={() => setInclude(person)}>Incluir</button> : <button className="gaia-button gaia-button-secondary !min-h-9 !px-3 !text-xs" onClick={() => { setExclude(person); setReason(""); }}><UserMinus size={14} />Excluir</button>)}</td></tr>)}{!visible.length && <tr><td className="p-10 text-center text-[var(--gaia-ink-500)]" colSpan={4}><UsersRound className="mx-auto mb-2" />No hay participantes para los filtros seleccionados.</td></tr>}</tbody></table>}</div>
        <footer className="flex flex-wrap items-center justify-between gap-3 border-t border-[var(--gaia-line)] px-5 py-4 text-xs text-[var(--gaia-ink-500)]"><span>{people.length ? `${(current - 1) * pageSize + 1}-${Math.min(current * pageSize, people.length)} de ${people.length}` : "0 resultados"}</span><div className="flex items-center gap-2"><button aria-label="Página anterior" className="gaia-button gaia-button-secondary !min-h-9 !px-3" disabled={current === 1} onClick={() => setPage(current - 1)}><ChevronLeft size={15} /></button><strong>Página {current} de {pages}</strong><button aria-label="Página siguiente" className="gaia-button gaia-button-secondary !min-h-9 !px-3" disabled={current === pages} onClick={() => setPage(current + 1)}><ChevronRight size={15} /></button></div></footer>
      </div>
    </section>

    <ConfirmDialog confirmLabel="Eliminar criterio" destructive loading={saving} onCancel={() => setRemoveId("")} onConfirm={() => void remove()} open={Boolean(removeId)} title="¿Eliminar este grupo o excepción?" description="Se recalculará el listado efectivo; no se eliminarán personas ni unidades." />
    <ConfirmDialog confirmLabel="Incluir participante" loading={saving} onCancel={() => setInclude(null)} onConfirm={() => void includePerson()} open={Boolean(include)} title="¿Incluir nuevamente a esta persona?" description={`Se retirará la excepción individual de ${include?.name ?? "la persona"} y volverá al alcance de la capacitación.`} />
    <FormDialog error="" formId="exclude-participant" loading={saving} onClose={() => setExclude(null)} open={Boolean(exclude)} submitLabel="Excluir participante" subtitle="La persona se conservará en Dataverse y quedará registrada como una excepción." title="Excluir del grupo">
      <form className="space-y-4" id="exclude-participant" onSubmit={excludePerson}><div className="rounded-xl bg-[var(--gaia-accent-pale)] p-4"><strong>{exclude?.name}</strong><small className="block">{exclude?.unit || "Sin unidad activa"}</small></div><Field label="Motivo de la excepción" help="Explica por qué esta persona no debe recibir la capacitación."><textarea minLength={5} required rows={4} value={reason} onChange={(event) => setReason(event.target.value)} /></Field></form>
    </FormDialog>
  </>;
}

function VersionSelect({ versions, value, set }: { versions: TrainingVersion[]; value: string; set: (value: string) => void }) {
  return <label className="text-xs font-semibold">Versión<select className="ml-2 min-h-10 max-w-full rounded-xl border px-3" value={value} onChange={(event) => set(event.target.value)}>{versions.map((version) => <option key={version.id} value={version.id}>{version.training} · v{version.number}</option>)}</select></label>;
}
function Field({ label, help, children }: { label: string; help: string; children: React.ReactNode }) {
  return <label className="block text-xs font-semibold"><span className="mb-2 block">{label}</span><span className="block [&>input]:min-h-11 [&>input]:w-full [&>input]:rounded-xl [&>input]:border [&>input]:bg-white [&>input]:px-3 [&>select]:min-h-11 [&>select]:w-full [&>select]:rounded-xl [&>select]:border [&>select]:bg-white [&>select]:px-3 [&>textarea]:min-h-11 [&>textarea]:w-full [&>textarea]:rounded-xl [&>textarea]:border [&>textarea]:bg-white [&>textarea]:px-3 [&>textarea]:py-3">{children}</span><small className="mt-1 block font-normal text-[var(--gaia-ink-500)]">{help}</small></label>;
}
function target(rule: Rule) { return rule.type === 299541000 ? "Toda la organización" : rule.type === 299541001 ? rule.unit || "Unidad" : rule.person || "Persona"; }
function message(value: unknown, fallback: string) { return value instanceof Error ? value.message : fallback; }
