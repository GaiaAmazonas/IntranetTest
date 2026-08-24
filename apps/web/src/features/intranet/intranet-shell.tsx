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
  ShieldCheck,
  UserRound,
  X,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { startLogin } from "@/lib/api-client";
import { useSecurity } from "@/components/security-context";
import {
  intranetNavigation,
  isIntranetRouteActive,
} from "./intranet-navigation";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";

export function IntranetShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const security = useSecurity();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const [loggingOut, setLoggingOut] = useState(false);
  const profileRef = useRef<HTMLDivElement>(null);

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
    return <IntranetStatus title="Preparando tu espacio Gaia…" />;
  }

  if (!security.user) {
    return (
      <IntranetStatus
        action="Iniciar sesión"
        description={security.error ?? "Tu sesión institucional no está disponible."}
        onAction={() => startLogin(`${location.origin}/intranet`)}
        title="Necesitas iniciar sesión"
      />
    );
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

        {security.can("INT.APP.ADMINCORE.VER") && (
          <Link className="intranet-product-switch" href="/admincore" rel="noopener noreferrer" target="_blank">
            <AppWindow aria-hidden="true" size={17} />
            <span><small>Plataforma empresarial</small><strong>AdminCore</strong></span>
            <ExternalLink aria-hidden="true" size={13} />
          </Link>
        )}

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
      <footer className="intranet-footer"><span>Fundación Gaia Amazonas</span><span>Espacio institucional protegido</span></footer>
    </div>
  );
}

function IntranetStatus({
  action,
  description,
  onAction,
  title,
}: {
  action?: string;
  description?: string;
  onAction?: () => void;
  title: string;
}) {
  return (
    <main className="intranet-status">
      <span><ShieldCheck size={26} /></span>
      <h1>{title}</h1>
      {description && <p>{description}</p>}
      {action && <button onClick={onAction} type="button">{action}</button>}
    </main>
  );
}
