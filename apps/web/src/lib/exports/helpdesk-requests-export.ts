import {downloadGaiaWorkbook,type GaiaExcelDocument} from "./gaia-excel-exporter";

export type HelpdeskRequestExport={id:string;number:string;subject:string;description:string|null;service:string;requester:string;requesterUnit:string|null;submittedAt:string|null;firstManagementAt:string|null;responsible:string|null;responsibleUnit:string|null;status:string;isFinal:boolean;dueDate:string|null;closedAt:string|null;businessManagementDays:number|null;calendarElapsedDays:number;metSla:boolean|null;solutionSummary:string|null};

export function helpdeskRequestsFileName(generatedAt=new Date()){
 const date=`${generatedAt.getFullYear()}-${String(generatedAt.getMonth()+1).padStart(2,"0")}-${String(generatedAt.getDate()).padStart(2,"0")}`;
 return `Gaia_Helpdesk_Solicitudes_${date}.xlsx`;
}

export function helpdeskRequestsDocument(rows:HelpdeskRequestExport[],generatedAt=new Date()):GaiaExcelDocument<HelpdeskRequestExport>{
 return{sheetName:"Solicitudes",title:"Helpdesk - Solicitudes",subtitle:"Detalle consolidado de atención y tiempos de gestión",moduleName:"Helpdesk",fileName:helpdeskRequestsFileName(generatedAt),generatedAt,rows,institutionalNote:"Fuente: Gaia Enterprise Platform · Exportación completa del modelo de solicitudes disponible para el usuario autenticado.",columns:[
  {header:"Número",key:"number",width:17,value:r=>r.number},
  {header:"Servicio",key:"service",width:30,value:r=>r.service,wrap:true},
  {header:"Asunto",key:"subject",width:36,value:r=>r.subject,wrap:true},
  {header:"Descripción",key:"description",width:48,value:r=>plainText(r.description),wrap:true},
  {header:"Solicitante",key:"requester",width:30,value:r=>r.requester},
  {header:"Área solicitante",key:"requesterUnit",width:30,value:r=>r.requesterUnit??"Sin área registrada"},
  {header:"Fecha de radicación",key:"submittedAt",width:21,value:r=>excelDate(r.submittedAt),numberFormat:"yyyy-mm-dd hh:mm",alignment:"center"},
  {header:"Primera gestión",key:"firstManagementAt",width:21,value:r=>excelDate(r.firstManagementAt),numberFormat:"yyyy-mm-dd hh:mm",alignment:"center"},
  {header:"Responsable",key:"responsible",width:30,value:r=>r.responsible??"Sin asignar"},
  {header:"Área responsable",key:"responsibleUnit",width:30,value:r=>r.responsibleUnit??"Sin área asignada"},
  {header:"Estado actual",key:"status",width:22,value:r=>r.status,alignment:"center"},
  {header:"Situación",key:"closureStatus",width:15,value:r=>r.closedAt||r.isFinal?"Cerrada":"En gestión",alignment:"center"},
  {header:"Fecha límite",key:"dueDate",width:17,value:r=>excelDate(r.dueDate),numberFormat:"yyyy-mm-dd",alignment:"center"},
  {header:"Fecha de cierre",key:"closedAt",width:21,value:r=>excelDate(r.closedAt),numberFormat:"yyyy-mm-dd hh:mm",alignment:"center"},
  {header:"Días hábiles de gestión",key:"businessManagementDays",width:22,value:r=>r.businessManagementDays,numberFormat:"#,##0",alignment:"center"},
  {header:"Días calendario transcurridos",key:"calendarElapsedDays",width:25,value:r=>r.calendarElapsedDays,numberFormat:"#,##0",alignment:"center"},
  {header:"Cumplió SLA",key:"metSla",width:15,value:r=>r.metSla===null?"Por determinar":r.metSla?"Sí":"No",alignment:"center"},
  {header:"Resumen de solución",key:"solutionSummary",width:48,value:r=>plainText(r.solutionSummary),wrap:true},
 ]};
}

export async function exportHelpdeskRequests(rows:HelpdeskRequestExport[]){await downloadGaiaWorkbook(helpdeskRequestsDocument(rows))}
function excelDate(value:string|null){if(!value)return null;const date=new Date(value);return Number.isNaN(date.getTime())?null:date}
function plainText(value:string|null){return(value??"").replace(/<[^>]*>/g," ").replace(/\s+/g," ").trim()}
