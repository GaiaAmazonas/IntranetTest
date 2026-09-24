import {describe,expect,it} from "vitest";
import {helpdeskRequestsDocument,helpdeskRequestsFileName,type HelpdeskRequestExport} from "./helpdesk-requests-export";

const row:HelpdeskRequestExport={id:"1",number:"HD-0000001",subject:"Acceso",description:"Solicitud de acceso",service:"Accesos y cuentas",requester:"Ana Gaia",requesterUnit:"Tecnología",submittedAt:"2026-09-01T13:00:00Z",firstManagementAt:"2026-09-01T14:00:00Z",responsible:"Edgar Gaia",responsibleUnit:"Tecnología",status:"Cerrada",isFinal:true,dueDate:"2026-09-03",closedAt:"2026-09-02T16:00:00Z",businessManagementDays:1,calendarElapsedDays:1,metSla:true,solutionSummary:"Acceso habilitado"};

describe("helpdesk requests export",()=>{
 it("uses a dated corporate file name",()=>expect(helpdeskRequestsFileName(new Date(2026,8,22))).toBe("Gaia_Helpdesk_Solicitudes_2026-09-22.xlsx"));
 it("includes the complete operational trace",()=>{const document=helpdeskRequestsDocument([row],new Date(2026,8,22));expect(document.columns).toHaveLength(18);expect(document.columns.map(x=>x.header)).toEqual(expect.arrayContaining(["Solicitante","Área solicitante","Fecha de radicación","Responsable","Estado actual","Fecha de cierre","Días hábiles de gestión"]));});
});
