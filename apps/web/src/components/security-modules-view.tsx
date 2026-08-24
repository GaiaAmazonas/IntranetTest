"use client";

import { useMemo, useState } from "react";
import type React from "react";
import { Boxes, ChevronDown, ChevronRight, Edit3, Eye, EyeOff, FolderTree, FunctionSquare, LayoutGrid, Plus, Route, Search, Settings2 } from "lucide-react";
import { apiRequest } from "@/lib/api-client";
import { useFeedback } from "./feedback";
import { ConfirmDialog, FormDialog } from "./form-dialog";
import { Badge, Button, EmptyState } from "./ui";
import type { SecurityModule } from "./security-roles-view";

type ModuleForm = { id?: string; code: string; name: string; description: string; type: string; parentId: string; route: string; icon: string; order: number; visible: boolean; isActive: boolean };

export function SecurityModulesView({ modules, onReload }: { modules: SecurityModule[]; onReload: () => Promise<void> }) {
  const feedback = useFeedback();
  const [query, setQuery] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(modules[0]?.id ?? null);
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set(modules.filter(item => !item.parentId).map(item => item.id)));
  const [technical, setTechnical] = useState(false);
  const [form, setForm] = useState<ModuleForm | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [stateItem, setStateItem] = useState<SecurityModule | null>(null);
  const byId = useMemo(() => new Map(modules.map(item => [item.id, item])), [modules]);
  const children = useMemo(() => {
    const result = new Map<string | null, SecurityModule[]>();
    for (const item of modules) { const key = item.parentId ?? null; if (!result.has(key)) result.set(key, []); result.get(key)!.push(item); }
    for (const rows of result.values()) rows.sort((a, b) => a.order - b.order || a.name.localeCompare(b.name, "es"));
    return result;
  }, [modules]);
  const selected = modules.find(item => item.id === selectedId) ?? modules[0] ?? null;
  const normalizedQuery = query.trim().toLocaleLowerCase("es");
  const visibleIds = useMemo(() => {
    if (!normalizedQuery) return null;
    const ids = new Set<string>();
    for (const item of modules) if (`${item.name} ${item.description ?? ""} ${item.code} ${item.route ?? ""}`.toLocaleLowerCase("es").includes(normalizedQuery)) {
      let current: SecurityModule | undefined = item; while (current) { ids.add(current.id); current = current.parentId ? byId.get(current.parentId) : undefined; }
    }
    return ids;
  }, [byId, modules, normalizedQuery]);
  const types = [...new Set(modules.map(item => item.type).filter(Boolean))];
  const descendants = selected ? collectDescendants(selected.id, children) : new Set<string>();
  const availableParents = modules.filter(item => item.id !== selected?.id && !descendants.has(item.id) && item.isActive && !normalize(item.type).includes("FUNCIONALIDAD"));

  function openNew(parent?: SecurityModule) {
    if (parent && normalize(parent.type).includes("FUNCIONALIDAD")) { feedback.notify({ tone: "info", title: "La funcionalidad es un nivel final", description: "Selecciona un módulo o submódulo para agregar un elemento hijo." }); return; }
    const type = parent ? (isRootType(parent.type) ? findType(types, "SUBMÓDULO") : findType(types, "FUNCIONALIDAD")) : findType(types, "MÓDULO");
    setForm({ code: "", name: "", description: "", type, parentId: parent?.id ?? "", route: "", icon: "", order: nextOrder(parent?.id ?? null, children), visible: true, isActive: true }); setFormError(null);
  }
  function openEdit(item: SecurityModule) { setForm({ id: item.id, code: item.code, name: item.name, description: item.description ?? "", type: item.type, parentId: item.parentId ?? "", route: item.route ?? "", icon: item.icon ?? "", order: item.order, visible: item.visible, isActive: item.isActive }); setFormError(null); }
  function updateForm(change: Partial<ModuleForm>) { setForm(current => current ? { ...current, ...change } : current); }

  async function saveModule() {
    if (!form) return; setSaving(true); setFormError(null);
    try {
      const parent = form.parentId ? byId.get(form.parentId) : undefined;
      const code = form.id ? form.code : generateModuleCode(form.name, parent?.code);
      await apiRequest(form.id ? `/api/security/modules/${form.id}` : "/api/security/modules", { method: form.id ? "PUT" : "POST", body: JSON.stringify({ code, name: form.name, description: form.description || null, type: form.type, parentId: form.parentId || null, route: form.route || null, icon: form.icon || null, order: form.order, visible: form.visible, isActive: form.isActive }) });
      feedback.notify({ tone: "success", title: form.id ? "Elemento actualizado" : "Elemento creado", description: `${form.name} quedó registrado en el catálogo de seguridad.` }); setForm(null); await onReload();
    } catch (reason) { setFormError(reason instanceof Error ? reason.message : "No fue posible guardar el elemento."); }
    finally { setSaving(false); }
  }

  async function toggleState() {
    if (!stateItem) return; setSaving(true);
    try {
      await apiRequest(`/api/security/modules/${stateItem.id}`, { method: "PUT", body: JSON.stringify({ code: stateItem.code, name: stateItem.name, description: stateItem.description, type: stateItem.type, parentId: stateItem.parentId, route: stateItem.route, icon: stateItem.icon, order: stateItem.order, visible: stateItem.visible, isActive: !stateItem.isActive }) });
      feedback.notify({ tone: "success", title: stateItem.isActive ? "Elemento inactivado" : "Elemento activado" }); setStateItem(null); await onReload();
    } catch (reason) { feedback.notify({ tone: "error", title: "No fue posible cambiar el estado", description: reason instanceof Error ? reason.message : undefined }); }
    finally { setSaving(false); }
  }

  function renderBranch(item: SecurityModule, depth: number): React.ReactNode {
    if (visibleIds && !visibleIds.has(item.id)) return null;
    const itemChildren = children.get(item.id) ?? []; const open = expanded.has(item.id) || Boolean(normalizedQuery); const active = selected?.id === item.id;
    return <div className="gaia-module-branch" key={item.id}><div className={`gaia-module-tree-row ${active ? "is-selected" : ""} ${!item.isActive ? "is-inactive" : ""}`} style={{ "--tree-depth": depth } as React.CSSProperties}>{itemChildren.length ? <button aria-label={`${open ? "Contraer" : "Expandir"} ${item.name}`} aria-expanded={open} onClick={() => setExpanded(current => { const next = new Set(current); if (next.has(item.id)) next.delete(item.id); else next.add(item.id); return next; })} type="button">{open ? <ChevronDown size={15} /> : <ChevronRight size={15} />}</button> : <span className="gaia-module-tree-spacer" />}<button className="gaia-module-tree-select" onClick={() => { setSelectedId(item.id); setTechnical(false); }} type="button"><ModuleIcon type={item.type} /><span><strong>{item.name}</strong><small>{friendlyType(item.type)}</small></span><Badge tone={item.isActive ? "success" : "danger"}>{item.isActive ? "Activo" : "Inactivo"}</Badge></button></div>{open && itemChildren.map(child => renderBranch(child, depth + 1))}</div>;
  }

  return <div className="gaia-modules-workspace">
    <aside className="gaia-module-tree-panel"><div className="gaia-module-tree-head"><label><Search size={16} /><span className="sr-only">Buscar elementos</span><input onChange={event => setQuery(event.target.value)} placeholder="Buscar en el catálogo…" type="search" value={query} /></label><Button onClick={() => openNew()}><Plus size={16} />Nuevo módulo principal</Button></div><div className="gaia-module-tree" role="tree">{(children.get(null) ?? []).map(item => renderBranch(item, 0))}</div>{visibleIds?.size === 0 && <EmptyState title="Sin resultados" description="No encontramos elementos con ese criterio." />}</aside>
    <section className="gaia-module-detail">{!selected ? <EmptyState title="Catálogo vacío" description="Crea el primer módulo para comenzar." /> : <><header><div className="gaia-module-detail-title"><ModuleIcon type={selected.type} /><div><p>Seguridad · Catálogo de módulos</p><h2>{selected.name}</h2><span>{friendlyType(selected.type)}{selected.parentId && byId.get(selected.parentId) ? ` · Dentro de ${byId.get(selected.parentId)!.name}` : " · Nivel principal"}</span></div></div><Badge tone={selected.isActive ? "success" : "danger"}>{selected.isActive ? "Activo" : "Inactivo"}</Badge></header><div className="gaia-module-detail-body"><section><h3>Información funcional</h3><p>{selected.description || "Este elemento todavía no tiene una descripción funcional."}</p><dl className="gaia-module-facts"><div><dt><FolderTree size={14} />Ubicación</dt><dd>{breadcrumb(selected, byId)}</dd></div><div><dt><Route size={14} />Destino</dt><dd>{selected.route || "Sin ruta; recurso interno de autorización"}</dd></div><div><dt><Eye size={14} />Navegación</dt><dd>{selected.visible ? "Visible cuando el usuario tiene acceso" : "Oculto en navegación"}</dd></div><div><dt><Settings2 size={14} />Orden</dt><dd>{selected.order}</dd></div></dl></section><section><div className="gaia-module-action-heading"><div><h3>Administración</h3><p>Los cambios de código no están permitidos porque afectarían las reglas de autorización.</p></div></div><div className="gaia-module-detail-actions"><Button onClick={() => openEdit(selected)} variant="secondary"><Edit3 size={15} />Editar información</Button><Button onClick={() => openNew(selected)} variant="secondary"><Plus size={15} />Agregar elemento hijo</Button><Button onClick={() => setStateItem(selected)} variant="secondary">{selected.isActive ? <EyeOff size={15} /> : <Eye size={15} />}{selected.isActive ? "Inactivar" : "Activar"}</Button></div></section><section><button aria-expanded={technical} className="gaia-module-technical-toggle" onClick={() => setTechnical(value => !value)} type="button"><Settings2 size={15} />{technical ? "Ocultar" : "Mostrar"} información técnica<ChevronRight className={technical ? "is-open" : ""} size={15} /></button>{technical && <dl className="gaia-module-technical"><div><dt>Código estable</dt><dd>{selected.code}</dd></div><div><dt>GUID</dt><dd>{selected.id}</dd></div><div><dt>Tipo Dataverse</dt><dd>{selected.type}</dd></div><div><dt>Icono configurado</dt><dd>{selected.icon || "Sin icono"}</dd></div></dl>}</section></div></>}
    </section>

    <FormDialog error={formError} formId="security-module-form" loading={saving} onClose={() => setForm(null)} open={Boolean(form)} submitLabel={form?.id ? "Guardar cambios" : "Crear elemento"} subtitle="Nombre, jerarquía y descripción funcional." title={form?.id ? "Editar elemento" : "Nuevo elemento del catálogo"}>{form && <form className="gaia-module-form" id="security-module-form" onSubmit={event => { event.preventDefault(); void saveModule(); }}><label><span>Nombre funcional</span><input maxLength={120} onChange={event => updateForm({ name: event.target.value, code: form.id ? form.code : generateModuleCode(event.target.value, form.parentId ? byId.get(form.parentId)?.code : undefined) })} required value={form.name} /></label><label><span>Descripción</span><textarea maxLength={500} onChange={event => updateForm({ description: event.target.value })} rows={3} value={form.description} /></label><div className="gaia-module-form-grid"><label><span>Tipo</span><select onChange={event => { const type = event.target.value; updateForm({ type, parentId: isRootType(type) ? "" : form.parentId }); }} required value={form.type}>{types.map(type => <option key={type} value={type}>{friendlyType(type)}</option>)}</select></label><label><span>Elemento padre</span><select disabled={isRootType(form.type)} onChange={event => { const parentId = event.target.value; updateForm({ parentId, code: form.id ? form.code : generateModuleCode(form.name, parentId ? byId.get(parentId)?.code : undefined), order: nextOrder(parentId || null, children) }); }} required={!isRootType(form.type)} value={form.parentId}><option value="">Nivel principal</option>{availableParents.map(item => <option key={item.id} value={item.id}>{breadcrumb(item, byId)}</option>)}</select></label></div>{modules.some(item => item.supportsVisibility) && <label className="gaia-module-visible"><input checked={form.visible} onChange={event => updateForm({ visible: event.target.checked })} type="checkbox" /><span><strong>Mostrar en menú</strong><small>Puede aparecer en la navegación cuando el usuario tenga permisos. Ocultarlo no modifica autorizaciones.</small></span></label>}</form>}</FormDialog>
    <ConfirmDialog confirmLabel={stateItem?.isActive ? "Sí, inactivar" : "Sí, activar"} description={stateItem ? `${stateItem.name} ${stateItem.isActive ? "dejará de estar disponible en el catálogo activo" : "volverá a estar disponible"}. Sus relaciones y código se conservarán.` : ""} destructive={Boolean(stateItem?.isActive)} loading={saving} onCancel={() => setStateItem(null)} onConfirm={() => void toggleState()} open={Boolean(stateItem)} title={stateItem?.isActive ? "¿Inactivar este elemento?" : "¿Activar este elemento?"} />
  </div>;
}

function ModuleIcon({ type }: { type: string }) { if (isRootType(type)) return <LayoutGrid size={17} />; if (normalize(type).includes("FUNCIONALIDAD")) return <FunctionSquare size={17} />; return <Boxes size={17} />; }
function normalize(value: string) { return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[ _]/g, "").toUpperCase(); }
function isRootType(type: string) { return normalize(type) === "MODULO"; }
function friendlyType(type: string) { const value = normalize(type); if (value === "MODULO") return "Módulo principal"; if (value === "SUBMODULO") return "Submódulo"; if (value === "FUNCIONALIDAD") return "Funcionalidad"; return type; }
function findType(types: string[], desired: string) { return types.find(type => normalize(type) === normalize(desired)) ?? desired; }
function codeSegment(name: string) { return name.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toUpperCase().replace(/[^A-Z0-9]+/g, "_").replace(/^_|_$/g, ""); }
function generateModuleCode(name: string, parentCode?: string) { const segment = codeSegment(name); return parentCode ? `${parentCode}.${segment}` : segment; }
function nextOrder(parentId: string | null, children: Map<string | null, SecurityModule[]>) { const rows = children.get(parentId) ?? []; return rows.length ? Math.max(...rows.map(item => item.order)) + 1 : 10; }
function collectDescendants(id: string, children: Map<string | null, SecurityModule[]>) { const result = new Set<string>(); const pending = [...(children.get(id) ?? [])]; while (pending.length) { const item = pending.pop()!; if (result.has(item.id)) continue; result.add(item.id); pending.push(...(children.get(item.id) ?? [])); } return result; }
function breadcrumb(item: SecurityModule, byId: Map<string, SecurityModule>) { const names = [item.name]; const visited = new Set([item.id]); let parent = item.parentId ? byId.get(item.parentId) : undefined; while (parent && !visited.has(parent.id)) { visited.add(parent.id); names.unshift(parent.name); parent = parent.parentId ? byId.get(parent.parentId) : undefined; } return names.join(" › "); }
