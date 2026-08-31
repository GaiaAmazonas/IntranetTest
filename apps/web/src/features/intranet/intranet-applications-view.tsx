"use client";

import Link from "next/link";
import Image from "next/image";
import { ArrowRight, Grid2X2, Search, ShieldCheck } from "lucide-react";
import { useMemo, useState } from "react";
import { useSecurity } from "@/components/security-context";
import { applicationsFromModules, authorizedApplications, filterApplications, intranetApplications } from "./intranet-applications";

export function IntranetApplicationsView() {
  const { can, loading, modules } = useSecurity(); const [search, setSearch] = useState(""); const [category, setCategory] = useState("Todas");
  const allowed = useMemo(() => {
    const configured = applicationsFromModules(modules);
    const fixed = authorizedApplications(intranetApplications, can);
    const configuredCodes = new Set(configured.map(item => item.code));
    return [...configured, ...fixed.filter(item => !configuredCodes.has(item.code))];
  }, [can, modules]);
  const categories = useMemo(() => ["Todas", ...new Set(allowed.map(application => application.category))], [allowed]);
  const visible = useMemo(() => filterApplications(allowed, search, category), [allowed, search, category]);
  return <section className="intranet-applications-page">
    <header className="intranet-applications-heading intranet-section-hero intranet-section-hero-applications"><div><p>Ecosistema digital</p><h1>Aplicaciones</h1><span>Tus herramientas institucionales, organizadas y disponibles desde un solo lugar.</span></div><label><Search aria-hidden="true" size={18}/><input aria-label="Buscar aplicaciones" onChange={event => setSearch(event.target.value)} placeholder="Buscar una herramienta" value={search}/></label></header>
    <div aria-label="Categorías de aplicaciones" className="intranet-application-categories" role="group">{categories.map(item => <button aria-pressed={category === item} className={category === item ? "is-active" : undefined} key={item} onClick={() => setCategory(item)} type="button">{item}</button>)}</div>
    {loading && <div className="intranet-data-state"><strong>Cargando aplicaciones autorizadas…</strong></div>}
    {!loading && visible.length === 0 && <div className="intranet-data-state"><Grid2X2 size={24}/><strong>{allowed.length ? "No encontramos aplicaciones" : "No tienes aplicaciones asignadas"}</strong><p>{allowed.length ? "Prueba con otro término o categoría." : "Cuando se te otorgue acceso, las herramientas aparecerán aquí automáticamente."}</p></div>}
    {!loading && visible.length > 0 && <div className="intranet-application-grid">{visible.map(application => <article key={application.code}><div aria-hidden="true" className={`intranet-application-icon tone-${application.tone}${application.logoUrl?" is-branded":""}`}>{application.logoUrl?<Image alt="" height={28} src={application.logoUrl} width={28}/>:application.initials}</div><div className="intranet-application-copy"><small>{application.category}</small><h2>{application.name}</h2><p>{application.description}</p></div><footer><span><ShieldCheck size={14}/>Acceso autorizado</span><Link href={application.href} rel={application.external || application.code.startsWith("INT.APP.") ? "noopener noreferrer" : undefined} target={application.external || application.code.startsWith("INT.APP.") ? "_blank" : undefined}>Abrir <ArrowRight size={15}/></Link></footer></article>)}</div>}
    <aside className="intranet-application-guidance"><ShieldCheck size={19}/><span><strong>Catálogo personalizado</strong><small>La visibilidad y el acceso directo dependen de los permisos vigentes de tu cuenta. Las aplicaciones sin autorización no se muestran.</small></span></aside>
  </section>;
}
