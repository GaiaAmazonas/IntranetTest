"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname } from "next/navigation";
import { Building2, ChevronDown, ChevronLeft, ChevronRight, Home, LoaderCircle, LogOut, Menu, PackageSearch, Palette, PanelLeftClose, PanelLeftOpen, Settings, UserRound, Users, X } from "lucide-react";
import { useEffect, useRef, useState, type MouseEvent as ReactMouseEvent } from "react";
import { Avatar, IconButton } from "./ui";
import { ConfirmDialog } from "./form-dialog";
import { startLogin } from "@/lib/api-client";
import { useSecurity } from "./security-context";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";
type User = { displayName: string; email: string };
type AccentTheme = "forest" | "teal" | "purple" | "red";
const accentThemes: { value: AccentTheme; label: string; color: string }[] = [
  { value: "forest", label: "Verde Gaia", color: "#214d38" },
  { value: "teal", label: "Azul territorio", color: "#286b78" },
  { value: "purple", label: "Púrpura Gaia", color: "#6f3873" },
  { value: "red", label: "Rojo Gaia", color: "#923449" },
];
const navigation = [
  { href: "/intranet", label: "Volver a la intranet", icon: ChevronLeft, exact: true, permission: "INTRANET.VER" },
  { href: "/admincore", label: "Inicio", icon: Home, exact: true, permission: "INICIO.VER" },
  { href: "/organizacion", label: "Organización", icon: Building2, permission: "ORG.UNIDADES.VER" },
  { label: "Talento Humano", icon: Users, permission: "TH.COLABORADORES.VER|TH.VINCULACIONES.VER", children: [{ href: "/talento-humano/colaboradores", aliases: ["/terceros"], label: "Colaboradores", permission: "TH.COLABORADORES.VER" },{ href: "/talento-humano/vinculaciones", aliases: [], label: "Vinculaciones", permission: "TH.VINCULACIONES.VER" }] },
  { href: "/inventario", label: "Inventario", icon: PackageSearch, permission: "INV.VER" },
  { label: "Seguridad", icon: Settings, permission: "TI.USUARIOS.VER|TI.ROLES.VER|TI.MODULOS.VER", children: [
    { href: "/seguridad/usuarios", aliases: [], label: "Usuarios", permission: "TI.USUARIOS.VER" },
    { href: "/seguridad/roles", aliases: [], label: "Roles y permisos", permission: "TI.ROLES.VER" },
    { href: "/seguridad/modulos", aliases: [], label: "Módulos", permission: "TI.MODULOS.VER" },
  ] },
];

export function AppShell({ title, user: suppliedUser }: { title: string; user?: User }) {
  const security = useSecurity();
  const pathname = usePathname(); const [user, setUser] = useState<User | null>(suppliedUser ?? null);
  const [collapsed, setCollapsed] = useState(false); const [mobileOpen, setMobileOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false); const [loggingOut, setLoggingOut] = useState(false);
  const [reauthRequired, setReauthRequired] = useState(false); const [expanded, setExpanded] = useState<string[]>([]);
  const [navigatingTo, setNavigatingTo] = useState<string | null>(null);
  const [accentTheme, setAccentTheme] = useState<AccentTheme>("forest");
  const accountRef = useRef<HTMLDivElement>(null);
  useEffect(() => { const frame = requestAnimationFrame(() => setCollapsed(localStorage.getItem("gaia-sidebar-collapsed") === "true")); return () => cancelAnimationFrame(frame); }, []);
  useEffect(() => { const frame = requestAnimationFrame(() => { const saved = localStorage.getItem("gaia-accent-theme"); if (accentThemes.some(theme => theme.value === saved)) setAccentTheme(saved as AccentTheme); }); return () => cancelAnimationFrame(frame); }, []);
  useEffect(() => { document.documentElement.dataset.gaiaSidebar = collapsed ? "collapsed" : "expanded"; localStorage.setItem("gaia-sidebar-collapsed", String(collapsed)); return () => { delete document.documentElement.dataset.gaiaSidebar; }; }, [collapsed]);
  useEffect(() => { if (suppliedUser) return; void fetch(`${apiUrl}/api/auth/me`, { credentials: "include" }).then(async response => { if (response.status === 401) { location.href = "/"; return; } if (response.ok) setUser(await response.json() as User); }); }, [suppliedUser]);
  useEffect(() => { const close = (event: MouseEvent) => { if (!accountRef.current?.contains(event.target as Node)) setAccountOpen(false); }; document.addEventListener("mousedown", close); return () => document.removeEventListener("mousedown", close); }, []);
  useEffect(() => { const show = () => setReauthRequired(true); addEventListener("gaia:reauth-required", show); return () => removeEventListener("gaia:reauth-required", show); }, []);
  useEffect(() => { const original = window.fetch.bind(window); window.fetch = async (...args) => { const response = await original(...args); if (response.status === 401) { const problem = await response.clone().json().catch(() => null) as { code?: string } | null; if (problem?.code === "reauth_required") dispatchEvent(new CustomEvent("gaia:reauth-required")); } return response; }; return () => { window.fetch = original; }; }, []);
  const isActive = (href: string, exact?: boolean) => exact ? pathname === href : pathname.startsWith(href);
  const pendingNavigation = navigatingTo === pathname ? null : navigatingTo;
  const displayedUser = security.user ? { displayName: security.user.name, email: security.user.email } : user;
  function logout() { setLoggingOut(true); location.href = `${apiUrl}/api/auth/logout?returnUrl=${encodeURIComponent(`${location.origin}/?logout=success`)}`; }
  function selectAccent(theme: AccentTheme) { setAccentTheme(theme); document.documentElement.dataset.gaiaAccent = theme; localStorage.setItem("gaia-accent-theme", theme); }
  function beginNavigation(href: string, event: ReactMouseEvent<HTMLAnchorElement>) { if (navigatingTo === href) { event.preventDefault(); return; } setNavigatingTo(href); setMobileOpen(false); }

  return <><div aria-hidden={!pendingNavigation} className={`gaia-navigation-progress ${pendingNavigation ? "is-active" : ""}`} role="progressbar" />{mobileOpen && <button aria-label="Cerrar navegación" className="gaia-sidebar-backdrop" onClick={() => setMobileOpen(false)} type="button" />}
    <aside aria-label="Navegación principal" className={`gaia-sidebar ${collapsed ? "is-collapsed" : ""} ${mobileOpen ? "is-mobile-open" : ""}`}>
      <div className="gaia-brand"><span className="gaia-brand-mark"><Image alt="Gaia Amazonas" height={41} priority src="/brand/logo-gaia.svg" width={75} /></span>{!collapsed && <div className="min-w-0"><p className="gaia-brand-name">Fundación Gaia Amazonas</p><p className="gaia-brand-caption">Plataforma empresarial</p></div>}<IconButton className="gaia-mobile-close" label="Cerrar navegación" onClick={() => setMobileOpen(false)}><X size={19} /></IconButton></div>
      <nav className="gaia-navigation">{!collapsed && <p className="gaia-navigation-label">Espacio de trabajo</p>}{navigation.filter(item => security.loading || security.can(item.permission)).map(item => {
        const Icon = item.icon; const children = "children" in item ? item.children : undefined;
        const childActive = children?.some(child => pathname.startsWith(child.href) || child.aliases?.some(alias => pathname.startsWith(alias))); const isExpanded = expanded.includes(item.label) || childActive;
        if (!children) return <Link aria-busy={pendingNavigation === item.href} aria-current={isActive(item.href!, item.exact) ? "page" : undefined} className={`gaia-nav-item ${isActive(item.href!, item.exact) ? "is-active" : ""}`} href={item.href!} key={item.label} onClick={event => beginNavigation(item.href!, event)} title={collapsed ? item.label : undefined}><Icon size={20} strokeWidth={1.8} />{!collapsed && <span>{item.label}</span>}{pendingNavigation === item.href && <LoaderCircle className="gaia-spin gaia-nav-loading" size={15} />}</Link>;
        return <div className="gaia-nav-group" key={item.label}><button aria-expanded={isExpanded} className={`gaia-nav-item gaia-nav-parent ${childActive ? "has-active-child" : ""}`} onClick={() => setExpanded(current => current.includes(item.label) ? current.filter(label => label !== item.label) : [...current, item.label])} title={collapsed ? item.label : undefined} type="button"><Icon size={20} strokeWidth={1.8} />{!collapsed && <><span>{item.label}</span><ChevronDown className="gaia-nav-chevron" size={15} /></>}</button>{isExpanded && !collapsed && <div className="gaia-subnavigation">{children.filter(child => security.can(child.permission)).map(child => { const active = pathname.startsWith(child.href) || child.aliases?.some(alias => pathname.startsWith(alias)); return <Link aria-busy={pendingNavigation === child.href} aria-current={active ? "page" : undefined} className={`gaia-subnav-item ${active ? "is-active" : ""}`} href={child.href} key={child.href} onClick={event => beginNavigation(child.href, event)}>{child.label}{pendingNavigation === child.href && <LoaderCircle className="gaia-spin gaia-nav-loading" size={13} />}</Link>; })}</div>}</div>;
      })}</nav>
      <div className="gaia-sidebar-footer" ref={accountRef}>{accountOpen && <div className={`gaia-account-menu ${collapsed ? "is-collapsed" : ""}`} role="menu"><div className="gaia-account-summary"><UserRound size={17} /><div><strong>{displayedUser?.displayName ?? "Usuario Gaia"}</strong><span>{displayedUser?.email}</span></div></div><div className="gaia-theme-selector"><div><Palette size={16} /><span>Color de la plataforma</span></div><div aria-label="Color de la plataforma" className="gaia-theme-options" role="radiogroup">{accentThemes.map(theme => <button aria-checked={accentTheme === theme.value} aria-label={theme.label} className={accentTheme === theme.value ? "is-selected" : ""} key={theme.value} onClick={() => selectAccent(theme.value)} role="radio" title={theme.label} type="button"><span style={{ backgroundColor: theme.color }} /><small>{theme.label}</small></button>)}</div></div><button className="gaia-account-action" disabled={loggingOut} onClick={logout} role="menuitem" type="button">{loggingOut ? <LoaderCircle className="gaia-spin" size={17} /> : <LogOut size={17} />}{loggingOut ? "Cerrando sesión..." : "Cerrar sesión"}</button></div>}
        <button aria-expanded={accountOpen} className="gaia-user-trigger" onClick={() => setAccountOpen(value => !value)} title={collapsed ? displayedUser?.displayName : undefined} type="button"><Avatar name={displayedUser?.displayName ?? "Usuario Gaia"} />{!collapsed && <><span className="min-w-0 flex-1 text-left"><strong>{displayedUser?.displayName ?? "Usuario Gaia"}</strong><small>{displayedUser?.email ?? "Cuenta institucional"}</small></span><ChevronRight size={17} /></>}</button>
        <button className="gaia-collapse-button" onClick={() => setCollapsed(value => !value)} title={collapsed ? "Expandir navegación" : "Contraer navegación"} type="button">{collapsed ? <PanelLeftOpen size={18} /> : <PanelLeftClose size={18} />}{!collapsed && <span>Contraer</span>}</button></div>
    </aside>
    <header className="gaia-topbar"><IconButton className="gaia-menu-button" label="Abrir navegación" onClick={() => setMobileOpen(true)}><Menu size={21} /></IconButton><h1>{title}</h1><div className="gaia-topbar-status"><span aria-hidden="true" /><span>Entorno institucional</span></div></header>
    <ConfirmDialog confirmLabel="Volver a iniciar sesión" description="Por seguridad, vuelve a iniciar sesión para continuar." onCancel={() => setReauthRequired(false)} onConfirm={() => startLogin(location.href)} open={reauthRequired} title="Tu sesión necesita renovarse" />
  </>;
}
