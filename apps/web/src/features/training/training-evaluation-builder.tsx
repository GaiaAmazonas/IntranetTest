"use client";

/* eslint-disable react-hooks/set-state-in-effect -- la versión controla la lectura remota. */
import { ConfirmDialog, FormDialog } from "@/components/form-dialog";
import { useSecurity } from "@/components/security-context";
import { apiRequest } from "@/lib/api-client";
import { CheckCircle2, ChevronDown, Circle, ClipboardCheck, Info, Pencil, Plus, Trash2 } from "lucide-react";
import { type FormEvent, useCallback, useEffect, useState } from "react";
import type { TrainingVersion } from "./training-administration";
import { canAddOptions, hasOptions, optionMarker, questionHelp } from "./training-evaluation-rules";

type O = { id: string; name: string; order: number; isCorrect: boolean; score: number | null; feedback: string | null };
type Q = { id: string; statement: string; type: number; required: boolean; order: number; maximumScore: number | null; gradable: boolean; requiresManualReview: boolean; minimumScale: number | null; maximumScale: number | null; minimumLabel: string | null; maximumLabel: string | null; correctFeedback: string | null; incorrectFeedback: string | null; options: O[] };
type E = { id: string; name: string; type: number; description: string | null; required: boolean; affectsApproval: boolean; minimumPercentage: number | null; maximumAttempts: number | null; timeLimitMinutes: number | null; randomizeQuestions: boolean; randomizeOptions: boolean; showResult: boolean; showCorrectAnswers: boolean; showFeedback: boolean; allowRetry: boolean; order: number; questions: Q[] };
type EF = Omit<E, "questions" | "description"> & { description: string };
type QF = Omit<Q, "options" | "minimumLabel" | "maximumLabel" | "correctFeedback" | "incorrectFeedback"> & { evaluationId: string; minimumLabel: string; maximumLabel: string; correctFeedback: string; incorrectFeedback: string };
type OF = Omit<O, "feedback"> & { questionId: string; feedback: string };
type Removal = { kind: "evaluation" | "question" | "option"; id: string; parentId: string; name: string; children: number };
const blankE: EF = { id: "", name: "", type: 299541050, description: "", required: true, affectsApproval: true, minimumPercentage: 80, maximumAttempts: 3, timeLimitMinutes: null, randomizeQuestions: false, randomizeOptions: false, showResult: true, showCorrectAnswers: false, showFeedback: true, allowRetry: true, order: 10 };

export function TrainingEvaluationBuilder({ versions }: { versions: TrainingVersion[] }) {
  const can = useSecurity().can("CAP.CONTENIDO.ADMINISTRAR");
  const [versionId, setVersionId] = useState(versions[0]?.id ?? "");
  const [items, setItems] = useState<E[]>([]);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [evaluation, setEvaluation] = useState<EF | null>(null);
  const [question, setQuestion] = useState<QF | null>(null);
  const [option, setOption] = useState<OF | null>(null);
  const [removal, setRemoval] = useState<Removal | null>(null);
  const editable = can && versions.find((item) => item.id === versionId)?.status === 299540000;

  const load = useCallback(async (id: string) => {
    if (!id) { setItems([]); return; }
    try { setItems(await apiRequest<E[]>(`/api/training/administration/versions/${id}/evaluations`)); setError(""); }
    catch (value) { setError(message(value)); }
  }, []);
  useEffect(() => { if (!versions.some((item) => item.id === versionId)) setVersionId(versions[0]?.id ?? ""); }, [versions, versionId]);
  useEffect(() => { void load(versionId); }, [load, versionId]);
  useEffect(() => { requestAnimationFrame(() => document.querySelectorAll<HTMLDetailsElement>('details[class*="group"]').forEach((item) => { item.open = true; })); }, [items]);

  async function send<T>(url: string, method: "POST" | "PUT", body: object, close?: () => void) {
    if (saving) return undefined;
    setSaving(true); setError("");
    try { const response = await apiRequest<T>(url, { method, body: JSON.stringify(body) }); close?.(); await load(versionId); return response; }
    catch (value) { const detail = message(value); setError(detail); window.dispatchEvent(new CustomEvent("gaia:form-error", { detail })); return undefined; }
    finally { setSaving(false); }
  }
  function saveE(event: FormEvent) { event.preventDefault(); if (!evaluation) return; const { id, ...body } = evaluation; void send(id ? `/api/training/administration/versions/${versionId}/evaluations/${id}` : `/api/training/administration/versions/${versionId}/evaluations`, id ? "PUT" : "POST", body, () => setEvaluation(null)); }
  function saveQ(event: FormEvent) { event.preventDefault(); if (!question) return; const { id, evaluationId, ...body } = question; void send(id ? `/api/training/administration/evaluations/${evaluationId}/questions/${id}` : `/api/training/administration/evaluations/${evaluationId}/questions`, id ? "PUT" : "POST", { ...body, name: body.statement.trim().slice(0, 200) }, () => setQuestion(null)); }
  function saveO(event: FormEvent) { event.preventDefault(); if (!option) return; const { id, questionId, ...body } = option; void send(id ? `/api/training/administration/questions/${questionId}/options/${id}` : `/api/training/administration/questions/${questionId}/options`, id ? "PUT" : "POST", body, () => setOption(null)); }
  function toggleCorrect(questionItem: Q, answer: O) { void send(`/api/training/administration/questions/${questionItem.id}/options/${answer.id}`, "PUT", { ...answer, isCorrect: true }); }

  async function remove() {
    if (!removal || saving) return;
    setSaving(true); setError("");
    try {
      const url = removal.kind === "evaluation" ? `/api/training/administration/versions/${versionId}/evaluations/${removal.id}` : removal.kind === "question" ? `/api/training/administration/evaluations/${removal.parentId}/questions/${removal.id}` : `/api/training/administration/questions/${removal.parentId}/options/${removal.id}`;
      await apiRequest(url, { method: "DELETE" });
      setItems((current) => removeLocally(current, removal));
      setRemoval(null);
      await load(versionId);
    } catch (value) { setError(message(value)); }
    finally { setSaving(false); }
  }

  return <section className="rounded-3xl border border-[var(--gaia-line)] bg-[var(--surface-card)] p-5">
    <Head title="Evaluación del aprendizaje" text="Comprueba el aprendizaje con preguntas guiadas. El formulario se adapta al tipo de respuesta y oculta configuraciones que no aplican." />
    <Version versions={versions} value={versionId} set={setVersionId} />
    {error && <p className="mt-4 rounded-xl bg-[#fff0f0] p-3 text-sm text-[#9a384d]">{error}</p>}
    {editable && <button className="gaia-button gaia-button-primary mt-5" onClick={() => setEvaluation({ ...blankE, order: (items.length + 1) * 10 })}><Plus size={16} />Crear evaluación</button>}
    <div className="mt-5 space-y-4">{items.map((item) => <details className="group overflow-hidden rounded-2xl border border-[var(--gaia-line)]" key={item.id}>
      <summary className="flex cursor-pointer list-none items-start gap-3 bg-[var(--gaia-accent-pale)] p-4"><ClipboardCheck className="text-[var(--brand-primary)]" /><div className="flex-1"><small className="font-bold uppercase text-[var(--brand-primary)]">{evaluationType(item.type)}</small><h3 className="font-semibold">{item.name}</h3><p className="text-xs text-[var(--gaia-ink-500)]">{item.questions.length} preguntas · {item.maximumAttempts ? `${item.maximumAttempts} intentos` : "sin límite"}{item.minimumPercentage !== null ? ` · aprueba con ${item.minimumPercentage}%` : ""}</p></div><ChevronDown className="transition group-open:rotate-180" /></summary>
      <div className="p-4"><Actions>{editable && <><Button text="Editar evaluación" run={() => setEvaluation({ ...item, description: item.description ?? "" })} icon="edit" /><Button text="Eliminar evaluación" run={() => setRemoval({ kind: "evaluation", id: item.id, parentId: versionId, name: item.name, children: item.questions.length })} icon="delete" /><Button text="Agregar pregunta" run={() => setQuestion(newQuestion(item))} /></>}</Actions>
        {item.questions.map((questionItem, index) => <details className="group/question mt-3 rounded-xl border border-[var(--gaia-line)]" key={questionItem.id}><summary className="flex cursor-pointer list-none items-start gap-3 p-4 transition hover:bg-[var(--gaia-surface-subtle)]"><b className="grid size-7 place-items-center rounded-full bg-[var(--gaia-accent-soft)] text-xs">{index + 1}</b><span className="flex-1"><strong className="block text-sm">{questionItem.statement}</strong><small className="text-[var(--gaia-ink-500)]">{questionType(questionItem.type)} · {questionItem.required ? "obligatoria" : "opcional"} · {questionItem.gradable ? "calificable" : "sin calificación"}{questionItem.maximumScore !== null ? ` · ${questionItem.maximumScore} puntos` : ""}</small></span><ChevronDown className="transition group-open/question:rotate-180" size={17} /></summary>
          <div className="border-t border-[var(--gaia-line)] p-4"><Actions>{editable && <><Button text="Editar pregunta" run={() => setQuestion(toQuestion(item.id, questionItem))} icon="edit" /><Button text="Eliminar pregunta" run={() => setRemoval({ kind: "question", id: questionItem.id, parentId: item.id, name: questionItem.statement, children: questionItem.options.length })} icon="delete" />{canAddOptions(questionItem.type) && <Button text="Agregar opción" run={() => setOption({ id: "", questionId: questionItem.id, name: "", order: (questionItem.options.length + 1) * 10, isCorrect: false, score: null, feedback: "" })} />}</>}</Actions>
            {hasOptions(questionItem.type) && <div className="mt-3 grid gap-2 sm:grid-cols-2">{questionItem.options.map((answer, answerIndex) => <div className={`flex items-center gap-2 rounded-xl border px-3 py-2 text-xs transition ${answer.isCorrect ? "border-[#8bc5a3] bg-[#edf8f1]" : "border-[var(--gaia-line)] hover:bg-[var(--gaia-surface-subtle)]"}`} key={answer.id}><span className="font-bold text-[var(--brand-primary)]">{optionMarker(questionItem.type, answerIndex)}</span>{answer.isCorrect ? <CheckCircle2 className="text-[#2f7a55]" size={16} /> : <Circle size={16} />}<span className="flex-1">{answer.name}</span>{answer.score !== null && <b>{answer.score} pt</b>}{editable && <><button className="rounded-lg border border-[var(--gaia-line)] px-2 py-1.5 font-semibold hover:bg-white" onClick={() => toggleCorrect(questionItem, answer)} type="button">{answer.isCorrect && questionItem.type === 299541061 ? "Desmarcar" : answer.isCorrect ? "Correcta" : "Marcar correcta"}</button>{questionItem.type !== 299541062 && <><button aria-label="Editar opción" className="rounded-lg border border-[var(--gaia-line)] p-1.5 hover:bg-white" onClick={() => setOption({ ...answer, questionId: questionItem.id, feedback: answer.feedback ?? "" })}><Pencil size={13} /></button><button aria-label="Eliminar opción" className="rounded-lg border border-[#e8c4c4] p-1.5 text-[#9a384d] hover:bg-[#fff0f0]" onClick={() => setRemoval({ kind: "option", id: answer.id, parentId: questionItem.id, name: answer.name, children: 0 })}><Trash2 size={13} /></button></>}</>}</div>)}{!questionItem.options.length && <p className="col-span-full rounded-lg border border-dashed p-3 text-center text-xs text-[#9a384d]">Agrega por lo menos dos opciones antes de publicar.</p>}</div>}
            {!hasOptions(questionItem.type) && <AnswerPreview question={questionItem} />}
          </div></details>)}
        {!item.questions.length && <p className="mt-3 rounded-xl border border-dashed p-5 text-center text-sm text-[#9a384d]">Esta evaluación todavía no tiene preguntas.</p>}
      </div></details>)}{versionId && !items.length && <p className="rounded-2xl border border-dashed p-7 text-center text-sm">Esta versión todavía no tiene evaluaciones.</p>}</div>
    <Editor kind="evaluation" value={evaluation} set={setEvaluation} saving={saving} save={saveE} />
    <Editor kind="question" value={question} set={setQuestion} saving={saving} save={saveQ} />
    <Editor kind="option" value={option} set={setOption} saving={saving} save={saveO} />
    <ConfirmDialog confirmLabel="Eliminar" destructive loading={saving} onCancel={() => setRemoval(null)} onConfirm={() => void remove()} open={Boolean(removal)} title="¿Eliminar este elemento?" description={removal?.children ? `“${removal.name}” contiene ${removal.children} elemento(s), que también se retirarán.` : `Se retirará “${removal?.name ?? ""}”.`} />
  </section>;
}

function Editor({ kind, value, set, saving, save }: { kind: "evaluation" | "question" | "option"; value: EF | QF | OF | null; set: (value: never) => void; saving: boolean; save: (event: FormEvent) => void }) {
  const close = () => set(null as never);
  return <FormDialog error="" formId={`training-${kind}`} loading={saving} onClose={close} open={Boolean(value)} submitLabel={(value as { id?: string })?.id ? "Guardar cambios" : "Agregar"} subtitle={kind === "question" ? "Selecciona cómo responderá la persona; mostraremos únicamente la configuración necesaria." : "Los códigos técnicos se generan automáticamente."} title={`${(value as { id?: string })?.id ? "Editar" : "Nuevo"} ${kind === "evaluation" ? "evaluación" : kind === "question" ? "pregunta" : "opción"}`}>{value && <form className="space-y-4" id={`training-${kind}`} onSubmit={save}>{kind === "evaluation" ? <EvaluationFields value={value as EF} set={set} /> : kind === "question" ? <QuestionFields value={value as QF} set={set} /> : <OptionFields value={value as OF} set={set} />}</form>}</FormDialog>;
}

function EvaluationFields({ value, set }: { value: EF; set: (value: never) => void }) {
  return <><Field label="Nombre" help="Identifica el propósito de esta evaluación."><input required value={value.name} onChange={(event) => set({ ...value, name: event.target.value } as never)} /></Field><Field label="Propósito" help="Conocimientos califica; diagnóstico mide el punto de partida; satisfacción recoge la experiencia."><select value={value.type} onChange={(event) => set({ ...value, type: Number(event.target.value) } as never)}><option value="299541050">Conocimientos</option><option value="299541051">Diagnóstico</option><option value="299541052">Satisfacción</option></select></Field><Field label="Descripción" help="Explica al participante qué se evaluará."><textarea rows={3} value={value.description} onChange={(event) => set({ ...value, description: event.target.value } as never)} /></Field><NumberField label="Porcentaje para aprobar" value={value.minimumPercentage} set={(next) => set({ ...value, minimumPercentage: next } as never)} /><NumberField label="Máximo de intentos" value={value.maximumAttempts} set={(next) => set({ ...value, maximumAttempts: next } as never)} /><Toggle label="Obligatoria" checked={value.required} set={(next) => set({ ...value, required: next } as never)} /><Toggle label="Define la aprobación" checked={value.affectsApproval} set={(next) => set({ ...value, affectsApproval: next } as never)} /><Toggle label="Aleatorizar preguntas" checked={value.randomizeQuestions} set={(next) => set({ ...value, randomizeQuestions: next } as never)} /><Toggle label="Aleatorizar opciones" checked={value.randomizeOptions} set={(next) => set({ ...value, randomizeOptions: next } as never)} /></>;
}

function QuestionFields({ value, set }: { value: QF; set: (value: never) => void }) {
  const setType = (type: number) => set({ ...value, type, gradable: [299541060, 299541061, 299541062].includes(type) ? value.gradable : false, requiresManualReview: [299541063, 299541064].includes(type) ? value.requiresManualReview : false, minimumScale: type === 299541065 ? value.minimumScale ?? 1 : null, maximumScale: type === 299541065 ? value.maximumScale ?? 5 : null, minimumLabel: type === 299541065 ? value.minimumLabel || "Muy malo" : "", maximumLabel: type === 299541065 ? value.maximumLabel || "Excelente" : "" } as never);
  return <>
    <Field label="Enunciado de la pregunta" help="Escribe exactamente lo que verá el participante."><textarea required rows={4} value={value.statement} onChange={(event) => set({ ...value, statement: event.target.value } as never)} /></Field>
    <Field label="Tipo de respuesta" help={questionHelp(value.type)}><select value={value.type} onChange={(event) => setType(Number(event.target.value))}><option value="299541060">Selección única con botones</option><option value="299541061">Lista desplegable</option><option value="299541062">Sí o No</option><option value="299541063">Texto corto</option><option value="299541064">Texto largo</option><option value="299541065">Escala de valoración</option></select></Field>
    {hasOptions(value.type) && <div className="flex gap-3 rounded-xl bg-[var(--gaia-accent-pale)] p-4 text-sm"><Info className="shrink-0 text-[var(--brand-primary)]" size={18} /><p>{value.type === 299541062 ? "Las opciones Sí y No se crearán automáticamente. Después de guardar solo debes marcar cuál es correcta." : "Después de guardar podrás agregar las opciones. En la vista final aparecerán automáticamente como a), b), c)…; no necesitas escribir la letra."}</p></div>}
    {value.type === 299541065 && <><div className="grid gap-4 sm:grid-cols-2"><NumberField label="Valor mínimo" value={value.minimumScale} set={(next) => set({ ...value, minimumScale: next } as never)} required /><NumberField label="Valor máximo" value={value.maximumScale} set={(next) => set({ ...value, maximumScale: next } as never)} required /><Field label="Etiqueta del mínimo" help="Ejemplo: Muy malo."><input required value={value.minimumLabel} onChange={(event) => set({ ...value, minimumLabel: event.target.value } as never)} /></Field><Field label="Etiqueta del máximo" help="Ejemplo: Excelente."><input required value={value.maximumLabel} onChange={(event) => set({ ...value, maximumLabel: event.target.value } as never)} /></Field></div><ScalePreview minimum={value.minimumScale} maximum={value.maximumScale} minimumLabel={value.minimumLabel} maximumLabel={value.maximumLabel} /></>}
    {value.type === 299541063 && <Preview title="Así responderá el participante"><input disabled className="min-h-11 w-full rounded-xl border bg-white px-3" placeholder="Respuesta en una sola línea" /></Preview>}
    {value.type === 299541064 && <Preview title="Así responderá el participante"><textarea disabled className="w-full rounded-xl border bg-white p-3" placeholder="Respuesta extensa" rows={4} /></Preview>}
    {value.gradable && <NumberField label="Puntaje máximo" value={value.maximumScore} set={(next) => set({ ...value, maximumScore: next } as never)} />}
    <Toggle label="Respuesta obligatoria" checked={value.required} set={(next) => set({ ...value, required: next } as never)} />
    {hasOptions(value.type) && <Toggle label="Pregunta calificable" checked={value.gradable} set={(next) => set({ ...value, gradable: next } as never)} />}
    {[299541063, 299541064].includes(value.type) && <Toggle label="Requiere revisión manual" checked={value.requiresManualReview} set={(next) => set({ ...value, requiresManualReview: next } as never)} />}
  </>;
}

function OptionFields({ value, set }: { value: OF; set: (value: never) => void }) {
  return <><Field label="Texto de la opción" help="Escribe una alternativa clara; la letra se agrega automáticamente."><input required value={value.name} onChange={(event) => set({ ...value, name: event.target.value } as never)} /></Field><NumberField label="Puntaje" value={value.score} set={(next) => set({ ...value, score: next } as never)} /><Field label="Retroalimentación" help="Explicación opcional para el participante."><textarea rows={3} value={value.feedback} onChange={(event) => set({ ...value, feedback: event.target.value } as never)} /></Field><Toggle label="Respuesta correcta" checked={value.isCorrect} set={(next) => set({ ...value, isCorrect: next } as never)} /></>;
}

function AnswerPreview({ question }: { question: Q }) { return question.type === 299541065 ? <ScalePreview minimum={question.minimumScale} maximum={question.maximumScale} minimumLabel={question.minimumLabel ?? "Mínimo"} maximumLabel={question.maximumLabel ?? "Máximo"} /> : <Preview title="Vista previa de la respuesta">{question.type === 299541064 ? <textarea disabled className="w-full rounded-xl border bg-white p-3" rows={3} placeholder="El participante escribirá aquí su respuesta extensa" /> : <input disabled className="min-h-11 w-full rounded-xl border bg-white px-3" placeholder="El participante escribirá aquí su respuesta" />}</Preview>; }
function ScalePreview({ minimum, maximum, minimumLabel, maximumLabel }: { minimum: number | null; maximum: number | null; minimumLabel: string; maximumLabel: string }) { const valid = minimum !== null && maximum !== null && minimum < maximum; const values = valid && maximum - minimum <= 10 ? Array.from({ length: maximum - minimum + 1 }, (_, index) => minimum + index) : []; return <Preview title="Así verá la escala el participante"><div className="flex items-center justify-between gap-3 text-xs text-[var(--gaia-ink-500)]"><span>{minimumLabel || "Mínimo"}</span><span>{maximumLabel || "Máximo"}</span></div><div className="mt-3 flex flex-wrap justify-between gap-2">{values.length ? values.map((item) => <span className="grid size-10 place-items-center rounded-full border bg-white font-semibold" key={item}>{item}</span>) : <span className="text-sm text-[#9a384d]">El mínimo debe ser menor que el máximo.</span>}</div></Preview>; }
function Preview({ title, children }: { title: string; children: React.ReactNode }) { return <div className="rounded-xl border border-[var(--gaia-line)] bg-[var(--gaia-surface-subtle)] p-4"><strong className="mb-3 block text-xs uppercase tracking-wide text-[var(--brand-primary)]">{title}</strong>{children}</div>; }
function Head({ title, text }: { title: string; text: string }) { return <div><h2 className="text-xl font-semibold">{title}</h2><p className="mt-1 max-w-2xl text-xs leading-5 text-[var(--gaia-ink-500)]">{text}</p></div>; }
function Version({ versions, value, set }: { versions: TrainingVersion[]; value: string; set: (value: string) => void }) { return <label className="mt-4 block text-xs font-semibold">Versión <select className="ml-2 min-h-10 max-w-[650px] rounded-xl border px-3" value={value} onChange={(event) => set(event.target.value)}>{versions.map((version) => <option key={version.id} value={version.id}>{version.training} · v{version.number} · {version.publicTitle}</option>)}</select></label>; }
function Actions({ children }: { children: React.ReactNode }) { return <div className="flex flex-wrap justify-end gap-2">{children}</div>; }
function Button({ text, run, icon }: { text: string; run: () => void; icon?: "edit" | "delete" }) { return <button className={`gaia-button ${icon === "delete" ? "gaia-button-danger" : "gaia-button-secondary"} !min-h-9 !px-3 !text-xs`} onClick={run} type="button">{icon === "edit" ? <Pencil size={14} /> : icon === "delete" ? <Trash2 size={14} /> : <Plus size={14} />}{text}</button>; }
function Field({ label, help, children }: { label: string; help: string; children: React.ReactNode }) { return <label className="block text-sm font-semibold">{label}<span className="mt-2 block [&>*]:min-h-11 [&>*]:w-full [&>*]:rounded-xl [&>*]:border [&>*]:px-3 [&>textarea]:py-3">{children}</span><small className="mt-1 block font-normal leading-5 text-[var(--gaia-ink-500)]">{help}</small></label>; }
function NumberField({ label, value, set, required = false }: { label: string; value: number | null; set: (value: number | null) => void; required?: boolean }) { return <Field label={label} help={required ? "Este valor es necesario para guardar." : "Déjalo vacío cuando no aplique."}><input min="0" required={required} step="1" type="number" value={value ?? ""} onChange={(event) => set(event.target.value ? Number(event.target.value) : null)} /></Field>; }
function Toggle({ label, checked, set }: { label: string; checked: boolean; set: (value: boolean) => void }) { return <label className="flex gap-3 rounded-xl border p-3 text-sm"><input checked={checked} onChange={(event) => set(event.target.checked)} type="checkbox" /><strong>{label}</strong></label>; }
function newQuestion(evaluation: E): QF { return { id: "", evaluationId: evaluation.id, statement: "", type: 299541060, required: true, order: (evaluation.questions.length + 1) * 10, maximumScore: null, gradable: evaluation.type === 299541050, requiresManualReview: false, minimumScale: null, maximumScale: null, minimumLabel: "", maximumLabel: "", correctFeedback: "", incorrectFeedback: "" }; }
function toQuestion(evaluationId: string, question: Q): QF { return { ...question, evaluationId, minimumLabel: question.minimumLabel ?? "", maximumLabel: question.maximumLabel ?? "", correctFeedback: question.correctFeedback ?? "", incorrectFeedback: question.incorrectFeedback ?? "" }; }
function evaluationType(type: number) { return type === 299541050 ? "Conocimientos" : type === 299541051 ? "Diagnóstico" : "Satisfacción"; }
function questionType(type: number) { return ({ 299541060: "Selección única con botones", 299541061: "Lista desplegable", 299541062: "Sí o No", 299541063: "Texto corto", 299541064: "Texto largo", 299541065: "Escala de valoración" } as Record<number, string>)[type]; }
function removeLocally(items: E[], removal: Removal) { if (removal.kind === "evaluation") return items.filter((item) => item.id !== removal.id); return items.map((item) => removal.kind === "question" ? { ...item, questions: item.questions.filter((question) => question.id !== removal.id) } : { ...item, questions: item.questions.map((question) => ({ ...question, options: question.options.filter((answer) => answer.id !== removal.id) })) }); }
function message(value: unknown) { return value instanceof Error ? value.message : "No fue posible completar la operación."; }
