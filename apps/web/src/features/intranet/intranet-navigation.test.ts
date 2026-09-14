import { describe, expect, it } from "vitest";
import { intranetNavigation, isIntranetRouteActive } from "./intranet-navigation";

describe("intranetNavigation", () => {
  it("agrupa servicios sin cambiar rutas ni permisos", () => {
    expect(intranetNavigation.filter(x=>x.group==="services").map(x=>x.permission)).toEqual(["INT.APLICACIONES.VER","INT.HELPDESK.VER","INT.CAPACITACIONES.VER"]);
    expect(intranetNavigation.filter(x=>!x.group).map(x=>x.label)).toEqual(["Inicio","Personas","Calendario"]);
  });
  it("mantiene las opciones principales aprobadas", () => {
    expect(intranetNavigation.map(item => item.label)).toEqual([
      "Inicio",
      "Personas",
      "Calendario",
      "Mis aplicaciones",
      "Mis solicitudes de ayuda",
      "Mis capacitaciones",
    ]);
  });

  it("no activa Inicio en las rutas internas", () => {
    expect(isIntranetRouteActive("/intranet", intranetNavigation[0])).toBe(true);
    expect(isIntranetRouteActive("/intranet/personas", intranetNavigation[0])).toBe(false);
    expect(isIntranetRouteActive("/intranet/personas/123", intranetNavigation[1])).toBe(true);
  });
});
