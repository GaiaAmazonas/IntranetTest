"use client";

import { useId } from "react";

export type WorkflowRouteMapManagement = { stepId: string; status: number; formTitle: string | null };
export type WorkflowRouteMapStep = { id: string; code: string; order: number; initial: boolean; final: boolean; active: boolean; positionX: number | null; positionY: number | null };
export type WorkflowRouteMapRoute = { id: string; sourceStepId: string; targetStepId: string; active: boolean };
export type WorkflowRouteMapData = { managements: WorkflowRouteMapManagement[]; steps: WorkflowRouteMapStep[] | null; routes: WorkflowRouteMapRoute[] | null };

type StepStatus = "completed" | "current" | "cancelled" | "pending";
type RouteMapNode = { step: WorkflowRouteMapStep; left: number; top: number; status: StepStatus; label: string };
export type WorkflowRouteMapLayout = { width: number; height: number; nodes: RouteMapNode[] };

const NODE_WIDTH = 220, NODE_HEIGHT = 82, COLUMN_GAP = 96, ROW_GAP = 46, PADDING_X = 42, PADDING_Y = 42;

function friendlyStep(value: string) {
  return value.toLocaleLowerCase("es").replaceAll("_", " ").replace(/(^|\s)\p{L}/gu, letter => letter.toLocaleUpperCase("es"));
}

function statusOf(workflow: WorkflowRouteMapData, stepId: string): StepStatus {
  const rows = workflow.managements.filter(item => item.stepId === stepId);
  if (rows.some(item => item.status === 299540194)) return "completed";
  if (rows.some(item => [299540191, 299540192, 299540193].includes(item.status))) return "current";
  if (rows.some(item => item.status === 299540195)) return "cancelled";
  return "pending";
}

export function buildWorkflowRouteMapLayout(workflow: WorkflowRouteMapData): WorkflowRouteMapLayout {
  const steps = (workflow.steps ?? []).filter(item => item.active);
  const positioned = steps.map(step => ({ step, rawX: step.positionX ?? step.order * 240, rawY: step.positionY ?? 0 }));
  const xValues = [...new Set(positioned.map(item => item.rawX))].sort((a, b) => a - b);
  const rowsByColumn = new Map<number, number[]>();
  for (const item of positioned) {
    const column = xValues.indexOf(item.rawX), values = rowsByColumn.get(column) ?? [];
    values.push(item.rawY); rowsByColumn.set(column, values);
  }
  for (const [column, values] of rowsByColumn) rowsByColumn.set(column, [...new Set(values)].sort((a, b) => a - b));
  const maxRows = Math.max(1, ...[...rowsByColumn.values()].map(values => values.length));
  const width = Math.max(760, PADDING_X * 2 + xValues.length * NODE_WIDTH + Math.max(0, xValues.length - 1) * COLUMN_GAP);
  const height = Math.max(270, PADDING_Y * 2 + maxRows * NODE_HEIGHT + Math.max(0, maxRows - 1) * ROW_GAP);
  const nodes = positioned.map(item => {
    const column = xValues.indexOf(item.rawX), columnRows = rowsByColumn.get(column) ?? [item.rawY], row = columnRows.indexOf(item.rawY);
    const columnHeight = columnRows.length * NODE_HEIGHT + Math.max(0, columnRows.length - 1) * ROW_GAP;
    const management = workflow.managements.find(value => value.stepId === item.step.id);
    return { step: item.step, left: PADDING_X + column * (NODE_WIDTH + COLUMN_GAP), top: (height - columnHeight) / 2 + row * (NODE_HEIGHT + ROW_GAP), status: statusOf(workflow, item.step.id), label: management?.formTitle || friendlyStep(item.step.code) };
  });
  return { width, height, nodes };
}

const palette: Record<StepStatus, { fill: string; stroke: string; label: string; text: string }> = {
  completed: { fill: "#e8f6f1", stroke: "#54a08d", label: "Completada", text: "#175f54" },
  current: { fill: "#edf7fa", stroke: "#27798a", label: "Etapa actual", text: "#185f6e" },
  cancelled: { fill: "#f5f6f5", stroke: "#aebbb7", label: "No recorrida", text: "#66756f" },
  pending: { fill: "#f5f7f6", stroke: "#c7d1ce", label: "Pendiente", text: "#66756f" },
};

function abbreviated(value: string) { return value.length > 29 ? `${value.slice(0, 28)}…` : value; }

export function WorkflowRouteMap({ workflow }: { workflow: WorkflowRouteMapData }) {
  const markerId = `workflow-arrow-${useId().replaceAll(":", "")}`, layout = buildWorkflowRouteMapLayout(workflow);
  if (!layout.nodes.length) return <div className="rounded-2xl border border-dashed border-[var(--gaia-line)] bg-[var(--surface-muted)] px-5 py-10 text-center text-sm text-[var(--gaia-ink-500)]">El recorrido gráfico no está disponible para esta solicitud.</div>;
  const byId = new Map(layout.nodes.map(item => [item.step.id, item]));
  return <section aria-labelledby={`${markerId}-title`} className="overflow-hidden rounded-2xl border border-[var(--gaia-line)] bg-white shadow-[0_10px_30px_rgba(23,75,64,.06)]">
    <header className="flex flex-wrap items-end justify-between gap-3 border-b border-[var(--gaia-line)] bg-[#fbfdfc] px-4 py-4 sm:px-5">
      <div><h3 className="text-sm font-semibold" id={`${markerId}-title`}>Ruta de la solicitud</h3><p className="mt-1 text-xs text-[var(--gaia-ink-500)]">Consulta el recorrido completo y la etapa en la que se encuentra el proceso.</p></div>
      <ul aria-label="Estados del recorrido" className="flex flex-wrap gap-2 text-[10px] font-semibold text-[var(--gaia-ink-700)]">{(["completed", "current", "pending"] as const).map(status => <li className="inline-flex items-center gap-1.5 rounded-full border border-[var(--gaia-line)] bg-white px-2.5 py-1" key={status}><span aria-hidden="true" className="size-2 rounded-full" style={{ background: palette[status].stroke }} />{palette[status].label}</li>)}</ul>
    </header>
    <div className="overflow-x-auto bg-[radial-gradient(circle_at_1px_1px,#dce7e3_1px,transparent_0)] bg-[size:20px_20px] p-3 sm:p-5">
      <svg aria-label="Diagrama del recorrido de la solicitud" className="block h-auto min-h-[270px] min-w-[720px] w-full" role="img" viewBox={`0 0 ${layout.width} ${layout.height}`}>
        <defs><marker id={markerId} markerHeight="8" markerWidth="8" orient="auto" refX="7" refY="4"><path d="M0,0 L8,4 L0,8 Z" fill="#8ca19b" /></marker><filter height="140%" id={`${markerId}-shadow`} width="140%" x="-20%" y="-20%"><feDropShadow dx="0" dy="3" floodColor="#173f36" floodOpacity=".12" stdDeviation="3" /></filter></defs>
        {(workflow.routes ?? []).filter(route => route.active).map(route => { const source = byId.get(route.sourceStepId), target = byId.get(route.targetStepId); if (!source || !target) return null; const x1 = source.left + NODE_WIDTH, y1 = source.top + NODE_HEIGHT / 2, x2 = target.left - 10, y2 = target.top + NODE_HEIGHT / 2, distance = Math.max(38, (x2 - x1) * .48), reached = target.status !== "pending"; return <path d={`M ${x1} ${y1} C ${x1 + distance} ${y1}, ${x2 - distance} ${y2}, ${x2} ${y2}`} fill="none" key={route.id} markerEnd={`url(#${markerId})`} stroke={reached ? "#28766f" : "#b9c7c3"} strokeWidth={reached ? 3 : 2} />; })}
        {layout.nodes.map(node => { const colors = palette[node.status]; return <g filter={`url(#${markerId}-shadow)`} key={node.step.id} transform={`translate(${node.left} ${node.top})`}><title>{`${node.label}: ${colors.label}`}</title><rect fill={colors.fill} height={NODE_HEIGHT} rx="15" stroke={colors.stroke} strokeDasharray={node.status === "pending" ? "5 4" : undefined} strokeWidth={node.status === "current" ? 3 : 2} width={NODE_WIDTH} /><circle cx="22" cy="23" fill={colors.stroke} r="7" /><text fill={colors.text} fontSize="10" fontWeight="700" letterSpacing=".8" x="37" y="27">{colors.label.toLocaleUpperCase("es")}</text><text fill="#172b26" fontSize="14" fontWeight="700" x="16" y="57">{abbreviated(node.label)}</text>{(node.step.initial || node.step.final) && <text fill={colors.text} fontSize="9" fontWeight="700" textAnchor="end" x={NODE_WIDTH - 14} y="27">{node.step.initial ? "INICIO" : "FINAL"}</text>}</g>; })}
      </svg>
    </div>
  </section>;
}
