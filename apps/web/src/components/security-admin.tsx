"use client";

import { useCallback, useEffect, useState } from "react";
import { LoaderCircle, RefreshCw } from "lucide-react";
import { apiRequest } from "@/lib/api-client";
import { AppShell } from "./app-shell";
import { useFeedback } from "./feedback";
import { Button } from "./ui";
import { SecurityUsersView, type SecurityPreprovisionAudit, type SecurityRole, type SecurityUserDetail } from "./security-users-view";
import { SecurityRolesView, type SecurityModule, type SecurityPermission } from "./security-roles-view";
import { SecurityModulesView } from "./security-modules-view";

type Role = SecurityRole;
type Permission = SecurityPermission;
type Module = SecurityModule;

export function SecurityAdmin({ view }: { view: "users" | "roles" | "modules" }) {
  const feedback = useFeedback();
  const [users, setUsers] = useState<SecurityUserDetail[]>([]); const [roles, setRoles] = useState<Role[]>([]);
  const [permissions, setPermissions] = useState<Permission[]>([]); const [modules, setModules] = useState<Module[]>([]);
  const [preprovisionAudit, setPreprovisionAudit] = useState<SecurityPreprovisionAudit | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      if (view === "users") { const [userRows, roleRows, audit] = await Promise.all([apiRequest<SecurityUserDetail[]>("/api/security/users"), apiRequest<Role[]>("/api/security/roles"), apiRequest<SecurityPreprovisionAudit>("/api/security/users/preprovision-audit")]); setUsers(userRows); setRoles(roleRows); setPreprovisionAudit(audit); }
      if (view === "roles") { const [roleRows, permissionRows, moduleRows] = await Promise.all([apiRequest<Role[]>("/api/security/roles"), apiRequest<Permission[]>("/api/security/permissions"), apiRequest<Module[]>("/api/security/modules")]); setRoles(roleRows); setPermissions(permissionRows); setModules(moduleRows); }
      if (view === "modules") {
        const response = await fetch(`${process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168"}/api/security/modules`, { credentials: "include" });
        if (response.status === 403) setModules([]);
        else if (!response.ok) throw new Error("No fue posible consultar el catálogo de módulos.");
        else setModules(await response.json() as Module[]);
      }
    } catch (reason) { feedback.notify({ tone: "error", title: "No fue posible cargar Seguridad", description: reason instanceof Error ? reason.message : undefined }); }
    finally { setLoading(false); }
  }, [feedback, view]);
  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  const title = view === "users" ? "Usuarios y accesos" : view === "roles" ? "Roles y permisos" : "Catálogo de módulos";
  return <main className="gaia-app-page"><AppShell title={`Tecnología · ${title}`} /><div className="mx-auto max-w-[1500px] px-6 pb-10">
    <div className="gaia-context-toolbar"><p>Administración centralizada de acceso sobre Microsoft Entra ID y Dataverse.</p><Button onClick={() => void load()} variant="secondary"><RefreshCw size={16} />Actualizar</Button></div>
    {loading ? <div className="grid min-h-64 place-items-center"><LoaderCircle className="gaia-spin text-[#386037]" /></div> : view === "users" ? <SecurityUsersView audit={preprovisionAudit} users={users} roles={roles} onReload={load} /> : view === "roles" ? <SecurityRolesView modules={modules} onReload={load} permissions={permissions} roles={roles} /> : <SecurityModulesView modules={modules} onReload={load} />}
  </div></main>;
}
