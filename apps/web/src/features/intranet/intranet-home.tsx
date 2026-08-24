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
import { useEffect, useState } from "react";
import { useSecurity } from "@/components/security-context";
import { intranetHomePreview } from "./intranet-home.preview";

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
  const firstName = user?.name.split(" ").filter(Boolean)[0] ?? "equipo Gaia";
  const formattedDate = new Intl.DateTimeFormat("es-CO", {
    weekday: "long",
    day: "numeric",
    month: "long",
  }).format(new Date());
  const event = intranetHomePreview.spotlight[activeEvent];

  useEffect(() => {
    const timer = window.setInterval(() => setActiveEvent(current => (current + 1) % intranetHomePreview.spotlight.length), 7000);
    return () => window.clearInterval(timer);
  }, []);

  return (
    <div className="intranet-home">
      <section className="intranet-personal-welcome">
        <p>{formattedDate}</p>
        <div><h1>Hola, {firstName}.</h1><h2>Esto es lo próximo en Gaia.</h2><span>Encuentros, novedades y momentos importantes para seguir conectados.</span></div>
      </section>

      <section aria-label="Eventos destacados" className="intranet-event-spotlight">
        <Image alt="Actividad institucional de Gaia en la Amazonía" fill key={event.image} priority sizes="(max-width: 760px) 100vw, 1380px" src={event.image} />
        <div className="intranet-event-shade" />
        <div className="intranet-event-copy">
          <p>{event.eyebrow}</p>
          <h2>{event.title}</h2>
          <span>{event.summary}</span>
          <div><strong>{event.detail}</strong><Link href="/intranet/calendario">Ver en calendario <ArrowRight size={15} /></Link></div>
        </div>
        <div className="intranet-event-controls">
          <button aria-label="Evento anterior" onClick={() => setActiveEvent(current => (current - 1 + intranetHomePreview.spotlight.length) % intranetHomePreview.spotlight.length)} type="button"><ChevronLeft size={18} /></button>
          <div aria-label={`Diapositiva ${activeEvent + 1} de ${intranetHomePreview.spotlight.length}`} className="intranet-event-pagination">{intranetHomePreview.spotlight.map((slide, index) => <button aria-current={index === activeEvent ? "true" : undefined} aria-label={`Ver evento ${index + 1}: ${slide.title}`} key={slide.title} onClick={() => setActiveEvent(index)} type="button" />)}</div>
          <button aria-label="Evento siguiente" onClick={() => setActiveEvent(current => (current + 1) % intranetHomePreview.spotlight.length)} type="button"><ChevronRight size={18} /></button>
        </div>
      </section>

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
            {intranetHomePreview.agenda.map(event => <article key={`${event.day}-${event.title}`}><time><strong>{event.day}</strong><small>{event.month}</small></time><span><small className={`tone-${event.tone}`}>{event.category}</small><strong>{event.title}</strong><em>{event.detail}</em></span></article>)}
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
