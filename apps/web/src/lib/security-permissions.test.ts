import { describe, expect, it } from "vitest";
import { hasPermission } from "./security-permissions";

describe("hasPermission", () => {
  it("accepts a directly assigned permission without case sensitivity", () => {
    expect(hasPermission(["ti.roles.ver"], "TI.ROLES.VER")).toBe(true);
  });

  it("accepts navigation when any permission in the expression is granted", () => {
    expect(hasPermission(["TI.MODULOS.VER"], "TI.USUARIOS.VER|TI.ROLES.VER|TI.MODULOS.VER")).toBe(true);
  });

  it("rejects navigation when no permission is granted", () => {
    expect(hasPermission(["INICIO.VER"], "TI.USUARIOS.VER|TI.ROLES.VER|TI.MODULOS.VER")).toBe(false);
  });
});
