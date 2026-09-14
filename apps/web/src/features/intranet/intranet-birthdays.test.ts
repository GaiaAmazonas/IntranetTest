import {describe,it,expect} from "vitest";
import {upcomingBirthdaysWithin} from "./intranet-birthdays";
describe("cumpleaños próximos",()=>{
 it("excluye los que pasaron y los de hoy",()=>{expect(upcomingBirthdaysWithin([{day:8,month:9},{day:14,month:9},{day:19,month:9},{day:3,month:10}],new Date(2026,8,14))).toEqual([{day:19,month:9},{day:3,month:10}])});
 it("incluye enero cuando estamos en diciembre",()=>{expect(upcomingBirthdaysWithin([{day:3,month:1},{day:5,month:12}],new Date(2026,11,20))).toEqual([{day:3,month:1}])});
 it("ordena fechas y limita el horizonte",()=>{expect(upcomingBirthdaysWithin([{day:20,month:12},{day:1,month:10},{day:15,month:9}],new Date(2026,8,14))).toEqual([{day:15,month:9},{day:1,month:10}])});
});
