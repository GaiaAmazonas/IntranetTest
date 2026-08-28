"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import { hasPermission } from "@/lib/security-permissions";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";

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
