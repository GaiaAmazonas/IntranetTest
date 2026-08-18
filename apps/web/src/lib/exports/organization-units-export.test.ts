import { describe, expect, it } from "vitest";
import fs from "node:fs/promises";
import { createGaiaWorkbook } from "./gaia-excel-exporter";
import { organizationUnitsDocument, organizationUnitsFileName, orderOrganizationUnits, unitsForExport, type OrganizationUnitExport } from "./organization-units-export";

const units: OrganizationUnitExport[] = [
  { id: "child-10", code: "10", name: "Décima", parentId: "root", unitTypeName: "Operativa", level: 2, effectiveFrom: "2026-01-01", isActive: false },
  { id: "no-code", code: "", name: "Sin código", parentId: "root", unitTypeName: "Operativa", level: 2, effectiveFrom: "2026-01-01", isActive: true },
  { id: "root", code: "1", name: "Raíz", unitTypeName: "Directivos", level: 1, effectiveFrom: "2025-01-01", isActive: true },
  { id: "child-2", code: "2", name: "Segunda", parentId: "root", unitTypeName: "Operativa", level: 2, effectiveFrom: "2026-01-01", effectiveTo: "2026-12-31", isActive: true },
];

describe("exportación de unidades", () => {
  it("ordena por jerarquía y código natural, dejando códigos vacíos al final", () => {
    expect(orderOrganizationUnits(units).map(unit => unit.id)).toEqual(["root", "child-2", "child-10", "no-code"]);
  });

  it("exporta todo sin filtro y conserva ancestros cuando filtra", () => {
    expect(unitsForExport(units, "")).toHaveLength(4);
    expect(unitsForExport(units, "Décima").map(unit => unit.id)).toEqual(["root", "child-10"]);
  });

  it("crea un nombre explícito para archivos completos y filtrados", () => {
    const date = new Date(2026, 7, 13, 9, 0);
    expect(organizationUnitsFileName(date, false)).toBe("Gaia_Unidades_2026-08-13.xlsx");
    expect(organizationUnitsFileName(date, true)).toBe("Gaia_Unidades_Filtradas_2026-08-13.xlsx");
  });

  it("define columnas, nulos, fechas y estados funcionales", () => {
    const document = organizationUnitsDocument(units, "", new Date(2026, 7, 13));
    expect(document.columns.map(column => column.header)).toEqual(["Código", "Nombre corto", "Nombre oficial", "Tipo de unidad", "Unidad padre", "Sede", "Nivel", "Descripción", "Vigente desde", "Vigente hasta", "Estado"]);
    const inactive = document.rows.find(unit => unit.id === "child-10")!;
    expect(document.columns.find(column => column.key === "status")?.value(inactive)).toBe("Inactivo");
    expect(document.columns.find(column => column.key === "site")?.value(inactive)).toBe("");
    expect(document.columns.find(column => column.key === "effectiveTo")?.value(inactive)).toBeNull();
    expect(document.columns.find(column => column.key === "level")?.value(document.rows[0])).toBe(1);
  });

  it.runIf(Boolean(process.env.GAIA_EXCEL_QA))("genera el libro corporativo verificable", async () => {
    const document = organizationUnitsDocument(units, "", new Date(2026, 7, 13, 13, 45));
    const logo = await fs.readFile(process.env.GAIA_EXCEL_LOGO!);
    document.logoDataUrl = `data:image/png;base64,${logo.toString("base64")}`;
    const workbook = await createGaiaWorkbook(document);
    await workbook.xlsx.writeFile(process.env.GAIA_EXCEL_QA!);
    expect(workbook.worksheets[0].getImages()).toHaveLength(1);
    expect(workbook.worksheets[0].views[0]).toMatchObject({ state: "frozen", ySplit: 8 });
  });
});
