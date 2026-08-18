"use client";

import { FormEvent, KeyboardEvent, useEffect, useMemo, useRef, useState } from "react";
import { AppHeader } from "@/components/app-header";
import { Button, IconButton } from "@/components/ui";
import { FormDialog } from "@/components/form-dialog";
import { useFeedback } from "@/components/feedback";
import { exportOrganizationUnits } from "@/lib/exports/organization-units-export";
import { Check, ChevronDown, ChevronRight, Download, FileSpreadsheet, LoaderCircle, Pencil, Plus, Search } from "lucide-react";
import Image from "next/image";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";

async function apiRequest<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${apiUrl}${path}`, {
    credentials: "include",
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...options?.headers,
    },
  });
  if (response.status === 401) {
    window.location.href = "/";
    throw new Error("Sesión finalizada.");
  }
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {
      detail?: string;
      errors?: Record<string, string[]>;
    } | null;
    throw new Error(
      problem?.detail ??
        Object.values(problem?.errors ?? {}).flat()[0] ??
        "No fue posible completar la operación.",
    );
  }
  return response.status === 204
    ? (undefined as T)
    : ((await response.json()) as T);
}

type UnitType = {
  id: string;
  name: string;
  colorToken: string;
};

type Site = {
  id: string;
  code: string;
  name: string;
  city?: string;
};

type Unit = {
  id: string;
  code: string;
  name: string;
  shortName?: string;
  unitTypeId: string;
  unitTypeName: string;
  parentId?: string;
  siteId?: string;
  siteName?: string;
  level: number;
  description?: string;
  visualOrder: number;
  effectiveFrom: string;
  effectiveTo?: string;
  isActive: boolean;
};

type Position = {
  id: string;
  code?: string | null;
  name: string;
  description?: string;
  isActive: boolean;
};

type UnitForm = {
  code: string;
  name: string;
  shortName: string;
  unitTypeId: string;
  parentId: string;
  siteId: string;
  description: string;
  visualOrder: number;
  effectiveFrom: string;
  effectiveTo: string;
  isActive: boolean;
};

const emptyUnit: UnitForm = {
  code: "",
  name: "",
  shortName: "",
  unitTypeId: "",
  parentId: "",
  siteId: "",
  description: "",
  visualOrder: 0,
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: "",
  isActive: true,
};

export default function OrganizationPage() {
  const [units, setUnits] = useState<Unit[]>([]);
  const [unitTypes, setUnitTypes] = useState<UnitType[]>([]);
  const [sites, setSites] = useState<Site[]>([]);
  const [positions, setPositions] = useState<Position[]>([]);
  const [tab, setTab] = useState<"chart" | "units" | "positions" | "catalogs">("chart");
  const [search, setSearch] = useState("");
  const [positionSearch, setPositionSearch] = useState("");
  const [expandedUnits, setExpandedUnits] = useState<Set<string>>(new Set());
  const [unitForm, setUnitForm] = useState<UnitForm>(emptyUnit);
  const [editingUnitId, setEditingUnitId] = useState<string | null>(null);
  const [positionForm, setPositionForm] = useState({
    code: "",
    name: "",
    description: "",
    isActive: true,
  });
  const [editingPositionId, setEditingPositionId] = useState<string | null>(null);
  const [panelOpen, setPanelOpen] = useState(false);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [exportingUnits, setExportingUnits] = useState(false);
  const [loadedTabs, setLoadedTabs] = useState(() => new Set<string>());
  const { notify } = useFeedback();

  async function loadData() {
    try {
      const [loadedUnits, loadedTypes, loadedSites, loadedPositions] =
        await Promise.all([
          apiRequest<Unit[]>("/api/organization/units"),
          apiRequest<UnitType[]>("/api/organization/unit-types"),
          apiRequest<Site[]>("/api/organization/sites"),
          apiRequest<Position[]>("/api/organization/positions"),
        ]);
      setUnits(loadedUnits);
      setExpandedUnits(current => current.size ? current : initialExpandedUnits(loadedUnits));
      setUnitTypes(loadedTypes);
      setSites(loadedSites);
      setPositions(loadedPositions);
      setUnitForm((current) => ({
        ...current,
        unitTypeId: current.unitTypeId || loadedTypes[0]?.id || "",
        siteId: current.siteId || loadedSites[0]?.id || "",
      }));
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Error inesperado.");
    }
  }

  useEffect(() => {
    void apiRequest<Unit[]>("/api/organization/units")
      .then((loadedUnits) => {
        setUnits(loadedUnits);
        setExpandedUnits(current => current.size ? current : initialExpandedUnits(loadedUnits));
        setLoadedTabs(current => new Set(current).add("chart"));
      })
      .catch((caught: unknown) => {
        setError(caught instanceof Error ? caught.message : "Error inesperado.");
      });
  }, []);

  useEffect(() => {
    if (loadedTabs.has(tab)) return;
    if (tab === "positions") void apiRequest<Position[]>("/api/organization/positions").then(rows=>{setPositions(rows);setLoadedTabs(current=>new Set(current).add(tab));});
    if (tab === "catalogs") void Promise.all([apiRequest<UnitType[]>("/api/organization/unit-types"),apiRequest<Site[]>("/api/organization/sites")]).then(([types,loadedSites])=>{setUnitTypes(types);setSites(loadedSites);setLoadedTabs(current=>{const next=new Set(current);next.add("catalogs");next.add("units");return next;});});
    if (tab === "units") void Promise.all([apiRequest<UnitType[]>("/api/organization/unit-types"),apiRequest<Site[]>("/api/organization/sites")]).then(([types,loadedSites])=>{setUnitTypes(types);setSites(loadedSites);setLoadedTabs(current=>{const next=new Set(current);next.add("catalogs");next.add("units");return next;});});
  }, [loadedTabs, tab]);

  const visibleUnits = useMemo(() => {
    const term = search.trim().toLocaleLowerCase("es");
    const ordered = orderUnitsByHierarchy(units);
    if (term) {
      const matches = units.filter(unit => unit.name.toLocaleLowerCase("es").includes(term) || unit.code.toLocaleLowerCase("es").includes(term));
      const visibleIds = new Set(matches.map(unit => unit.id));
      const byId = new Map(units.map(unit => [unit.id, unit]));
      matches.forEach(unit => { let parentId = unit.parentId; while (parentId) { visibleIds.add(parentId); parentId = byId.get(parentId)?.parentId; } });
      return ordered.filter(unit => visibleIds.has(unit.id));
    }
    const byId = new Map(units.map(unit => [unit.id, unit]));
    return ordered.filter(unit => { let parentId = unit.parentId; while (parentId) { if (!expandedUnits.has(parentId)) return false; parentId = byId.get(parentId)?.parentId; } return true; });
  }, [expandedUnits, search, units]);

  const visiblePositions = useMemo(() => {
    const term = positionSearch.trim().toLocaleLowerCase("es");
    return [...positions]
      .sort((left, right) => left.name.localeCompare(right.name, "es", { sensitivity: "base" }))
      .filter(position => !term || position.name.toLocaleLowerCase("es").includes(term) || (position.code ?? "").toLocaleLowerCase("es").includes(term));
  }, [positionSearch, positions]);

  function startUnit(unit?: Unit) {
    setError("");
    setEditingUnitId(unit?.id ?? null);
    setUnitForm(
      unit
        ? {
            code: unit.code,
            name: unit.name,
            shortName: unit.shortName ?? "",
            unitTypeId: unit.unitTypeId,
            parentId: unit.parentId ?? "",
            siteId: unit.siteId ?? "",
            description: unit.description ?? "",
            visualOrder: unit.visualOrder,
            effectiveFrom: unit.effectiveFrom,
            effectiveTo: unit.effectiveTo ?? "",
            isActive: unit.isActive,
          }
        : {
            ...emptyUnit,
            unitTypeId: unitTypes[0]?.id ?? "",
            siteId: sites[0]?.id ?? "",
          },
    );
    setPanelOpen(true);
  }

  async function saveUnit(event: FormEvent) {
    event.preventDefault();
    if (saving) return;
    setSaving(true);
    setError("");
    const payload = {
      ...unitForm,
      parentId: unitForm.parentId || null,
      siteId: unitForm.siteId || null,
      effectiveTo: unitForm.effectiveTo || null,
    };
    try {
      await apiRequest(
        editingUnitId
          ? `/api/organization/units/${editingUnitId}`
          : "/api/organization/units",
        {
          method: editingUnitId ? "PUT" : "POST",
          body: JSON.stringify(payload),
        },
      );
      notify({ tone: "success", title: editingUnitId ? "Unidad actualizada correctamente" : "Unidad creada correctamente" });
      setPanelOpen(false);
      await loadData();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Error inesperado.");
    } finally {
      setSaving(false);
    }
  }

  function startPosition(position?: Position) {
    setError("");
    setEditingPositionId(position?.id ?? null);
    setPositionForm({
      code: position?.code ?? "",
      name: position?.name ?? "",
      description: position?.description ?? "",
      isActive: position?.isActive ?? true,
    });
    setPanelOpen(true);
  }

  async function savePosition(event: FormEvent) {
    event.preventDefault();
    if (saving) return;
    setSaving(true);
    setError("");
    try {
      await apiRequest(
        editingPositionId
          ? `/api/organization/positions/${editingPositionId}`
          : "/api/organization/positions",
        {
          method: editingPositionId ? "PUT" : "POST",
          body: JSON.stringify(positionForm),
        },
      );
      notify({ tone: "success", title: editingPositionId ? "Cambios guardados correctamente" : "Cargo creado correctamente" });
      setPanelOpen(false);
      await loadData();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Error inesperado.");
    } finally {
      setSaving(false);
    }
  }

  async function exportUnits() {
    if (exportingUnits) return;
    setExportingUnits(true);
    try {
      await exportOrganizationUnits(units, search);
      notify({ tone: "success", title: "Excel generado correctamente", description: search.trim() ? "Se exportaron los resultados filtrados." : "Se exportó la estructura completa de unidades." });
    } catch (caught) {
      notify({ tone: "error", title: "No fue posible generar el Excel", description: caught instanceof Error ? caught.message : "Intenta nuevamente." });
    } finally {
      setExportingUnits(false);
    }
  }

  return (
    <main className="gaia-app-page min-h-screen bg-[#f4f7f2] text-[#193522]">
      <AppHeader title="Estructura organizacional" />

      <div className="mx-auto max-w-7xl px-6 py-8">
        <section className="gaia-metrics grid gap-4 sm:grid-cols-3">
          {[
            ["Unidades", units.length.toString(), "Áreas y equipos"],
            ["Cargos", positions.length.toString(), "Catálogo institucional"],
            ["Sedes", sites.length.toString(), "Ubicaciones operativas"],
          ].map(([label, value, detail]) => (
            <article className="gaia-metric" key={label}>
              <p className="gaia-metric-label">{label}</p>
              <p className="gaia-metric-value">{value}</p>
              <p className="gaia-metric-detail">{detail}</p>
            </article>
          ))}
        </section>

        <section className="mt-7 rounded-3xl bg-white p-6 shadow-sm">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div aria-label="Secciones de Organización" className="gaia-tabs" role="tablist">
              {[
                ["chart", "Organigrama"],
                ["units", "Unidades"],
                ["positions", "Cargos"],
                ["catalogs", "Sedes y tipos"],
              ].map(([value, label]) => (
                <button
                  aria-selected={tab === value}
                  className="gaia-tab"
                  key={value}
                  onClick={() => {
                    setTab(value as typeof tab);
                    setPanelOpen(false);
                  }}
                  role="tab"
                  type="button"
                >
                  {label}
                </button>
              ))}
            </div>
            {tab === "chart" && (
              <Button
                onClick={() => window.print()}
                variant="secondary"
              >
                <Download size={16} />Descargar PDF
              </Button>
            )}
            {(tab === "units" || tab === "positions") && <div className="flex flex-wrap items-center gap-3">
              {tab === "positions" && <div className="relative w-72 max-w-full"><Search className="absolute left-3 top-1/2 -translate-y-1/2 text-[#7a887f]" size={17}/><input aria-label="Buscar cargos" className="w-full rounded-xl border border-[#d6dfd3] py-2.5 pl-10 pr-4 outline-none focus:border-[#66804e]" onChange={event=>setPositionSearch(event.target.value)} placeholder="Buscar por nombre o código" value={positionSearch}/></div>}
              <Button onClick={() => tab === "units" ? startUnit() : startPosition()}><Plus size={17} />{tab === "units" ? "Nueva unidad" : "Nuevo cargo"}</Button>
            </div>}
          </div>

          {error && !panelOpen && (
            <p className="mt-5 rounded-xl bg-[#fff0eb] px-4 py-3 text-sm text-[#8a3f25]">
              {error}
            </p>
          )}

          {tab === "units" && (
            <div className="mt-6">
              <div className="flex flex-wrap items-center justify-between gap-3"><div className="relative w-full max-w-sm"><Search className="absolute left-3 top-1/2 -translate-y-1/2 text-[#7a887f]" size={17} /><input className="w-full rounded-xl border border-[#d6dfd3] py-2.5 pl-10 pr-4 outline-none focus:border-[#66804e]" onChange={(event) => setSearch(event.target.value)} placeholder="Buscar por código o nombre" value={search} /></div><Button disabled={exportingUnits || !units.length} onClick={() => void exportUnits()} variant="secondary">{exportingUnits ? <LoaderCircle className="gaia-spin" size={17} /> : <FileSpreadsheet size={17} />}{exportingUnits ? "Generando..." : "Exportar a Excel"}</Button></div>
              <div className="mt-5 overflow-x-auto">
                <table className="w-full min-w-[760px] text-left text-sm">
                  <thead className="border-b border-[#e5ebe3] text-xs uppercase tracking-wider text-[#7a887f]">
                    <tr>
                      <th className="px-3 py-3">Unidad</th>
                      <th className="px-3 py-3">Tipo</th>
                      <th className="px-3 py-3">Sede</th>
                      <th className="px-3 py-3">Nivel</th>
                      <th className="px-3 py-3">Estado</th>
                      <th className="px-3 py-3" />
                    </tr>
                  </thead>
                  <tbody>
                    {visibleUnits.map((unit) => {
                      const hasChildren = units.some((candidate) => candidate.parentId === unit.id);
                      return (
                      <tr className="gaia-unit-row border-b border-[#edf1eb]" key={unit.id}>
                        <td className="px-3 py-4">
                          <div className="gaia-unit-identity" style={{ paddingLeft: `${Math.max(0, unit.level - 1) * 22}px` }}>
                            {hasChildren ? <button aria-expanded={search.trim()?true:expandedUnits.has(unit.id)} aria-label={`${search.trim()||expandedUnits.has(unit.id)?"Contraer":"Expandir"} ${unit.name}`} className="gaia-unit-chevron has-children rounded focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#66804e]" disabled={Boolean(search.trim())} onClick={()=>setExpandedUnits(current=>{const next=new Set(current);if(next.has(unit.id))next.delete(unit.id);else next.add(unit.id);return next;})} type="button">{search.trim()||expandedUnits.has(unit.id)?<ChevronDown size={15}/>:<ChevronRight size={15}/>}</button>:<span className="gaia-unit-chevron" aria-hidden="true"/>}
                            <div><p className="gaia-unit-name">{unit.name}</p><span className="gaia-unit-code">{unit.code}</span></div>
                          </div>
                        </td>
                        <td className="px-3 py-4">{unit.unitTypeName}</td>
                        <td className="px-3 py-4">{unit.siteName ?? "—"}</td>
                        <td className="px-3 py-4">{unit.level}</td>
                        <td className="px-3 py-4">
                          <Status active={unit.isActive} />
                        </td>
                        <td className="px-3 py-4 text-right">
                          <IconButton label={`Editar ${unit.name}`} onClick={() => startUnit(unit)}><Pencil size={16} /></IconButton>
                        </td>
                      </tr>
                    );})}
                  </tbody>
                </table>
                {!visibleUnits.length && (
                  <p className="py-12 text-center text-sm text-[#7b887f]">
                    Aún no hay unidades. Crea la primera unidad raíz.
                  </p>
                )}
              </div>
            </div>
          )}

          {tab === "chart" && (
            <OrganizationChart units={units} />
          )}

          {tab === "positions" && (
            <div className="mt-6 grid gap-3 md:grid-cols-2">
              {visiblePositions.map((position) => (
                <article
                  className="flex items-center justify-between rounded-2xl border border-[#e3eae0] p-4"
                  key={position.id}
                >
                  <div>
                    <p className="font-semibold">{position.name}</p>
                    {position.code && <p className="text-xs text-[#7b887f]">{position.code}</p>}
                  </div>
                  <div className="flex items-center gap-4">
                    <Status active={position.isActive} />
                    <IconButton label={`Editar ${position.name}`} onClick={() => startPosition(position)}><Pencil size={16} /></IconButton>
                  </div>
                </article>
              ))}
              {!visiblePositions.length && (
                <p className="py-10 text-sm text-[#7b887f]">
                  {positionSearch.trim()?"No se encontraron cargos que coincidan con la búsqueda.":"Aún no se han creado cargos."}
                </p>
              )}
            </div>
          )}

          {tab === "catalogs" && (
            <div className="mt-6 grid gap-6 md:grid-cols-2">
              <Catalog title="Sedes">
                {sites.map((site) => (
                  <CatalogRow
                    detail={site.city ?? "Ciudad sin definir"}
                    key={site.id}
                    label={site.name}
                    value={site.code}
                  />
                ))}
              </Catalog>
              <Catalog title="Tipos de unidad">
                {unitTypes.map((type) => (
                  <CatalogRow
                    detail={type.colorToken}
                    key={type.id}
                    label={type.name}
                    value="Activo"
                  />
                ))}
              </Catalog>
            </div>
          )}
        </section>
      </div>

      <FormDialog error={error} formId={tab === "units" ? "unit-form" : "position-form"} loading={saving} onClose={() => setPanelOpen(false)} open={panelOpen} submitLabel={editingUnitId || editingPositionId ? "Actualizar" : "Crear"} subtitle={tab === "units" ? "Datos, jerarquía y vigencia de la unidad" : "Información del catálogo institucional"} title={tab === "units" ? (editingUnitId ? "Editar unidad" : "Nueva unidad") : (editingPositionId ? "Editar cargo" : "Nuevo cargo")}>
            {tab === "units" ? (
              <UnitEditor
                form={unitForm}
                onChange={setUnitForm}
                onSubmit={saveUnit}
                sites={sites}
                types={unitTypes}
                units={units.filter((unit) => unit.id !== editingUnitId)}
              />
            ) : (
              <PositionEditor
                form={positionForm}
                onChange={setPositionForm}
                onSubmit={savePosition}
              />
            )}
      </FormDialog>
    </main>
  );
}

function UnitEditor({
  form,
  onChange,
  onSubmit,
  sites,
  types,
  units,
}: {
  form: UnitForm;
  onChange: (form: UnitForm) => void;
  onSubmit: (event: FormEvent) => void;
  sites: Site[];
  types: UnitType[];
  units: Unit[];
}) {
  return (
    <form className="space-y-5" id="unit-form" onSubmit={onSubmit}>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Código">
          <input
            required
            value={form.code}
            onChange={(event) => onChange({ ...form, code: event.target.value })}
          />
        </Field>
        <Field label="Nombre corto">
          <input
            value={form.shortName}
            onChange={(event) =>
              onChange({ ...form, shortName: event.target.value })
            }
          />
        </Field>
      </div>
      <Field label="Nombre oficial">
        <input
          required
          value={form.name}
          onChange={(event) => onChange({ ...form, name: event.target.value })}
        />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Tipo de unidad">
          <select
            required
            value={form.unitTypeId}
            onChange={(event) =>
              onChange({ ...form, unitTypeId: event.target.value })
            }
          >
            {types.map((type) => (
              <option key={type.id} value={type.id}>
                {type.name}
              </option>
            ))}
          </select>
        </Field>
        <Field label="Sede">
          <select
            value={form.siteId}
            onChange={(event) =>
              onChange({ ...form, siteId: event.target.value })
            }
          >
            <option value="">Sin sede</option>
            {sites.map((site) => (
              <option key={site.id} value={site.id}>
                {site.name}
              </option>
            ))}
          </select>
        </Field>
      </div>
      <div><span className="text-sm font-semibold text-[#405247]">Unidad padre</span><div className="mt-2"><HierarchySelect onChange={(parentId) => onChange({ ...form, parentId })} units={units} value={form.parentId} /></div></div>
      <Field label="Descripción">
        <textarea
          rows={3}
          value={form.description}
          onChange={(event) =>
            onChange({ ...form, description: event.target.value })
          }
        />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Vigente desde">
          <input
            required
            type="date"
            value={form.effectiveFrom}
            onChange={(event) =>
              onChange({ ...form, effectiveFrom: event.target.value })
            }
          />
        </Field>
        <Field label="Vigente hasta">
          <input
            type="date"
            value={form.effectiveTo}
            onChange={(event) =>
              onChange({ ...form, effectiveTo: event.target.value })
            }
          />
        </Field>
      </div>
      <label className="flex items-center gap-3 text-sm font-semibold">
        <input
          checked={form.isActive}
          onChange={(event) =>
            onChange({ ...form, isActive: event.target.checked })
          }
          type="checkbox"
        />
        Unidad activa
      </label>
    </form>
  );
}

function HierarchySelect({ units, value, onChange }: { units: Unit[]; value: string; onChange: (value: string) => void }) {
  const containerRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const ordered = useMemo(() => orderUnitsByHierarchy(units), [units]);
  const selected = units.find((unit) => unit.id === value);
  const normalizedQuery = query.trim().toLocaleLowerCase("es");
  const matches = normalizedQuery
    ? ordered.filter((unit) => unit.name.toLocaleLowerCase("es").includes(normalizedQuery) || unit.code.toLocaleLowerCase("es").includes(normalizedQuery))
    : ordered;
  const options: Array<Unit | null> = normalizedQuery && !"unidad raíz".includes(normalizedQuery) ? matches : [null, ...matches];

  useEffect(() => {
    function close(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);

  useEffect(() => {
    if (open) window.requestAnimationFrame(() => searchRef.current?.focus());
  }, [open]);

  function choose(option: Unit | null) {
    onChange(option?.id ?? "");
    setOpen(false);
    setQuery("");
    setActiveIndex(0);
  }

  function navigate(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "ArrowDown") { event.preventDefault(); setActiveIndex((current) => Math.min(current + 1, options.length - 1)); }
    if (event.key === "ArrowUp") { event.preventDefault(); setActiveIndex((current) => Math.max(current - 1, 0)); }
    if (event.key === "Enter" && options[activeIndex] !== undefined) { event.preventDefault(); choose(options[activeIndex]); }
    if (event.key === "Escape") { event.preventDefault(); setOpen(false); }
  }

  return <div className="gaia-hierarchy-select" ref={containerRef}>
    <button aria-controls="unit-parent-options" aria-expanded={open} aria-haspopup="listbox" className="gaia-hierarchy-trigger" onClick={() => setOpen((current) => !current)} type="button">
      <span>{selected ? <><strong>{selected.name}</strong><small>{selected.code}</small></> : <><strong>Unidad raíz</strong><small>Sin unidad padre</small></>}</span>
      <ChevronDown aria-hidden="true" className={open ? "is-open" : ""} size={18} />
    </button>
    {open && <div className="gaia-hierarchy-popover">
      <div className="gaia-hierarchy-search"><Search aria-hidden="true" size={16} /><input aria-autocomplete="list" aria-controls="unit-parent-options" aria-expanded="true" onChange={(event) => { setQuery(event.target.value); setActiveIndex(0); }} onKeyDown={navigate} placeholder="Buscar por nombre o código" ref={searchRef} role="combobox" value={query} /></div>
      <div className="gaia-hierarchy-options" id="unit-parent-options" role="listbox">
        {options.map((option, index) => {
          const id = option?.id ?? "root";
          const isSelected = (option?.id ?? "") === value;
          const hasChildren = option ? units.some((candidate) => candidate.parentId === option.id) : false;
          return <button aria-selected={isSelected} className={`gaia-hierarchy-option ${index === activeIndex ? "is-highlighted" : ""}`} key={id} onClick={() => choose(option)} onMouseEnter={() => setActiveIndex(index)} role="option" style={{ paddingLeft: `${12 + (option?.level ?? 0) * 20}px` }} type="button">
            <span className={`gaia-hierarchy-branch ${hasChildren ? "has-children" : ""}`}><ChevronRight size={14} /></span>
            <span className="gaia-hierarchy-option-label"><strong>{option?.name ?? "Unidad raíz"}</strong><small>{option?.code ?? "Sin unidad padre"}</small></span>
            {isSelected && <Check aria-hidden="true" className="gaia-hierarchy-check" size={16} />}
          </button>;
        })}
        {!options.length && <p className="gaia-hierarchy-empty">No se encontraron unidades.</p>}
      </div>
      <p className="gaia-hierarchy-help">Usa ↑ ↓ para navegar, Enter para seleccionar y Esc para cerrar.</p>
    </div>}
  </div>;
}

function orderUnitsByHierarchy(units: Unit[]) {
  const ordered: Unit[] = [];
  const visited = new Set<string>();
  const byParent = new Map<string | null, Unit[]>();
  units.forEach((unit) => {
    const key = unit.parentId ?? null;
    byParent.set(key, [...(byParent.get(key) ?? []), unit]);
  });
  const visit = (parentId: string | null) => (byParent.get(parentId) ?? [])
    .sort(compareUnitsByCode)
    .forEach((unit) => { if (!visited.has(unit.id)) { visited.add(unit.id); ordered.push(unit); visit(unit.id); } });
  visit(null);
  units.forEach((unit) => { if (!visited.has(unit.id)) ordered.push(unit); });
  return ordered;
}

function initialExpandedUnits(units: Unit[]) {
  return new Set(units.filter(unit => unit.level < 2 && units.some(child => child.parentId === unit.id)).map(unit => unit.id));
}

function compareUnitsByCode(left: Unit, right: Unit) {
  const leftCode = left.code?.trim();
  const rightCode = right.code?.trim();
  if (!leftCode && rightCode) return 1;
  if (leftCode && !rightCode) return -1;
  const byCode = (leftCode ?? "").localeCompare(rightCode ?? "", "es", { numeric: true, sensitivity: "base" });
  return byCode || left.name.localeCompare(right.name, "es", { sensitivity: "base" }) || left.id.localeCompare(right.id);
}

function PositionEditor({
  form,
  onChange,
  onSubmit,
}: {
  form: { code: string; name: string; description: string; isActive: boolean };
  onChange: (form: {
    code: string;
    name: string;
    description: string;
    isActive: boolean;
  }) => void;
  onSubmit: (event: FormEvent) => void;
}) {
  return (
    <form className="space-y-5" id="position-form" onSubmit={onSubmit}>
      <Field label="Código (opcional)">
        <input
          value={form.code}
          onChange={(event) => onChange({ ...form, code: event.target.value })}
        />
      </Field>
      <Field label="Nombre del cargo">
        <input
          required
          value={form.name}
          onChange={(event) => onChange({ ...form, name: event.target.value })}
        />
      </Field>
      <Field label="Descripción">
        <textarea
          rows={4}
          value={form.description}
          onChange={(event) =>
            onChange({ ...form, description: event.target.value })
          }
        />
      </Field>
      <label className="flex items-center gap-3 text-sm font-semibold">
        <input
          checked={form.isActive}
          onChange={(event) =>
            onChange({ ...form, isActive: event.target.checked })
          }
          type="checkbox"
        />
        Cargo activo
      </label>
    </form>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: React.ReactElement<{ className?: string }>;
}) {
  return (
    <label className="block">
      <span className="text-sm font-semibold text-[#405247]">{label}</span>
      <div className="mt-2 [&>*]:w-full [&>*]:rounded-xl [&>*]:border [&>*]:border-[#d6dfd3] [&>*]:bg-[#fbfcfa] [&>*]:px-4 [&>*]:py-3 [&>*]:outline-none focus-within:[&>*]:border-[#66804e]">
        {children}
      </div>
    </label>
  );
}

function Status({ active }: { active: boolean }) {
  return (
    <span
      className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
        active
          ? "bg-[#e8f2e4] text-[#3d6544]"
          : "bg-[#f1f1ee] text-[#777a74]"
      }`}
    >
      {active ? "Activo" : "Inactivo"}
    </span>
  );
}

function Catalog({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-2xl border border-[#e2e9df] p-5">
      <h3 className="font-semibold">{title}</h3>
      <div className="mt-4 space-y-3">{children}</div>
    </div>
  );
}

function CatalogRow({
  label,
  detail,
  value,
}: {
  label: string;
  detail: string;
  value: string;
}) {
  return (
    <div className="flex items-center justify-between rounded-xl bg-[#f6f8f5] p-3">
      <div>
        <p className="text-sm font-semibold">{label}</p>
        <p className="text-xs text-[#7b887f]">{detail}</p>
      </div>
      <span className="text-xs font-semibold text-[#66804e]">{value}</span>
    </div>
  );
}

function OrganizationChart({ units }: { units: Unit[] }) {
  const ordered = orderUnitsByHierarchy(units);
  const roots = ordered.filter((unit) => !unit.parentId);
  const maxLevel = Math.max(1, ...units.map(unit => unit.level));
  const types = Array.from(new Set(units.map(unit => unit.unitTypeName)));
  return (
    <div className="organization-chart mt-7" id="organization-chart">
      <div className="org-chart-heading">
        <div><h2>Diagrama organizacional de jerarquía</h2><p>Fuente: base de datos institucional · {units.length} unidades</p></div>
        <div className="org-chart-seal"><strong>Gaia Amazonas</strong><span>Organigrama consolidado</span></div>
      </div>
      <div className="org-chart-body">
        <aside className="org-level-rail" aria-label={`${maxLevel} niveles jerárquicos`}>{Array.from({ length: maxLevel }, (_, index) => <span key={index}>Nivel {index + 1}</span>)}</aside>
        <div className="org-chart-viewport">
          <div className="org-tree">
            <ul className="org-tree-roots">{roots.map(root => <OrganizationBranch key={root.id} unit={root} units={ordered} />)}</ul>
          </div>
        </div>
      </div>
      <div className="org-chart-legend"><div className="org-legend-label"><strong>Atributo</strong><span>Tipo de unidad</span></div>{types.map(type => <div className="org-legend-item" key={type}><span style={{ backgroundColor: unitColor(type) }}>{type}</span></div>)}<div className="org-legend-status"><strong>Status</strong><span><i />Activo</span></div></div>
      {!roots.length && <div className="gaia-empty-state"><strong>No existe una raíz organizacional</strong><span>Revisa las relaciones de Unidad Padre en Dataverse.</span></div>}
      <div className="org-chart-source">
        <Image alt="Gaia Amazonas" height={41} src="/brand/logo-gaia.svg" width={75} />
      </div>
    </div>
  );
}

function OrganizationBranch({ unit, units }: { unit: Unit; units: Unit[] }) {
  const children = units.filter((candidate) => candidate.parentId === unit.id);
  return (
    <li className="org-branch">
      <article className="org-node">
        <div className="org-node-main" style={{ backgroundColor: unitColor(unit.unitTypeName) }}>
          <span>{unit.code}</span><strong>{unit.name}</strong>
        </div>
        <footer><span>{unit.unitTypeName}</span><span className={unit.isActive ? "is-active" : "is-inactive"}><i />{unit.isActive ? "Activo" : "Inactivo"}</span></footer>
      </article>
      {children.length > 0 && <ul>{children.map(child => <OrganizationBranch key={child.id} unit={child} units={units} />)}</ul>}
    </li>
  );
}

function unitColor(type: string) {
  const normalized = type.toLocaleUpperCase("es");
  if (normalized.includes("DIRECTIVO")) return "#A0384D";
  if (normalized.includes("SUBDIRECCI")) return "#3C838C";
  if (normalized.includes("ASESOR")) return "#6F3873";
  if (normalized.includes("DIRECTA")) return "#386037";
  if (normalized.includes("TRANSVERSAL")) return "#2F5048";
  return "#52685E";
}
