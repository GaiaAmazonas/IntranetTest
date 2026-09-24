import { describe, expect, it } from "vitest";
import { intranetNavigationFromModules, isIntranetRouteActive } from "./intranet-navigation";

const modules = [
  { id:"people", code:"INT.PERSONAS", name:"Directorio", description:"Personas de Gaia", route:"/intranet/personas", icon:"people", order:7 },
  { id:"home", code:"INT.INICIO", name:"Portada", description:"Inicio", route:"/intranet", icon:"home", order:6 },
  { id:"help", code:"INT.HELPDESK", name:"Mis solicitudes", description:"Consulta tus casos", route:"/intranet/helpdesk", icon:"helpdesk", order:10 },
  { id:"app", code:"INT.APP.ADMINCORE", name:"AdminCore", description:"Administración", route:"/admincore", icon:"admincore", order:11 },
];

describe("intranetNavigationFromModules", () => {
  it("construye y ordena el menú usando los datos recibidos de seguridad", () => {
    const navigation = intranetNavigationFromModules(modules);
    expect(navigation.map(item => item.label)).toEqual(["Portada", "Directorio", "Mis solicitudes"]);
    expect(navigation[0].exact).toBe(true);
    expect(navigation[2].group).toBe("services");
  });

  it("separa las aplicaciones del menú de navegación", () => {
    expect(intranetNavigationFromModules(modules).some(item => item.code === "INT.APP.ADMINCORE")).toBe(false);
  });

  it("no activa Inicio en las rutas internas", () => {
    const navigation = intranetNavigationFromModules(modules);
    expect(isIntranetRouteActive("/intranet", navigation[0])).toBe(true);
    expect(isIntranetRouteActive("/intranet/personas", navigation[0])).toBe(false);
    expect(isIntranetRouteActive("/intranet/personas/123", navigation[1])).toBe(true);
  });
});
