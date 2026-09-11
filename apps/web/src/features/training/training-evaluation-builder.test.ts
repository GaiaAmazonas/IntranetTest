import { describe, expect, it } from "vitest";
import { canAddOptions, hasOptions, optionMarker, questionHelp } from "./training-evaluation-rules";

describe("reglas del constructor de preguntas", () => {
  it("diferencia listas editables de Sí o No", () => {
    expect(hasOptions(299541061)).toBe(true);
    expect(hasOptions(299541062)).toBe(true);
    expect(canAddOptions(299541061)).toBe(true);
    expect(canAddOptions(299541062)).toBe(false);
  });

  it("numera opciones editables con letras", () => {
    expect(optionMarker(299541060, 0)).toBe("a)");
    expect(optionMarker(299541061, 2)).toBe("c)");
    expect(optionMarker(299541062, 0)).toBe("");
  });

  it("explica sin ambigüedad las respuestas múltiples", () => {
    expect(questionHelp(299541061)).toContain("lista desplegable");
    expect(questionHelp(299541063)).toContain("una sola línea");
    expect(questionHelp(299541065)).toContain("extremos");
  });
});
