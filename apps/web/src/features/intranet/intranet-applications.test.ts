import { describe, expect, it } from "vitest";
import { applicationsFromModules, authorizedApplications, filterApplications, intranetApplications } from "./intranet-applications";

describe("intranet application catalog", () => {
  it("keeps the institutional productivity shortcuts fixed", () => {
    expect(authorizedApplications(intranetApplications, () => false).map(item => item.code)).toEqual(["MICROSOFT_TEAMS", "MICROSOFT_OUTLOOK", "GOOGLE_DRIVE"]);
  });
  it("filters by search and category without changing authorization", () => {
    expect(filterApplications(intranetApplications, "correo", "Todas")).toHaveLength(1);
    expect(filterApplications(intranetApplications, "help desk", "Todas")).toHaveLength(0);
  });
  it("builds authorized applications from configured module routes", () => {
    const applications = applicationsFromModules([
      { code: "INT.APP.ADMINCORE", name: "AdminCore", description: "Administración", route: "/admincore", order: 2 },
      { code: "INT.APP.PORTAL", name: "Portal externo", route: "https://example.org", order: 1 },
      { code: "ORG", name: "Organización", route: "/organizacion", order: 0 },
    ]);
    expect(applications.map(item => item.name)).toEqual(["Portal externo", "AdminCore"]);
    expect(applications[0].external).toBe(true);
    expect(applications[1].external).toBe(false);
  });
});
