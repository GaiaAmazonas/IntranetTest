import { downloadGaiaWorkbook, type GaiaExcelDocument } from "./gaia-excel-exporter";

export type AssignmentExportRow = {
  thirdPartyName: string;
  positionName: string;
  organizationalUnitCode: string;
  organizationalUnitName: string;
  organizationalUnitPath: string;
  startDate?: string;
  endDate?: string;
  isPrimary: boolean;
  isActive: boolean;
  isCurrent: boolean;
};

export function organizationalAssignmentsFileName(generatedAt: Date) {
  const date = `${generatedAt.getFullYear()}-${String(generatedAt.getMonth() + 1).padStart(2, "0")}-${String(generatedAt.getDate()).padStart(2, "0")}`;
  return `asignaciones-organizacionales-${date}.xlsx`;
}

export function organizationalAssignmentsDocument(rows: AssignmentExportRow[], generatedAt = new Date()): GaiaExcelDocument<AssignmentExportRow> {
  return {
    sheetName: "Asignaciones",
    title: "Asignaciones organizacionales",
    subtitle: "Resultados según los filtros aplicados en AdminCore",
    moduleName: "Organización",
    fileName: organizationalAssignmentsFileName(generatedAt),
    generatedAt,
    rows,
    institutionalNote: "Fuente: Dataverse · No incluye identificadores personales ni campos técnicos.",
    columns: [
      { header: "Colaborador", key: "person", width: 36, value: row => row.thirdPartyName, wrap: true },
      { header: "Cargo", key: "position", width: 34, value: row => row.positionName, wrap: true },
      { header: "Código unidad", key: "unitCode", width: 16, value: row => row.organizationalUnitCode },
      { header: "Unidad", key: "unit", width: 38, value: row => row.organizationalUnitName, wrap: true },
      { header: "Ruta jerárquica", key: "path", width: 55, value: row => row.organizationalUnitPath, wrap: true },
      { header: "Fecha de inicio", key: "start", width: 17, value: row => excelDate(row.startDate), numberFormat: "yyyy-mm-dd", alignment: "center" },
      { header: "Fecha de finalización", key: "end", width: 21, value: row => excelDate(row.endDate), numberFormat: "yyyy-mm-dd", alignment: "center" },
      { header: "Estado", key: "status", width: 14, value: row => row.isActive ? "Activo" : "Inactivo", alignment: "center" },
    ],
  };
}

export async function exportOrganizationalAssignments(rows: AssignmentExportRow[]) {
  if (!rows.length) throw new Error("No hay asignaciones que coincidan con los filtros actuales.");
  await downloadGaiaWorkbook(organizationalAssignmentsDocument(rows));
}

function excelDate(value?: string) {
  if (!value) return null;
  const date = new Date(`${value.slice(0, 10)}T00:00:00`);
  return Number.isNaN(date.getTime()) ? null : date;
}
