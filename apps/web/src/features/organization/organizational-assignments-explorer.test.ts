import { describe, expect, it } from "vitest";
import { isAssignmentCurrent } from "./organizational-assignment-model";

describe("organizational assignment vigency", () => {
  const today = new Date("2026-08-21T12:00:00Z");

  it("requires an active Dataverse row inside its date range", () => {
    expect(isAssignmentCurrent({ isActive:true, startDate:"2026-01-01", endDate:"2026-12-31" }, today)).toBe(true);
    expect(isAssignmentCurrent({ isActive:true, startDate:"2026-09-01" }, today)).toBe(false);
    expect(isAssignmentCurrent({ isActive:true, endDate:"2026-08-20" }, today)).toBe(false);
    expect(isAssignmentCurrent({ isActive:false }, today)).toBe(false);
  });

  it("supports open date ranges", () => {
    expect(isAssignmentCurrent({ isActive:true }, today)).toBe(true);
  });
});
