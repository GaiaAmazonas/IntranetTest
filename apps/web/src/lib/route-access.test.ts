import { describe, expect, it } from "vitest";
import { routeRuleFor } from "./route-access";

describe("routeRuleFor", () => {
  it("requires the intranet and feature permissions for internal pages", () => {
    expect(routeRuleFor("/intranet/personas")?.requirements).toEqual(["INTRANET.VER", "INT.PERSONAS.VER"]);
  });

  it("does not reuse the Inicio permission for internal intranet routes", () => {
    expect(routeRuleFor("/intranet/calendario")?.requirements).not.toContain("INT.INICIO.VER");
  });

  it("requires AdminCore plus the operational permission for direct URLs", () => {
    expect(routeRuleFor("/organizacion")?.requirements[0]).toBe("INT.APP.ADMINCORE.VER");
    expect(routeRuleFor("/seguridad/usuarios")?.requirements).toContain("TI.USUARIOS.VER");
    expect(routeRuleFor("/seguridad/modulos")?.requirements[0]).toContain("TI.MODULOS.ADMINISTRAR");
  });

  it("protects assignments and workforce links with their own permissions", () => {
    expect(routeRuleFor("/organizacion")?.requirements[1]).toContain("ORG.ASIGNACIONES.VER");
    expect(routeRuleFor("/talento-humano/vinculaciones")?.requirements).toContain("TH.VINCULACIONES.VER");
  });

  it("does not protect the public login route", () => {
    expect(routeRuleFor("/")).toBeNull();
  });

  it("normalizes trailing slashes before resolving protected routes", () => {
    expect(routeRuleFor("/intranet/")?.requirements).toEqual(["INTRANET.VER", "INT.INICIO.VER"]);
    expect(routeRuleFor("/intranet/personas/")?.requirements).toEqual(["INTRANET.VER", "INT.PERSONAS.VER"]);
  });
});
