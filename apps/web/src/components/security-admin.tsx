"use client";

import { useCallback, useEffect, useState } from "react";
import { LoaderCircle, RefreshCw, ShieldCheck } from "lucide-react";
import { apiRequest } from "@/lib/api-client";
import { AppShell } from "./app-shell";
import { useFeedback } from "./feedback";
import { Button } from "./ui";
import { SecurityUsersView, type SecurityPreprovisionAudit, type SecurityRole, type SecurityUserDetail } from "./security-users-view";
import { SecurityRolesView, type SecurityModule, type SecurityPermission } from "./security-roles-view";
import { SecurityModulesView } from "./security-modules-view";
import { useSecurity } from "./security-context";

type Role = SecurityRole;
type Permission = SecurityPermission;
type Module = SecurityModule;

export function SecurityAdmin({ view }: { view: "users" | "roles" | "modules" }) {
  const feedback = useFeedback();
  const security = useSecurity();
  const [users, setUsers] = useState<SecurityUserDetail[]>([]); const [roles, setRoles] = useState<Role[]>([]);
  const [permissions, setPermissions] = useState<Permission[]>([]); const [modules, setModules] = useState<Module[]>([]);
  const [preprovisionAudit, setPreprovisionAudit] = useState<SecurityPreprovisionAudit | null>(null);
  const [loading, setLoading] = useState(true);
  const [bootstrapRequired, setBootstrapRequired] = useState(false);
  const [bootstrapping, setBootstrapping] = useState(false);

  const reloadUsers = useCallback(async () => {
    const [userRows, roleRows, audit] = await Promise.all([
      apiRequest<SecurityUserDetail[]>("/api/security/users"),
      apiRequest<Role[]>("/api/security/roles"),
      apiRequest<SecurityPreprovisionAudit>("/api/security/users/preprovision-audit"),
    ]);
    setUsers(userRows);
    setRoles(roleRows);
    setPreprovisionAudit(audit);
  }, []);
  const updateUserAssignments = useCallback((userId: string, update: (assignments: SecurityUserDetail["roles"]) => SecurityUserDetail["roles"]) => {
    setUsers(current => current.map(item => item.user.id === userId ? { ...item, roles: update(item.roles) } : item));
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      if (view === "users") await reloadUsers();
      if (view === "roles") { const [roleRows, permissionRows, moduleRows] = await Promise.all([apiRequest<Role[]>("/api/security/roles"), apiRequest<Permission[]>("/api/security/permissions"), apiRequest<Module[]>("/api/security/modules")]); setRoles(roleRows); setPermissions(permissionRows); setModules(moduleRows); }
      if (view === "modules") {
        const response = await fetch(`${process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168"}/api/security/modules`, { credentials: "include" });
        if (response.status === 403) { setModules([]); setBootstrapRequired(true); }
        else if (!response.ok) throw new Error("No fue posible consultar el catálogo de módulos.");
        else { const rows = await response.json() as Module[]; setModules(rows); setBootstrapRequired(rows.length === 0); }
      }
    } catch (reason) { feedback.notify({ tone: "error", title: "No fue posible cargar Seguridad", description: reason instanceof Error ? reason.message : undefined }); }
    finally { setLoading(false); }
  }, [feedback, reloadUsers, view]);
  useEffect(() => { const timer = window.setTimeout(() => { void load(); }, 0); return () => window.clearTimeout(timer); }, [load]);

  async function initializeSecurity() {
    if (bootstrapping) return; setBootstrapping(true);
    try {
      const result = await apiRequest<{ modules: number; permissions: number; roles: number }>("/api/security/bootstrap", { method: "POST" });
      await security.refresh(); await load();
      feedback.notify({ tone: "success", title: "Seguridad inicializada", description: `${result.modules} módulos, ${result.permissions} permisos y ${result.roles} roles quedaron alineados.` });
    } catch (reason) { feedback.notify({ tone: "error", title: "No fue posible inicializar Seguridad", description: reason instanceof Error ? reason.message : undefined }); }
    finally { setBootstrapping(false); }
  }

  const title = view === "users" ? "Usuarios y accesos" : view === "roles" ? "Roles y permisos" : "Catálogo de módulos";
  return <main className="gaia-app-page"><AppShell title={`Seguridad · ${title}`} /><div className="mx-auto max-w-[1500px] px-6 pb-10">
    <div className="gaia-context-toolbar"><p>Administración centralizada de acceso sobre Microsoft Entra ID y Dataverse.</p><div className="gaia-context-toolbar-actions">{view === "modules" && <Button disabled={bootstrapping} onClick={() => void initializeSecurity()} variant="secondary">{bootstrapping ? <LoaderCircle className="gaia-spin" size={16}/> : <ShieldCheck size={16}/>} {bootstrapping ? "Sincronizando…" : "Sincronizar seguridad"}</Button>}<Button onClick={() => void load()} variant="secondary"><RefreshCw size={16} />Actualizar</Button></div></div>
    {loading ? <div className="grid min-h-64 place-items-center"><LoaderCircle className="gaia-spin text-[#386037]" /></div> : view === "users" ? <SecurityUsersView audit={preprovisionAudit} users={users} roles={roles} onAssignmentsChanged={updateUserAssignments} onReload={reloadUsers} /> : view === "roles" ? <SecurityRolesView modules={modules} onPermissionsPublished={security.refresh} onReload={load} permissions={permissions} roles={roles} /> : bootstrapRequired ? <section className="gaia-security-bootstrap"><span><ShieldCheck size={25}/></span><div><p>Seguridad · Recuperación controlada</p><h2>El catálogo de Seguridad no está inicializado</h2><small>Ejecuta el proceso idempotente para crear o alinear módulos, permisos, roles y tu acceso administrativo sin duplicar registros.</small></div><Button disabled={bootstrapping} onClick={() => void initializeSecurity()}>{bootstrapping ? <LoaderCircle className="gaia-spin" size={16}/> : <ShieldCheck size={16}/>} {bootstrapping ? "Inicializando…" : "Inicializar seguridad"}</Button></section> : <SecurityModulesView modules={modules} onAccessChanged={security.refresh} onReload={load} />}
  </div></main>;
}
