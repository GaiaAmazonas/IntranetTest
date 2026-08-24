import { describe, expect, it } from "vitest";
import { resolveOrganizationSearchTarget } from "./organizational-assignment-model";

const units = [
  { id:"direction", code:"30", name:"DIRECCIÓN", unitTypeName:"DIRECTIVOS", level:1, visualOrder:1, isActive:true },
  { id:"technology", code:"300204", name:"TECNOLOGÍAS DE LA INFORMACIÓN", unitTypeName:"COORDINACIÓN TRANSVERSAL", parentId:"direction", level:2, visualOrder:1, isActive:true },
];
const assignments = [{
  id:"assignment", thirdPartyId:"person", thirdPartyName:"Edgar Eduardo Munar Guevara", documentNumber:"",
  positionId:"position", positionName:"Profesional TI", organizationalUnitId:"technology",
  organizationalUnitCode:"300204", organizationalUnitName:"TECNOLOGÍAS DE LA INFORMACIÓN",
  startDate:"2026-01-01", isPrimary:true, isActive:true,
}];

describe("organization search target", () => {
  it("selects the direct unit of a matching person", () => {
    expect(resolveOrganizationSearchTarget("Edgar Munar", units, assignments)).toBe("technology");
  });

  it("selects a unit by name or exact code", () => {
    expect(resolveOrganizationSearchTarget("tecnologías", units, assignments)).toBe("technology");
    expect(resolveOrganizationSearchTarget("30", units, assignments)).toBe("direction");
  });
});
