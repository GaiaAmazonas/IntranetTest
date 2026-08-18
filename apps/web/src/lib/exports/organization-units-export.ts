import { downloadGaiaWorkbook, type GaiaExcelDocument } from "./gaia-excel-exporter";

export type OrganizationUnitExport = {
  id: string;
  code: string;
  name: string;
  shortName?: string;
  unitTypeName: string;
  parentId?: string;
  siteName?: string;
  level: number;
  description?: string;
  effectiveFrom: string;
  effectiveTo?: string;
  isActive: boolean;
};

export function naturalUnitCodeCompare(left: OrganizationUnitExport, right: OrganizationUnitExport) {
  const leftCode = left.code.trim();
  const rightCode = right.code.trim();
  if (!leftCode && rightCode) return 1;
  if (leftCode && !rightCode) return -1;
  const codeResult = leftCode.localeCompare(rightCode, "es", { numeric: true, sensitivity: "base" });
  return codeResult || left.name.localeCompare(right.name, "es", { sensitivity: "base" });
}

export function orderOrganizationUnits(units: OrganizationUnitExport[]) {
  const children = new Map<string | undefined, OrganizationUnitExport[]>();
  const ids = new Set(units.map(unit => unit.id));
  units.forEach(unit => {
    const parent = unit.parentId && ids.has(unit.parentId) ? unit.parentId : undefined;
    children.set(parent, [...(children.get(parent) ?? []), unit]);
  });
  children.forEach(group => group.sort(naturalUnitCodeCompare));
  const ordered: OrganizationUnitExport[] = [];
  const visited = new Set<string>();
  const visit = (unit: OrganizationUnitExport) => {
    if (visited.has(unit.id)) return;
    visited.add(unit.id);
    ordered.push(unit);
    (children.get(unit.id) ?? []).forEach(visit);
  };
  (children.get(undefined) ?? []).forEach(visit);
  [...units].sort(naturalUnitCodeCompare).forEach(visit);
  return ordered;
}

export function unitsForExport(units: OrganizationUnitExport[], search: string) {
  const ordered = orderOrganizationUnits(units);
  const term = search.trim().toLocaleLowerCase("es");
  if (!term) return ordered;
  const matches = units.filter(unit => unit.name.toLocaleLowerCase("es").includes(term) || unit.code.toLocaleLowerCase("es").includes(term));
  const visible = new Set(matches.map(unit => unit.id));
  const byId = new Map(units.map(unit => [unit.id, unit]));
  matches.forEach(unit => {
    let parentId = unit.parentId;
    while (parentId) {
      visible.add(parentId);
      parentId = byId.get(parentId)?.parentId;
    }
  });
  return ordered.filter(unit => visible.has(unit.id));
}

export function organizationUnitsFileName(generatedAt: Date, filtered: boolean) {
  const date = `${generatedAt.getFullYear()}-${String(generatedAt.getMonth() + 1).padStart(2, "0")}-${String(generatedAt.getDate()).padStart(2, "0")}`;
  return `Gaia_Unidades${filtered ? "_Filtradas" : ""}_${date}.xlsx`;
}

export function organizationUnitsDocument(units: OrganizationUnitExport[], search: string, generatedAt = new Date()): GaiaExcelDocument<OrganizationUnitExport> {
  const rows = unitsForExport(units, search);
  const names = new Map(units.map(unit => [unit.id, unit.name]));
  const filtered = Boolean(search.trim());
  return {
    sheetName: "Unidades",
    title: "Estructura Organizacional - Unidades",
    subtitle: filtered ? `Resultados filtrados por “${search.trim()}”` : "Estructura organizacional consolidada",
    moduleName: "Organización",
    fileName: organizationUnitsFileName(generatedAt, filtered),
    generatedAt,
    rows,
    institutionalNote: "Fuente: Gaia Enterprise Platform · Información visible para el usuario autenticado.",
    columns: [
      { header: "Código", key: "code", width: 15, value: unit => unit.code || "Sin código" },
      { header: "Nombre corto", key: "shortName", width: 25, value: unit => unit.shortName ?? "" },
      { header: "Nombre oficial", key: "name", width: 42, value: unit => unit.name, wrap: true, indent: unit => Math.max(0, unit.level - 1) },
      { header: "Tipo de unidad", key: "type", width: 28, value: unit => unit.unitTypeName },
      { header: "Unidad padre", key: "parent", width: 36, value: unit => unit.parentId ? (names.get(unit.parentId) ?? "") : "Unidad raíz", wrap: true },
      { header: "Sede", key: "site", width: 22, value: unit => unit.siteName ?? "" },
      { header: "Nivel", key: "level", width: 10, value: unit => unit.level, alignment: "center" },
      { header: "Descripción", key: "description", width: 45, value: unit => unit.description ?? "", wrap: true },
      { header: "Vigente desde", key: "effectiveFrom", width: 16, value: unit => excelDate(unit.effectiveFrom), numberFormat: "yyyy-mm-dd", alignment: "center" },
      { header: "Vigente hasta", key: "effectiveTo", width: 16, value: unit => excelDate(unit.effectiveTo), numberFormat: "yyyy-mm-dd", alignment: "center" },
      { header: "Estado", key: "status", width: 13, value: unit => unit.isActive ? "Activo" : "Inactivo", alignment: "center" },
    ],
  };
}

export async function exportOrganizationUnits(units: OrganizationUnitExport[], search: string) {
  await downloadGaiaWorkbook(organizationUnitsDocument(units, search));
}

function excelDate(value?: string) {
  if (!value) return null;
  const date = new Date(`${value.slice(0, 10)}T00:00:00`);
  return Number.isNaN(date.getTime()) ? null : date;
}
