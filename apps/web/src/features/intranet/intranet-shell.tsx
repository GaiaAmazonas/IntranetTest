"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  AppWindow,
  ChevronDown,
  ExternalLink,
  LogOut,
  Menu,
  UserRound,
  X,
} from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { startLogin } from "@/lib/api-client";
import { AccessState } from "@/components/route-access-gate";
import { useSecurity } from "@/components/security-context";
import {
  intranetNavigation,
  isIntranetRouteActive,
} from "./intranet-navigation";
import { applicationsFromModules } from "./intranet-applications";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";
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

  const initials = security.user.name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map(part => part[0])
    .join("")
    .toUpperCase();

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
          {intranetNavigation.filter(item => security.can(item.permission)).map(item => {
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
        </nav>

        <div className="intranet-profile" ref={profileRef}>
          <button
            aria-expanded={profileOpen}
            className="intranet-profile-trigger"
            onClick={() => setProfileOpen(value => !value)}
            type="button"
          >
            <span className="intranet-avatar">{initials || "GA"}</span>
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
            </nav>
            <div className="intranet-mobile-user"><span className="intranet-avatar">{initials || "GA"}</span><span><strong>{security.user.name}</strong><small>{security.user.email}</small></span></div>
          </aside>
        </>
      )}

      <main className="intranet-main">{children}</main>
      <footer className="intranet-footer">
        <strong>Gaia Amazonas · Intranet institucional</strong>
        <nav aria-label="Enlaces de la Intranet">{intranetNavigation.filter(item=>security.can(item.permission)).map(item=><Link href={item.href} key={item.href}>{item.label}</Link>)}</nav>
        <nav aria-label="Redes sociales de Gaia Amazonas" className="intranet-footer-social">{gaiaSocialNetworks.map(network=><a aria-label={network.label} href={network.href} key={network.label} rel="noopener noreferrer" target="_blank"><i aria-hidden="true">{network.mark}</i><span>{network.label}</span></a>)}</nav>
        <span><a href="https://gaiaamazonas.org/politica-de-datos/" rel="noopener noreferrer" target="_blank">Política de tratamiento de datos</a><Link href="/intranet/helpdesk">Ayuda técnica</Link><small>© Fundación Gaia Amazonas</small></span>
      </footer>
    </div>
  );
}
