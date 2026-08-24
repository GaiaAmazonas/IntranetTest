import { describe, expect, it } from "vitest";
import { intranetNavigation, isIntranetRouteActive } from "./intranet-navigation";

describe("intranetNavigation", () => {
  it("mantiene las cinco opciones principales aprobadas", () => {
    expect(intranetNavigation.map(item => item.label)).toEqual([
      "Inicio",
      "Personas",
      "Calendario",
      "Aplicaciones",
      "Helpdesk",
    ]);
  });

  it("no activa Inicio en las rutas internas", () => {
    expect(isIntranetRouteActive("/intranet", intranetNavigation[0])).toBe(true);
    expect(isIntranetRouteActive("/intranet/personas", intranetNavigation[0])).toBe(false);
    expect(isIntranetRouteActive("/intranet/personas/123", intranetNavigation[1])).toBe(true);
  });
});
