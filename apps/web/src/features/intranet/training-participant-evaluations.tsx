"use client";

import { Badge, Button } from "@/components/ui";
import { apiRequest } from "@/lib/api-client";
import { ChevronDown, ClipboardCheck, LoaderCircle } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

type Option={id:string;name:string};
type Question={id:string;statement:string;type:number;required:boolean;minimumScale:number|null;maximumScale:number|null;minimumLabel:string|null;maximumLabel:string|null;options:Option[]};
type Answer={questionId:string;text:string|null;number:number|null;optionId:string|null};
type Result={questionId:string;score:number|null;correct:boolean|null;feedback:string|null;correctOptionIds:string[]|null};
type Attempt={id:string;number:number;status:number;startedAt:string;submittedAt:string|null;deadline:string|null;percentage:number|null;passed:boolean|null;results:Result[]};
type Evaluation={id:string;name:string;description:string|null;type:number;required:boolean;affectsApproval:boolean;maximumAttempts:number|null;allowRetry:boolean;timeLimitMinutes:number|null;questions:Question[];attempts:Attempt[]};
const message=(e:unknown)=>e instanceof Error?e.message:"No fue posible guardar la evaluación.";

export function ParticipantEvaluations({assignmentId,contentComplete,canContinue,onUpdated}:{assignmentId:string;contentComplete:boolean;canContinue:boolean;onUpdated:()=>Promise<void>}) {
  const [items,setItems]=useState<Evaluation[]|null>(null),[error,setError]=useState(""),[busy,setBusy]=useState(""),[collapsed,setCollapsed]=useState<Set<string>>(new Set());
  const tokens=useRef(new Map<string,string>());
  const load=useCallback(async()=>{const values=await apiRequest<Evaluation[]>(`/api/training/my/assignments/${assignmentId}/evaluations`);setItems(values);return values},[assignmentId]);
  useEffect(()=>{let live=true;apiRequest<Evaluation[]>(`/api/training/my/assignments/${assignmentId}/evaluations`).then(x=>{if(live)setItems(x)}).catch(e=>{if(live)setError(message(e))});return()=>{live=false}},[assignmentId]);
  async function start(e:Evaluation) { setBusy(e.id);setError("");let token=tokens.current.get(e.id);if(!token){token=crypto.randomUUID();tokens.current.set(e.id,token)}try{await apiRequest(`/api/training/my/assignments/${assignmentId}/evaluations/${e.id}/start`,{method:"POST",body:JSON.stringify({token})});await load();tokens.current.delete(e.id)}catch(reason){setError(message(reason))}finally{setBusy("")} }
  async function submitted() { await load();await onUpdated(); }
  return <section id="training-evaluations" className="mt-7 training-learning-evaluations"><div className="training-section-toolbar"><div><small className="training-learning-eyebrow">02 · Pon en práctica lo aprendido</small><h2>Evaluaciones y encuestas</h2><p>Inicia cuando estés listo. Las respuestas se guardan al enviarlas; el tiempo sigue corriendo aunque cierres la página.</p></div><div className="flex flex-wrap gap-2"><Button variant="secondary" onClick={()=>setCollapsed(new Set())}>Expandir todo</Button><Button variant="secondary" onClick={()=>setCollapsed(new Set((items??[]).map(x=>x.id)))}>Contraer todo</Button></div></div>{error&&<div className="training-notice training-notice-error" role="alert">{error}<Button variant="secondary" onClick={()=>{setError("");void load().catch(e=>setError(message(e)))}}>Reintentar</Button></div>}
    {items===null&&!error&&<p>Cargando evaluaciones…</p>}{items?.length===0&&<p className="training-notice">Esta capacitación no tiene evaluaciones ni encuestas.</p>}
    <div className="space-y-4">{items?.map(e=>{
      const active=e.attempts.find(x=>x.status===299541110),passed=e.attempts.some(x=>x.passed===true||(!e.affectsApproval&&x.status===299541113)),pending=e.attempts.some(x=>x.status===299541112);
      const canStart=canContinue&&!active&&!passed&&!pending&&(!e.maximumAttempts||e.attempts.length<e.maximumAttempts)&&(!e.attempts.length||e.allowRetry)&&(contentComplete||e.type===299541051);
      return <article className="training-participant-section" key={e.id}><button type="button" className="training-collapse-header" aria-expanded={!collapsed.has(e.id)} onClick={()=>setCollapsed(previous=>{const next=new Set(previous);if(next.has(e.id))next.delete(e.id);else next.add(e.id);return next})}><span><small>{e.type===299541052?"Encuesta":e.type===299541051?"Diagnóstico":"Evaluación del aprendizaje"}{e.required?" · Obligatoria":""}</small><strong>{e.name}</strong></span><ChevronDown size={20} className={collapsed.has(e.id)?"":"rotate-180"}/></button>{!collapsed.has(e.id)&&<div className="training-section-body">{e.description&&<p className="mb-4">{e.description}</p>}<p className="mb-4 text-sm">{e.maximumAttempts?`${e.attempts.length} de ${e.maximumAttempts} intentos`:`${e.attempts.length} intento(s) · Sin límite de intentos`}{e.timeLimitMinutes?` · ${e.timeLimitMinutes} minutos por intento`:" · Sin límite de tiempo"}{e.affectsApproval?" · Cuenta para aprobar":" · No afecta la calificación general"}</p>
        {pending&&<div className="training-notice">Tus respuestas fueron enviadas. Esta evaluación incluye respuestas abiertas calificables: una persona autorizada debe revisarlas antes de que recibas el resultado final.</div>}
        {passed&&<Badge tone="success">{e.affectsApproval?"Evaluación aprobada":"Respondida"}</Badge>}
        {active&&canContinue?<AttemptForm key={active.id} assignmentId={assignmentId} evaluation={e} attempt={active} onSubmitted={submitted}/>:canStart?<Button disabled={Boolean(busy)} onClick={()=>void start(e)}>{busy===e.id?<LoaderCircle className="animate-spin" size={16}/>:<ClipboardCheck size={16}/>} {e.attempts.length?"Iniciar nuevo intento":"Iniciar"}</Button>:!passed&&!pending&&<p className="training-notice">{!canContinue?"Esta capacitación no está disponible para nuevas respuestas.":!contentComplete&&e.type!==299541051?"Primero completa el contenido obligatorio.":"No quedan intentos disponibles. Comunícate con el responsable si necesitas ayuda."}</p>}
        {e.attempts.filter(x=>x.status!==299541110).map(a=><div className="training-attempt-result" key={a.id}><strong>Intento {a.number} · {a.status===299541112?"Pendiente de revisión":!e.affectsApproval?"Enviado":a.passed?"Aprobado":"No aprobado"}{a.percentage!==null?` · ${a.percentage}%`:""}</strong>{a.submittedAt&&<small>{new Date(a.submittedAt).toLocaleString("es-CO")}</small>}{a.results.map(r=>{const q=e.questions.find(x=>x.id===r.questionId);return <div className="mt-3" key={r.questionId}><p>{q?.statement}</p>{r.score!==null&&<small>Puntaje: {r.score}</small>}{r.feedback&&<p>{r.feedback}</p>}{r.correctOptionIds?.length?<small>Respuesta correcta: {q?.options.filter(o=>r.correctOptionIds?.includes(o.id)).map(o=>o.name).join(", ")}</small>:null}</div>})}</div>)}
      </div>}</article>;
    })}</div>
  </section>;
}

function AttemptForm({assignmentId,evaluation,attempt,onSubmitted}:{assignmentId:string;evaluation:Evaluation;attempt:Attempt;onSubmitted:()=>Promise<void>}) {
  const storageKey=`gaia-training-draft:${assignmentId}:${attempt.id}`;
  const [answers,setAnswers]=useState<Record<string,Answer>>(()=>{try{return JSON.parse(sessionStorage.getItem(storageKey)||"{}")}catch{return {}}});
  const [busy,setBusy]=useState(false),[sent,setSent]=useState(false),[error,setError]=useState(""),[now,setNow]=useState(()=>Date.now());
  const sending=useRef(false),autoSent=useRef(false);
  const remaining=attempt.deadline?Math.max(0,Math.ceil((new Date(attempt.deadline).getTime()-now)/1000)):null;
  useEffect(()=>{try{sessionStorage.setItem(storageKey,JSON.stringify(answers))}catch{/* Draft storage is optional. */}},[answers,storageKey]);
  useEffect(()=>{if(!attempt.deadline)return;const timer=setInterval(()=>setNow(Date.now()),1000);return()=>clearInterval(timer)},[attempt.deadline]);
  const submit=useCallback(async()=>{
    if(sending.current||sent)return;sending.current=true;setBusy(true);setError("");
    try{await apiRequest(`/api/training/my/assignments/${assignmentId}/attempts/${attempt.id}/submit`,{method:"POST",body:JSON.stringify({answers:Object.values(answers)})});setSent(true);try{sessionStorage.removeItem(storageKey)}catch{}await onSubmitted()}catch(e){setError(message(e))}finally{sending.current=false;setBusy(false)}
  },[answers,assignmentId,attempt.id,onSubmitted,sent,storageKey]);
  useEffect(()=>{if(remaining===0&&!autoSent.current){autoSent.current=true;void submit()}},[remaining,submit]);
  function change(q:Question,value:Partial<Answer>){setAnswers(previous=>({...previous,[q.id]:{...(previous[q.id]??{questionId:q.id,text:null,number:null,optionId:null}),...value}}))}
  if(sent)return <div className="training-notice">Respuestas guardadas.<Button variant="secondary" onClick={()=>void onSubmitted().catch(e=>setError(message(e)))}>Actualizar resultado</Button>{error&&<p role="alert">{error}</p>}</div>;
  return <form className="training-answer-form" onSubmit={e=>{e.preventDefault();void submit()}}>
    {remaining!==null&&<div className="training-notice" role="timer">{remaining===0?"El tiempo terminó. Se enviarán tus respuestas y el intento se registrará fuera de tiempo.":`Tiempo restante: ${Math.floor(remaining/60)}:${String(remaining%60).padStart(2,"0")}`}</div>}
    {evaluation.questions.map((q,index)=><fieldset className="training-question" key={q.id} disabled={busy||remaining===0}><legend className="sr-only">{q.statement}</legend><h3>{index+1}. {q.statement}{q.required&&<span> *</span>}</h3>
      {q.type===299541061?<select aria-label={q.statement} required={q.required} value={answers[q.id]?.optionId??""} onChange={e=>change(q,{optionId:e.target.value||null})}><option value="">Selecciona una opción</option>{q.options.map(o=><option value={o.id} key={o.id}>{o.name}</option>)}</select>:q.type===299541060||q.type===299541062?<div className="space-y-2">{q.options.map(o=><label className="training-answer-choice" key={o.id}><input type="radio" name={`${attempt.id}-${q.id}`} required={q.required} checked={answers[q.id]?.optionId===o.id} onChange={()=>change(q,{optionId:o.id})}/><span>{o.name}</span></label>)}</div>:q.type===299541064?<textarea aria-label={q.statement} required={q.required} maxLength={4000} rows={4} value={answers[q.id]?.text??""} onChange={e=>change(q,{text:e.target.value})}/>:q.type===299541065?<div><div className="flex flex-wrap gap-2">{Array.from({length:Math.max(0,Math.min(101,(q.maximumScale??5)-(q.minimumScale??1)+1))},(_,i)=>i+(q.minimumScale??1)).map(n=><label className="training-scale" key={n}><input type="radio" name={`${attempt.id}-${q.id}`} required={q.required} checked={answers[q.id]?.number===n} onChange={()=>change(q,{number:n})}/><span>{n}</span></label>)}</div><p className="mt-2 text-sm">{q.minimumLabel} — {q.maximumLabel}</p></div>:<input aria-label={q.statement} required={q.required} maxLength={4000} placeholder="Escribe tu respuesta" value={answers[q.id]?.text??""} onChange={e=>change(q,{text:e.target.value})}/>}
    </fieldset>)}
    {error&&<div className="training-notice training-notice-error" role="alert">{error}</div>}<Button type="submit" disabled={busy}>{busy&&<LoaderCircle className="animate-spin" size={16}/>}Enviar respuestas</Button><small className="block mt-2">Una vez enviadas no podrás editar este intento.</small>
  </form>;
}
