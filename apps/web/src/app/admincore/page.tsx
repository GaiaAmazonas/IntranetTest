"use client";

import Link from "@/components/document-link";
import { ArrowRight, Building2, Cake, CalendarDays, Grid2X2, LockKeyhole, Megaphone, PackageSearch, ShieldCheck, Sparkles, Users } from "lucide-react";
import { AppHeader } from "@/components/app-header";
import { useSecurity } from "@/components/security-context";
import { apiRequest } from "@/lib/api-client";
import { useEffect, useMemo, useState } from "react";

type Birthday={id:string;fullName:string;day:number;month:number;photoUrl:string|null};
const moduleIcons={organization:Building2,people:Users,inventory:PackageSearch,security:LockKeyhole,communications:Megaphone,calendar:CalendarDays} as const;
const moduleAccents=["#317c87","#8b3c72","#55754b","#9a384d","#9b7736"];

export default function AdminCoreHomePage() {
  const security = useSecurity();
  const { user } = security;
  const availableModules = useMemo(() => security.modules.filter(module => {
    const route = module.route.replace(/\/$/, "").toLocaleLowerCase();
    return route !== "/admincore" && module.code.toLocaleUpperCase() !== "INT.APP.ADMINCORE";
  }), [security.modules]);
  const [birthdays,setBirthdays]=useState<Birthday[]>([]),[birthdaysLoading,setBirthdaysLoading]=useState(true);
  const firstName = user?.name.split(" ").filter(Boolean)[0] ?? "";
  useEffect(()=>{const timer=window.setTimeout(()=>{const today=new Date(),month=today.getMonth()+1;void apiRequest<Birthday[]>(`/api/intranet/birthdays?month=${month}`).then(rows=>setBirthdays(rows.filter(item=>item.day===today.getDate()&&item.month===month))).catch(()=>setBirthdays([])).finally(()=>setBirthdaysLoading(false));},400);return()=>window.clearTimeout(timer);},[]);
  if (!user) return null;

  return <main className="gaia-app-page gaia-admincore-home min-h-screen">
    <AppHeader title="AdminCore · Inicio" user={{ displayName:user.name, email:user.email }}/>
    <div className="mx-auto max-w-[1400px] px-5 py-7 lg:px-8 lg:py-10">
      <section className="gaia-admincore-hero relative overflow-hidden rounded-[30px] px-6 py-8 text-white sm:px-9 lg:grid lg:grid-cols-[1fr_auto] lg:items-end lg:px-12 lg:py-11">
        <div className="relative z-10 max-w-3xl"><p className="gaia-admincore-hero-kicker flex items-center gap-2 text-[10px] font-bold uppercase tracking-[.18em]"><Sparkles size={14}/>Centro de gestión institucional</p><h1 className="mt-4 text-3xl font-semibold tracking-[-.035em] sm:text-4xl">Hola, {firstName}.</h1><p className="gaia-admincore-hero-copy mt-3 max-w-2xl text-sm leading-6 sm:text-base">Aquí encuentras únicamente las herramientas que tienes autorizadas para gestionar. Tu espacio se adapta automáticamente a tus responsabilidades.</p></div>
        <div className="gaia-admincore-hero-metric relative z-10 mt-7 flex items-center gap-3 rounded-2xl border border-white/15 px-4 py-3 backdrop-blur lg:mt-0"><span className="gaia-admincore-hero-metric-icon grid size-10 place-items-center rounded-xl"><ShieldCheck size={20}/></span><div><strong className="block text-xl">{availableModules.length}</strong><small className="gaia-admincore-hero-copy">módulos disponibles</small></div></div>
        <span className="pointer-events-none absolute -right-20 -top-32 size-80 rounded-full border-[55px] border-white/[.055]"/><span className="pointer-events-none absolute -bottom-36 right-40 size-60 rounded-full border-[42px] border-white/[.055]"/>
      </section>

      <section className={`gaia-admincore-today${birthdays.length?" has-celebrations":""}`}>
        <div className="gaia-admincore-date"><CalendarDays size={18}/><span><small>Hoy en Gaia</small><strong>{new Intl.DateTimeFormat("es-CO",{weekday:"long",day:"numeric",month:"long"}).format(new Date())}</strong></span></div>
        <div className="gaia-admincore-celebration"><Cake size={18}/><span><small>Cumpleaños del equipo</small>{birthdaysLoading?<strong>Preparando las celebraciones de hoy…</strong>:birthdays.length?<><strong>{birthdays.length===1?"¡Hoy celebramos una vida que hace parte de Gaia!":`¡Hoy celebramos la vida de ${birthdays.length} integrantes de Gaia!`}</strong><p>Les deseamos un feliz cumpleaños, bienestar y muchos motivos para celebrar.</p><div>{birthdays.map(item=>{const name=properName(item.fullName);return <span className="gaia-admincore-birthday-person" key={item.id}><b>{initials(name)}</b><em>{name}</em></span>;})}</div></>:<><strong>Hoy no tenemos cumpleaños en el equipo</strong><p>La próxima celebración aparecerá aquí para que podamos acompañarla juntos.</p></>}</span></div>
      </section>

      <section className="mt-9"><div className="flex flex-wrap items-end justify-between gap-3"><div><p className="gaia-admincore-eyebrow text-[10px] font-bold uppercase tracking-[.15em]">Tu espacio de trabajo</p><h2 className="mt-2 text-2xl font-semibold tracking-tight text-[var(--gaia-ink-900)]">¿Qué necesitas gestionar hoy?</h2></div><p className="max-w-md text-xs leading-5 text-[var(--gaia-ink-500)]">Las opciones se muestran según los permisos asignados a tu cuenta en Seguridad.</p></div>
        {availableModules.length ? <div className="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-4">{availableModules.map((module,index)=>{const Icon=moduleIcons[(module.icon??"") as keyof typeof moduleIcons]??Grid2X2,accent=moduleAccents[index%moduleAccents.length];return <Link className="gaia-admincore-card group relative flex min-h-[235px] flex-col overflow-hidden rounded-[24px] border border-[var(--gaia-line)] bg-white p-5 transition duration-200 hover:-translate-y-1" href={module.route} key={module.id} style={{"--module-card-accent":accent} as React.CSSProperties}>
          <div className="flex items-start justify-between"><span className="grid size-12 place-items-center rounded-2xl border border-white/80 bg-white shadow-sm" style={{color:accent}}><Icon size={22}/></span><small className="text-[10px] font-bold tracking-[.14em] text-[#87948c]">{String(index+1).padStart(2,"0")}</small></div>
          <p className="mt-6 text-[10px] font-bold uppercase tracking-[.13em]" style={{color:accent}}>Módulo autorizado</p><h3 className="mt-2 text-xl font-semibold tracking-tight text-[var(--gaia-ink-900)]">{module.name}</h3><p className="mt-2 flex-1 text-xs leading-5 text-[var(--gaia-ink-500)]">{module.description||"Herramienta institucional disponible según tus permisos."}</p><span className="gaia-admincore-card-action mt-5 flex items-center gap-2 text-xs font-bold">Abrir módulo <ArrowRight className="transition group-hover:translate-x-1" size={15}/></span>
        </Link>;})}</div>:<div className="mt-5 grid min-h-52 place-items-center rounded-3xl border border-dashed border-[var(--gaia-line-strong)] bg-white text-center"><div><LockKeyhole className="gaia-admincore-empty-icon mx-auto"/><h3 className="mt-3 font-semibold">No tienes módulos habilitados</h3><p className="mt-1 text-xs text-[var(--gaia-ink-500)]">Solicita a un administrador la revisión de tus permisos.</p></div></div>}
      </section>
    </div>
  </main>;
}

function properName(value:string){return value.toLocaleLowerCase("es").replace(/(^|\s)\p{L}/gu,letter=>letter.toLocaleUpperCase("es"));}
function initials(value:string){return value.split(/\s+/).filter(Boolean).slice(0,2).map(word=>word[0]).join("").toLocaleUpperCase("es");}
