"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { BriefcaseBusiness, ChevronDown, ChevronRight, Download, List, Pencil, Search, Users, X } from "lucide-react";
import { Button, IconButton } from "@/components/ui";
import { useFeedback } from "@/components/feedback";
import { useSecurity } from "@/components/security-context";
import { exportOrganizationalAssignments, type AssignmentExportRow } from "@/lib/exports/organizational-assignments-export";
import { isAssignmentCurrent, resolveOrganizationSearchTarget } from "./organizational-assignment-model";

export type AssignmentUnit = { id:string; code:string; name:string; unitTypeName:string; parentId?:string; level:number; visualOrder:number; isActive:boolean };
export type AssignmentPosition = { id:string; name:string; isActive:boolean };
export type OrganizationalAssignmentItem = { id:string; thirdPartyId:string; thirdPartyName:string; documentNumber:string; positionId:string; positionName:string; organizationalUnitId:string; organizationalUnitCode:string; organizationalUnitName:string; startDate?:string; endDate?:string; isPrimary:boolean; observations?:string; isActive:boolean };

type Props = {
  assignments: OrganizationalAssignmentItem[];
  units: AssignmentUnit[];
  positions: AssignmentPosition[];
  loading?: boolean;
  error?: string;
  onEdit: (assignment: OrganizationalAssignmentItem) => void;
};

type ViewMode = "explore" | "list";
type StatusFilter = "all" | "active" | "inactive";

export function OrganizationalAssignmentsExplorer({ assignments, units, positions, loading, error, onEdit }: Props) {
  const [view, setView] = useState<ViewMode>("explore");
  const [selectedUnitId, setSelectedUnitId] = useState(() => units.find(unit => !unit.parentId)?.id ?? units[0]?.id ?? "");
  const [collapsed, setCollapsed] = useState(() => new Set<string>());
  const [includeDescendants, setIncludeDescendants] = useState(false);
  const [search, setSearch] = useState("");
  const [positionId, setPositionId] = useState("");
  const [status, setStatus] = useState<StatusFilter>("all");
  const [currentOnly, setCurrentOnly] = useState(true);
  const [sortBy, setSortBy] = useState<"name"|"position">("name");
  const [groupByPosition, setGroupByPosition] = useState(false);
  const [selectedPersonId, setSelectedPersonId] = useState<string|null>(null);
  const [exporting, setExporting] = useState(false);
  const treeInitialized = useRef(false);
  const { notify } = useFeedback();
  const { can } = useSecurity();

  const byId = useMemo(() => new Map(units.map(unit => [unit.id, unit])), [units]);
  const activeUnitId = selectedUnitId;
  const children = useMemo(() => {
    const result = new Map<string|undefined, AssignmentUnit[]>();
    const ids = new Set(units.map(unit => unit.id));
    units.forEach(unit => {
      const parentId = unit.parentId && ids.has(unit.parentId) ? unit.parentId : undefined;
      result.set(parentId, [...(result.get(parentId) ?? []), unit]);
    });
    result.forEach(rows => rows.sort((a,b) => a.visualOrder-b.visualOrder || a.name.localeCompare(b.name,"es")));
    return result;
  }, [units]);
  const descendantIds = useMemo(() => descendantsOf(activeUnitId, children), [activeUnitId, children]);
  const scopeIds = useMemo(() => new Set([activeUnitId, ...(includeDescendants ? descendantIds : [])]), [activeUnitId, descendantIds, includeDescendants]);
  const selectedUnit = byId.get(activeUnitId) ?? units[0];
  const countByUnit = useMemo(() => {
    const counts = new Map<string, Set<string>>();
    assignments.filter(row => isAssignmentCurrent(row)).forEach(row => {
      if (!counts.has(row.organizationalUnitId)) counts.set(row.organizationalUnitId, new Set());
      counts.get(row.organizationalUnitId)!.add(row.thirdPartyId);
    });
    return counts;
  }, [assignments]);
  const filtered = useMemo(() => {
    const term = normalize(search);
    return assignments.filter(row => {
      if (activeUnitId && !scopeIds.has(row.organizationalUnitId)) return false;
      if (positionId && row.positionId !== positionId) return false;
      if (status === "active" && !row.isActive) return false;
      if (status === "inactive" && row.isActive) return false;
      if (currentOnly && !isAssignmentCurrent(row)) return false;
      return !term || [row.thirdPartyName,row.positionName,row.organizationalUnitName,row.organizationalUnitCode].some(value => normalize(value).includes(term));
    }).sort((a,b) => sortBy === "position" ? a.positionName.localeCompare(b.positionName,"es") || a.thirdPartyName.localeCompare(b.thirdPartyName,"es") : a.thirdPartyName.localeCompare(b.thirdPartyName,"es"));
  }, [activeUnitId,assignments,currentOnly,positionId,scopeIds,search,sortBy,status]);
  const selectedPersonAssignments = useMemo(() => assignments.filter(row => row.thirdPartyId === selectedPersonId).sort((a,b) => (b.startDate??"").localeCompare(a.startDate??"")), [assignments,selectedPersonId]);
  const selectedAssignment = selectedPersonAssignments.find(row => isAssignmentCurrent(row)) ?? selectedPersonAssignments[0];
  const groups = useMemo(() => groupByPosition ? groupAssignments(filtered) : [["",filtered] as [string,OrganizationalAssignmentItem[]]], [filtered,groupByPosition]);
  const canEdit = can("ORG.ASIGNACIONES.ACTUALIZAR");

  useEffect(() => {
    if (treeInitialized.current || !units.length) return;
    const parentIds = new Set(units.map(unit => unit.parentId).filter((id): id is string => Boolean(id)));
    setCollapsed(parentIds);
    treeInitialized.current = true;
  }, [units]);

  function searchOrganization(value: string) {
    setSearch(value);
    const term = normalize(value.trim());
    if (!term) return;
    const targetUnitId = resolveOrganizationSearchTarget(term, units, assignments);
    if (!targetUnitId) return;
    setSelectedUnitId(targetUnitId);
    setCollapsed(current => {
      const next = new Set(current);
      let unit = byId.get(targetUnitId);
      while (unit) {
        next.delete(unit.id);
        unit = unit.parentId ? byId.get(unit.parentId) : undefined;
      }
      return next;
    });
  }

  async function exportRows() {
    setExporting(true);
    try {
      await exportOrganizationalAssignments(filtered.map(row => toExportRow(row, byId)));
      notify({tone:"success",title:"Excel generado correctamente",description:`${filtered.length} asignaciones exportadas con los filtros actuales.`});
    } catch (caught) {
      notify({tone:"error",title:"No fue posible generar el Excel",description:caught instanceof Error?caught.message:undefined});
    } finally { setExporting(false); }
  }

  if (loading) return <EmptyState title="Cargando asignaciones…" detail="Estamos preparando la estructura y sus colaboradores."/>;
  if (error) return <EmptyState title="No fue posible cargar las asignaciones" detail={error}/>;

  return <div className="mt-6">
    <div className="flex flex-col gap-3 border-b border-[#e2e9df] pb-4 sm:flex-row sm:items-center sm:justify-between">
      <div className="gaia-tabs w-full overflow-x-auto sm:w-auto" role="tablist" aria-label="Vista de asignaciones">
        <button aria-selected={view==="explore"} className="gaia-tab" onClick={()=>setView("explore")} role="tab" type="button"><Users size={15}/>Explorar organización</button>
        <button aria-selected={view==="list"} className="gaia-tab" onClick={()=>{setView("list");setSelectedUnitId("");}} role="tab" type="button"><List size={15}/>Listado</button>
      </div>
      {can("ORG.ASIGNACIONES.EXPORTAR") && <Button className="w-full justify-center sm:w-auto" disabled={exporting||!filtered.length} onClick={()=>void exportRows()} variant="secondary"><Download size={16}/>{exporting?"Generando…":"Exportar a Excel"}</Button>}
    </div>
    <AssignmentFilters currentOnly={currentOnly} onCurrentOnly={setCurrentOnly} onPosition={setPositionId} onSearch={searchOrganization} onSort={setSortBy} onStatus={setStatus} positionId={positionId} positions={positions} search={search} sortBy={sortBy} status={status}/>

    {view === "explore" ? <div className="mt-4 grid min-h-[610px] overflow-hidden rounded-2xl border border-[#dfe7dc] bg-white lg:grid-cols-[300px_minmax(0,1fr)]">
      <aside className="border-b border-[#dfe7dc] bg-[#f7f9f6] lg:border-b-0 lg:border-r">
        <div className="border-b border-[#dfe7dc] px-4 py-3"><p className="text-[10px] font-bold uppercase tracking-[.1em] text-[#66804e]">Unidades organizacionales</p><p className="mt-1 text-[10px] text-[#7b887f]">Selecciona una unidad para explorar su equipo.</p></div>
        <div className="max-h-[540px] overflow-y-auto p-2" role="tree"><button className={`mb-1 flex min-h-11 w-full items-center gap-2 rounded-xl px-3 text-left text-[12px] font-bold ${!activeUnitId?"bg-[var(--gaia-accent-soft)] text-[var(--gaia-green-800)] shadow-sm ring-1 ring-[var(--gaia-line-strong)]":"text-[var(--gaia-ink-700)] hover:bg-[var(--gaia-accent-pale)]"}`} onClick={()=>setSelectedUnitId("")} type="button"><Users size={15}/>Toda la organización</button>{treeRows(children, collapsed).map(({unit,depth,hasChildren}) => <div className={`flex min-h-11 items-center rounded-xl ${unit.id===activeUnitId?"bg-[var(--gaia-accent-soft)] shadow-sm ring-1 ring-[var(--gaia-line-strong)]":"hover:bg-[var(--gaia-accent-pale)]"}`} key={unit.id} style={{paddingLeft:`${8+depth*15}px`}}>
          {hasChildren?<button aria-label={`${collapsed.has(unit.id)?"Expandir":"Contraer"} ${unit.name}`} className="grid h-8 w-7 place-items-center text-[#6e7d74]" onClick={()=>setCollapsed(current=>{const next=new Set(current);if(next.has(unit.id))next.delete(unit.id);else next.add(unit.id);return next;})} type="button">{collapsed.has(unit.id)?<ChevronRight size={14}/>:<ChevronDown size={14}/>}</button>:<span className="w-7"/>}
          <button className="min-w-0 flex-1 py-2 pr-2 text-left" onClick={()=>setSelectedUnitId(unit.id)} type="button"><strong className="block truncate text-[12px] text-[var(--gaia-ink-900)]">{unit.name}</strong><small className="mt-0.5 flex items-center gap-2 text-[9px] text-[var(--gaia-ink-500)]"><span>{unit.code}</span><span>{countByUnit.get(unit.id)?.size??0} personas</span></small></button>
        </div>)}</div>
      </aside>
      <section className="min-w-0 p-4 sm:p-6">
        {selectedUnit ? <>
          <header className="flex flex-wrap items-start justify-between gap-4 border-b border-[#e5ebe3] pb-5"><div><p className="text-[10px] font-bold uppercase tracking-[.12em] text-[#66804e]">{selectedUnit.unitTypeName}</p><h2 className="mt-1 text-2xl font-bold tracking-tight text-[#153729]">{selectedUnit.name}</h2><p className="mt-1 text-xs text-[#718078]">Código {selectedUnit.code} · Superior: {selectedUnit.parentId?byId.get(selectedUnit.parentId)?.name??"Sin información":"Unidad raíz"}</p></div><label className="flex items-center gap-2 rounded-xl bg-[#f0f5ee] px-3 py-2 text-xs font-semibold text-[#315342]"><input checked={includeDescendants} onChange={event=>setIncludeDescendants(event.target.checked)} type="checkbox"/>Incluir descendientes</label></header>
          <div className="my-4 grid gap-3 sm:grid-cols-3"><Metric label="Colaboradores" value={new Set(filtered.map(row=>row.thirdPartyId)).size}/><Metric label="Cargos ocupados" value={new Set(filtered.map(row=>row.positionId)).size}/><Metric label="Asignaciones" value={filtered.length}/></div>
          <div className="mb-3 flex justify-end"><label className="flex items-center gap-2 text-xs text-[#64756c]"><input checked={groupByPosition} onChange={event=>setGroupByPosition(event.target.checked)} type="checkbox"/>Agrupar por cargo</label></div>
          {!filtered.length?<EmptyState title="Unidad sin colaboradores" detail="No hay asignaciones que coincidan con los filtros seleccionados."/>:<div className="space-y-5">{groups.map(([label,rows])=><section key={label||"all"}>{label&&<h3 className="mb-2 flex items-center gap-2 text-sm font-bold text-[#244636]"><BriefcaseBusiness size={15}/>{label}<span className="rounded-full bg-[#e9f1e5] px-2 py-0.5 text-[10px]">{rows.length}</span></h3>}<div className="grid gap-2 xl:grid-cols-2">{rows.map(row=><PersonRow key={row.id} onClick={()=>setSelectedPersonId(row.thirdPartyId)} row={row}/>)}</div></section>)}</div>}
        </>:<><header className="flex flex-wrap items-start justify-between gap-4 border-b border-[#e5ebe3] pb-5"><div><p className="text-[10px] font-bold uppercase tracking-[.12em] text-[#66804e]">Vista consolidada</p><h2 className="mt-1 text-2xl font-bold tracking-tight text-[#153729]">Toda la organización</h2><p className="mt-1 text-xs text-[#718078]">Incluye todas las unidades raíz y sus descendientes.</p></div></header><div className="my-4 grid gap-3 sm:grid-cols-3"><Metric label="Colaboradores" value={new Set(filtered.map(row=>row.thirdPartyId)).size}/><Metric label="Cargos ocupados" value={new Set(filtered.map(row=>row.positionId)).size}/><Metric label="Asignaciones" value={filtered.length}/></div>{!filtered.length?<EmptyState title="Sin asignaciones" detail="No hay registros que coincidan con los filtros seleccionados."/>:<div className="grid gap-2 xl:grid-cols-2">{filtered.map(row=><PersonRow key={row.id} onClick={()=>setSelectedPersonId(row.thirdPartyId)} row={row}/>)}</div>}</>}
      </section>
    </div> : <AssignmentList assignments={filtered} onEdit={onEdit} onSelect={setSelectedPersonId}/>}

    {selectedAssignment && <PersonDetail assignments={selectedPersonAssignments} canEdit={canEdit} onClose={()=>setSelectedPersonId(null)} onEdit={onEdit} selected={selectedAssignment}/>}
  </div>;
}

function AssignmentFilters(props:{search:string;onSearch:(v:string)=>void;positionId:string;onPosition:(v:string)=>void;positions:AssignmentPosition[];status:StatusFilter;onStatus:(v:StatusFilter)=>void;currentOnly:boolean;onCurrentOnly:(v:boolean)=>void;sortBy:"name"|"position";onSort:(v:"name"|"position")=>void}) {
  return <div className="mt-4 grid min-w-0 gap-3 rounded-2xl bg-[#f6f8f5] p-3 sm:grid-cols-2 xl:grid-cols-[minmax(220px,1fr)_minmax(180px,220px)_minmax(145px,170px)_minmax(145px,170px)_auto]">
    <SearchBox label="Buscar persona, cargo o unidad" onChange={props.onSearch} value={props.search}/>
    <select aria-label="Filtrar por cargo" className="h-10 min-w-0 w-full rounded-xl border border-[#d6dfd3] bg-white px-3 text-xs" onChange={e=>props.onPosition(e.target.value)} value={props.positionId}><option value="">Todos los cargos</option>{props.positions.filter(row=>row.isActive).sort((a,b)=>a.name.localeCompare(b.name,"es")).map(row=><option key={row.id} value={row.id}>{row.name}</option>)}</select>
    <select aria-label="Filtrar por estado" className="h-10 min-w-0 w-full rounded-xl border border-[#d6dfd3] bg-white px-3 text-xs" onChange={e=>props.onStatus(e.target.value as StatusFilter)} value={props.status}><option value="all">Todos los estados</option><option value="active">Activos</option><option value="inactive">Inactivos</option></select>
    <select aria-label="Ordenar asignaciones" className="h-10 min-w-0 w-full rounded-xl border border-[#d6dfd3] bg-white px-3 text-xs" onChange={e=>props.onSort(e.target.value as "name"|"position")} value={props.sortBy}><option value="name">Ordenar por nombre</option><option value="position">Ordenar por cargo</option></select>
    <label className="flex min-h-10 items-center gap-2 whitespace-nowrap rounded-xl bg-white px-3 text-xs font-semibold text-[#315342] sm:col-span-2 xl:col-span-1"><input checked={props.currentOnly} onChange={e=>props.onCurrentOnly(e.target.checked)} type="checkbox"/>Solo actuales</label>
  </div>;
}

function AssignmentList({assignments,onEdit,onSelect}:{assignments:OrganizationalAssignmentItem[];onEdit:(row:OrganizationalAssignmentItem)=>void;onSelect:(id:string)=>void}) {
  const {can}=useSecurity();
  const canEdit=can("ORG.ASIGNACIONES.ACTUALIZAR");
  if (!assignments.length) return <div className="mt-4 rounded-2xl border border-[#e1e8df]"><EmptyState title="Sin resultados" detail="Ajusta los filtros para consultar otras asignaciones."/></div>;
  return <>
    <div className="mt-4 grid gap-3 md:hidden">
      {assignments.map(row=><article className={`rounded-2xl border p-4 shadow-sm ${row.isActive?"border-[#dce6d9] bg-white":"border-[#e1e4e1] bg-[#f7f8f7] text-[#6f7d75]"}`} key={row.id}>
        <button className="flex w-full items-start gap-3 text-left" onClick={()=>onSelect(row.thirdPartyId)} type="button">
          <Avatar name={row.thirdPartyName}/>
          <span className="min-w-0 flex-1"><strong className="block text-sm text-[#174b35]">{row.thirdPartyName}</strong><small className="mt-1 block text-[11px] text-[#596d62]">{row.positionName}</small></span>
          <AssignmentStatus row={row}/>
        </button>
        <dl className="mt-4 grid grid-cols-2 gap-3 border-t border-[#e6ece4] pt-3 text-[10px]">
          <div className="col-span-2"><dt className="font-bold uppercase tracking-wide text-[#819087]">Unidad</dt><dd className="mt-1 font-semibold text-[#294b3b]">{row.organizationalUnitName} <span className="font-normal text-[#7b887f]">[{row.organizationalUnitCode}]</span></dd></div>
          <div><dt className="font-bold uppercase tracking-wide text-[#819087]">Desde</dt><dd className="mt-1">{formatDate(row.startDate)}</dd></div>
          <div><dt className="font-bold uppercase tracking-wide text-[#819087]">Hasta</dt><dd className="mt-1">{formatDate(row.endDate)}</dd></div>
        </dl>
        <div className="mt-3 flex items-center justify-between">{row.isPrimary?<span className="rounded-full bg-[#e7f0e3] px-2 py-1 text-[9px] font-bold text-[#386037]">Asignación principal</span>:<span/>}{canEdit&&<Button onClick={()=>onEdit(row)} variant="secondary"><Pencil size={14}/>Editar</Button>}</div>
      </article>)}
    </div>
    <div className="mt-4 hidden overflow-x-auto rounded-2xl border border-[#e1e8df] md:block">
      <table className="w-full min-w-[950px] text-left text-xs"><thead className="bg-[#f5f8f3] text-[10px] uppercase tracking-wider text-[#6b7b72]"><tr><th className="px-4 py-3">Colaborador</th><th className="px-4 py-3">Cargo</th><th className="px-4 py-3">Unidad</th><th className="px-4 py-3">Desde</th><th className="px-4 py-3">Hasta</th><th className="px-4 py-3">Estado</th><th/></tr></thead><tbody>{assignments.map(row=><tr className={`border-t border-[#e8eee6] ${row.isActive?"":"bg-[#fafafa] text-[#7b887f]"}`} key={row.id}><td className="px-4 py-3"><button className="font-semibold text-[#174b35] hover:underline" onClick={()=>onSelect(row.thirdPartyId)}>{row.thirdPartyName}</button>{row.isPrimary&&<span className="ml-2 rounded-full bg-[#e7f0e3] px-2 py-0.5 text-[9px] font-bold text-[#386037]">Principal</span>}</td><td className="px-4 py-3">{row.positionName}</td><td className="px-4 py-3"><strong>{row.organizationalUnitName}</strong><small className="block text-[#7b887f]">{row.organizationalUnitCode}</small></td><td className="px-4 py-3">{formatDate(row.startDate)}</td><td className="px-4 py-3">{formatDate(row.endDate)}</td><td className="px-4 py-3"><AssignmentStatus row={row}/></td><td className="px-4 py-3">{canEdit&&<IconButton label={`Editar asignación de ${row.thirdPartyName}`} onClick={()=>onEdit(row)}><Pencil size={15}/></IconButton>}</td></tr>)}</tbody></table>
    </div>
  </>;
}

function PersonRow({row,onClick}:{row:OrganizationalAssignmentItem;onClick:()=>void}) { return <button className="flex min-w-0 items-center gap-3 rounded-xl border border-[#e1e8df] p-3 text-left transition hover:-translate-y-px hover:border-[#b8cbb1] hover:shadow-sm" onClick={onClick} type="button"><Avatar name={row.thirdPartyName}/><span className="min-w-0 flex-1"><strong className="block truncate text-[12px] text-[#173b2c]">{row.thirdPartyName}</strong><span className="mt-0.5 block truncate text-[10px] text-[#596d62]">{row.positionName}</span><small className="mt-1 block truncate text-[9px] text-[#819087]">{row.organizationalUnitName}</small></span><span className="grid justify-items-end gap-1"><AssignmentStatus row={row}/>{row.isPrimary&&<small className="text-[8px] font-bold uppercase text-[#66804e]">Principal</small>}</span></button> }

function PersonDetail({selected,assignments,canEdit,onClose,onEdit}:{selected:OrganizationalAssignmentItem;assignments:OrganizationalAssignmentItem[];canEdit:boolean;onClose:()=>void;onEdit:(row:OrganizationalAssignmentItem)=>void}) { return <div className="fixed inset-0 z-50 bg-[#0d2b20]/25" onMouseDown={event=>{if(event.target===event.currentTarget)onClose();}}><aside aria-label={`Detalle de ${selected.thirdPartyName}`} className="absolute inset-y-0 right-0 w-full max-w-md overflow-y-auto bg-white p-6 shadow-2xl"><div className="flex justify-end"><IconButton label="Cerrar detalle" onClick={onClose}><X size={18}/></IconButton></div><div className="flex items-center gap-4"><Avatar large name={selected.thirdPartyName}/><div><h2 className="text-xl font-bold text-[#153729]">{selected.thirdPartyName}</h2><p className="mt-1 text-sm text-[#546a5e]">{selected.positionName}</p></div></div><dl className="mt-6 grid grid-cols-2 gap-3 text-xs"><Detail label="Unidad" value={selected.organizationalUnitName}/><Detail label="Estado" value={selected.isActive?"Activo":"Inactivo"}/><Detail label="Desde" value={formatDate(selected.startDate)}/><Detail label="Hasta" value={formatDate(selected.endDate)}/><Detail label="Asignación" value={selected.isPrimary?"Principal":"Secundaria"}/><Detail label="Vigencia" value={isAssignmentCurrent(selected)?"Actual":"Histórica"}/></dl><div className="mt-5 flex gap-2">{canEdit&&<Button onClick={()=>onEdit(selected)} variant="secondary"><Pencil size={15}/>Editar asignación</Button>}<Link className="gaia-button gaia-button-secondary" href="/talento-humano/colaboradores">Ir a colaboradores</Link></div><section className="mt-7"><h3 className="text-sm font-bold text-[#244636]">Asignaciones de la persona</h3><div className="mt-3 space-y-2">{assignments.map(row=><article className="rounded-xl border border-[#e1e8df] p-3" key={row.id}><div className="flex justify-between gap-3"><strong className="text-xs">{row.positionName}</strong><AssignmentStatus row={row}/></div><p className="mt-1 text-[10px] text-[#687970]">{row.organizationalUnitName}</p><p className="mt-2 text-[9px] text-[#829087]">{formatDate(row.startDate)} — {formatDate(row.endDate)}</p></article>)}</div></section></aside></div> }

function AssignmentStatus({row}:{row:OrganizationalAssignmentItem}) { const current=isAssignmentCurrent(row); return <span className={`rounded-full px-2 py-1 text-[9px] font-bold ${current?"bg-[#e5f1e1] text-[#2f643d]":row.isActive?"bg-[#fff0d8] text-[#7d5b1d]":"bg-[#eef0ee] text-[#69766f]"}`}>{current?"Actual":row.isActive?"Fuera de fecha":"Histórica"}</span> }
function SearchBox({value,onChange,label}:{value:string;onChange:(value:string)=>void;label:string}) { return <label className="relative block"><Search className="absolute left-3 top-1/2 -translate-y-1/2 text-[#7a887f]" size={16}/><input aria-label={label} className="h-10 w-full rounded-xl border border-[#d6dfd3] bg-white pl-9 pr-3 text-xs outline-none focus:border-[#66804e] focus:ring-2 focus:ring-[#66804e]/10" onChange={e=>onChange(e.target.value)} placeholder={label} value={value}/></label> }
function Avatar({name,large=false}:{name:string;large?:boolean}) { const initials=name.split(/\s+/).filter(Boolean).slice(0,2).map(part=>part[0]).join("").toUpperCase(); return <span aria-hidden="true" className={`grid shrink-0 place-items-center rounded-full bg-[#e4eee1] font-bold text-[#315b42] ${large?"h-16 w-16 text-lg":"h-10 w-10 text-xs"}`}>{initials}</span> }
function Metric({label,value}:{label:string;value:number}) { return <div className="rounded-xl bg-[#f4f7f2] p-3"><strong className="block text-xl text-[#174b35]">{value}</strong><span className="text-[10px] uppercase tracking-wide text-[#738178]">{label}</span></div> }
function Detail({label,value}:{label:string;value:string}) { return <div className="rounded-xl bg-[#f5f8f3] p-3"><dt className="text-[9px] font-bold uppercase text-[#7b887f]">{label}</dt><dd className="mt-1 font-semibold text-[#294b3b]">{value}</dd></div> }
function EmptyState({title,detail}:{title:string;detail:string}) { return <div className="grid min-h-40 place-content-center px-5 text-center"><strong className="text-sm text-[#315342]">{title}</strong><p className="mt-1 text-xs text-[#7b887f]">{detail}</p></div> }

function descendantsOf(id:string,children:Map<string|undefined,AssignmentUnit[]>) { const result:string[]=[]; const visit=(parent:string)=>{(children.get(parent)??[]).forEach(child=>{result.push(child.id);visit(child.id);});}; if(id)visit(id); return result; }
function treeRows(children:Map<string|undefined,AssignmentUnit[]>,collapsed:Set<string>) { const rows:{unit:AssignmentUnit;depth:number;hasChildren:boolean}[]=[]; const visited=new Set<string>(); const visit=(parent:string|undefined,depth:number)=>{(children.get(parent)??[]).forEach(unit=>{if(visited.has(unit.id))return;visited.add(unit.id);rows.push({unit,depth,hasChildren:Boolean(children.get(unit.id)?.length)});if(!collapsed.has(unit.id))visit(unit.id,depth+1);});}; visit(undefined,0); return rows; }
function toExportRow(row:OrganizationalAssignmentItem,byId:Map<string,AssignmentUnit>):AssignmentExportRow { const names:string[]=[];let unit=byId.get(row.organizationalUnitId);const visited=new Set<string>();while(unit&&!visited.has(unit.id)){visited.add(unit.id);names.unshift(unit.name);unit=unit.parentId?byId.get(unit.parentId):undefined;}return {...row,organizationalUnitPath:names.join(" > "),isCurrent:isAssignmentCurrent(row)}; }
function formatDate(value?:string) { return value?new Intl.DateTimeFormat("es-CO",{dateStyle:"medium",timeZone:"UTC"}).format(new Date(`${value.slice(0,10)}T00:00:00Z`)):"Sin fecha"; }
function normalize(value:string) { return value.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es"); }
function groupAssignments(rows:OrganizationalAssignmentItem[]) { const groups=new Map<string,OrganizationalAssignmentItem[]>();rows.forEach(row=>groups.set(row.positionName,[...(groups.get(row.positionName)??[]),row]));return [...groups.entries()].sort(([a],[b])=>a.localeCompare(b,"es")); }
