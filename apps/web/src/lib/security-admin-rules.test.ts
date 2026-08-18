import { describe, expect, it } from "vitest";
import { applyVisiblePermissions, permissionDelta, resolveGaiaTheme } from "./security-admin-rules";

describe("security administration UI rules", () => {
  it("selects and removes only visible permissions while preserving the rest", () => {
    const selected = applyVisiblePermissions(new Set(["A", "HIDDEN"]), ["A", "B"], true);
    expect([...selected].sort()).toEqual(["A", "B", "HIDDEN"]);
    expect([...applyVisiblePermissions(selected, ["A", "B"], false)]).toEqual(["HIDDEN"]);
  });

  it("reports pending additions and removals", () => {
    expect(permissionDelta(["A", "B"], new Set(["B", "C", "D"]))).toEqual({ added: 2, removed: 1 });
  });

  it("keeps Gaia Clásico available and defaults safely to Gaia Renovado", () => {
    expect(resolveGaiaTheme("classic")).toBe("classic");
    expect(resolveGaiaTheme("unknown")).toBe("renewed");
  });
});
