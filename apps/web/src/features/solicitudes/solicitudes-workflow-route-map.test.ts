import { describe, expect, it } from "vitest";
import { buildWorkflowRouteMapLayout, type WorkflowRouteMapData } from "./solicitudes-workflow-route-map";

const workflow: WorkflowRouteMapData = {
  managements: [{ stepId: "initial", status: 299540194, formTitle: "Revisión inicial" }, { stepId: "legal", status: 299540192, formTitle: "Revisión jurídica" }],
  steps: [
    { id: "initial", code: "REVISION_INICIAL", order: 0, initial: true, final: false, active: true, positionX: 0, positionY: 100 },
    { id: "legal", code: "REVISION_JURIDICA", order: 1, initial: false, final: false, active: true, positionX: 300, positionY: 0 },
    { id: "finance", code: "REVISION_FINANCIERA", order: 2, initial: false, final: false, active: true, positionX: 300, positionY: 200 },
    { id: "final", code: "CIERRE_FINAL", order: 3, initial: false, final: true, active: true, positionX: 600, positionY: 100 },
    { id: "inactive", code: "INACTIVA", order: 4, initial: false, final: false, active: false, positionX: 900, positionY: 0 },
  ], routes: [],
};

describe("workflow route map layout", () => {
  it("uses all available width and preserves top-to-bottom branches", () => { const result = buildWorkflowRouteMapLayout(workflow); expect(result.width).toBeGreaterThanOrEqual(760); expect(result.nodes).toHaveLength(4); expect(result.nodes.find(item => item.step.id === "legal")!.top).toBeLessThan(result.nodes.find(item => item.step.id === "finance")!.top); });
  it("derives labels and visual states from the execution", () => { const result = buildWorkflowRouteMapLayout(workflow); expect(result.nodes.find(item => item.step.id === "initial")).toMatchObject({ label: "Revisión inicial", status: "completed" }); expect(result.nodes.find(item => item.step.id === "legal")).toMatchObject({ label: "Revisión jurídica", status: "current" }); expect(result.nodes.find(item => item.step.id === "final")).toMatchObject({ label: "Cierre Final", status: "pending" }); });
});
