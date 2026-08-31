"use client";

import Image from "next/image";
import Link from "next/link";
import {
  ArrowRight,
  BookOpenText,
  Cake,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  ExternalLink,
  Grid2X2,
  LifeBuoy,
  PartyPopper,
  RotateCcw,
  Search,
} from "lucide-react";
import { useEffect, useMemo, useState, type CSSProperties } from "react";
import { useSecurity } from "@/components/security-context";
import { intranetHomePreview } from "./intranet-home.preview";
import { apiRequest } from "@/lib/api-client";
const apiUrl=process.env.NEXT_PUBLIC_GAIA_API_URL??"https://localhost:7168";
type PublicBanner={id:string;eyebrow?:string;title:string;description?:string;destinationType:number;actionUrl?:string;eventId?:string;desktopImageUrl:string;mobileImageUrl:string};
type PublicEvent={id:string;name:string;type:string;color:string;summary?:string;startsAt:string;allDay:boolean;modality?:number;location?:string};
type Birthday={id:string;fullName:string;day:number;month:number;photoUrl:string|null};

const quickActions = [
  { href: "/intranet/helpdesk", label: "Nueva solicitud", detail: "Autoservicio Helpdesk", icon: LifeBuoy, tone: "coral", permission: "INT.HELPDESK.VER" },
  { href: "/intranet/personas", label: "Buscar persona", detail: "Directorio interno", icon: Search, tone: "blue", permission: "INT.PERSONAS.VER" },
  { href: "/intranet/calendario", label: "Consultar agenda", detail: "Eventos y actividades", icon: CalendarDays, tone: "purple", permission: "INT.CALENDARIO.VER" },
] as const;

const frequentApps = [
  { label: "AdminCore", detail: "Administración", initials: "AC", tone: "green", href: "/admincore", permission: "INT.APP.ADMINCORE.VER" },
] as const;

export function IntranetHome() {
  const { can, user } = useSecurity();
  const [activeEvent, setActiveEvent] = useState(0);
  const [banners,setBanners]=useState<PublicBanner[]>([]),[upcoming,setUpcoming]=useState<PublicEvent[]>([]);
  const [bannersLoading,setBannersLoading]=useState(true);
  const [birthdays,setBirthdays]=useState<Birthday[]>([]),[nextBirthdays,setNextBirthdays]=useState<Birthday[]>([]),[birthdaysLoading,setBirthdaysLoading]=useState(true);
  const [birthdaySlide,setBirthdaySlide]=useState(0),[celebrationReplay,setCelebrationReplay]=useState(0);
  const [loadedBannerImage,setLoadedBannerImage]=useState<string | null>(null);
  const firstName = user?.name.split(" ").filter(Boolean)[0] ?? "equipo Gaia";
  const formattedDate = new Intl.DateTimeFormat("es-CO", {
    weekday: "long",
    day: "numeric",
    month: "long",
  }).format(new Date());
  const slides=useMemo(()=>banners.map(x=>({eyebrow:x.eyebrow||"Destacado Gaia",title:x.title,summary:x.description||"Información institucional destacada.",detail:"Fundación Gaia Amazonas",image:`${apiUrl}${x.desktopImageUrl}`,mobileImage:`${apiUrl}${x.mobileImageUrl}`,href:x.destinationType===1&&x.eventId?`/intranet/calendario?evento=${x.eventId}`:x.destinationType===2&&x.actionUrl?x.actionUrl:"/intranet"})),[banners]);
  const nextBirthdayGroups=useMemo(()=>groupBirthdaysByDate(nextBirthdays).slice(0,8),[nextBirthdays]);
  const event = slides.length ? slides[activeEvent%slides.length] : null;
  const bannerImageReady=event?.image===loadedBannerImage;

  useEffect(() => {
    if(slides.length<2)return;
    const timer = window.setInterval(() => setActiveEvent(current => (current + 1) % slides.length), 7000);
    return () => window.clearInterval(timer);
  }, [slides.length]);
  useEffect(()=>{let active=true;const load=()=>apiRequest<PublicBanner[]>("/api/intranet/banners",{cache:"no-store"}).then(rows=>{if(active){setBanners(rows);setActiveEvent(0);}}).catch(()=>{if(active)setBanners([]);}).finally(()=>{if(active)setBannersLoading(false);});void load();const refresh=()=>{if(document.visibilityState==="visible")void load();};window.addEventListener("focus",refresh);document.addEventListener("visibilitychange",refresh);return()=>{active=false;window.removeEventListener("focus",refresh);document.removeEventListener("visibilitychange",refresh);};},[]);
  useEffect(()=>{const from=new Date(),until=new Date();from.setHours(0,0,0,0);until.setMonth(until.getMonth()+6);void apiRequest<PublicEvent[]>(`/api/intranet/events?from=${encodeURIComponent(from.toISOString())}&to=${encodeURIComponent(until.toISOString())}`).then(e=>setUpcoming(e.slice(0,5))).catch(()=>setUpcoming([]));},[]);
  useEffect(()=>{if(bannersLoading)return;const timer=window.setTimeout(()=>{const today=new Date(),month=today.getMonth()+1,nextMonth=month===12?1:month+1;void Promise.all([apiRequest<Birthday[]>(`/api/intranet/birthdays?month=${month}`),apiRequest<Birthday[]>(`/api/intranet/birthdays?month=${nextMonth}`)]).then(([current,next])=>{const all=[...current,...next].filter((item,index,rows)=>rows.findIndex(candidate=>candidate.id===item.id&&candidate.day===item.day&&candidate.month===item.month)===index);setBirthdays(all.filter(item=>item.day===today.getDate()&&item.month===month));setNextBirthdays(all.filter(item=>!(item.day===today.getDate()&&item.month===month)).sort((a,b)=>nextBirthdayDate(a,today).getTime()-nextBirthdayDate(b,today).getTime()));}).catch(()=>{setBirthdays([]);setNextBirthdays([]);}).finally(()=>setBirthdaysLoading(false));},350);return()=>window.clearTimeout(timer);},[bannersLoading]);
  useEffect(()=>{if(nextBirthdayGroups.length<2)return;const timer=window.setInterval(()=>setBirthdaySlide(current=>(current+1)%nextBirthdayGroups.length),6000);return()=>window.clearInterval(timer);},[nextBirthdayGroups.length]);
  useEffect(()=>{if(!event)return;const image=new window.Image();image.src=event.image;image.onload=()=>setLoadedBannerImage(event.image);image.onerror=()=>setLoadedBannerImage(event.image);return()=>{image.onload=null;image.onerror=null};},[event]);
  useEffect(()=>{if(slides.length<2)return;const next=slides[(activeEvent+1)%slides.length];const image=new window.Image();image.src=next.image;},[activeEvent,slides]);

  return (
    <div className="intranet-home">
      <section className="intranet-personal-welcome">
        <p>{formattedDate}</p>
        <div><h1>Hola, {firstName}.</h1><h2>Esto es lo próximo en Gaia.</h2><span>Encuentros, novedades y momentos importantes para seguir conectados.</span></div>
      </section>

      {event?<section aria-busy={!bannerImageReady} aria-label="Eventos destacados" className={`intranet-event-spotlight${bannerImageReady?" is-ready":" is-loading"}`}>
        <div aria-label="Actividad institucional de Gaia en la Amazonía" className="intranet-event-background" key={event.image} style={bannerImageReady?{"--banner-desktop":`url('${event.image}')`,"--banner-mobile":`url('${event.mobileImage}')`} as CSSProperties:undefined} />
        <div className="intranet-event-shade" />
        <div className="intranet-event-copy">
          <p>{event.eyebrow}</p>
          <h2>{event.title}</h2>
          <span>{event.summary}</span>
          <div><strong>{event.detail}</strong><Link href={event.href}>Ver detalle <ArrowRight size={15} /></Link></div>
        </div>
        <div className="intranet-event-controls">
          <button aria-label="Evento anterior" onClick={() => setActiveEvent(current => (current - 1 + slides.length) % slides.length)} type="button"><ChevronLeft size={18} /></button>
          <div aria-label={`Diapositiva ${activeEvent + 1} de ${slides.length}`} className="intranet-event-pagination">{slides.map((slide, index) => <button aria-current={index === activeEvent ? "true" : undefined} aria-label={`Ver evento ${index + 1}: ${slide.title}`} key={`${slide.title}-${index}`} onClick={() => setActiveEvent(index)} type="button" />)}</div>
          <button aria-label="Evento siguiente" onClick={() => setActiveEvent(current => (current + 1) % slides.length)} type="button"><ChevronRight size={18} /></button>
        </div>
      </section>:bannersLoading?<section aria-busy="true" aria-label="Cargando eventos destacados" className="intranet-event-spotlight intranet-event-spotlight-loading"><div><span>Cargando contenido destacado</span></div></section>:<section aria-label="Eventos destacados" className="intranet-event-spotlight intranet-event-spotlight-empty"><div><p>Destacados Gaia</p><h2>Pronto encontrarás nuevas historias aquí.</h2><span>Comunicaciones está preparando los próximos eventos y contenidos institucionales.</span></div></section>}

      <section className="intranet-action-band">
        <div className="intranet-quick-actions">
          <header><p>Empieza aquí</p><h2>¿Qué necesitas hacer?</h2></header>
          <div>
            {quickActions.filter(action => can(action.permission)).map(action => {
              const Icon = action.icon;
              return (
                <Link href={action.href} key={action.label}>
                  <i className={`tone-${action.tone}`}><Icon aria-hidden="true" size={19} /></i>
                  <span><strong>{action.label}</strong><small>{action.detail}</small></span>
                  <ArrowRight aria-hidden="true" size={16} />
                </Link>
              );
            })}
          </div>
        </div>

        <div className="intranet-frequent-apps">
          <header><span><p>Espacio de trabajo</p><h2>Tu plataforma empresarial</h2></span><Link href="/intranet/aplicaciones">Ver catálogo <ArrowRight size={14} /></Link></header>
          <div>
            {frequentApps.filter(app => can(app.permission)).map(app => (
              <Link className="intranet-frequent-app" href={app.href} key={app.label} rel="noopener noreferrer" target="_blank">
                <i className={`tone-${app.tone}`}>{app.initials}</i>
                <span><strong>{app.label}</strong><small>{app.detail}</small></span>
                <ExternalLink aria-hidden="true" size={14} />
              </Link>
            ))}
            {!frequentApps.some(app => can(app.permission)) && <p className="intranet-apps-empty">No tienes aplicaciones frecuentes asignadas.</p>}
          </div>
        </div>
      </section>

      <section className="intranet-content-grid">
        <div className="intranet-communications">
          <header className="intranet-section-title"><span><p>Comunicaciones</p><h2>Lo que está pasando</h2></span><button disabled type="button">Ver todas <ArrowRight size={14} /></button></header>
          <article className="intranet-lead-story">
            <div className="intranet-story-art"><Image alt="" height={122} src="/brand/icons/login-yarumo.png" width={122} /><span>GAIA<br /><strong>AMAZONAS</strong></span></div>
            <div><small>{intranetHomePreview.communication.category}</small><h3>{intranetHomePreview.communication.title}</h3><p>{intranetHomePreview.communication.summary}</p><span>Contenido de referencia visual</span></div>
          </article>
          <div className="intranet-secondary-stories">
            {intranetHomePreview.secondaryCommunications.map(story => <article key={story.title}><i className={`tone-${story.tone}`}><BookOpenText size={16} /></i><span><small>{story.category}</small><strong>{story.title}</strong></span></article>)}
          </div>
        </div>

        <aside className="intranet-agenda">
          <header className="intranet-section-title"><span><p>Agenda</p><h2>Próximamente</h2></span><Link href="/intranet/calendario">Calendario <ArrowRight size={14} /></Link></header>
          <div className="intranet-agenda-list">
            {upcoming.map(x=>({day:String(new Date(x.startsAt).getDate()).padStart(2,"0"),month:new Intl.DateTimeFormat("es-CO",{month:"short"}).format(new Date(x.startsAt)),category:x.type,title:x.name,detail:x.allDay?"Todo el día":new Intl.DateTimeFormat("es-CO",{hour:"numeric",minute:"2-digit"}).format(new Date(x.startsAt)),tone:"green"})).map(event => <article key={`${event.day}-${event.title}`}><time><strong>{event.day}</strong><small>{event.month}</small></time><span><small className={`tone-${event.tone}`}>{event.category}</small><strong>{event.title}</strong><em>{event.detail}</em></span></article>)}
            {!upcoming.length&&<p className="intranet-agenda-empty">No hay eventos publicados próximamente.</p>}
          </div>
          <div className={`intranet-birthday ${birthdays.length?"has-celebrations":"is-empty"}`} key={celebrationReplay}>
            {birthdays.length>0&&<div aria-hidden="true" className="intranet-birthday-confetti">{Array.from({length:18},(_,index)=><i key={index}/>)}</div>}
            <span className="intranet-birthday-avatar"><Cake size={19} /></span>
            <div className="intranet-birthday-content">
              <header><small>Cumpleaños del equipo</small>{birthdays.length>0&&<button aria-label="Repetir animación de confeti" onClick={()=>setCelebrationReplay(value=>value+1)} title="Repetir confeti" type="button"><RotateCcw size={13}/> Repetir</button>}</header>
              {birthdaysLoading?<><strong>Preparando las celebraciones de hoy…</strong><em>Un momento mientras revisamos a quiénes acompañamos en su día.</em></>:birthdays.length?<>
                <strong>{birthdays.length===1?"¡Hoy tenemos un motivo especial para celebrar!":`¡Hoy celebramos la vida de ${birthdays.length} personas de nuestro equipo!`}</strong>
                <p>Desde Gaia les deseamos un feliz cumpleaños, lleno de alegría, bienestar y nuevos motivos para seguir construyendo juntos.</p>
                <div className="intranet-birthday-people">{birthdays.map(item=>{const name=properName(item.fullName);return <article key={item.id}><span>{initials(name)}</span><b>{name}</b><i>¡Feliz cumpleaños!</i></article>;})}</div>
              </>:<><strong>Hoy no tenemos cumpleaños en el equipo</strong><em>Cuando llegue una nueva celebración, este espacio nos ayudará a acompañarla juntos.</em></>}
              {!birthdaysLoading&&nextBirthdayGroups.length>0&&<div className="intranet-next-birthdays">
                <div className="intranet-next-birthdays-heading"><span><PartyPopper size={14}/><b>Próximos cumpleaños</b></span>{nextBirthdayGroups.length>1&&<span><button aria-label="Fecha anterior" onClick={()=>setBirthdaySlide(current=>(current-1+nextBirthdayGroups.length)%nextBirthdayGroups.length)} type="button"><ChevronLeft size={14}/></button><button aria-label="Fecha siguiente" onClick={()=>setBirthdaySlide(current=>(current+1)%nextBirthdayGroups.length)} type="button"><ChevronRight size={14}/></button></span>}</div>
                {nextBirthdayGroups[birthdaySlide%nextBirthdayGroups.length]&&(()=>{const group=nextBirthdayGroups[birthdaySlide%nextBirthdayGroups.length];return <article className="intranet-next-birthday-group"><header><strong>{birthdayDateLabel(group.people[0])}</strong><i>{birthdaySlide+1} / {nextBirthdayGroups.length}</i></header><div>{group.people.map(item=>{const name=properName(item.fullName);return <span key={item.id}><b>{initials(name)}</b><em>{name}</em></span>;})}</div></article>;})()}
              </div>}
            </div>
          </div>
        </aside>
      </section>

      <section className="intranet-information-strip">
        <Grid2X2 aria-hidden="true" size={21} />
        <span><strong>Todo Gaia en un solo lugar</strong><small>Las opciones y aplicaciones visibles dependerán de tus permisos institucionales.</small></span>
        <Link href="/intranet/aplicaciones">Explorar aplicaciones <ArrowRight size={14} /></Link>
      </section>

    </div>
  );
}

function properName(value:string){return value.toLocaleLowerCase("es").replace(/(^|\s)\p{L}/gu,letter=>letter.toLocaleUpperCase("es"));}
function initials(value:string){return value.split(/\s+/).filter(Boolean).slice(0,2).map(word=>word[0]).join("").toLocaleUpperCase("es");}
function nextBirthdayDate(item:Birthday,today:Date){const date=new Date(today.getFullYear(),item.month-1,item.day);if(date<new Date(today.getFullYear(),today.getMonth(),today.getDate()))date.setFullYear(date.getFullYear()+1);return date;}
function birthdayDateLabel(item:Birthday){return new Intl.DateTimeFormat("es-CO",{day:"numeric",month:"long"}).format(new Date(2024,item.month-1,item.day));}
function groupBirthdaysByDate(items:Birthday[]){const groups=new Map<string,Birthday[]>();for(const item of items){const key=`${item.month}-${item.day}`,current=groups.get(key)??[];current.push(item);groups.set(key,current);}return Array.from(groups,([key,people])=>({key,people}));}
