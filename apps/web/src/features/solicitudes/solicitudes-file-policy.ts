export const solicitudesDefaultFileAccept = ".pdf,.png,.jpg,.jpeg,.docx,.xlsx,.pptx";

export const solicitudesDefaultFileHelp =
  "Formatos permitidos: PDF, PNG, JPG, Word, Excel y PowerPoint.";

export function solicitudesFileAccept(configured?: string | null) {
  return configured?.trim() || solicitudesDefaultFileAccept;
}
