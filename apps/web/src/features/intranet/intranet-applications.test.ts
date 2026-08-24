import { describe, expect, it } from "vitest";
import { authorizedApplications, filterApplications, intranetApplications } from "./intranet-applications";

describe("intranet application catalog", () => {
  it("never exposes an application without its explicit permission", () => {
    expect(authorizedApplications(intranetApplications, () => false)).toEqual([]);
    expect(authorizedApplications(intranetApplications, permission => permission === "INT.APP.ADMINCORE.VER").map(item => item.code)).toEqual(["ADMINCORE"]);
  });
  it("filters by search and category without changing authorization", () => {
    expect(filterApplications(intranetApplications, "administración", "Todas")).toHaveLength(1);
    expect(filterApplications(intranetApplications, "admincore", "Administración")).toHaveLength(1);
    expect(filterApplications(intranetApplications, "help desk", "Todas")).toHaveLength(0);
  });
});
