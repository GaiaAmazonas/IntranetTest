"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";
import { AppHeader } from "@/components/app-header";

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
  code: string;
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
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

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
    void Promise.all([
      apiRequest<Unit[]>("/api/organization/units"),
      apiRequest<UnitType[]>("/api/organization/unit-types"),
      apiRequest<Site[]>("/api/organization/sites"),
      apiRequest<Position[]>("/api/organization/positions"),
    ])
      .then(([loadedUnits, loadedTypes, loadedSites, loadedPositions]) => {
        setUnits(loadedUnits);
        setUnitTypes(loadedTypes);
        setSites(loadedSites);
        setPositions(loadedPositions);
        setUnitForm((current) => ({
          ...current,
          unitTypeId: current.unitTypeId || loadedTypes[0]?.id || "",
          siteId: current.siteId || loadedSites[0]?.id || "",
        }));
      })
      .catch((caught: unknown) => {
        setError(caught instanceof Error ? caught.message : "Error inesperado.");
      });
  }, []);

  const visibleUnits = useMemo(() => {
    const term = search.trim().toLocaleLowerCase("es");
    return term
      ? units.filter(
          (unit) =>
            unit.name.toLocaleLowerCase("es").includes(term) ||
            unit.code.toLocaleLowerCase("es").includes(term),
        )
      : units;
  }, [search, units]);

  function startUnit(unit?: Unit) {
    setError("");
    setMessage("");
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
      setMessage(editingUnitId ? "Unidad actualizada." : "Unidad creada.");
      setPanelOpen(false);
      await loadData();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Error inesperado.");
    }
  }

  function startPosition(position?: Position) {
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
      setMessage(editingPositionId ? "Cargo actualizado." : "Cargo creado.");
      setPanelOpen(false);
      await loadData();
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : "Error inesperado.");
    }
  }

  return (
    <main className="min-h-screen bg-[#f4f7f2] text-[#193522]">
      <AppHeader title="Estructura organizacional" />

      <div className="mx-auto max-w-7xl px-6 py-8">
        <section className="grid gap-4 sm:grid-cols-3">
          {[
            ["Unidades", units.length.toString(), "Áreas y equipos"],
            ["Cargos", positions.length.toString(), "Catálogo institucional"],
            ["Sedes", sites.length.toString(), "Ubicaciones operativas"],
          ].map(([label, value, detail]) => (
            <article className="rounded-2xl bg-white p-5 shadow-sm" key={label}>
              <p className="text-sm text-[#6d7c71]">{label}</p>
              <p className="mt-2 text-3xl font-semibold">{value}</p>
              <p className="mt-1 text-xs text-[#8a968e]">{detail}</p>
            </article>
          ))}
        </section>

        <section className="mt-7 rounded-3xl bg-white p-6 shadow-sm">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="flex rounded-xl bg-[#f1f5ef] p-1">
              {[
                ["chart", "Organigrama"],
                ["units", "Unidades"],
                ["positions", "Cargos"],
                ["catalogs", "Sedes y tipos"],
              ].map(([value, label]) => (
                <button
                  className={`rounded-lg px-4 py-2 text-sm font-semibold ${
                    tab === value ? "bg-white shadow-sm" : "text-[#68776d]"
                  }`}
                  key={value}
                  onClick={() => {
                    setTab(value as typeof tab);
                    setPanelOpen(false);
                  }}
                  type="button"
                >
                  {label}
                </button>
              ))}
            </div>
            {tab === "chart" && (
              <button
                className="rounded-xl border border-[#b9c9b5] bg-white px-4 py-2.5 text-sm font-semibold text-[#294f35] hover:bg-[#edf4e9]"
                onClick={() => window.print()}
                type="button"
              >
                Descargar / guardar PDF
              </button>
            )}
            {(tab === "units" || tab === "positions") && (
              <button
                className="rounded-xl bg-[#294f35] px-5 py-2.5 text-sm font-semibold text-white"
                onClick={() =>
                  tab === "units" ? startUnit() : startPosition()
                }
                type="button"
              >
                + {tab === "units" ? "Nueva unidad" : "Nuevo cargo"}
              </button>
            )}
          </div>

          {message && (
            <p className="mt-5 rounded-xl bg-[#eaf4e6] px-4 py-3 text-sm text-[#345a3c]">
              {message}
            </p>
          )}
          {error && (
            <p className="mt-5 rounded-xl bg-[#fff0eb] px-4 py-3 text-sm text-[#8a3f25]">
              {error}
            </p>
          )}

          {tab === "units" && (
            <div className="mt-6">
              <input
                className="w-full max-w-sm rounded-xl border border-[#d6dfd3] px-4 py-2.5 outline-none focus:border-[#66804e]"
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Buscar por código o nombre"
                value={search}
              />
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
                    {visibleUnits.map((unit) => (
                      <tr className="border-b border-[#edf1eb]" key={unit.id}>
                        <td className="px-3 py-4">
                          <div style={{ paddingLeft: `${unit.level * 20}px` }}>
                            <p className="font-semibold">{unit.name}</p>
                            <p className="text-xs text-[#7b887f]">{unit.code}</p>
                          </div>
                        </td>
                        <td className="px-3 py-4">{unit.unitTypeName}</td>
                        <td className="px-3 py-4">{unit.siteName ?? "—"}</td>
                        <td className="px-3 py-4">{unit.level + 1}</td>
                        <td className="px-3 py-4">
                          <Status active={unit.isActive} />
                        </td>
                        <td className="px-3 py-4 text-right">
                          <button
                            className="font-semibold text-[#42634b]"
                            onClick={() => startUnit(unit)}
                            type="button"
                          >
                            Editar
                          </button>
                        </td>
                      </tr>
                    ))}
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
              {positions.map((position) => (
                <article
                  className="flex items-center justify-between rounded-2xl border border-[#e3eae0] p-4"
                  key={position.id}
                >
                  <div>
                    <p className="font-semibold">{position.name}</p>
                    <p className="text-xs text-[#7b887f]">{position.code}</p>
                  </div>
                  <div className="flex items-center gap-4">
                    <Status active={position.isActive} />
                    <button
                      className="text-sm font-semibold text-[#42634b]"
                      onClick={() => startPosition(position)}
                      type="button"
                    >
                      Editar
                    </button>
                  </div>
                </article>
              ))}
              {!positions.length && (
                <p className="py-10 text-sm text-[#7b887f]">
                  Aún no se han creado cargos.
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

      {panelOpen && (
        <div className="fixed inset-0 z-20 flex justify-end bg-[#102418]/35">
          <button
            aria-label="Cerrar formulario"
            className="flex-1"
            onClick={() => setPanelOpen(false)}
            type="button"
          />
          <aside className="h-full w-full max-w-xl overflow-y-auto bg-white p-7 shadow-2xl">
            <div className="flex items-center justify-between">
              <h2 className="text-2xl font-semibold">
                {tab === "units"
                  ? editingUnitId
                    ? "Editar unidad"
                    : "Nueva unidad"
                  : editingPositionId
                    ? "Editar cargo"
                    : "Nuevo cargo"}
              </h2>
              <button
                className="text-2xl text-[#718078]"
                onClick={() => setPanelOpen(false)}
                type="button"
              >
                ×
              </button>
            </div>
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
          </aside>
        </div>
      )}
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
    <form className="mt-7 space-y-5" onSubmit={onSubmit}>
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
      <Field label="Unidad padre">
        <select
          value={form.parentId}
          onChange={(event) =>
            onChange({ ...form, parentId: event.target.value })
          }
        >
          <option value="">Unidad raíz</option>
          {units.map((unit) => (
            <option key={unit.id} value={unit.id}>
              {"— ".repeat(unit.level)}
              {unit.name}
            </option>
          ))}
        </select>
      </Field>
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
      <button className="w-full rounded-xl bg-[#294f35] px-5 py-3 font-semibold text-white">
        Guardar unidad
      </button>
    </form>
  );
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
    <form className="mt-7 space-y-5" onSubmit={onSubmit}>
      <Field label="Código">
        <input
          required
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
      <button className="w-full rounded-xl bg-[#294f35] px-5 py-3 font-semibold text-white">
        Guardar cargo
      </button>
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
  const roots = units.filter((unit) => !unit.parentId);
  return (
    <div className="organization-chart mt-7 rounded-2xl bg-[#f3f6f1] p-4 sm:p-6" id="organization-chart">
      <div className="mb-5 border-b border-[#dce5d8] pb-4">
        <p className="text-[10px] font-bold uppercase tracking-[.22em] text-[#66804e]">Fundación Gaia Amazonas</p>
        <h2 className="mt-1 text-xl font-semibold">Diagrama organizacional de jerarquía</h2>
        <p className="mt-1 text-xs text-[#7b887f]">Vista consolidada · {units.length} unidades</p>
      </div>
      <div className="space-y-8">
        {roots.map((root) => (
          <OrganizationBranch key={root.id} unit={root} units={units} />
        ))}
      </div>
      <div className="mt-8 flex flex-wrap gap-3 border-t border-[#dce5d8] pt-5">
        {Array.from(new Set(units.map((unit) => unit.unitTypeName))).map((type) => (
          <span className="rounded-full px-3 py-1.5 text-xs font-bold text-white" key={type} style={{ backgroundColor: unitColor(type) }}>
            {type}
          </span>
        ))}
      </div>
    </div>
  );
}

function OrganizationBranch({ unit, units }: { unit: Unit; units: Unit[] }) {
  const children = units.filter((candidate) => candidate.parentId === unit.id);
  return (
    <div className="flex flex-col items-center">
      <article className="w-48 overflow-hidden rounded-xl border border-black/10 bg-white shadow-md sm:w-52">
        <div className="px-3 py-2.5 text-white" style={{ backgroundColor: unitColor(unit.unitTypeName) }}>
          <p className="text-[10px] font-bold tracking-widest opacity-80">{unit.code}</p>
          <p className="mt-1 text-center text-xs font-bold leading-4">{unit.name}</p>
        </div>
        <div className="flex items-center justify-between px-3 py-1.5 text-[10px] font-semibold">
          <span>{unit.unitTypeName}</span><span className="text-[#2d9f31]">▮ Activo</span>
        </div>
      </article>
      {children.length > 0 && (
        <>
          <div className="h-5 w-px bg-[#718078]" />
          <div className="flex max-w-full flex-wrap items-start justify-center gap-3 border-t border-[#718078] px-2 pt-4">
            {children.map((child) => (
              <OrganizationBranch key={child.id} unit={child} units={units} />
            ))}
          </div>
        </>
      )}
    </div>
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
