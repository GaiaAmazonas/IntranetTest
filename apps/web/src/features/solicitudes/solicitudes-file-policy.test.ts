import { describe, expect, it } from "vitest";
import { solicitudesDefaultFileAccept, solicitudesFileAccept } from "./solicitudes-file-policy";

describe("solicitudes file policy", () => {
  it("offers every approved document type by default", () => {
    expect(solicitudesFileAccept()).toBe(".pdf,.png,.jpg,.jpeg,.docx,.xlsx,.pptx");
  });

  it("preserves a narrower field configuration", () => {
    expect(solicitudesFileAccept(".pdf,.docx")).toBe(".pdf,.docx");
  });

  it("exports one stable default for every generic uploader", () => {
    expect(solicitudesDefaultFileAccept).toContain(".pptx");
  });
});
