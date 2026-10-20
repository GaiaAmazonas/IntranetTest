import { describe, expect, it } from "vitest";
import { defaultLoginPrimaryTitle, loginDisplayText, loginPrimaryTitle, normalizeLoginLineBreaks } from "./login-configuration";

describe("loginPrimaryTitle", () => {
  it("muestra el valor publicado en Texto alternativo imagen", () => {
    expect(loginPrimaryTitle("Todo Gaia, en un solo lugar *3")).toBe("Todo Gaia, en un solo lugar *3");
  });

  it("convierte etiquetas br heredadas en saltos de línea seguros", () => {
    expect(loginPrimaryTitle("Todo Gaia, <br>en un solo lugar")).toBe("Todo Gaia,\nen un solo lugar");
    expect(normalizeLoginLineBreaks("Línea 1<br />Línea 2")).toBe("Línea 1\nLínea 2");
  });

  it("conserva saltos escritos con Enter en los demás textos publicados", () => {
    expect(loginDisplayText("Primera línea\nSegunda línea", "Respaldo")).toBe("Primera línea\nSegunda línea");
  });

  it("conserva un título seguro cuando la instantánea está vacía o contiene una descripción extensa", () => {
    expect(loginPrimaryTitle(null)).toBe(defaultLoginPrimaryTitle);
    expect(loginPrimaryTitle("Información, personas y herramientas conectadas para facilitar nuestro trabajo en toda la Amazonía colombiana."))
      .toBe(defaultLoginPrimaryTitle);
  });
});
