"use client";

import Image from "next/image";
import Link from "next/link";
import {
  ArrowRight,
  BookOpenText,
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  ExternalLink,
  Grid2X2,
  LifeBuoy,
  Search,
  Users,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { useSecurity } from "@/components/security-context";
import { intranetHomePreview } from "./intranet-home.preview";
import { apiRequest } from "@/lib/api-client";
const apiUrl=process.env.NEXT_PUBLIC_GAIA_API_URL??"https://localhost:7168";
type PublicBanner={id:string;eyebrow?:string;title:string;description?:string;destinationType:number;actionUrl?:string;eventId?:string;desktopImageUrl:string};
type PublicEvent={id:string;name:string;type:string;color:string;summary?:string;startsAt:string;allDay:boolean;modality?:number;location?:string};

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
  const [loadedBannerImage,setLoadedBannerImage]=useState<string | null>(null);
  const firstName = user?.name.split(" ").filter(Boolean)[0] ?? "equipo Gaia";
  const formattedDate = new Intl.DateTimeFormat("es-CO", {
    weekday: "long",
    day: "numeric",
    month: "long",
  }).format(new Date());
  const slides=useMemo(()=>banners.map(x=>({eyebrow:x.eyebrow||"Destacado Gaia",title:x.title,summary:x.description||"Información institucional destacada.",detail:"Fundación Gaia Amazonas",image:`${apiUrl}${x.desktopImageUrl}`,href:x.destinationType===1&&x.eventId?`/intranet/calendario?evento=${x.eventId}`:x.destinationType===2&&x.actionUrl?x.actionUrl:"/intranet"})),[banners]);
  const event = slides.length ? slides[activeEvent%slides.length] : null;
  const bannerImageReady=event?.image===loadedBannerImage;

  useEffect(() => {
    if(slides.length<2)return;
    const timer = window.setInterval(() => setActiveEvent(current => (current + 1) % slides.length), 7000);
    return () => window.clearInterval(timer);
  }, [slides.length]);
  useEffect(()=>{void apiRequest<PublicBanner[]>("/api/intranet/banners").then(b=>{setBanners(b);setActiveEvent(0)}).catch(()=>setBanners([])).finally(()=>setBannersLoading(false));},[]);
  useEffect(()=>{const now=new Date(),until=new Date();until.setMonth(until.getMonth()+6);void apiRequest<PublicEvent[]>(`/api/intranet/events?from=${encodeURIComponent(now.toISOString())}&to=${encodeURIComponent(until.toISOString())}`).then(e=>setUpcoming(e.slice(0,5))).catch(()=>{});},[]);
  useEffect(()=>{if(!event)return;const image=new window.Image();image.src=event.image;image.onload=()=>setLoadedBannerImage(event.image);image.onerror=()=>setLoadedBannerImage(event.image);return()=>{image.onload=null;image.onerror=null};},[event]);
  useEffect(()=>{if(slides.length<2)return;const next=slides[(activeEvent+1)%slides.length];const image=new window.Image();image.src=next.image;},[activeEvent,slides]);

  return (
    <div className="intranet-home">
      <section className="intranet-personal-welcome">
        <p>{formattedDate}</p>
        <div><h1>Hola, {firstName}.</h1><h2>Esto es lo próximo en Gaia.</h2><span>Encuentros, novedades y momentos importantes para seguir conectados.</span></div>
      </section>

      {event?<section aria-busy={!bannerImageReady} aria-label="Eventos destacados" className={`intranet-event-spotlight${bannerImageReady?" is-ready":" is-loading"}`}>
        <div aria-label="Actividad institucional de Gaia en la Amazonía" className="intranet-event-background" key={event.image} style={bannerImageReady?{backgroundImage:`url('${event.image}')`}:undefined} />
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
          <div className="intranet-birthday"><span className="intranet-birthday-avatar"><Users size={19} /></span><span><small>Cumpleaños del equipo</small><strong>Celebraciones de hoy</strong><em>Se conectarán respetando la visibilidad de datos.</em></span></div>
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
