"use client";

import Image from "next/image";
import Link from "@/components/document-link";
import { usePathname } from "next/navigation";
import {
  AppWindow,
  ChevronDown,
  ChevronRight,
  ExternalLink,
  LogOut,
  Menu,
  UserRound,
  X,
  LayoutDashboard,
} from "lucide-react";
import { useEffect, useId, useMemo, useRef, useState } from "react";
import { startLogin } from "@/lib/api-client";
import { AccessState } from "@/components/route-access-gate";
import { useSecurity } from "@/components/security-context";
import { PersonAvatar } from "@/components/person-avatar";
import {
  intranetNavigation,
  isIntranetRouteActive,
} from "./intranet-navigation";
import { applicationsFromModules } from "./intranet-applications";
import { IntranetFooter } from "./intranet-footer";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";
export function IntranetShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const security = useSecurity();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);
  const profileRef = useRef<HTMLDivElement>(null);
  const configuredApplications = useMemo(() => applicationsFromModules(security.modules), [security.modules]);

  useEffect(() => {
    const close = (event: MouseEvent) => {
      if (!profileRef.current?.contains(event.target as Node)) setProfileOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);

  useEffect(() => {
    if (!mobileOpen) return;
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setMobileOpen(false);
    };
    document.addEventListener("keydown", closeOnEscape);
    return () => document.removeEventListener("keydown", closeOnEscape);
  }, [mobileOpen]);

  function logout() {
    setLoggingOut(true);
    location.href = `${apiUrl}/api/auth/logout?returnUrl=${encodeURIComponent(
      `${location.origin}/?logout=success`,
    )}`;
  }

  if (security.loading) {
    return <AccessState icon="loading" title="Preparando tu espacio Gaia…" />;
  }

  if (!security.user) {
    return <AccessState
      action="Iniciar sesión"
      description="Ingresa con tu cuenta institucional para continuar."
      icon="login"
      onAction={() => startLogin(`${location.origin}/intranet`)}
      title=""
    />;
  }

  return (
    <div className="intranet-frame">
      <header className="intranet-header">
        <Link className="intranet-brand" href="/intranet">
          <Image alt="Gaia Amazonas" height={42} priority src="/brand/logo-gaia.svg" width={76} />
          <span>
            <strong>Intranet Gaia</strong>
            <small>Fundación Gaia Amazonas</small>
          </span>
        </Link>

        <nav aria-label="Navegación de la intranet" className="intranet-desktop-nav">
          {intranetNavigation.filter(item => !item.group && security.can(item.permission)).map(item => {
            const Icon = item.icon;
            const active = isIntranetRouteActive(pathname, item);
            return (
              <Link
                aria-current={active ? "page" : undefined}
                className={active ? "is-active" : undefined}
                href={item.href}
                key={item.href}
              >
                <Icon aria-hidden="true" size={17} strokeWidth={1.8} />
                {item.label}
              </Link>
            );
          })}
          <ServiceNavigation pathname={pathname} can={security.can}/>
        </nav>

        <div className="intranet-profile" ref={profileRef}>
          <button
            aria-expanded={profileOpen}
            className="intranet-profile-trigger"
            onClick={() => setProfileOpen(value => !value)}
            type="button"
          >
            <PersonAvatar className="intranet-avatar" currentUser name={security.user.name} size={34} />
            <span className="intranet-profile-name">{security.user.name}</span>
            <ChevronDown aria-hidden="true" size={15} />
          </button>
          {profileOpen && (
            <div className="intranet-profile-menu" role="menu">
              <div>
                <UserRound aria-hidden="true" size={17} />
                <span><strong>{security.user.name}</strong><small>{security.user.email}</small></span>
              </div>
              <Link href="/intranet/perfil" role="menuitem"><UserRound size={16} /> Mi perfil</Link>
              {configuredApplications.map(application => (
                <Link href={application.href} key={application.code} onClick={() => setProfileOpen(false)} rel="noopener noreferrer" role="menuitem" target="_blank">
                  <AppWindow aria-hidden="true" size={16} />
                  <span>{application.name}</span>
                  <ExternalLink aria-hidden="true" className="intranet-profile-app-external" size={12} />
                </Link>
              ))}
              <button disabled={loggingOut} onClick={logout} role="menuitem" type="button">
                <LogOut size={16} /> {loggingOut ? "Cerrando sesión…" : "Cerrar sesión"}
              </button>
            </div>
          )}
        </div>

        <button
          aria-expanded={mobileOpen}
          aria-label="Abrir navegación"
          className="intranet-menu-button"
          onClick={() => setMobileOpen(true)}
          type="button"
        >
          <Menu size={22} />
        </button>
      </header>

      {mobileOpen && (
        <>
          <button aria-label="Cerrar navegación" className="intranet-nav-backdrop" onClick={() => setMobileOpen(false)} type="button" />
          <aside aria-label="Navegación móvil" className="intranet-mobile-nav">
            <div className="intranet-mobile-head">
              <span><strong>Intranet Gaia</strong><small>Espacio del colaborador</small></span>
              <button aria-label="Cerrar navegación" onClick={() => setMobileOpen(false)} type="button"><X size={21} /></button>
            </div>
            <nav>
              {intranetNavigation.filter(item => security.can(item.permission)).map(item => {
                const Icon = item.icon;
                const active = isIntranetRouteActive(pathname, item);
                return <Link aria-current={active ? "page" : undefined} className={active ? "is-active" : undefined} href={item.href} key={item.href} onClick={() => setMobileOpen(false)}><Icon size={19} />{item.label}</Link>;
              })}
              <ServiceNavigation pathname={pathname} can={security.can} onNavigate={()=>setMobileOpen(false)}/>
            </nav>
            <div className="intranet-mobile-user"><PersonAvatar className="intranet-avatar" currentUser name={security.user.name} size={34} /><span><strong>{security.user.name}</strong><small>{security.user.email}</small></span></div>
          </aside>
        </>
      )}

      <main className="intranet-main">{children}</main>
      <IntranetFooter />
    </div>
  );
}
function ServiceNavigation({pathname,can,onNavigate}:{pathname:string;can:(permission:string)=>boolean;onNavigate?:()=>void}) {
 const [open,setOpen]=useState(false),ref=useRef<HTMLDivElement>(null),trigger=useRef<HTMLButtonElement>(null),panelId=useId();
 const items=intranetNavigation.filter(x=>x.group==="services"&&can(x.permission));
 const descriptions:Record<string,string>={"/intranet/aplicaciones":"Herramientas para tu trabajo diario","/intranet/helpdesk":"Crea solicitudes y consulta tus casos","/intranet/capacitaciones":"Continúa tu aprendizaje y ve tus resultados"};
 useEffect(()=>{const close=(event:MouseEvent)=>{if(!ref.current?.contains(event.target as Node))setOpen(false)};document.addEventListener("mousedown",close);return()=>document.removeEventListener("mousedown",close)},[]);
 if(!items.length)return null;
 const active=items.some(x=>isIntranetRouteActive(pathname,x));
 return <div className="intranet-service-nav" ref={ref} onBlur={e=>{if(!e.currentTarget.contains(e.relatedTarget as Node))setOpen(false)}} onKeyDown={e=>{if(e.key==="Escape"){setOpen(false);trigger.current?.focus()}}}>
   <button ref={trigger} className={active||open?"is-active":undefined} type="button" aria-expanded={open} aria-controls={panelId} onClick={()=>setOpen(!open)}><LayoutDashboard size={19}/><span>Mi espacio</span><ChevronDown size={16} className={open?"intranet-service-chevron is-open":"intranet-service-chevron"}/></button>
   {open&&<div className="intranet-service-links" id={panelId}><header><small>Mi espacio</small><strong>Tus accesos de trabajo</strong><p>Herramientas, apoyo y aprendizaje en un solo lugar.</p></header><nav aria-label="Accesos de Mi espacio">{items.map(x=>{const Icon=x.icon,selected=isIntranetRouteActive(pathname,x);return <Link className={selected?"intranet-service-item is-selected":"intranet-service-item"} key={x.href} href={x.href} aria-current={selected?"page":undefined} onClick={()=>{setOpen(false);onNavigate?.()}}><span className="intranet-service-icon"><Icon size={23}/></span><span className="intranet-service-copy"><strong>{x.label}</strong><small>{descriptions[x.href]}</small></span><ChevronRight size={19}/></Link>})}</nav></div>}
 </div>;
}
