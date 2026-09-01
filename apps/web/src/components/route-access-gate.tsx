"use client";

import Image from "next/image";
import Link from "@/components/document-link";
import { ArrowRight, Building2, LockKeyhole, LogIn, RotateCcw, ShieldCheck } from "lucide-react";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { useSecurity } from "./security-context";
import { loginTransitionKey, startLogin } from "@/lib/api-client";
import { routeRuleFor } from "@/lib/route-access";

const loginTransitionMinimumMs = 1500;

export function RouteAccessGate({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const security = useSecurity();
  const rule = routeRuleFor(pathname);
  const [finishingLogin, setFinishingLogin] = useState(() => {
    if (typeof window === "undefined") return false;
    return window.sessionStorage.getItem(loginTransitionKey) !== null;
  });

  useEffect(() => {
    if (!finishingLogin) return;
    if (security.loading) return;
    const timer = window.setTimeout(() => {
      window.sessionStorage.removeItem(loginTransitionKey);
      setFinishingLogin(false);
    }, security.user ? loginTransitionMinimumMs : 0);
    return () => window.clearTimeout(timer);
  }, [finishingLogin, security.loading, security.user]);

  if (!rule) return children;
  if (security.loading || finishingLogin) return <AccessState icon="loading" title={security.user ? "Abriendo la Intranet Gaia…" : "Verificando tu acceso…"} />;
  if (!security.user) {
    return <AccessState action="Iniciar sesión" description="Ingresa con tu cuenta institucional para continuar." icon="login" onAction={() => startLogin(window.location.href)} title="" />;
  }

  const allowed = rule.requirements.every(requirement => security.can(requirement));
  if (!allowed) {
    const recoveryHref = pathname === "/intranet" && security.can("TI.MODULOS.ADMINISTRAR") ? "/seguridad/modulos" : "/intranet";
    const recoveryAction = recoveryHref === "/seguridad/modulos" ? "Revisar configuración de Seguridad" : "Volver a la Intranet";
    return <AccessState action={recoveryAction} description="Tu cuenta está autenticada, pero no tiene el permiso requerido para esta opción." href={recoveryHref} title="Acceso no autorizado" />;
  }

  return children;
}

const gaiaSocialNetworks = [
  { label: "Página web", mark: "G", href: "https://gaiaamazonas.org/" },
  { label: "X", mark: "X", href: "https://x.com/gaiaamazonas" },
  { label: "Facebook", mark: "f", href: "https://www.facebook.com/gaiaamazonas/" },
  { label: "Instagram", mark: "◎", href: "https://www.instagram.com/gaiaamazonas/" },
  { label: "YouTube", mark: "▶", href: "https://www.youtube.com/user/gaiaamazonas" },
  { label: "Vimeo", mark: "v", href: "https://vimeo.com/gaiaamazonas" },
  { label: "Spotify", mark: "≋", href: "https://open.spotify.com/show/37hXfsGxzUDK0PZFnO0Rm3?si=a86be126facb4562&nd=1&dlsi=299e7b66ffae45c8" },
  { label: "TikTok", mark: "♪", href: "https://www.tiktok.com/@gaiaamazonas" },
] as const;

export function AccessState({ action, description, href, icon, notice, onAction, title }: { action?: string; description?: string; href?: string; icon?: "login" | "loading"; notice?: string; onAction?: () => void; title: string }) {
  const Icon = icon === "login" ? LogIn : LockKeyhole;
  if (icon === "loading") {
    return <main className="gaia-access-loading"><div className="gaia-access-loading-brand"><Image alt="Gaia Amazonas" height={54} priority src="/brand/logo-gaia.svg" width={98} /><span>ECOSISTEMA DIGITAL GAIA</span></div><div className="gaia-access-loading-orbit"><i /><i /><i /><span><ShieldCheck size={26} /></span></div><h1>{title}</h1><p>Estamos preparando tu espacio institucional.</p><div className="gaia-access-loading-progress"><span /></div></main>;
  }
  if (icon === "login") {
    return (
      <main className="gaia-access-portal">
        <section className="gaia-access-story">
          <Image alt="Paisaje de la Amazonía colombiana junto a un río" fill priority sizes="(max-width: 820px) 100vw, 62vw" src="/brand/intranet/evento-amazonia-gaia2.jpg" />
          <div className="gaia-access-story-shade" />
          <header><Image alt="Gaia Amazonas" height={48} src="/brand/logo-gaia.svg" width={88} /><span><strong>Fundación Gaia Amazonas</strong><small>Ecosistema digital institucional</small></span></header>
          <div className="gaia-access-story-copy">
            <p>Territorio · conocimiento · futuro</p>
            <h1>Todo Gaia,<br />en un solo lugar</h1>
            <span>Información, personas y herramientas conectadas para acompañar nuestro trabajo en la Amazonía.</span>
          </div>
          <div className="gaia-access-capabilities">
            <span><Building2 size={17} /><strong>Intranet</strong><small>Información que nos conecta</small></span>
            <nav aria-label="Redes sociales de Gaia Amazonas" className="gaia-access-socials gaia-access-socials-story">
              {gaiaSocialNetworks.map(network => <a aria-label={`Gaia Amazonas en ${network.label}`} href={network.href} key={network.label} rel="noopener noreferrer" target="_blank"><i aria-hidden="true">{network.mark}</i><span>{network.label}</span></a>)}
            </nav>
          </div>
        </section>
        <section className="gaia-access-entry">
          <div className="gaia-access-entry-card">
            <button aria-label={action ?? "Iniciar sesión"} className="gaia-access-entry-icon" onClick={onAction} type="button"><LogIn size={23} /></button>
            {title && <p>{title}</p>}
            <h2>Tu espacio institucional te espera.</h2>
            <span>{description}</span>
            {notice && <div className="gaia-access-notice" role="status"><ShieldCheck size={16} />{notice}</div>}
            {action && <button className="gaia-access-primary-action" onClick={onAction} type="button"><i aria-hidden="true"><b /><b /><b /><b /></i>{action}<ArrowRight size={17} /></button>}
            <div className="gaia-access-security"><ShieldCheck size={17} /><span><strong>Acceso protegido por Microsoft</strong><small>Utiliza tu cuenta institucional autorizada.</small></span></div>
            <nav aria-label="Redes sociales de Gaia Amazonas" className="gaia-access-socials gaia-access-socials-mobile">
              {gaiaSocialNetworks.map(network => <a aria-label={`Gaia Amazonas en ${network.label}`} href={network.href} key={network.label} rel="noopener noreferrer" target="_blank"><i aria-hidden="true">{network.mark}</i><span>{network.label}</span></a>)}
            </nav>
          </div>
          <footer><span>GAIA ENTERPRISE PLATFORM</span><small>Una plataforma creada para evolucionar con Gaia.</small></footer>
        </section>
      </main>
    );
  }
  return <main className="gaia-route-state"><span><Icon aria-hidden="true" size={25} /></span><h1>{title}</h1>{description && <p>{description}</p>}{href && action ? <Link href={href}>{action}</Link> : action ? <button onClick={onAction} type="button">{action}</button> : <RotateCcw aria-hidden="true" className="gaia-spin" size={18} />}</main>;
}
