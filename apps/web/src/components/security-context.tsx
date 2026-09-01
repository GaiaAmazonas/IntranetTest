"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { hasPermission } from "@/lib/security-permissions";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";
const inactivityTimeoutMs = 40 * 60 * 1000;
const lastActivityStorageKey = "gaia:last-user-activity";

export type SecurityUser = {
  id: string;
  name: string;
  email: string;
  thirdPartyId?: string | null;
  documentNumber?: string | null;
  lastAccess?: string | null;
  isActive: boolean;
};

export type SecurityContextValue = {
  user: SecurityUser | null;
  roles: string[];
  permissions: string[];
  modules: SecurityNavigationModule[];
  loading: boolean;
  error: string | null;
  can: (permission: string) => boolean;
  refresh: () => Promise<void>;
};

export type SecurityNavigationModule = {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  route: string;
  icon?: string | null;
  order: number;
};

const SecurityContext = createContext<SecurityContextValue | null>(null);

export function SecurityProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<SecurityUser | null>(null);
  const [roles, setRoles] = useState<string[]>([]);
  const [permissions, setPermissions] = useState<string[]>([]);
  const [modules, setModules] = useState<SecurityNavigationModule[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const inactivityLogoutStarted = useRef(false);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${apiUrl}/api/security/me`, { credentials: "include" });
      if (response.status === 401) {
        setUser(null);
        setRoles([]);
        setPermissions([]);
        setModules([]);
        setError(null);
        return;
      }
      if (!response.ok) throw new Error("No fue posible cargar tus permisos de acceso.");
      const result = await response.json() as { user: SecurityUser; roles: string[]; permissions: string[]; modules?: SecurityNavigationModule[] };
      setUser(result.user);
      setRoles(result.roles);
      setPermissions(result.permissions);
      setModules(result.modules ?? []);
      setError(null);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "No fue posible cargar tus permisos.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { const timer = window.setTimeout(() => { void refresh(); }, 0); return () => window.clearTimeout(timer); }, [refresh]);
  useEffect(() => {
    if (!user) {
      inactivityLogoutStarted.current = false;
      return;
    }

    let timer: number | undefined;
    let lastActivity = Date.now();

    const logoutForInactivity = () => {
      if (inactivityLogoutStarted.current) return;
      inactivityLogoutStarted.current = true;
      window.location.assign(`${apiUrl}/api/auth/logout?returnUrl=${encodeURIComponent(`${window.location.origin}/?logout=inactivity`)}`);
    };
    const schedule = () => {
      if (timer) window.clearTimeout(timer);
      const remaining = inactivityTimeoutMs - (Date.now() - lastActivity);
      if (remaining <= 0) logoutForInactivity();
      else timer = window.setTimeout(logoutForInactivity, remaining);
    };
    const registerActivity = () => {
      lastActivity = Date.now();
      window.localStorage.setItem(lastActivityStorageKey, String(lastActivity));
      schedule();
    };
    const synchronizeActivity = (event: StorageEvent) => {
      if (event.key !== lastActivityStorageKey || !event.newValue) return;
      const synchronized = Number(event.newValue);
      if (Number.isFinite(synchronized) && synchronized > lastActivity) {
        lastActivity = synchronized;
        schedule();
      }
    };
    const verifyWhenVisible = () => {
      if (document.visibilityState !== "visible") return;
      const synchronized = Number(window.localStorage.getItem(lastActivityStorageKey));
      if (Number.isFinite(synchronized) && synchronized > lastActivity) lastActivity = synchronized;
      schedule();
    };

    registerActivity();
    const activityEvents: (keyof WindowEventMap)[] = ["pointerdown", "keydown", "scroll", "touchstart"];
    activityEvents.forEach(eventName => window.addEventListener(eventName, registerActivity, { passive: true }));
    window.addEventListener("storage", synchronizeActivity);
    document.addEventListener("visibilitychange", verifyWhenVisible);
    return () => {
      if (timer) window.clearTimeout(timer);
      activityEvents.forEach(eventName => window.removeEventListener(eventName, registerActivity));
      window.removeEventListener("storage", synchronizeActivity);
      document.removeEventListener("visibilitychange", verifyWhenVisible);
    };
  }, [user]);
  const permissionSet = useMemo(() => new Set(permissions.map(value => value.toUpperCase())), [permissions]);
  const value = useMemo<SecurityContextValue>(() => ({
    user, roles, permissions, modules, loading, error,
    can: permission => hasPermission(permissionSet, permission),
    refresh,
  }), [user, roles, permissions, modules, loading, error, permissionSet, refresh]);

  return <SecurityContext.Provider value={value}>{children}</SecurityContext.Provider>;
}

export function useSecurity() {
  const context = useContext(SecurityContext);
  if (!context) throw new Error("useSecurity debe utilizarse dentro de SecurityProvider.");
  return context;
}
