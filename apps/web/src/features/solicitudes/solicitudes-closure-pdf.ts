export type ClosurePdfManagement = {
  stepCode: string;
  formTitle?: string | null;
  unitName?: string | null;
  responsibleName?: string | null;
  result?: number | null;
  observation?: string | null;
  completedAt?: string | null;
};

export type ClosurePdfData = {
  number: string;
  subject: string;
  service: string;
  status: string;
  submittedAt?: string | null;
  managements: ClosurePdfManagement[];
};

export function downloadClosurePdf(data: ClosurePdfData) {
  const completed = data.managements.filter((item) => item.completedAt);
  const final = [...completed].reverse().find((item) => item.observation)?.observation;
  const lines = [
    { text: "CONSTANCIA DE ATENCION", size: 18, bold: true },
    { text: "Fundacion Gaia Amazonas", size: 11, bold: true },
    { text: `Solicitud ${data.number}`, size: 14, bold: true },
    { text: data.subject, size: 11, bold: true },
    { text: `Servicio: ${data.service}`, size: 9 },
    { text: `Estado final: ${data.status}`, size: 9 },
    { text: `Radicacion: ${date(data.submittedAt)}`, size: 9 },
    { text: `Emision: ${date(new Date().toISOString())}`, size: 9 },
    { text: "RESULTADO FINAL", size: 11, bold: true },
    { text: final || "La solicitud termino su recorrido de atencion.", size: 9 },
    { text: "TRAZABILIDAD DE AREAS", size: 11, bold: true },
    ...completed.flatMap((item, index) => [
      { text: `${index + 1}. ${item.formTitle || friendly(item.stepCode)}`, size: 9, bold: true },
      { text: `Area: ${item.unitName || "Area no registrada"}`, size: 9 },
      { text: `Resultado: ${result(item.result)} · ${date(item.completedAt)}`, size: 9 },
      ...(item.observation ? [{ text: `Observacion: ${item.observation}`, size: 8 }] : []),
    ]),
    { text: "Documento generado automaticamente por el sistema institucional Gaia.", size: 8 },
  ];
  const pages = paginate(lines.flatMap(wrapLine));
  const pdf = buildPdf(pages);
  const link = document.createElement("a");
  link.href = URL.createObjectURL(new Blob([pdf], { type: "application/pdf" }));
  link.download = `Constancia-${data.number}.pdf`;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(link.href), 1000);
}

type PdfLine = { text: string; size: number; bold?: boolean };
function wrapLine(line: PdfLine): PdfLine[] {
  const words = line.text.replace(/\s+/g, " ").trim().split(" ");
  const rows: PdfLine[] = [];
  let current = "";
  const limit = line.size >= 14 ? 48 : line.size >= 11 ? 68 : 92;
  for (const word of words) {
    if (current && `${current} ${word}`.length > limit) {
      rows.push({ ...line, text: current });
      current = word;
    } else current = current ? `${current} ${word}` : word;
  }
  if (current) rows.push({ ...line, text: current });
  return rows;
}
function paginate(lines: PdfLine[]) {
  const pages: PdfLine[][] = [[]];
  let used = 0;
  for (const line of lines) {
    const height = line.size + (line.size >= 11 ? 10 : 6);
    if (used + height > 650) {
      pages.push([]);
      used = 0;
    }
    pages.at(-1)!.push(line);
    used += height;
  }
  return pages;
}
function buildPdf(pages: PdfLine[][]) {
  const objects: string[] = [];
  const add = (value: string) => (objects.push(value), objects.length);
  const catalog = add("");
  const pagesId = add("");
  const regular = add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
  const bold = add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>");
  const pageIds: number[] = [];
  for (const [pageIndex, page] of pages.entries()) {
    let y = 742;
    const commands = ["q", "0.10 0.38 0.35 rg", "0 775 612 67 re f", "Q"];
    commands.push("BT /F2 10 Tf 1 1 1 rg 48 806 Td (GAIA AMAZONAS) Tj ET");
    commands.push(`BT /F1 8 Tf 0.35 0.43 0.40 rg 500 35 Td (Pagina ${pageIndex + 1} de ${pages.length}) Tj ET`);
    for (const line of page) {
      const font = line.bold ? "F2" : "F1";
      const color = line.size >= 11 ? "0.08 0.20 0.18" : "0.18 0.24 0.22";
      commands.push(`BT /${font} ${line.size} Tf ${color} rg 48 ${y} Td (${pdfText(line.text)}) Tj ET`);
      y -= line.size + (line.size >= 11 ? 10 : 6);
    }
    const stream = commands.join("\n");
    const content = add(`<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`);
    pageIds.push(add(`<< /Type /Page /Parent ${pagesId} 0 R /MediaBox [0 0 612 842] /Resources << /Font << /F1 ${regular} 0 R /F2 ${bold} 0 R >> >> /Contents ${content} 0 R >>`));
  }
  objects[catalog - 1] = `<< /Type /Catalog /Pages ${pagesId} 0 R >>`;
  objects[pagesId - 1] = `<< /Type /Pages /Kids [${pageIds.map((id) => `${id} 0 R`).join(" ")}] /Count ${pageIds.length} >>`;
  let output = "%PDF-1.4\n";
  const offsets = [0];
  objects.forEach((object, index) => {
    offsets.push(output.length);
    output += `${index + 1} 0 obj\n${object}\nendobj\n`;
  });
  const xref = output.length;
  output += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n`;
  output += offsets.slice(1).map((offset) => `${String(offset).padStart(10, "0")} 00000 n \n`).join("");
  output += `trailer\n<< /Size ${objects.length + 1} /Root ${catalog} 0 R >>\nstartxref\n${xref}\n%%EOF`;
  return new TextEncoder().encode(output);
}
function pdfText(value: string) {
  return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[^\x20-\x7E]/g, " ").replace(/([\\()])/g, "\\$1");
}
function friendly(value: string) { return value.toLocaleLowerCase("es").replace(/_/g, " ").replace(/(^|\s)\p{L}/gu, (x) => x.toLocaleUpperCase("es")); }
function date(value?: string | null) { return value ? new Intl.DateTimeFormat("es-CO", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)) : "Sin fecha"; }
function result(value?: number | null) { return value === 299540171 ? "Aprobada" : value === 299540172 ? "Rechazada" : value === 299540175 ? "No aplica" : "Completada"; }
