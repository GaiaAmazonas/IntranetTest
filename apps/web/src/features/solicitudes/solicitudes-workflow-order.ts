export type ChronologicalManagement = {
  id: string;
  execution: number;
  availableAt: string | null;
  completedAt: string | null;
};

function timestamp(value: string | null) {
  if (!value) return Number.POSITIVE_INFINITY;
  const parsed = Date.parse(value);
  return Number.isFinite(parsed) ? parsed : Number.POSITIVE_INFINITY;
}

export function orderCompletedManagements<T extends ChronologicalManagement>(items: readonly T[]) {
  return [...items].sort((left, right) =>
    timestamp(left.completedAt) - timestamp(right.completedAt)
    || timestamp(left.availableAt) - timestamp(right.availableAt)
    || left.execution - right.execution
    || left.id.localeCompare(right.id));
}
