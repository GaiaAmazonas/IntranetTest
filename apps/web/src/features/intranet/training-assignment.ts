export type TrainingAssignment = {
  assignmentId: string;
  versionId: string;
  training: string;
  version: string;
  summary: string | null;
  status: number;
  assignedAt: string;
  dueAt: string | null;
  progress: number;
  result: number | null;
};

export const trainingStatus = (value: number) => ({
  299541090: "Pendiente",
  299541091: "En curso",
  299541092: "Pendiente de evaluación",
  299541093: "Aprobada",
  299541094: "No aprobada",
  299541095: "Vencida",
}[value] ?? "Asignada");

export const activeTrainingStatuses = new Set([299541090, 299541091, 299541092, 299541094, 299541095]);

export function trainingDueLabel(value: string | null) {
  if (!value) return "Sin vencimiento";
  return new Intl.DateTimeFormat("es-CO", { dateStyle: "medium" }).format(new Date(value));
}

export function trainingDueDescription(item: TrainingAssignment) {
  const due = item.dueAt ? `Fecha límite: ${trainingDueLabel(item.dueAt)}.` : "No tiene fecha límite.";
  return `${item.progress}% completado. ${due}`;
}
