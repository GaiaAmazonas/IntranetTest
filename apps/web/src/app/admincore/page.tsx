"use client";

import Link from "@/components/document-link";
import { ArrowRight, Building2, Cake, CalendarDays, CheckCircle2, GraduationCap, Headphones, LockKeyhole, Megaphone, PackageSearch, ShieldCheck, Sparkles, Users } from "lucide-react";
import { AppHeader } from "@/components/app-header";
import { useSecurity } from "@/components/security-context";
import { apiRequest } from "@/lib/api-client";
import { useEffect, useMemo, useState } from "react";

type Birthday={id:string;fullName:string;day:number;month:number;photoUrl:string|null};
const moduleIcons={organization:Building2,people:Users,inventory:PackageSearch,security:LockKeyhole,communications:Megaphone,calendar:CalendarDays,helpdesk:Headphones,training:GraduationCap} as const;

export default function AdminCoreHomePage() {
  const security = useSecurity();
  const { user } = security;
  const availableModules = useMemo(() => security.modules.filter(module => {
    const route = module.route.replace(/\/$/, "").toLocaleLowerCase();
    return route !== "/admincore" && module.code.toLocaleUpperCase() !== "INT.APP.ADMINCORE";
  }), [security.modules]);
  const [birthdays,setBirthdays]=useState<Birthday[]>([]),[birthdaysLoading,setBirthdaysLoading]=useState(true);
  const firstName = user?.name.split(" ").filter(Boolean)[0] ?? "";
  const priorityModules=useMemo(()=>{
    const priorities=[
      {words:["helpdesk","solicitud"],title:"Revisar solicitudes",description:"Consulta casos nuevos, vencimientos y gestiones que requieren seguimiento.",icon:Headphones},
      {words:["comunic"],title:"Actualizar comunicaciones",description:"Verifica publicaciones, destacados y contenido visible en la Intranet.",icon:Megaphone},
      {words:["talento","persona"],title:"Mantener el equipo al día",description:"Revisa la información institucional de personas y estructura organizacional.",icon:Users},
      {words:["seguridad"],title:"Revisar accesos",description:"Administra permisos y módulos disponibles para cada responsabilidad.",icon:ShieldCheck},
    ];
    return priorities.map(priority=>({priority,module:availableModules.find(module=>priority.words.some(word=>`${module.code} ${module.name} ${module.description??""}`.toLocaleLowerCase().includes(word)))})).filter(item=>item.module).slice(0,3);
  },[availableModules]);
  useEffect(()=>{const timer=window.setTimeout(()=>{const today=new Date(),month=today.getMonth()+1;void apiRequest<Birthday[]>(`/api/intranet/birthdays?month=${month}`).then(rows=>setBirthdays(rows.filter(item=>item.day===today.getDate()&&item.month===month))).catch(()=>setBirthdays([])).finally(()=>setBirthdaysLoading(false));},400);return()=>window.clearTimeout(timer);},[]);
  if (!user) return null;

  return <main className="gaia-app-page gaia-admincore-home min-h-screen">
    <AppHeader title="Gaia Gestión · Inicio" user={{ displayName:user.name, email:user.email }}/>
    <div className="mx-auto max-w-[1400px] px-5 py-7 lg:px-8 lg:py-10">
      <section className="gaia-admincore-hero relative overflow-hidden rounded-[30px] px-6 py-8 text-white sm:px-9 lg:grid lg:grid-cols-[1fr_auto] lg:items-end lg:px-12 lg:py-11">
        <div className="relative z-10 max-w-3xl"><p className="gaia-admincore-hero-kicker flex items-center gap-2 text-[10px] font-bold uppercase tracking-[.18em]"><Sparkles size={14}/>Gaia Gestión</p><h1 className="mt-4 text-3xl font-semibold tracking-[-.035em] sm:text-4xl">Hola, {firstName}. Este es tu centro de trabajo.</h1><p className="gaia-admincore-hero-copy mt-3 max-w-2xl text-sm leading-6 sm:text-base">Prioriza lo importante, continúa tus gestiones y accede rápidamente a las herramientas autorizadas para tu responsabilidad.</p></div>
        <div className="gaia-admincore-hero-metric relative z-10 mt-7 flex items-center gap-3 rounded-2xl border border-white/15 px-4 py-3 backdrop-blur lg:mt-0"><span className="gaia-admincore-hero-metric-icon grid size-10 place-items-center rounded-xl"><ShieldCheck size={20}/></span><div><strong className="block text-xl">{availableModules.length}</strong><small className="gaia-admincore-hero-copy">módulos disponibles</small></div></div>
        <span className="pointer-events-none absolute -right-20 -top-32 size-80 rounded-full border-[55px] border-white/[.055]"/><span className="pointer-events-none absolute -bottom-36 right-40 size-60 rounded-full border-[42px] border-white/[.055]"/>
      </section>

      <section className={`gaia-admincore-today${birthdays.length?" has-celebrations":""}`}>
        <div className="gaia-admincore-date"><CalendarDays size={18}/><span><small>Hoy en Gaia</small><strong>{new Intl.DateTimeFormat("es-CO",{weekday:"long",day:"numeric",month:"long"}).format(new Date())}</strong></span></div>
        <div className="gaia-admincore-celebration"><Cake size={18}/><span><small>Cumpleaños del equipo</small>{birthdaysLoading?<strong>Preparando las celebraciones de hoy…</strong>:birthdays.length?<><strong>{birthdays.length===1?"¡Hoy celebramos una vida que hace parte de Gaia!":`¡Hoy celebramos la vida de ${birthdays.length} integrantes de Gaia!`}</strong><p>Les deseamos un feliz cumpleaños, bienestar y muchos motivos para celebrar.</p><div>{birthdays.map(item=>{const name=properName(item.fullName);return <span className="gaia-admincore-birthday-person" key={item.id}><b>{initials(name)}</b><em>{name}</em></span>;})}</div></>:<><strong>Hoy no tenemos cumpleaños en el equipo</strong><p>La próxima celebración aparecerá aquí para que podamos acompañarla juntos.</p></>}</span></div>
      </section>

      {availableModules.length?<section className="gaia-management-dashboard mt-6">
        <article className="gaia-management-priorities"><header><span><p className="gaia-admincore-eyebrow">ENFOQUE DE HOY</p><h2>Continúa tus gestiones</h2></span><CheckCircle2 size={22}/></header><div>{priorityModules.length?priorityModules.map(({priority,module})=>{const Icon=priority.icon;return <Link href={module!.route} key={module!.id}><span><Icon size={19}/></span><div><strong>{priority.title}</strong><p>{priority.description}</p><small>Abrir {module!.name} <ArrowRight size={13}/></small></div></Link>}):<div className="gaia-management-empty"><CheckCircle2 size={24}/><strong>Todo listo para comenzar</strong><p>Usa tus accesos rápidos para abrir una herramienta de gestión.</p></div>}</div></article>
        <article className="gaia-management-access"><header><span><p className="gaia-admincore-eyebrow">ACCESOS AUTORIZADOS</p><h2>Tus herramientas</h2></span><b>{availableModules.length}</b></header><div>{availableModules.map(module=>{const Icon=moduleIcons[(module.icon??"") as keyof typeof moduleIcons]??Building2;return <Link href={module.route} key={module.id}><span><Icon size={18}/></span><div><strong>{module.name}</strong><small>{module.description||"Herramienta institucional"}</small></div><ArrowRight size={14}/></Link>})}</div></article>
      </section>:<div className="mt-5 grid min-h-52 place-items-center rounded-3xl border border-dashed border-[var(--gaia-line-strong)] bg-white text-center"><div><LockKeyhole className="gaia-admincore-empty-icon mx-auto"/><h3 className="mt-3 font-semibold">No tienes módulos habilitados</h3><p className="mt-1 text-xs text-[var(--gaia-ink-500)]">Solicita a un administrador la revisión de tus permisos.</p></div></div>}
    </div>
  </main>;
}

function properName(value:string){return value.toLocaleLowerCase("es").replace(/(^|\s)\p{L}/gu,letter=>letter.toLocaleUpperCase("es"));}
function initials(value:string){return value.split(/\s+/).filter(Boolean).slice(0,2).map(word=>word[0]).join("").toLocaleUpperCase("es");}
