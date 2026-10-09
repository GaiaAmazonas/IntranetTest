import { describe, expect, it } from "vitest";
import { buildClosurePdf, type ClosurePdfData } from "./solicitudes-closure-pdf";

const request: ClosurePdfData = {
  number: "HD-0000123",
  subject: "Solicitud de prueba",
  description: "Detalle de la solicitud",
  service: "Paz y salvo",
  status: "Cerrada",
  submittedAt: "2026-10-08T16:31:00Z",
  dueDate: "2026-10-12T23:59:00Z",
  managements: [
    { stepCode: "SEGUNDA", formTitle: "SEGUNDA_ETAPA", completedAt: "2026-10-10T14:00:00Z", result: 299540172, observation: "DECISION_SEGUNDA" },
    { stepCode: "PRIMERA", formTitle: "PRIMERA_ETAPA", completedAt: "2026-10-09T14:00:00Z", result: 299540171, observation: "DECISION_PRIMERA", answers: [{ label: "CAMPO_ETAPA", value: "RESPUESTA_ETAPA" }] },
  ],
  comments: [
    { content: "COMENTARIO_INTERNO", publishedAt: "2026-10-09T15:00:00Z", isInternal: true, authorRole: "Equipo interno" },
    { content: "COMENTARIO_PUBLICO", publishedAt: "2026-10-09T16:00:00Z", isInternal: false, authorRole: "Solicitante" },
  ],
};

describe("buildClosurePdf", () => {
  it("incluye la gestión cronológica y solo los comentarios públicos", () => {
    const content = new TextDecoder().decode(buildClosurePdf(request));

    expect(content.startsWith("%PDF-1.4")).toBe(true);
    expect(content.indexOf("PRIMERA_ETAPA")).toBeLessThan(content.indexOf("SEGUNDA_ETAPA"));
    expect(content).toContain("DECISION_PRIMERA");
    expect(content).toContain("DECISION_SEGUNDA");
    expect(content).toContain("CAMPO_ETAPA: RESPUESTA_ETAPA");
    expect(content).toContain("COMENTARIO_PUBLICO");
    expect(content).not.toContain("COMENTARIO_INTERNO");
  });
});
