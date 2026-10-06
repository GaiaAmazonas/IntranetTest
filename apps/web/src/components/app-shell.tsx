"use client";

import Link from "@/components/document-link";
import Image from "next/image";
import { usePathname } from "next/navigation";
import { Building2, CalendarRange, ChevronDown, ChevronLeft, ChevronRight, GraduationCap, Headphones, Home, LoaderCircle, LogOut, Menu, PackageSearch, Palette, PanelLeftClose, PanelLeftOpen, Settings, UserRound, Users, X } from "lucide-react";
import { useEffect, useMemo, useRef, useState, type ComponentType, type MouseEvent as ReactMouseEvent } from "react";
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
type NavigationChild = { href: string; label: string; aliases?: string[] };
type NavigationItem = { key: string; href?: string; label: string; icon: ComponentType<{ size?: number; strokeWidth?: number; className?: string }>; exact?: boolean; children?: NavigationChild[] };
const normalizedRoute = (value: string) => value.length > 1 ? value.replace(/\/$/, "") : value;
const routeMatches = (pathname: string, route: string) => {
  const normalizedPathname = normalizedRoute(pathname);
  const normalizedTarget = normalizedRoute(route);
  return normalizedPathname === normalizedTarget || (normalizedTarget !== "/" && normalizedPathname.startsWith(`${normalizedTarget}/`));
};
const iconByCode: Record<string, NavigationItem["icon"]> = { INICIO: Home, ORG: Building2, TH: Users, INV: PackageSearch, COM: CalendarRange, CONFIG: Settings, HD: Headphones, CAP: GraduationCap, TI: Settings };
function moduleIcon(code: string) { return iconByCode[code.toUpperCase()] ?? Settings; }

export function AppShell({ title, user: suppliedUser }: { title: string; user?: User }) {
  const security = useSecurity();
  const pathname = usePathname() ?? ""; const [user, setUser] = useState<User | null>(suppliedUser ?? null);
  const [collapsed, setCollapsed] = useState(false); const [mobileOpen, setMobileOpen] = useState(false);
  const [accountOpen, setAccountOpen] = useState(false); const [loggingOut, setLoggingOut] = useState(false);
  const [reauthRequired, setReauthRequired] = useState(false); const [expanded, setExpanded] = useState<string[]>([]); const [collapsedGroups, setCollapsedGroups] = useState<string[]>([]);
  const [navigatingTo, setNavigatingTo] = useState<string | null>(null);
  const [accentTheme, setAccentTheme] = useState<AccentTheme>("forest");
  const accountRef = useRef<HTMLDivElement>(null);
  const navigation = useMemo<NavigationItem[]>(() => {
    const configured = security.modules;
    const inferredParentIds = new Map<string, string | null>();
    for (const configuredModule of configured) {
      if (configuredModule.parentId) {
        inferredParentIds.set(configuredModule.id, configuredModule.parentId);
        continue;
      }
      const parent = configured
        .filter(candidate => candidate.id !== configuredModule.id && configuredModule.code.toUpperCase().startsWith(`${candidate.code.toUpperCase()}.`))
        .sort((left, right) => right.code.length - left.code.length)[0];
      inferredParentIds.set(configuredModule.id, parent?.id ?? null);
    }
    const childrenByParent = new Map<string, typeof configured>();
    for (const configuredModule of configured) {
      const parentId = inferredParentIds.get(configuredModule.id);
      if (!parentId) continue;
      const siblings = childrenByParent.get(parentId) ?? [];
      siblings.push(configuredModule);
      childrenByParent.set(parentId, siblings);
    }
    const roots = configured
      .filter(configuredModule => !inferredParentIds.get(configuredModule.id) && !configuredModule.code.toUpperCase().startsWith("INT."))
      .sort((left, right) => left.order - right.order || left.name.localeCompare(right.name, "es"));
    const result: NavigationItem[] = [{ key: "back-intranet", href: "/intranet", label: "Volver a la intranet", icon: ChevronLeft, exact: true }];
    for (const root of roots) {
      const children = (childrenByParent.get(root.id) ?? [])
        .filter(child => Boolean(child.route))
        .sort((left, right) => left.order - right.order || left.name.localeCompare(right.name, "es"))
        .map(child => ({ href: child.route, label: child.name }));
      const usesContextualWorkspace = root.code.toUpperCase() === "CAP";
      result.push(children.length && !usesContextualWorkspace
        ? { key: root.id, label: root.name, icon: moduleIcon(root.code), children }
        : { key: root.id, href: root.route, label: root.name, icon: moduleIcon(root.code), exact: normalizedRoute(root.route) === "/admincore" });
    }
    return result;
  }, [security.modules]);
  useEffect(() => { const frame = requestAnimationFrame(() => setCollapsed(localStorage.getItem("gaia-sidebar-collapsed") === "true")); return () => cancelAnimationFrame(frame); }, []);
  useEffect(() => { const frame = requestAnimationFrame(() => { const saved = localStorage.getItem("gaia-accent-theme"); if (accentThemes.some(theme => theme.value === saved)) setAccentTheme(saved as AccentTheme); }); return () => cancelAnimationFrame(frame); }, []);
  useEffect(() => { document.documentElement.dataset.gaiaSidebar = collapsed ? "collapsed" : "expanded"; localStorage.setItem("gaia-sidebar-collapsed", String(collapsed)); return () => { delete document.documentElement.dataset.gaiaSidebar; }; }, [collapsed]);
  useEffect(() => { if (suppliedUser) return; void fetch(`${apiUrl}/api/auth/me`, { credentials: "include" }).then(async response => { if (response.status === 401) { location.href = "/"; return; } if (response.ok) setUser(await response.json() as User); }); }, [suppliedUser]);
  useEffect(() => { const close = (event: MouseEvent) => { if (!accountRef.current?.contains(event.target as Node)) setAccountOpen(false); }; document.addEventListener("mousedown", close); return () => document.removeEventListener("mousedown", close); }, []);
  useEffect(() => { const show = () => setReauthRequired(true); addEventListener("gaia:reauth-required", show); return () => removeEventListener("gaia:reauth-required", show); }, []);
  useEffect(() => { const original = window.fetch.bind(window); window.fetch = async (...args) => { const response = await original(...args); if (response.status === 401) { const problem = await response.clone().json().catch(() => null) as { code?: string } | null; if (problem?.code === "reauth_required") dispatchEvent(new CustomEvent("gaia:reauth-required")); } return response; }; return () => { window.fetch = original; }; }, []);
  useEffect(() => { const frame = requestAnimationFrame(() => setNavigatingTo(null)); return () => cancelAnimationFrame(frame); }, [pathname]);
  useEffect(() => { if (!navigatingTo) return; const timeout = window.setTimeout(() => setNavigatingTo(null), 8000); return () => window.clearTimeout(timeout); }, [navigatingTo]);
  const isActive = (href: string, exact?: boolean) => exact ? normalizedRoute(pathname) === normalizedRoute(href) : routeMatches(pathname,href);
  const pendingNavigation = navigatingTo === pathname ? null : navigatingTo;
  const displayedUser = security.user ? { displayName: security.user.name, email: security.user.email } : user;
  function logout() { setLoggingOut(true); location.href = `${apiUrl}/api/auth/logout?returnUrl=${encodeURIComponent(`${location.origin}/?logout=success`)}`; }
  function selectAccent(theme: AccentTheme) { setAccentTheme(theme); document.documentElement.dataset.gaiaAccent = theme; localStorage.setItem("gaia-accent-theme", theme); }
  function beginNavigation(href: string, event: ReactMouseEvent<HTMLAnchorElement>) { if (navigatingTo === href) { event.preventDefault(); return; } setNavigatingTo(href); setMobileOpen(false); }

  return <><div aria-hidden={!pendingNavigation} className={`gaia-navigation-progress ${pendingNavigation ? "is-active" : ""}`} role="progressbar" />{mobileOpen && <button aria-label="Cerrar navegación" className="gaia-sidebar-backdrop" onClick={() => setMobileOpen(false)} type="button" />}
    <aside aria-label="Navegación principal" className={`gaia-sidebar ${collapsed ? "is-collapsed" : ""} ${mobileOpen ? "is-mobile-open" : ""}`}>
      <div className="gaia-brand"><span className="gaia-brand-mark"><Image alt="Gaia Amazonas" height={41} priority src="/brand/logo-gaia.svg" width={75} /></span>{!collapsed && <div className="min-w-0"><p className="gaia-brand-name">Fundación Gaia Amazonas</p><p className="gaia-brand-caption">Plataforma empresarial</p></div>}<IconButton className="gaia-mobile-close" label="Cerrar navegación" onClick={() => setMobileOpen(false)}><X size={19} /></IconButton></div>
      <nav className="gaia-navigation">{!collapsed && <p className="gaia-navigation-label">Espacio de trabajo</p>}{navigation.map(item => {
        const Icon = item.icon; const children = "children" in item ? item.children : undefined;
        const itemLabel = item.label;
        const activeChild = children
          ?.flatMap(child => [child.href,...(child.aliases ?? [])].filter(route => routeMatches(pathname,route)).map(route => ({ child,route })))
          .sort((left,right) => normalizedRoute(right.route).length-normalizedRoute(left.route).length)[0]?.child;
        const childActive = Boolean(activeChild); const isExpanded = expanded.includes(item.key) || (childActive && !collapsedGroups.includes(item.key));
        if (!children) return <Link aria-busy={pendingNavigation === item.href} aria-current={isActive(item.href!, item.exact) ? "page" : undefined} className={`gaia-nav-item ${isActive(item.href!, item.exact) ? "is-active" : ""}`} href={item.href!} key={item.key} onClick={event => beginNavigation(item.href!, event)} title={collapsed ? itemLabel : undefined}><Icon size={20} strokeWidth={1.8} />{!collapsed && <span>{itemLabel}</span>}{pendingNavigation === item.href && <LoaderCircle className="gaia-spin gaia-nav-loading" size={15} />}</Link>;
        return <div className="gaia-nav-group" key={item.key}><button aria-expanded={isExpanded} className={`gaia-nav-item gaia-nav-parent ${childActive ? "has-active-child" : ""}`} onClick={() => { if(isExpanded){setExpanded(current=>current.filter(key=>key!==item.key));setCollapsedGroups(current=>current.includes(item.key)?current:[...current,item.key]);}else{setCollapsedGroups(current=>current.filter(key=>key!==item.key));setExpanded(current=>current.includes(item.key)?current:[...current,item.key]);} }} title={collapsed ? itemLabel : undefined} type="button"><Icon size={20} strokeWidth={1.8} />{!collapsed && <><span>{itemLabel}</span><ChevronDown className="gaia-nav-chevron" size={15} /></>}</button>{isExpanded && !collapsed && <div className="gaia-subnavigation">{children.map(child => { const active = activeChild?.href===child.href; return <Link aria-busy={pendingNavigation === child.href} aria-current={active ? "page" : undefined} className={`gaia-subnav-item ${active ? "is-active" : ""}`} href={child.href} key={child.href} onClick={event => beginNavigation(child.href, event)}>{child.label}{pendingNavigation === child.href && <LoaderCircle className="gaia-spin gaia-nav-loading" size={13} />}</Link>; })}</div>}</div>;
      })}</nav>
      <div className="gaia-sidebar-footer" ref={accountRef}>{accountOpen && <div className={`gaia-account-menu ${collapsed ? "is-collapsed" : ""}`} role="menu"><div className="gaia-account-summary"><UserRound size={17} /><div><strong>{displayedUser?.displayName ?? "Usuario Gaia"}</strong><span>{displayedUser?.email}</span></div></div><div className="gaia-theme-selector"><div><Palette size={16} /><span>Color de la plataforma</span></div><div aria-label="Color de la plataforma" className="gaia-theme-options" role="radiogroup">{accentThemes.map(theme => <button aria-checked={accentTheme === theme.value} aria-label={theme.label} className={accentTheme === theme.value ? "is-selected" : ""} key={theme.value} onClick={() => selectAccent(theme.value)} role="radio" title={theme.label} type="button"><span style={{ backgroundColor: theme.color }} /><small>{theme.label}</small></button>)}</div></div><button className="gaia-account-action" disabled={loggingOut} onClick={logout} role="menuitem" type="button">{loggingOut ? <LoaderCircle className="gaia-spin" size={17} /> : <LogOut size={17} />}{loggingOut ? "Cerrando sesión..." : "Cerrar sesión"}</button></div>}
        <button aria-expanded={accountOpen} className="gaia-user-trigger" onClick={() => setAccountOpen(value => !value)} title={collapsed ? displayedUser?.displayName : undefined} type="button"><Avatar currentUser name={displayedUser?.displayName ?? "Usuario Gaia"} />{!collapsed && <><span className="min-w-0 flex-1 text-left"><strong>{displayedUser?.displayName ?? "Usuario Gaia"}</strong><small>{displayedUser?.email ?? "Cuenta institucional"}</small></span><ChevronRight size={17} /></>}</button>
        <button className="gaia-collapse-button" onClick={() => setCollapsed(value => !value)} title={collapsed ? "Expandir navegación" : "Contraer navegación"} type="button">{collapsed ? <PanelLeftOpen size={18} /> : <PanelLeftClose size={18} />}{!collapsed && <span>Contraer</span>}</button></div>
    </aside>
    <header className="gaia-topbar"><IconButton className="gaia-menu-button" label="Abrir navegación" onClick={() => setMobileOpen(true)}><Menu size={21} /></IconButton><h1>{title}</h1><div className="gaia-topbar-status"><span aria-hidden="true" /><span>Entorno institucional</span></div></header>
    <ConfirmDialog confirmLabel="Volver a iniciar sesión" description="Por seguridad, vuelve a iniciar sesión para continuar." onCancel={() => setReauthRequired(false)} onConfirm={() => startLogin(location.href)} open={reauthRequired} title="Tu sesión necesita renovarse" />
  </>;
}
