"use client";

import { useEffect, useState, type FormEvent } from "react";
import { ArrowRight, CheckCircle2, LoaderCircle, Paperclip, X } from "lucide-react";
import { useFeedback } from "@/components/feedback";
import { apiRequest } from "@/lib/api-client";
import { DynamicFormRenderer, type DynamicValues, type StageForm } from "./solicitudes-dynamic-form";

type ManagementForm = { managementId: string; requestId: string; form: StageForm | null; answers: { responseId: string; fieldId: string; value: string | null; optionIds: string[] }[] };
type WorkflowDestination = { stepId: string; code: string; name: string; final: boolean };
type WorkflowAction = { result: number; destinations: WorkflowDestination[] };
type WorkflowManagement = { id: string; final: boolean; nextActions: WorkflowAction[] };
type Workflow = { managements: WorkflowManagement[] };

export function SolicitudesManagementFormDialog({ managementId, requestId, result, requiresObservation, requiresFile, hasRelatedFile, finalStep, onCancel, onCompleted }: {
  managementId: string; requestId: string; result: number; requiresObservation: boolean; requiresFile: boolean; hasRelatedFile: boolean; finalStep: boolean;
  onCancel: () => void; onCompleted: (observation: string | null, uploadedInForm: boolean) => Promise<void>;
}) {
  const { notify } = useFeedback();
  const [data, setData] = useState<ManagementForm | null>(null), [values, setValues] = useState<DynamicValues>({}), [observation, setObservation] = useState(""), [files, setFiles] = useState<File[]>([]), [busy, setBusy] = useState(false), [error, setError] = useState(""), [impact, setImpact] = useState("");

  useEffect(() => {
    let live = true;
    Promise.all([
      apiRequest<ManagementForm>(`/api/solicitudes/management/workflows/${managementId}/form`, { cache: "no-store" }),
      apiRequest<Workflow>(`/api/solicitudes/requests/${requestId}/workflow`, { cache: "no-store" }),
    ]).then(([value, workflow]) => {
      if (!live) return;
      setData(value);
      const initial: DynamicValues = {};
      for (const answer of value.answers) initial[answer.fieldId] = answer.optionIds.length ? answer.optionIds : answer.value ?? "";
      setValues(initial);
      setImpact(describeImpact(workflow.managements.find((item) => item.id === managementId), result, finalStep));
    }).catch((value) => live && setError(message(value)));
    return () => { live = false; };
  }, [finalStep, managementId, requestId, result]);

  const requiresComment = requiresObservation || finalStep || result === 299540173;
  async function uploadFile(file: File, responseId?: string) {
    const body = new FormData();
    body.append("file", file); body.append("visibility", "internal"); body.append("managementId", managementId);
    if (responseId) body.append("managementFieldResponseId", responseId);
    await apiRequest(`/api/solicitudes/requests/${requestId}/attachments`, { method: "POST", body });
  }
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!data) return;
    if (requiresFile && !hasRelatedFile && files.length === 0) { setError("Debes adjuntar al menos un archivo para completar esta gestión."); return; }
    setBusy(true); setError("");
    try {
      let uploaded = false;
      if (data.form && result !== 299540173) {
        const answers = data.form.fields.filter((field) => field.visible).map((field) => {
          const value = values[field.id];
          return { fieldId: field.id, value: field.dataType === 299540046 || field.dataType === 299540047 ? null : typeof value === "string" ? value : null, optionIds: field.dataType === 299540046 ? Array.isArray(value) ? value : typeof value === "string" && value ? [value] : [] : [] };
        });
        const saved = await apiRequest<{ fieldId: string; responseId: string }[]>(`/api/solicitudes/management/workflows/${managementId}/form/responses`, { method: "PUT", body: JSON.stringify({ answers }) });
        const responseIds = new Map(saved.map((item) => [item.fieldId, item.responseId]));
        for (const field of data.form.fields.filter((item) => item.visible && item.dataType === 299540047)) {
          const fieldFiles = values[field.id];
          if (!Array.isArray(fieldFiles)) continue;
          for (const file of fieldFiles as File[]) { await uploadFile(file, responseIds.get(field.id)); uploaded = true; }
        }
      }
      for (const file of files) { await uploadFile(file); uploaded = true; }
      await onCompleted(observation.trim() || null, uploaded);
      notify({ tone: "success", title: successTitle(result), description: impact || "El resultado quedó registrado correctamente." });
    } catch (value) {
      const description = message(value); setError(description); setBusy(false);
      notify({ tone: "error", title: "No se pudo registrar el resultado", description });
    }
  }

  const title = finalStep && result === 299540171 ? "Cerrar solicitud como aprobada" : finalStep && result === 299540172 ? "Cerrar solicitud como rechazada" : finalStep ? "Cerrar solicitud" : result === 299540171 ? "Aprobar etapa" : result === 299540172 ? "Rechazar etapa" : result === 299540173 ? "Solicitar información" : "Completar etapa";
  return <div className="fixed inset-0 z-[90] grid place-items-center bg-black/45 p-3"><form aria-busy={busy} className="max-h-[94vh] w-[min(96vw,64rem)] overflow-auto rounded-3xl bg-[var(--surface-card)] p-6 shadow-2xl" onSubmit={submit}>
    <header className="flex items-start justify-between gap-4"><div><small className="font-bold uppercase tracking-widest text-[var(--brand-primary)]">Gestión de solicitud</small><h2 className="mt-1 text-2xl font-semibold">{title}</h2><p className="mt-1 text-sm text-[var(--gaia-ink-500)]">{result === 299540173 ? "Indica con claridad qué información necesitas para continuar." : "Revisa la información y el efecto de esta decisión antes de confirmarla."}</p></div><button aria-label="Cerrar" className="rounded-full border p-2 transition hover:bg-[var(--surface-muted)]" disabled={busy} onClick={onCancel} type="button"><X size={18} /></button></header>
    {busy && <section className="mt-5 flex items-center gap-3 rounded-2xl border border-[#b8d8d2] bg-[#eef8f5] p-4 text-[#245f58]" role="status"><LoaderCircle className="shrink-0 animate-spin" size={22} /><div><strong className="text-sm">Finalizando etapa y actualizando expediente…</strong><p className="mt-1 text-xs opacity-80">Espera un momento. La ventana se cerrará cuando el nuevo estado esté listo.</p></div></section>}
    {impact && result !== 299540173 && <section className="mt-5 flex gap-3 rounded-2xl border border-[#b8d8d2] bg-[#eef8f5] p-4"><span className="grid size-9 shrink-0 place-items-center rounded-full bg-white text-[var(--brand-primary)]"><ArrowRight size={18} /></span><div><strong className="text-sm">Qué ocurrirá al confirmar</strong><p className="mt-1 text-sm leading-5 text-[var(--gaia-ink-500)]">{impact}</p></div></section>}
    {error && <p className="mt-4 rounded-xl bg-[#fff0f0] p-3 text-sm text-[#9a384d]" role="alert">{error}</p>}
    {!data ? <p className="mt-8 flex items-center justify-center gap-2 text-sm"><LoaderCircle className="animate-spin" size={17} />Preparando la gestión…</p> : <>
      {result !== 299540173 && data.form && <div className="mt-6"><DynamicFormRenderer form={data.form} onChange={(id, value) => setValues((current) => ({ ...current, [id]: value }))} values={values} /></div>}
      <label className="mb-4 mt-5 block text-sm font-semibold">{result === 299540173 ? "Información que debe aportar el solicitante" : finalStep ? "Resumen de la solución" : "Observación de la gestión"}<textarea autoFocus={result === 299540173} className="mt-2 w-full rounded-xl border border-[var(--gaia-line)] p-3 disabled:opacity-60" disabled={busy} maxLength={4000} onChange={(event) => setObservation(event.target.value)} placeholder={result === 299540173 ? "Describe únicamente la información faltante" : requiresComment ? "Este texto es obligatorio para continuar" : "Agrega una observación si aporta contexto al recorrido"} required={requiresComment} rows={4} value={observation} /></label>
      <label className="block rounded-xl border border-dashed border-[var(--gaia-line)] p-4 text-sm font-semibold transition hover:bg-[var(--surface-muted)]"><span className="flex items-center gap-2"><Paperclip size={17} />Archivos adjuntos {requiresFile ? <em className="not-italic text-[#9a384d]">(obligatorio)</em> : <span className="font-normal text-[var(--gaia-ink-500)]">(opcional)</span>}</span><input className="mt-3 block w-full text-sm font-normal" disabled={busy} multiple onChange={(event) => setFiles(Array.from(event.target.files ?? []))} type="file" />{files.length > 0 && <small className="mt-2 block font-normal text-[var(--gaia-ink-500)]">{files.map((file) => file.name).join(" · ")}</small>}{hasRelatedFile && <small className="mt-2 block font-normal text-[#317c70]">Esta gestión ya tiene un archivo asociado.</small>}</label>
      <div className="sticky bottom-0 -mx-6 -mb-6 mt-6 flex flex-wrap justify-end gap-2 border-t bg-[var(--surface-card)] px-6 py-4"><button className="rounded-xl border px-4 py-2 transition hover:bg-[var(--surface-muted)]" disabled={busy} onClick={onCancel} type="button">Cancelar</button><button className="flex min-w-40 items-center justify-center gap-2 rounded-xl bg-[var(--brand-primary)] px-5 py-2 font-semibold text-white disabled:opacity-60" disabled={busy}>{busy ? <><LoaderCircle className="animate-spin" size={17} />Actualizando expediente…</> : <><CheckCircle2 size={17} />{title}</>}</button></div>
    </>}
  </form></div>;
}

function describeImpact(item: WorkflowManagement | undefined, result: number, finalStep: boolean) { if (result === 299540173) return "La gestión quedará en espera y el solicitante deberá aportar la información indicada antes de continuar."; const destinations = item?.nextActions.find((action) => action.result === result)?.destinations ?? []; if (destinations.length) { const names = destinations.map((destination) => `${destination.name}${destination.final ? " (etapa final)" : ""}`); return `Se registrará ${resultLabel(result).toLowerCase()} y el recorrido continuará en ${joinNames(names)}.`; } if (finalStep) return `Se registrará ${resultLabel(result).toLowerCase()} y esta etapa podrá cerrar el recorrido de la solicitud.`; return `Se registrará ${resultLabel(result).toLowerCase()}. No existe una etapa posterior configurada para este resultado.`; }
function joinNames(names: string[]) { return names.length < 2 ? names[0] ?? "la siguiente etapa" : `${names.slice(0, -1).join(", ")} y ${names.at(-1)}`; }
function resultLabel(result: number) { return result === 299540171 ? "Aprobación" : result === 299540172 ? "Rechazo" : result === 299540173 ? "Solicitud de información" : "Finalización de etapa"; }
function successTitle(result: number) { return result === 299540171 ? "Aprobación registrada" : result === 299540172 ? "Rechazo registrado" : result === 299540173 ? "Información solicitada" : "Etapa completada"; }
const message = (error: unknown) => error instanceof Error ? error.message : "No fue posible completar la gestión.";
