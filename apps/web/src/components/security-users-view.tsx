"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { CalendarDays, ChevronRight, Clock3, Fingerprint, KeyRound, Link2, LoaderCircle, Mail, Search, ShieldCheck, SlidersHorizontal, UserRoundCheck, Users, X } from "lucide-react";
import { apiRequest } from "@/lib/api-client";
import { ConfirmDialog } from "./form-dialog";
import { useFeedback } from "./feedback";
import { Badge, Button, EmptyState, IconButton } from "./ui";

export type RoleAssignment = { id: string; roleId: string; roleCode: string; roleName: string; startDate: string; endDate?: string | null; isActive: boolean };
export type SecurityUserDetail = { user: { id?: string | null; name: string; email: string; entraObjectId?: string | null; thirdPartyId?: string | null; documentNumber?: string | null; lastAccess?: string | null; isActive: boolean; provisioningStatus: "PROVISIONED" | "PENDING_FIRST_ACCESS" }; roles: RoleAssignment[] };
export type SecurityRole = { id: string; code: string; name: string; description?: string | null; isSystem: boolean; isActive: boolean; assignedUsers: number; permissions: string[] };
export type SecurityPreprovisionAudit = { activeThirdParties: number; withInstitutionalEmail: number; eligible: number; existingApplicationUsers: number; toPreprovision: number; duplicateEmails: number; multipleInstitutionalEmails: number; entraObjectIdAllowsNull: boolean; issues: { code: string; description: string; email?: string | null; thirdPartyId?: string | null }[] };

type StatusFilter = "all" | "active" | "pending" | "inactive";

export function SecurityUsersView({ users, roles, audit, onAssignmentsChanged, onReload }: { users: SecurityUserDetail[]; roles: SecurityRole[]; audit: SecurityPreprovisionAudit | null; onAssignmentsChanged: (userId: string, update: (assignments: RoleAssignment[]) => RoleAssignment[]) => void; onReload: () => Promise<void> }) {
  const feedback = useFeedback();
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<StatusFilter>("all");
  const [roleFilter, setRoleFilter] = useState("");
  const [selectedUserId, setSelectedUserIdRaw] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [roleId, setRoleId] = useState("");
  const [startDate, setStartDate] = useState(today());
  const [endDate, setEndDate] = useState("");
  const [ending, setEnding] = useState<RoleAssignment | null>(null);
  const [technicalOpen, setTechnicalOpen] = useState(false);
  const panelRef = useRef<HTMLElement>(null);
  const selected = users.find(item => item.user.id === selectedUserId) ?? null;

  useEffect(() => {
    if (!selectedUserId) return;
    const previous = document.activeElement as HTMLElement | null;
    const frame = requestAnimationFrame(() => panelRef.current?.focus());
    const close = (event: KeyboardEvent) => { if (event.key === "Escape") setSelectedUserId(null); };
    document.addEventListener("keydown", close); document.body.style.overflow = "hidden";
    return () => { cancelAnimationFrame(frame); document.removeEventListener("keydown", close); document.body.style.overflow = ""; previous?.focus(); };
  }, [selectedUserId]);

  function setSelectedUserId(userId: string | null) {
    if (userId) { setRoleId(""); setStartDate(today()); setEndDate(""); setTechnicalOpen(false); }
    setSelectedUserIdRaw(userId);
  }

  const filtered = useMemo(() => {
    const normalized = query.trim().toLocaleLowerCase("es");
    return users.filter(item => {
      if (status === "active" && (!item.user.isActive || item.user.provisioningStatus === "PENDING_FIRST_ACCESS")) return false;
      if (status === "pending" && item.user.provisioningStatus !== "PENDING_FIRST_ACCESS") return false;
      if (status === "inactive" && item.user.isActive) return false;
      if (roleFilter && !item.roles.some(assignment => isCurrent(assignment) && assignment.roleId === roleFilter)) return false;
      return !normalized || `${item.user.name} ${item.user.email} ${item.user.documentNumber ?? ""}`.toLocaleLowerCase("es").includes(normalized);
    });
  }, [query, roleFilter, status, users]);

  const availableRoles = selected ? roles.filter(role => role.isActive && !selected.roles.some(assignment => isCurrent(assignment) && assignment.roleId === role.id)) : [];
  const accessDashboard = useMemo(() => ({
    assigned: users.filter(item => item.roles.some(isCurrent)).length,
    pending: users.filter(item => item.user.provisioningStatus === "PENDING_FIRST_ACCESS").length,
    withoutRole: users.filter(item => !item.roles.some(isCurrent)).length,
    activeAccess: users.filter(item => item.user.isActive && item.user.provisioningStatus === "PROVISIONED").length,
    inactiveAccess: users.filter(item => !item.user.isActive).length,
    withAccessHistory: users.filter(item => Boolean(item.user.lastAccess)).length,
    withoutAccessHistory: users.filter(item => !item.user.lastAccess).length,
    roleDistribution: roles.filter(role => role.isActive).sort((left, right) => right.assignedUsers - left.assignedUsers || left.name.localeCompare(right.name, "es")),
  }), [roles, users]);

  async function assignRole() {
    if (!selected?.user.id || !roleId || !startDate) return;
    setSaving(true);
    try {
      const assignmentId = await apiRequest<string>(`/api/security/users/${selected.user.id}/roles`, { method: "POST", body: JSON.stringify({ roleId, startDate, endDate: endDate || null, observations: "Asignación administrativa" }) });
      const assignedRole = roles.find(role => role.id === roleId);
      if (assignedRole) onAssignmentsChanged(selected.user.id, assignments => [...assignments, { id: assignmentId, roleId: assignedRole.id, roleCode: assignedRole.code, roleName: assignedRole.name, startDate, endDate: endDate || null, isActive: true }]);
      feedback.notify({ tone: "success", title: "Rol asignado", description: "La vigencia quedó registrada correctamente." });
      setRoleId(""); setStartDate(today()); setEndDate("");
      void onReload().catch(reason => feedback.notify({ tone: "error", title: "No fue posible sincronizar el listado", description: reason instanceof Error ? reason.message : undefined }));
    } catch (reason) { feedback.notify({ tone: "error", title: "No fue posible asignar el rol", description: reason instanceof Error ? reason.message : undefined }); }
    finally { setSaving(false); }
  }

  async function endAssignment() {
    if (!selected?.user.id || !ending) return;
    setSaving(true);
    const effectiveEnd = ending.startDate > today() ? ending.startDate : today();
    try {
      await apiRequest(`/api/security/users/${selected.user.id}/roles/${ending.id}/end?endDate=${effectiveEnd}`, { method: "PUT" });
      onAssignmentsChanged(selected.user.id, assignments => assignments.map(assignment => assignment.id === ending.id ? { ...assignment, endDate: effectiveEnd, isActive: false } : assignment));
      feedback.notify({ tone: "success", title: "Asignación finalizada", description: `El rol ${ending.roleName} dejó de estar vigente.` });
      setEnding(null);
      void onReload().catch(reason => feedback.notify({ tone: "error", title: "No fue posible sincronizar el listado", description: reason instanceof Error ? reason.message : undefined }));
    } catch (reason) { feedback.notify({ tone: "error", title: "No fue posible retirar el rol", description: reason instanceof Error ? reason.message : undefined }); }
    finally { setSaving(false); }
  }

  return <>
    {audit && <details className="gaia-security-preprovision-audit gaia-security-users-dashboard">
      <summary><span><strong>Resumen de usuarios y accesos</strong><small>Indicadores de preparación, asignación y roles vigentes</small></span><ChevronRight size={17}/></summary>
      <div className="gaia-security-dashboard-metrics"><article><Users size={18}/><span><strong>{users.length}</strong><small>Usuarios registrados</small></span></article><article><UserRoundCheck size={18}/><span><strong>{accessDashboard.assigned}</strong><small>Con rol vigente</small></span></article><article><Clock3 size={18}/><span><strong>{accessDashboard.pending}</strong><small>Pendientes de ingreso</small></span></article><article><KeyRound size={18}/><span><strong>{accessDashboard.withoutRole}</strong><small>Sin rol vigente</small></span></article></div>
      <section className="gaia-security-dashboard-roles"><header><span><strong>Distribución por rol</strong><small>Usuarios con asignación vigente por perfil</small></span><b>{accessDashboard.roleDistribution.length} roles activos</b></header><div>{accessDashboard.roleDistribution.map(role=><span key={role.id}><i>{role.name}</i><strong>{role.assignedUsers}</strong></span>)}</div></section>
      <section className="gaia-security-dashboard-access"><header><strong>Estado operativo de los accesos</strong><small>Disponibilidad y uso registrado de las cuentas</small></header><div><span><i>Accesos activos</i><strong>{accessDashboard.activeAccess}</strong></span><span><i>Accesos inactivos</i><strong>{accessDashboard.inactiveAccess}</strong></span><span><i>Ya ingresaron</i><strong>{accessDashboard.withAccessHistory}</strong></span><span><i>Sin ingreso registrado</i><strong>{accessDashboard.withoutAccessHistory}</strong></span></div></section>
      <p><strong>{audit.eligible}</strong> colaboradores elegibles · <strong>{audit.toPreprovision}</strong> pendientes de preaprovisionar · <strong>{audit.issues.length}</strong> incidencias. {audit.entraObjectIdAllowsNull ? "El modelo admite preaprovisionamiento." : "Dataverse exige Entra Object ID; la preparación está bloqueada."}</p>
    </details>}
    <section aria-label="Filtros de usuarios" className="gaia-security-filters">
      <label className="gaia-security-search"><Search size={17} /><span className="sr-only">Buscar usuarios</span><input onChange={event => setQuery(event.target.value)} placeholder="Buscar por nombre, correo o documento…" type="search" value={query} /></label>
      <label><span>Estado</span><select onChange={event => setStatus(event.target.value as StatusFilter)} value={status}><option value="all">Todos</option><option value="active">Activos</option><option value="pending">Pendientes de primer acceso</option><option value="inactive">Inactivos</option></select></label>
      <label><span>Rol</span><select onChange={event => setRoleFilter(event.target.value)} value={roleFilter}><option value="">Todos los roles</option>{roles.map(role => <option key={role.id} value={role.id}>{role.name}</option>)}</select></label>
      <div className="gaia-security-result-count"><SlidersHorizontal size={15} /><strong>{filtered.length}</strong><span>de {users.length} usuarios</span></div>
    </section>

    {!filtered.length ? <EmptyState title="No se encontraron usuarios" description="Ajusta la búsqueda o los filtros seleccionados." /> : <div className="gaia-security-users-table-wrap"><table className="gaia-security-users-table"><thead><tr><th>Usuario</th><th>Estado de acceso</th><th>Roles vigentes</th><th>Último acceso</th><th><span className="sr-only">Acciones</span></th></tr></thead><tbody>{filtered.map(item => { const activeRoles = item.roles.filter(isCurrent); const pending = item.user.provisioningStatus === "PENDING_FIRST_ACCESS"; const preprovisioned = pending && Boolean(item.user.id); return <tr className={!item.user.isActive ? "is-inactive" : ""} key={item.user.id ?? `pending-${item.user.thirdPartyId}`}><td data-label="Usuario"><div className="gaia-security-user-cell"><span aria-hidden="true">{initials(item.user.name)}</span><div><strong>{item.user.name}</strong><small>{item.user.email}</small></div></div></td><td data-label="Estado de acceso"><Badge tone={pending ? "warning" : item.user.isActive ? "success" : "danger"}>{preprovisioned ? "Preparado · pendiente de primer acceso" : pending ? "Pendiente de preaprovisionar" : item.user.isActive ? "Aprovisionado · activo" : "Aprovisionado · inactivo"}</Badge></td><td data-label="Roles vigentes"><div className="gaia-security-role-summary">{activeRoles.length ? <>{activeRoles.slice(0, 2).map(assignment => <Badge key={assignment.id}>{assignment.roleName}</Badge>)}{activeRoles.length > 2 && <span>+{activeRoles.length - 2}</span>}</> : pending && !item.user.id ? <small>Consulta al preaprovisionar</small> : <small>Sin roles vigentes</small>}</div></td><td data-label="Último acceso"><span className="gaia-security-last-access">{pending ? "Aún no ha ingresado" : formatDateTime(item.user.lastAccess)}</span></td><td>{item.user.id ? <Button aria-label={`Gestionar acceso de ${item.user.name}`} onClick={() => setSelectedUserId(item.user.id!)} variant="secondary">Gestionar acceso<ChevronRight size={16} /></Button> : <Badge tone="neutral">Requiere preaprovisionamiento</Badge>}</td></tr>; })}</tbody></table></div>}

    {selected && <div className="gaia-access-drawer-backdrop" onMouseDown={event => { if (event.target === event.currentTarget && !saving) setSelectedUserId(null); }}><aside aria-label={`Acceso de ${selected.user.name}`} aria-modal="true" className="gaia-access-drawer" ref={panelRef} role="dialog" tabIndex={-1}>
      <header><div className="gaia-access-identity"><span aria-hidden="true">{initials(selected.user.name)}</span><div><p>Seguridad · Accesos</p><h2>{selected.user.name}</h2><small>{selected.user.email}</small></div></div><IconButton disabled={saving} label="Cerrar gestión de acceso" onClick={() => setSelectedUserId(null)}><X size={19} /></IconButton></header>
      <div className="gaia-access-content">
        <section aria-labelledby="user-information-title"><div className="gaia-access-section-title"><UserRoundCheck size={18} /><div><h3 id="user-information-title">Información del usuario</h3><p>{selected.user.provisioningStatus === "PENDING_FIRST_ACCESS" ? "Acceso preparado; la identidad Entra se vinculará en el primer ingreso." : "Identidad corporativa vinculada a la plataforma."}</p></div><Badge tone={selected.user.provisioningStatus === "PENDING_FIRST_ACCESS" ? "warning" : selected.user.isActive ? "success" : "danger"}>{selected.user.provisioningStatus === "PENDING_FIRST_ACCESS" ? "Pendiente" : selected.user.isActive ? "Activo" : "Inactivo"}</Badge></div><dl className="gaia-access-facts"><div><dt><Mail size={14} />Correo institucional</dt><dd>{selected.user.email}</dd></div><div><dt><Fingerprint size={14} />Documento</dt><dd>{selected.user.documentNumber ?? "Sin información"}</dd></div><div><dt><Clock3 size={14} />Último acceso</dt><dd>{formatDateTime(selected.user.lastAccess)}</dd></div><div><dt><Link2 size={14} />Tercero relacionado</dt><dd>{selected.user.thirdPartyId ? "Relacionado" : "Sin relación"}</dd></div></dl><button aria-expanded={technicalOpen} className="gaia-technical-toggle" onClick={() => setTechnicalOpen(value => !value)} type="button">{technicalOpen ? "Ocultar" : "Mostrar"} información técnica<ChevronRight className={technicalOpen ? "is-open" : ""} size={15} /></button>{technicalOpen && <dl className="gaia-technical-details"><div><dt>Entra Object ID</dt><dd>{selected.user.entraObjectId || "Pendiente de vinculación en el primer acceso"}</dd></div><div><dt>Usuario Aplicación ID</dt><dd>{selected.user.id}</dd></div><div><dt>Tercero ID</dt><dd>{selected.user.thirdPartyId ?? "No relacionado"}</dd></div></dl>}</section>

        <section aria-labelledby="assigned-roles-title"><div className="gaia-access-section-title"><ShieldCheck size={18} /><div><h3 id="assigned-roles-title">Roles asignados</h3><p>Vigencias actuales, programadas e históricas.</p></div><Badge>{selected.roles.length}</Badge></div><div className="gaia-role-assignment-list">{selected.roles.length ? selected.roles.map(assignment => <article className={!assignment.isActive || isHistorical(assignment) ? "is-history" : ""} key={assignment.id}><div><strong>{assignment.roleName}</strong><small>{assignment.roleCode}</small></div><Badge tone={assignmentStatus(assignment).tone}>{assignmentStatus(assignment).label}</Badge><p><CalendarDays size={13} />{formatDate(assignment.startDate)} → {assignment.endDate ? formatDate(assignment.endDate) : "Sin fecha final"}</p>{isCurrent(assignment) && <Button disabled={saving} onClick={() => setEnding(assignment)} variant="secondary">Retirar rol</Button>}</article>) : <EmptyState title="Sin roles asignados" description="Asigna un rol para habilitar acceso funcional." />}</div></section>

        <section aria-labelledby="assign-role-title"><div className="gaia-access-section-title"><KeyRound size={18} /><div><h3 id="assign-role-title">Asignar un rol</h3><p>Solo se muestran roles que el usuario no tiene vigentes.</p></div></div>{availableRoles.length ? <form className="gaia-role-assignment-form" onSubmit={event => { event.preventDefault(); void assignRole(); }}><label><span>Rol</span><select disabled={saving} onChange={event => setRoleId(event.target.value)} required value={roleId}><option value="">Seleccionar rol</option>{availableRoles.map(role => <option key={role.id} value={role.id}>{role.name}</option>)}</select></label><div><label><span>Fecha inicial</span><input disabled={saving} onChange={event => setStartDate(event.target.value)} required type="date" value={startDate} /></label><label><span>Fecha final <small>Opcional</small></span><input disabled={saving} min={startDate} onChange={event => setEndDate(event.target.value)} type="date" value={endDate} /></label></div><Button disabled={saving || !roleId || !startDate} type="submit">{saving ? <LoaderCircle className="gaia-spin" size={16} /> : <KeyRound size={16} />}Asignar rol</Button></form> : <div className="gaia-access-note"><ShieldCheck size={17} /><span>El usuario ya tiene todos los roles activos disponibles.</span></div>}</section>
      </div>
    </aside></div>}

    <ConfirmDialog confirmLabel="Sí, retirar rol" description={ending ? `La asignación de ${ending.roleName} finalizará con fecha ${formatDate(ending.startDate > today() ? ending.startDate : today())}. El historial se conservará.` : ""} destructive loading={saving} onCancel={() => setEnding(null)} onConfirm={() => void endAssignment()} open={Boolean(ending)} title="¿Retirar este rol?" />
  </>;
}

function today() { return new Date().toISOString().slice(0, 10); }
function isCurrent(assignment: RoleAssignment) { const value = today(); return assignment.isActive && assignment.startDate <= value && (!assignment.endDate || assignment.endDate >= value); }
function isHistorical(assignment: RoleAssignment) { return Boolean(assignment.endDate && assignment.endDate < today()); }
function assignmentStatus(assignment: RoleAssignment): { label: string; tone: "success" | "warning" | "danger" | "neutral" } { if (!assignment.isActive) return { label: "Inactivo", tone: "danger" }; if (assignment.startDate > today()) return { label: "Programado", tone: "warning" }; if (isHistorical(assignment)) return { label: "Finalizado", tone: "neutral" }; return { label: "Vigente", tone: "success" }; }
function initials(name: string) { return name.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join("").toUpperCase() || "GA"; }
function formatDate(value: string) { const [year, month, day] = value.split("-").map(Number); return new Intl.DateTimeFormat("es-CO", { day: "2-digit", month: "2-digit", year: "numeric" }).format(new Date(year, month - 1, day)); }
function formatDateTime(value?: string | null) { if (!value) return "Sin registro"; const date = new Date(value); return Number.isNaN(date.getTime()) ? "Sin registro" : new Intl.DateTimeFormat("es-CO", { dateStyle: "medium", timeStyle: "short" }).format(date); }
