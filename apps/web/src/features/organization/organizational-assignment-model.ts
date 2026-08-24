export type AssignmentVigency = { isActive:boolean; startDate?:string; endDate?:string };

export function isAssignmentCurrent(row: AssignmentVigency, today = new Date()) {
  const date = today.toISOString().slice(0, 10);
  return row.isActive && (!row.startDate || row.startDate.slice(0, 10) <= date) && (!row.endDate || row.endDate.slice(0, 10) >= date);
}

type SearchUnit = { id:string; code:string; name:string };
type SearchAssignment = AssignmentVigency & { thirdPartyName:string; positionName:string; organizationalUnitId:string; organizationalUnitCode:string; organizationalUnitName:string };

export function resolveOrganizationSearchTarget(search:string, units:SearchUnit[], assignments:SearchAssignment[]) {
  const term=normalize(search.trim());
  if(!term)return undefined;
  const unitMatch=units.find(unit=>normalize(unit.code)===term)
    ?? units.find(unit=>normalize(unit.name).startsWith(term))
    ?? units.find(unit=>normalize(`${unit.name} ${unit.code}`).includes(term));
  if(unitMatch)return unitMatch.id;
  const pool=[...assignments].sort((left,right)=>Number(isAssignmentCurrent(right))-Number(isAssignmentCurrent(left)));
  return (pool.find(row=>matchesSearch(row.thirdPartyName,term))
    ?? pool.find(row=>matchesSearch(`${row.positionName} ${row.organizationalUnitName} ${row.organizationalUnitCode}`,term)))?.organizationalUnitId;
}

function normalize(value:string) { return value.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es"); }
function matchesSearch(value:string,term:string) { const source=normalize(value);return term.split(/\s+/).filter(Boolean).every(token=>source.includes(token)); }
