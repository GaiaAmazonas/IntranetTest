import type ExcelJS from "exceljs";

export type GaiaExcelColumn<Row> = {
  header: string;
  key: string;
  width: number;
  value: (row: Row) => ExcelJS.CellValue;
  numberFormat?: string;
  wrap?: boolean;
  alignment?: "left" | "center" | "right";
  indent?: (row: Row) => number;
};

export type GaiaExcelDocument<Row> = {
  sheetName: string;
  title: string;
  subtitle: string;
  moduleName: string;
  fileName: string;
  rows: Row[];
  columns: GaiaExcelColumn<Row>[];
  generatedAt?: Date;
  logoPath?: string;
  logoDataUrl?: string;
  institutionalNote?: string;
};

const colors = {
  forest: "FF174B35",
  green: "FF386037",
  pale: "FFE9F1E5",
  line: "FFD6E1D2",
  white: "FFFFFFFF",
  ink: "FF193522",
  muted: "FF66766C",
};

export async function createGaiaWorkbook<Row>(document: GaiaExcelDocument<Row>) {
  const { default: Excel } = await import("exceljs");
  const workbook = new Excel.Workbook();
  const generatedAt = document.generatedAt ?? new Date();
  workbook.creator = "Fundación Gaia Amazonas";
  workbook.company = "Fundación Gaia Amazonas";
  workbook.subject = document.subtitle;
  workbook.title = document.title;
  workbook.description = `Exportación corporativa del módulo ${document.moduleName}.`;
  workbook.created = generatedAt;
  workbook.modified = generatedAt;

  const sheet = workbook.addWorksheet(document.sheetName, {
    properties: { defaultRowHeight: 19 },
    pageSetup: { orientation: "landscape", fitToPage: true, fitToWidth: 1, fitToHeight: 0 },
    views: [{ state: "frozen", ySplit: 8, showGridLines: false }],
  });
  const lastColumn = Math.max(1, document.columns.length);
  const lastColumnLetter = columnLetter(lastColumn);
  sheet.mergeCells(`C1:${lastColumnLetter}1`);
  sheet.mergeCells(`C2:${lastColumnLetter}2`);
  sheet.mergeCells(`C3:${lastColumnLetter}3`);
  sheet.mergeCells(`A5:${lastColumnLetter}5`);
  sheet.getCell("C1").value = "FUNDACIÓN GAIA AMAZONAS";
  sheet.getCell("C2").value = document.title;
  sheet.getCell("C3").value = document.subtitle;
  sheet.getCell("A5").value = `Módulo: ${document.moduleName}  ·  Generado: ${formatGenerationDate(generatedAt)}  ·  Registros: ${document.rows.length}`;
  sheet.getCell("A6").value = document.institutionalNote ?? "Información institucional para uso autorizado.";
  sheet.mergeCells(`A6:${lastColumnLetter}6`);

  sheet.getCell("C1").font = { name: "Aptos Display", bold: true, size: 10, color: { argb: colors.green } };
  sheet.getCell("C2").font = { name: "Aptos Display", bold: true, size: 20, color: { argb: colors.ink } };
  sheet.getCell("C3").font = { name: "Aptos", size: 11, color: { argb: colors.muted } };
  sheet.getCell("A5").font = { name: "Aptos", bold: true, size: 9, color: { argb: colors.green } };
  sheet.getCell("A6").font = { name: "Aptos", italic: true, size: 9, color: { argb: colors.muted } };
  for (let row = 1; row <= 4; row++) for (let column = 1; column <= lastColumn; column++)
    sheet.getCell(row, column).fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF5F8F3" } };
  for (let column = 1; column <= lastColumn; column++)
    sheet.getCell(5, column).fill = { type: "pattern", pattern: "solid", fgColor: { argb: colors.pale } };
  sheet.getRow(1).height = 18;
  sheet.getRow(2).height = 30;
  sheet.getRow(3).height = 20;
  sheet.getRow(4).height = 13;
  sheet.getRow(5).height = 24;
  sheet.getRow(6).height = 22;

  const logo = document.logoDataUrl ?? await loadPngDataUrl(document.logoPath ?? "/brand/logo-gaia.svg");
  const imageId = workbook.addImage({ base64: logo, extension: "png" });
  sheet.addImage(imageId, { tl: { col: 0.15, row: 0.25 }, ext: { width: 118, height: 65 } });

  const headerRowNumber = 8;
  const header = sheet.getRow(headerRowNumber);
  document.columns.forEach((column, index) => {
    const cell = header.getCell(index + 1);
    cell.value = column.header;
    cell.font = { name: "Aptos", bold: true, size: 10, color: { argb: colors.white } };
    cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: colors.forest } };
    cell.alignment = { vertical: "middle", horizontal: "left" };
    cell.border = { bottom: { style: "medium", color: { argb: colors.green } } };
    sheet.getColumn(index + 1).width = column.width;
  });
  header.height = 26;

  document.rows.forEach((item, rowIndex) => {
    const row = sheet.getRow(headerRowNumber + rowIndex + 1);
    document.columns.forEach((column, columnIndex) => {
      const cell = row.getCell(columnIndex + 1);
      cell.value = column.value(item);
      cell.font = { name: "Aptos", size: 9.5, color: { argb: colors.ink } };
      cell.alignment = {
        vertical: "middle",
        horizontal: column.alignment ?? "left",
        wrapText: column.wrap ?? false,
        indent: Math.min(15, Math.max(0, column.indent?.(item) ?? 0)),
      };
      if (column.numberFormat) cell.numFmt = column.numberFormat;
      cell.border = { bottom: { style: "hair", color: { argb: colors.line } } };
      if (rowIndex % 2 === 1) cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF8FAF7" } };
      if (column.key === "status" && cell.value === "Activo") {
        cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFE4F1DF" } };
        cell.font = { name: "Aptos", bold: true, size: 9.5, color: { argb: colors.green } };
      }
      if (column.key === "status" && cell.value === "Inactivo") {
        cell.fill = { type: "pattern", pattern: "solid", fgColor: { argb: "FFF8E2DF" } };
        cell.font = { name: "Aptos", bold: true, size: 9.5, color: { argb: "FF963B32" } };
      }
    });
    row.height = 30;
  });

  const finalRow = headerRowNumber + document.rows.length;
  sheet.autoFilter = { from: { row: headerRowNumber, column: 1 }, to: { row: Math.max(headerRowNumber, finalRow), column: lastColumn } };
  sheet.pageSetup.printTitlesRow = `${headerRowNumber}:${headerRowNumber}`;
  sheet.pageSetup.margins = { left: 0.25, right: 0.25, top: 0.5, bottom: 0.5, header: 0.2, footer: 0.2 };
  sheet.headerFooter.oddFooter = "&LFundación Gaia Amazonas&C&P de &N&RDocumento institucional";
  return workbook;
}

export async function downloadGaiaWorkbook<Row>(document: GaiaExcelDocument<Row>) {
  const workbook = await createGaiaWorkbook(document);
  const buffer = await workbook.xlsx.writeBuffer();
  const blob = new Blob([new Uint8Array(buffer)], { type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" });
  const url = URL.createObjectURL(blob);
  const anchor = window.document.createElement("a");
  anchor.href = url;
  anchor.download = document.fileName;
  anchor.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}

async function loadPngDataUrl(path: string) {
  const response = await fetch(path);
  if (!response.ok) throw new Error("No fue posible cargar la imagen corporativa.");
  const svg = await response.text();
  const source = URL.createObjectURL(new Blob([svg], { type: "image/svg+xml" }));
  try {
    const image = await new Promise<HTMLImageElement>((resolve, reject) => {
      const element = new Image();
      element.onload = () => resolve(element);
      element.onerror = () => reject(new Error("La imagen corporativa no pudo convertirse para Excel."));
      element.src = source;
    });
    const canvas = window.document.createElement("canvas");
    canvas.width = 450;
    canvas.height = 247;
    const context = canvas.getContext("2d");
    if (!context) throw new Error("El navegador no permite preparar la imagen corporativa.");
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL("image/png");
  } finally {
    URL.revokeObjectURL(source);
  }
}

function columnLetter(number: number) {
  let value = number;
  let result = "";
  while (value > 0) {
    value--;
    result = String.fromCharCode(65 + (value % 26)) + result;
    value = Math.floor(value / 26);
  }
  return result;
}

function formatGenerationDate(value: Date) {
  return new Intl.DateTimeFormat("es-CO", { dateStyle: "medium", timeStyle: "short" }).format(value);
}
