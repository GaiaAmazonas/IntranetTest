import { describe, expect, it } from "vitest";
import { organizationalAssignmentsDocument, organizationalAssignmentsFileName } from "./organizational-assignments-export";

describe("organizational assignments export", () => {
  it("uses a stable descriptive file name", () => {
    expect(organizationalAssignmentsFileName(new Date(2026, 7, 21))).toBe("asignaciones-organizacionales-2026-08-21.xlsx");
  });

  it("exports authorized functional fields without technical or document identifiers", () => {
    const document = organizationalAssignmentsDocument([{
      thirdPartyName: "Edgar Munar",
      positionName: "Profesional TI",
      organizationalUnitCode: "300204",
      organizationalUnitName: "TECNOLOGÍAS DE LA INFORMACIÓN",
      organizationalUnitPath: "DIRECCIÓN > TECNOLOGÍAS DE LA INFORMACIÓN",
      startDate: "2026-01-15",
      isPrimary: true,
      isActive: true,
      isCurrent: true,
    }], new Date(2026, 7, 21));

    expect(document.columns.map(column => column.header)).toEqual([
      "Colaborador", "Cargo", "Código unidad", "Unidad", "Ruta jerárquica",
      "Fecha de inicio", "Fecha de finalización", "Estado",
    ]);
    expect(document.columns.some(column => /guid|identificaci|documento/i.test(column.header))).toBe(false);
    expect(document.columns.find(column => column.key === "start")?.value(document.rows[0])).toBeInstanceOf(Date);
  });
});
