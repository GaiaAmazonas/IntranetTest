import {describe,expect,it} from "vitest";
import {orderCompletedManagements} from "./solicitudes-workflow-order";

const item=(id:string,completedAt:string|null,availableAt:string|null,execution:number)=>({id,completedAt,availableAt,execution});

describe("solicitudes workflow chronological order",()=>{
  it("orders completed stage forms by the instant they were managed",()=>{
    const result=orderCompletedManagements([
      item("financial","2026-10-08T15:00:00Z","2026-10-08T14:00:00Z",1),
      item("initial","2026-10-08T13:00:00Z","2026-10-08T12:00:00Z",4),
      item("legal","2026-10-08T16:00:00Z","2026-10-08T14:00:00Z",2),
    ]);
    expect(result.map(value=>value.id)).toEqual(["initial","financial","legal"]);
  });

  it("uses availability, execution and id only as stable fallbacks",()=>{
    const result=orderCompletedManagements([
      item("b",null,"2026-10-08T12:00:00Z",2),
      item("c",null,"2026-10-08T12:00:00Z",1),
      item("a",null,"2026-10-08T12:00:00Z",1),
    ]);
    expect(result.map(value=>value.id)).toEqual(["a","c","b"]);
  });

  it("does not mutate the API collection",()=>{
    const source=[item("later","2026-10-08T14:00:00Z",null,2),item("first","2026-10-08T13:00:00Z",null,1)];
    orderCompletedManagements(source);
    expect(source.map(value=>value.id)).toEqual(["later","first"]);
  });
});
