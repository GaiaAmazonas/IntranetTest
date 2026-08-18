"use client";

import { useEffect, useMemo, useState } from "react";
import { Check, ChevronDown, Copy, Edit3, Eye, EyeOff, KeyRound, LoaderCircle, Plus, Search, ShieldCheck, UsersRound } from "lucide-react";
import { apiRequest } from "@/lib/api-client";
import { applyVisiblePermissions, permissionDelta } from "@/lib/security-admin-rules";
import { useFeedback } from "./feedback";
import { ConfirmDialog, FormDialog } from "./form-dialog";
import { Badge, Button, EmptyState } from "./ui";
import type { SecurityRole } from "./security-users-view";

export type SecurityPermission = { id: string; code: string; name: string; action: string; moduleId: string; isActive: boolean };
export type SecurityModule = { id: string; code: string; name: string; description?: string | null; type: string; parentId?: string | null; route?: string | null; icon?: string | null; order: number; visible: boolean; supportsVisibility: boolean; isActive: boolean };
type RoleForm = { id?: string; code: string; name: string; description: string; isActive: boolean; duplicate?: boolean };

export function SecurityRolesView({ roles, permissions, modules, onReload }: { roles: SecurityRole[]; permissions: SecurityPermission[]; modules: SecurityModule[]; onReload: () => Promise<void> }) {
  const feedback = useFeedback();
  const [roleQuery, setRoleQuery] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(roles[0]?.id ?? null);
  const [permissionQuery, setPermissionQuery] = useState("");
  const [moduleFilter, setModuleFilter] = useState("");
  const [assignedOnly, setAssignedOnly] = useState(false);
  const [technical, setTechnical] = useState(false);
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());
  const [draft, setDraft] = useState<Set<string> | null>(null);
  const [draftRoleId, setDraftRoleId] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [confirmPermissions, setConfirmPermissions] = useState(false);
  const [bulkChange, setBulkChange] = useState<boolean | null>(null);
  const [pendingRoleId, setPendingRoleId] = useState<string | null>(null);
  const [roleForm, setRoleForm] = useState<RoleForm | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [stateRole, setStateRole] = useState<SecurityRole | null>(null);

  const selected = roles.find(role => role.id === selectedId) ?? roles[0] ?? null;
  const selectedCodes = useMemo(() => selected && draftRoleId === selected.id && draft ? draft : new Set(selected?.permissions ?? []), [draft, draftRoleId, selected]);
  const dirty = Boolean(selected && draftRoleId === selected.id && draft && !setsEqual(draft, new Set(selected.permissions)));
  const { added: addedCount, removed: removedCount } = permissionDelta(selected?.permissions ?? [], selectedCodes);
  const moduleMap = useMemo(() => new Map(modules.map(module => [module.id, module])), [modules]);
  const rootModules = useMemo(() => modules.filter(module => !module.parentId).sort((a, b) => a.order - b.order), [modules]);
  const filteredRoles = roles.filter(role => `${role.name} ${role.code} ${role.description ?? ""}`.toLocaleLowerCase("es").includes(roleQuery.trim().toLocaleLowerCase("es")));
  const grouped = useMemo(() => {
    const normalized = permissionQuery.trim().toLocaleLowerCase("es");
    const result = new Map<string, Map<string, SecurityPermission[]>>();
    for (const permission of permissions) {
      if (!permission.isActive || (assignedOnly && !selectedCodes.has(permission.code))) continue;
      const direct = moduleMap.get(permission.moduleId); if (!direct) continue;
      const root = findRoot(direct, moduleMap);
      if (moduleFilter && root.id !== moduleFilter) continue;
      if (normalized && !`${permission.name} ${permission.action} ${permission.code} ${direct.name} ${root.name}`.toLocaleLowerCase("es").includes(normalized)) continue;
      if (!result.has(root.id)) result.set(root.id, new Map());
      const children = result.get(root.id)!; if (!children.has(direct.id)) children.set(direct.id, []); children.get(direct.id)!.push(permission);
    }
    return [...result.entries()].sort(([a], [b]) => (moduleMap.get(a)?.order ?? 0) - (moduleMap.get(b)?.order ?? 0));
  }, [assignedOnly, moduleFilter, moduleMap, permissionQuery, permissions, selectedCodes]);

  function chooseRole(id: string) { if (dirty) { setPendingRoleId(id); return; } switchRole(id); }
  function switchRole(id: string) { setSelectedId(id); setDraft(null); setDraftRoleId(null); setPermissionQuery(""); setModuleFilter(""); setAssignedOnly(false); setTechnical(false); setPendingRoleId(null); }
  function togglePermission(permission: SecurityPermission) { if (!selected) return; const next = new Set(selectedCodes); if (next.has(permission.code)) next.delete(permission.code); else next.add(permission.code); setDraft(next); setDraftRoleId(selected.id); }
  function selectVisible(enable: boolean) { if (!selected) return; const visible = grouped.flatMap(([, group]) => [...group.values()].flat()).map(permission => permission.code); setDraft(applyVisiblePermissions(selectedCodes, visible, enable)); setDraftRoleId(selected.id); setBulkChange(null); }
  function discardDraft() { setDraft(null); setDraftRoleId(null); }
  useEffect(() => { if (!dirty) return; const warn = (event: BeforeUnloadEvent) => { event.preventDefault(); }; addEventListener("beforeunload", warn); return () => removeEventListener("beforeunload", warn); }, [dirty]);

  async function savePermissions() {
    if (!selected || !draft) return; setSaving(true);
    try {
      const permissionIds = permissions.filter(permission => draft.has(permission.code)).map(permission => permission.id);
      await apiRequest(`/api/security/roles/${selected.id}/permissions`, { method: "PUT", body: JSON.stringify({ permissionIds }) });
      feedback.notify({ tone: "success", title: "Permisos actualizados", description: `El rol ${selected.name} quedó con ${permissionIds.length} permisos explícitos.` });
      setConfirmPermissions(false); setDraft(null); setDraftRoleId(null); await onReload();
    } catch (reason) { feedback.notify({ tone: "error", title: "No fue posible actualizar permisos", description: reason instanceof Error ? reason.message : undefined }); }
    finally { setSaving(false); }
  }

  async function saveRole() {
    if (!roleForm) return; setSaving(true); setFormError(null);
    try {
      const body = JSON.stringify({ code: roleForm.code, name: roleForm.name, description: roleForm.description || null, isActive: roleForm.isActive });
      await apiRequest(roleForm.id ? `/api/security/roles/${roleForm.id}` : "/api/security/roles", { method: roleForm.id ? "PUT" : "POST", body });
      feedback.notify({ tone: "success", title: roleForm.id ? "Rol actualizado" : "Rol creado", description: `${roleForm.name} quedó disponible en el catálogo.` });
      setRoleForm(null); await onReload();
    } catch (reason) { setFormError(reason instanceof Error ? reason.message : "No fue posible guardar el rol."); }
    finally { setSaving(false); }
  }

  async function toggleRoleState() {
    if (!stateRole || stateRole.isSystem) return; setSaving(true);
    try {
      await apiRequest(`/api/security/roles/${stateRole.id}`, { method: "PUT", body: JSON.stringify({ code: stateRole.code, name: stateRole.name, description: stateRole.description, isActive: !stateRole.isActive }) });
      feedback.notify({ tone: "success", title: stateRole.isActive ? "Rol inactivado" : "Rol activado" }); setStateRole(null); await onReload();
    } catch (reason) { feedback.notify({ tone: "error", title: "No fue posible cambiar el estado", description: reason instanceof Error ? reason.message : undefined }); }
    finally { setSaving(false); }
  }

  return <div className="gaia-roles-workspace">
    <aside className="gaia-role-master" aria-label="Catálogo de roles">
      <div className="gaia-role-master-head"><label><Search size={16} /><span className="sr-only">Buscar rol</span><input onChange={event => setRoleQuery(event.target.value)} placeholder="Buscar rol…" type="search" value={roleQuery} /></label><Button onClick={() => setRoleForm({ code: "", name: "", description: "", isActive: true })}><Plus size={16} />Nuevo rol</Button></div>
      <div className="gaia-role-list">{filteredRoles.map(role => <button aria-current={selected?.id === role.id ? "true" : undefined} className={selected?.id === role.id ? "is-selected" : ""} key={role.id} onClick={() => chooseRole(role.id)} type="button"><span><strong>{role.name}</strong><small>{role.description || "Sin descripción"}</small></span><Badge tone={role.isActive ? "success" : "danger"}>{role.isActive ? "Activo" : "Inactivo"}</Badge><span className="gaia-role-list-meta"><UsersRound size={13} />{role.assignedUsers} usuario{role.assignedUsers === 1 ? "" : "s"}<i>·</i>{role.permissions.length} permisos</span></button>)}</div>
      {!filteredRoles.length && <EmptyState title="No se encontraron roles" description="Ajusta el término de búsqueda." />}
    </aside>

    <section className="gaia-role-detail" aria-live="polite">
      {!selected ? <EmptyState title="No hay roles configurados" description="Crea el primer rol para comenzar." /> : <>
        <header className="gaia-role-detail-head"><div><div><ShieldCheck size={22} /><Badge tone={selected.isActive ? "success" : "danger"}>{selected.isActive ? "Activo" : "Inactivo"}</Badge>{selected.isSystem && <Badge>Rol del sistema</Badge>}</div><h2>{selected.name}</h2><p>{selected.description || "Sin descripción funcional."}</p><span><UsersRound size={14} />{selected.assignedUsers} usuario{selected.assignedUsers === 1 ? "" : "s"} con asignación vigente</span></div><div className="gaia-role-actions">{!selected.isSystem && <Button onClick={() => setRoleForm({ id: selected.id, code: selected.code, name: selected.name, description: selected.description ?? "", isActive: selected.isActive })} variant="secondary"><Edit3 size={15} />Editar</Button>}<Button onClick={() => setRoleForm({ code: uniqueCopyCode(selected.code, roles), name: `${selected.name} (copia)`, description: selected.description ?? "", isActive: true, duplicate: true })} variant="secondary"><Copy size={15} />Duplicar</Button>{!selected.isSystem && <Button onClick={() => setStateRole(selected)} variant="secondary">{selected.isActive ? <EyeOff size={15} /> : <Eye size={15} />}{selected.isActive ? "Inactivar" : "Activar"}</Button>}</div></header>

        <div className="gaia-permission-toolbar"><label className="gaia-permission-search"><Search size={16} /><span className="sr-only">Buscar permisos</span><input onChange={event => setPermissionQuery(event.target.value)} placeholder="Buscar funcionalidad o acción…" type="search" value={permissionQuery} /></label><select aria-label="Filtrar por módulo" onChange={event => setModuleFilter(event.target.value)} value={moduleFilter}><option value="">Todos los módulos</option>{rootModules.map(module => <option key={module.id} value={module.id}>{module.name}</option>)}</select><label className="gaia-assigned-only"><input checked={assignedOnly} onChange={event => setAssignedOnly(event.target.checked)} type="checkbox" />Solo asignados</label><button aria-expanded={technical} className="gaia-technical-toggle" onClick={() => setTechnical(value => !value)} type="button">{technical ? "Ocultar" : "Ver"} códigos técnicos</button></div>
        <div className="gaia-permission-summary"><span><strong>{selectedCodes.size}</strong> de {permissions.filter(permission => permission.isActive).length} permisos · <b>{addedCount} agregados</b> · <b>{removedCount} retirados</b></span><div><Button onClick={() => setBulkChange(true)} variant="secondary">Seleccionar visibles</Button><Button onClick={() => setBulkChange(false)} variant="secondary">Quitar visibles</Button><Button disabled={!dirty || saving} onClick={discardDraft} variant="secondary">Descartar</Button><Button disabled={!dirty || saving} onClick={() => setConfirmPermissions(true)}>{saving ? <LoaderCircle className="gaia-spin" size={16} /> : <Check size={16} />}Publicar cambios</Button></div></div>

        <div className="gaia-permission-tree">{grouped.map(([rootId, children]) => {
          const root = moduleMap.get(rootId)!; const rows = [...children.values()].flat();
          const selectedCount = rows.filter(permission => selectedCodes.has(permission.code)).length; const closed = collapsed.has(rootId);
          return <section key={rootId}><button aria-expanded={!closed} className="gaia-permission-root" onClick={() => setCollapsed(current => { const next = new Set(current); if (next.has(rootId)) next.delete(rootId); else next.add(rootId); return next; })} type="button"><ChevronDown className={closed ? "is-closed" : ""} size={18} /><span><strong>{root.name}</strong><small>{root.description}</small></span><Badge tone={selectedCount ? "success" : "neutral"}>{selectedCount}/{rows.length}</Badge></button>{!closed && <div className="gaia-permission-children">{[...children.entries()].sort(([a], [b]) => (moduleMap.get(a)?.order ?? 0) - (moduleMap.get(b)?.order ?? 0)).map(([moduleId, modulePermissions]) => {
            const featureModule = moduleMap.get(moduleId)!;
            const featureClosed = collapsed.has(moduleId); return <article key={moduleId}><button aria-expanded={!featureClosed} className="gaia-permission-feature-toggle" onClick={() => setCollapsed(current => { const next = new Set(current); if (next.has(moduleId)) next.delete(moduleId); else next.add(moduleId); return next; })} type="button"><ChevronDown className={featureClosed ? "is-closed" : ""} size={16} /><span><strong>{featureModule.name}</strong>{featureModule.id !== root.id && <small>{featureModule.type}</small>}</span><Badge>{modulePermissions.filter(permission => selectedCodes.has(permission.code)).length}/{modulePermissions.length}</Badge></button>{!featureClosed && <div className="gaia-permission-actions">{modulePermissions.map(permission => <label className={selectedCodes.has(permission.code) ? "is-checked" : ""} key={permission.id}><input checked={selectedCodes.has(permission.code)} onChange={() => togglePermission(permission)} type="checkbox" /><span><strong>{actionLabel(permission.action)}</strong><small>{functionalPermissionName(permission)}</small>{technical && <code>{permission.code}</code>}</span></label>)}</div>}</article>;
          })}</div>}</section>;
        })}</div>
        {!grouped.length && <EmptyState title="No hay permisos para mostrar" description="Ajusta la búsqueda o los filtros seleccionados." />}
      </>}
    </section>

    <FormDialog error={formError} formId="security-role-form" loading={saving} onClose={() => setRoleForm(null)} open={Boolean(roleForm)} submitLabel={roleForm?.id ? "Guardar cambios" : "Crear rol"} subtitle="Los códigos se generan como identificadores técnicos estables." title={roleForm?.id ? "Editar rol" : roleForm?.duplicate ? "Duplicar rol" : "Nuevo rol"}>{roleForm && <form className="gaia-role-form" id="security-role-form" onSubmit={event => { event.preventDefault(); void saveRole(); }}><label><span>Nombre funcional</span><input maxLength={100} onChange={event => { const name = event.target.value; setRoleForm(current => current ? { ...current, name, code: current.id ? current.code : roleCode(name) } : current); }} required value={roleForm.name} /></label><label><span>Descripción</span><textarea maxLength={500} onChange={event => setRoleForm(current => current ? { ...current, description: event.target.value } : current)} rows={4} value={roleForm.description} /></label><div className="gaia-role-code-preview"><KeyRound size={15} /><span>Identificador técnico</span><code>{roleForm.code || "Se generará a partir del nombre"}</code></div></form>}</FormDialog>
    <ConfirmDialog confirmLabel="Guardar permisos" description={selected ? `Se reemplazará la configuración explícita de permisos del rol ${selected.name}. No se aplica ninguna herencia implícita.` : ""} loading={saving} onCancel={() => setConfirmPermissions(false)} onConfirm={() => void savePermissions()} open={confirmPermissions} title="¿Confirmar cambios de permisos?" />
    <ConfirmDialog confirmLabel="Descartar y cambiar de rol" description={`Hay ${addedCount} permiso(s) agregado(s) y ${removedCount} retirado(s) sin publicar. Si continúas, estos cambios se descartarán.`} destructive onCancel={() => setPendingRoleId(null)} onConfirm={() => pendingRoleId && switchRole(pendingRoleId)} open={Boolean(pendingRoleId)} title="¿Salir sin publicar los cambios?" />
    <ConfirmDialog confirmLabel={bulkChange ? "Asignar permisos visibles" : "Quitar permisos visibles"} description={selected && bulkChange !== null ? `${bulkChange ? "Se asignarán" : "Se retirarán"} ${grouped.flatMap(([, group]) => [...group.values()].flat()).length} permisos visibles al rol ${selected.name}${moduleFilter ? ` dentro del módulo ${moduleMap.get(moduleFilter)?.name ?? "seleccionado"}` : ""}. ${bulkChange ? "Los permisos ya seleccionados se conservarán." : "Esta acción puede afectar el acceso de sus usuarios."}` : ""} destructive={bulkChange === false} onCancel={() => setBulkChange(null)} onConfirm={() => selectVisible(Boolean(bulkChange))} open={bulkChange !== null} title={bulkChange ? "Asignar permisos visibles" : "Quitar permisos visibles"} />
    <ConfirmDialog confirmLabel={stateRole?.isActive ? "Sí, inactivar" : "Sí, activar"} description={stateRole ? `${stateRole.name} ${stateRole.isActive ? "dejará de otorgarse como rol activo" : "volverá a estar disponible"}. Las asignaciones históricas se conservarán.` : ""} destructive={Boolean(stateRole?.isActive)} loading={saving} onCancel={() => setStateRole(null)} onConfirm={() => void toggleRoleState()} open={Boolean(stateRole)} title={stateRole?.isActive ? "¿Inactivar este rol?" : "¿Activar este rol?"} />
  </div>;
}

function findRoot(module: SecurityModule, modules: Map<string, SecurityModule>) { let current = module; const visited = new Set<string>(); while (current.parentId && modules.has(current.parentId) && !visited.has(current.id)) { visited.add(current.id); current = modules.get(current.parentId)!; } return current; }
function setsEqual(left: Set<string>, right: Set<string>) { return left.size === right.size && [...left].every(value => right.has(value)); }
function actionLabel(action: string) { const labels: Record<string, string> = { VER: "Consultar", CONSULTAR: "Consultar", CREAR: "Crear", ACTUALIZAR: "Actualizar", ACTIVAR: "Activar", INACTIVAR: "Inactivar", ELIMINAR: "Eliminar", EXPORTAR: "Exportar", ADMINISTRAR: "Administrar" }; return labels[action.toUpperCase()] ?? action; }
function functionalPermissionName(permission: SecurityPermission) { const separator = permission.name.indexOf("·"); return separator >= 0 ? permission.name.slice(separator + 1).trim() : permission.name; }
function roleCode(name: string) { return name.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toUpperCase().replace(/[^A-Z0-9]+/g, "_").replace(/^_|_$/g, "").slice(0, 30); }
function uniqueCopyCode(source: string, roles: SecurityRole[]) { const used = new Set(roles.map(role => role.code.toUpperCase())); const base = `${source.slice(0, 24)}_COPIA`; let code = base; let index = 2; while (used.has(code)) code = `${base.slice(0, 27)}_${index++}`.slice(0, 30); return code; }
