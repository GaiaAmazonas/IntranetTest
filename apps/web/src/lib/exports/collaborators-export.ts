import { downloadGaiaWorkbook, type GaiaExcelDocument } from "./gaia-excel-exporter";

export type CollaboratorExportRow = {
  fullName: string;
  documentType: string;
  documentNumber: string;
  email: string;
  emailType: string;
  phone: string;
  phoneType: string;
  position: string;
  organizationalUnitCode: string;
  organizationalUnit: string;
  isActive: boolean;
};

export function collaboratorsDocument(rows: CollaboratorExportRow[], generatedAt = new Date()): GaiaExcelDocument<CollaboratorExportRow> {
  const date = generatedAt.toISOString().slice(0, 10);
  return {
    sheetName: "Colaboradores",
    title: "Talento Humano - Colaboradores",
    subtitle: "Directorio según los filtros aplicados",
    moduleName: "Talento Humano",
    fileName: `colaboradores-${date}.xlsx`,
    generatedAt,
    rows,
    institutionalNote: "Fuente: Dataverse · Exportación del directorio de colaboradores.",
    columns: [
      { header: "Colaborador", key: "name", width: 42, value: row => row.fullName, wrap: true },
      { header: "Tipo de documento", key: "documentType", width: 22, value: row => row.documentType },
      { header: "Número de documento", key: "documentNumber", width: 22, value: row => row.documentNumber },
      { header: "Correo", key: "email", width: 38, value: row => row.email },
      { header: "Tipo de correo", key: "emailType", width: 18, value: row => row.emailType, alignment: "center" },
      { header: "Teléfono", key: "phone", width: 20, value: row => row.phone },
      { header: "Tipo de teléfono", key: "phoneType", width: 20, value: row => row.phoneType, alignment: "center" },
      { header: "Cargo vigente", key: "position", width: 34, value: row => row.position, wrap: true },
      { header: "Código de unidad", key: "unitCode", width: 18, value: row => row.organizationalUnitCode },
      { header: "Unidad vigente", key: "unit", width: 40, value: row => row.organizationalUnit, wrap: true },
      { header: "Estado", key: "status", width: 14, value: row => row.isActive ? "Activo" : "Inactivo", alignment: "center" },
    ],
  };
}

export async function exportCollaborators(rows: CollaboratorExportRow[]) {
  if (!rows.length) throw new Error("No hay colaboradores que coincidan con los filtros actuales.");
  await downloadGaiaWorkbook(collaboratorsDocument(rows));
}
