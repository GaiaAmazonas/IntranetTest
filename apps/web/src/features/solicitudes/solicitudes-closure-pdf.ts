export type ClosurePdfAnswer = { label:string; value?:string|null; options?:string[] };
export type ClosurePdfManagement = { stepCode:string; formTitle?:string|null; unitName?:string|null; responsibleName?:string|null; result?:number|null; observation?:string|null; completedAt?:string|null; answers?:ClosurePdfAnswer[] };
export type ClosurePdfComment = { content:string; publishedAt:string; isInternal?:boolean; authorRole?:string|null };
export type ClosurePdfData = { number:string; subject:string; description?:string|null; service:string; status:string; submittedAt?:string|null; dueDate?:string|null; managements:ClosurePdfManagement[]; comments?:ClosurePdfComment[] };
type ImageData = { bytes:Uint8Array; width:number; height:number };
type Page = { commands:string[] };

const W=612,H=842,L=44,R=568,TOP=730,BOTTOM=58,CW=R-L;
const C={ teal:"0.05 0.31 0.32", dark:"0.04 0.20 0.20", soft:"0.91 0.96 0.95", green:"0.11 0.48 0.36", greenSoft:"0.91 0.97 0.94", purple:"0.43 0.16 0.43", ink:"0.08 0.14 0.13", muted:"0.38 0.45 0.42", border:"0.82 0.87 0.85", panel:"0.97 0.98 0.98", white:"1 1 1" };

export async function downloadClosurePdf(data:ClosurePdfData) {
  const pdf=buildClosurePdf(data,await loadLogo());
  const link=document.createElement("a");
  link.href=URL.createObjectURL(new Blob([pdf],{type:"application/pdf"}));
  link.download=`Constancia-${data.number}.pdf`; link.click();
  window.setTimeout(()=>URL.revokeObjectURL(link.href),1000);
}

export function buildClosurePdf(data:ClosurePdfData,logo:ImageData|null=null) {
  const pdf=new PdfDocument(logo);
  const stages=data.managements.filter(x=>x.completedAt).sort((a,b)=>timestamp(a.completedAt)-timestamp(b.completedAt));
  const comments=(data.comments??[]).filter(x=>!x.isInternal&&x.content.trim()).sort((a,b)=>timestamp(a.publishedAt)-timestamp(b.publishedAt));
  pdf.hero(data);
  pdf.summary(data,stages.at(-1)?.observation);
  pdf.section("Gestión realizada por etapa",`${stages.length} etapas finalizadas`);
  if (stages.length) stages.forEach((x,i)=>pdf.stage(x,i+1));
  else pdf.empty("No hay gestiones de etapa finalizadas para esta solicitud.");
  pdf.section("Conversación de la solicitud",`${comments.length} comentarios visibles`,comments.length?110:50);
  if (comments.length) comments.forEach(x=>pdf.comment(x));
  else pdf.empty("No se registraron comentarios visibles para el solicitante.");
  pdf.note("Esta constancia consolida la información registrada en el sistema institucional Gaia. Las notas internas del equipo no forman parte del documento descargable.");
  return pdf.build();
}

class PdfDocument {
  private pages:Page[]=[]; private page!:Page; private y=TOP;
  private logo:ImageData|null;
  constructor(logo:ImageData|null){this.logo=logo;this.newPage();}
  hero(data:ClosurePdfData){
    const lines=wrap(data.subject||data.service,335,15,true),height=Math.max(92,62+lines.length*18); this.ensure(height+16);
    this.rect(L,this.y-height,CW,height,C.dark); this.text("CONSTANCIA DE ATENCIÓN",L+20,this.y-25,8,true,C.white);
    this.text(`SOLICITUD ${data.number}`,L+20,this.y-45,10,true,C.white); this.textLines(lines,L+20,this.y-67,15,18,true,C.white);
    const state=(data.status||"Finalizada").toLocaleUpperCase("es"),sw=Math.min(142,Math.max(74,state.length*6.2+22));
    this.rect(R-sw-18,this.y-45,sw,24,C.greenSoft); this.text(state,R-sw-7,this.y-37,8,true,C.green); this.y-=height+18;
  }
  summary(data:ClosurePdfData,final?:string|null){
    this.section("Resumen de la solicitud","Información general");
    this.grid([["Servicio",data.service||"Sin servicio"],["Estado",data.status||"Sin estado"],["Fecha de radicación",formatDate(data.submittedAt)],["Fecha límite",formatDate(data.dueDate)]]);
    const lines=wrap(final?.trim()||data.description?.trim()||"La solicitud completó su recorrido de atención.",CW-36,9,false),height=46+lines.length*13; this.ensure(height+12);
    this.rect(L,this.y-height,CW,height,C.soft,C.border); this.text("RESULTADO DEL CIERRE",L+16,this.y-21,8,true,C.teal); this.textLines(lines,L+16,this.y-40,9,13,false,C.ink); this.y-=height+18;
  }
  section(title:string,detail:string,minFollowing=0){const topGap=16;this.ensure(50+minFollowing+topGap);this.y-=topGap;this.rect(L,this.y-6,5,28,C.purple);this.text(title,L+16,this.y+8,13,true,C.ink);this.text(detail,L+16,this.y-8,8,false,C.muted);this.y-=38;}
  grid(items:Array<[string,string]>){const gap=10,width=(CW-gap)/2;for(let i=0;i<items.length;i+=2){this.ensure(62);for(let c=0;c<2;c++){const item=items[i+c];if(!item)continue;const x=L+c*(width+gap);this.rect(x,this.y-48,width,48,C.panel,C.border);this.text(item[0].toLocaleUpperCase("es"),x+12,this.y-17,7,true,C.muted);this.text(short(item[1],50),x+12,this.y-35,9,true,C.ink);}this.y-=58;}this.y-=4;}
  stage(item:ClosurePdfManagement,index:number){
    const lines=wrap(item.observation?.trim()||"Sin observación registrada.",CW-62,9,false),answerLines=(item.answers??[]).flatMap(answer=>wrap(`${answer.label}: ${answer.options?.length?answer.options.join(", "):answer.value||"Sin respuesta"}`,CW-62,8,false)),meta=[item.unitName,item.responsibleName].filter(Boolean).join(" · ")||"Responsable no registrado",height=90+lines.length*13+(answerLines.length?18+answerLines.length*12:0); this.ensure(height+12,"Gestión realizada por etapa");
    this.rect(L+14,this.y-height,CW-14,height,C.white,C.border);this.circle(L+15,this.y-24,13,C.teal);this.center(String(index),L+15,this.y-27,8,true,C.white);
    this.text(short(item.formTitle||friendly(item.stepCode),66),L+38,this.y-22,10,true,C.ink);this.text(`${result(item.result)} · ${formatDate(item.completedAt)}`,L+38,this.y-40,8,true,resultColor(item.result));this.text(short(meta,82),L+38,this.y-56,8,false,C.muted);
    this.line(L+38,this.y-65,R-14,this.y-65,C.border);this.textLines(lines,L+38,this.y-82,9,13,false,C.ink);
    if(answerLines.length){const answerY=this.y-82-lines.length*13-8;this.text("RESPUESTAS REGISTRADAS",L+38,answerY,7,true,C.muted);this.textLines(answerLines,L+38,answerY-16,8,12,false,C.ink);}this.y-=height+24;
  }
  comment(item:ClosurePdfComment){const lines=wrap(item.content.trim(),CW-42,9,false),height=48+lines.length*13;this.ensure(height+10,"Conversación de la solicitud");this.rect(L,this.y-height,CW,height,C.panel,C.border);this.text(role(item.authorRole),L+14,this.y-20,8,true,C.teal);this.text(formatDate(item.publishedAt),R-150,this.y-20,8,false,C.muted);this.textLines(lines,L+14,this.y-42,9,13,false,C.ink);this.y-=height+10;}
  empty(message:string){this.ensure(56);this.rect(L,this.y-42,CW,42,C.panel,C.border);this.text(message,L+14,this.y-25,9,false,C.muted);this.y-=56;}
  note(message:string){const lines=wrap(message,CW-28,8,false),height=28+lines.length*11;this.ensure(height+8);this.rect(L,this.y-height,CW,height,C.soft);this.textLines(lines,L+14,this.y-20,8,11,false,C.dark);this.y-=height;}
  build(){return serialize(this.pages,this.logo);}
  private ensure(height:number,title?:string){if(this.y-height>=BOTTOM)return;this.newPage();if(title){this.text(`${title} (continuación)`,L,this.y,11,true,C.ink);this.y-=26;}}
  private newPage(){this.page={commands:[]};this.pages.push(this.page);this.y=TOP;this.rect(0,H-82,W,82,C.dark);this.rect(0,H-86,W,4,C.purple);this.rect(36,H-68,118,43,C.white);if(this.logo)this.page.commands.push("q",`74 0 0 40 58 ${H-67} cm`,"/Logo Do","Q");else{this.text("GAIA",49,H-43,15,true,C.dark);this.text("AMAZONAS",49,H-57,7,true,C.purple);}this.text("CONSTANCIA DE ATENCIÓN",178,H-39,15,true,C.white);this.text("Sistema institucional de solicitudes",178,H-58,8,false,C.white);}
  private text(v:string,x:number,y:number,s:number,b:boolean,color:string){this.page.commands.push(`BT /${b?"F2":"F1"} ${s} Tf ${color} rg ${x} ${y} Td (${pdfText(v)}) Tj ET`);}
  private center(v:string,x:number,y:number,s:number,b:boolean,color:string){this.text(v,x-textWidth(v,s,b)/2,y,s,b,color);}
  private textLines(lines:string[],x:number,y:number,s:number,lh:number,b:boolean,color:string){lines.forEach((v,i)=>this.text(v,x,y-i*lh,s,b,color));}
  private rect(x:number,y:number,w:number,h:number,fill:string,stroke?:string){this.page.commands.push("q",`${fill} rg`,...(stroke?[`${stroke} RG`,"0.8 w"]:[]),`${x} ${y} ${w} ${h} re ${stroke?"B":"f"}`,"Q");}
  private line(x1:number,y1:number,x2:number,y2:number,color:string){this.page.commands.push("q",`${color} RG`,"0.7 w",`${x1} ${y1} m ${x2} ${y2} l S`,"Q");}
  private circle(x:number,y:number,r:number,color:string){const k=r*.5522847498;this.page.commands.push("q",`${color} rg`,`${x+r} ${y} m ${x+r} ${y+k} ${x+k} ${y+r} ${x} ${y+r} c`,`${x-k} ${y+r} ${x-r} ${y+k} ${x-r} ${y} c`,`${x-r} ${y-k} ${x-k} ${y-r} ${x} ${y-r} c`,`${x+k} ${y-r} ${x+r} ${y-k} ${x+r} ${y} c f`,"Q");}
}

function serialize(pages:Page[],logo:ImageData|null){
  const objects:string[]=[];const add=(v:string)=>(objects.push(v),objects.length),catalog=add(""),pagesId=add(""),regular=add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),bold=add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
  const logoId=logo?add(`<< /Type /XObject /Subtype /Image /Width ${logo.width} /Height ${logo.height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter [/ASCIIHexDecode /DCTDecode] /Length ${logo.bytes.length*2+1} >>\nstream\n${hex(logo.bytes)}>\nendstream`):null,pageIds:number[]=[];
  pages.forEach((page,i)=>{page.commands.push(`BT /F1 7 Tf ${C.muted} rg ${L} 29 Td (${pdfText("Fundación Gaia Amazonas · Documento generado automáticamente")}) Tj ET`,`BT /F1 7 Tf ${C.muted} rg 502 29 Td (${pdfText(`Página ${i+1} de ${pages.length}`)}) Tj ET`);const stream=page.commands.join("\n"),content=add(`<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`),x=logoId?`/XObject << /Logo ${logoId} 0 R >>`:"";pageIds.push(add(`<< /Type /Page /Parent ${pagesId} 0 R /MediaBox [0 0 ${W} ${H}] /Resources << /Font << /F1 ${regular} 0 R /F2 ${bold} 0 R >> ${x} >> /Contents ${content} 0 R >>`));});
  objects[catalog-1]=`<< /Type /Catalog /Pages ${pagesId} 0 R >>`;objects[pagesId-1]=`<< /Type /Pages /Kids [${pageIds.map(id=>`${id} 0 R`).join(" ")}] /Count ${pageIds.length} >>`;
  let output="%PDF-1.4\n%GAIA\n";const offsets=[0];objects.forEach((o,i)=>{offsets.push(output.length);output+=`${i+1} 0 obj\n${o}\nendobj\n`;});const xref=output.length;output+=`xref\n0 ${objects.length+1}\n0000000000 65535 f \n`+offsets.slice(1).map(o=>`${String(o).padStart(10,"0")} 00000 n \n`).join("")+`trailer\n<< /Size ${objects.length+1} /Root ${catalog} 0 R >>\nstartxref\n${xref}\n%%EOF`;return new TextEncoder().encode(output);
}

let logoPromise:Promise<ImageData|null>|null=null;
function loadLogo(){logoPromise??=new Promise(resolve=>{const image=new Image();image.onload=()=>{const canvas=document.createElement("canvas");canvas.width=600;canvas.height=330;const ctx=canvas.getContext("2d");if(!ctx)return resolve(null);ctx.fillStyle="#fff";ctx.fillRect(0,0,600,330);ctx.drawImage(image,18,18,564,294);const base64=canvas.toDataURL("image/jpeg",.92).split(",")[1],binary=window.atob(base64);resolve({bytes:Uint8Array.from(binary,c=>c.charCodeAt(0)),width:600,height:330});};image.onerror=()=>resolve(null);image.src="/brand/logo-gaia.svg";});return logoPromise;}
function wrap(value:string,width:number,size:number,bold:boolean){const words=value.replace(/\s+/g," ").trim().split(" ").filter(Boolean),rows:string[]=[];let current="";for(const word of words){const candidate=current?`${current} ${word}`:word;if(current&&textWidth(candidate,size,bold)>width){rows.push(current);current=word;}else current=candidate;}if(current)rows.push(current);return rows.length?rows:["-"];}
function textWidth(value:string,size:number,bold:boolean){return value.length*size*(bold?.56:.51);}
function pdfText(value:string){const extra:Record<string,number>={"€":128,"‘":145,"’":146,"“":147,"”":148,"•":149,"–":150,"—":151};return [...value].map(ch=>{if("\\()".includes(ch))return `\\${ch}`;const code=extra[ch]??ch.codePointAt(0)??32;if(code>=32&&code<=126)return ch;if(code>=128&&code<=255)return `\\${code.toString(8).padStart(3,"0")}`;return " ";}).join("");}
function hex(bytes:Uint8Array){return Array.from(bytes,b=>b.toString(16).padStart(2,"0")).join("").toUpperCase();}
function short(v:string,n:number){return v.length<=n?v:`${v.slice(0,n-3)}...`;}
function friendly(v:string){return v.toLocaleLowerCase("es").replace(/_/g," ").replace(/(^|\s)\p{L}/gu,x=>x.toLocaleUpperCase("es"));}
function timestamp(v?:string|null){const n=v?new Date(v).getTime():Number.MAX_SAFE_INTEGER;return Number.isNaN(n)?Number.MAX_SAFE_INTEGER:n;}
function formatDate(v?:string|null){if(!v)return "Sin fecha";const d=new Date(v);return Number.isNaN(d.getTime())?"Sin fecha":new Intl.DateTimeFormat("es-CO",{dateStyle:"medium",timeStyle:"short"}).format(d);}
function result(v?:number|null){return v===299540171?"Aprobada":v===299540172?"Rechazada":v===299540175?"No aplica":"Completada";}
function resultColor(v?:number|null){return v===299540172?C.purple:v===299540175?C.muted:C.green;}
function role(v?:string|null){return !v?"Participante":v.toLocaleLowerCase("es").includes("solicit")?"Solicitante":v;}
