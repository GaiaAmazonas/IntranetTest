"use client";

import { AlertCircle, AlertTriangle, CheckCircle2, Info, X } from "lucide-react";
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { IconButton } from "./ui";

export type ToastTone = "success" | "error" | "warning" | "info";
type ToastInput = { tone: ToastTone; title: string; description?: string; persistent?: boolean };
type ToastItem = ToastInput & { id: number };
type ToastContextValue = { notify: (toast: ToastInput) => void };
const ToastContext = createContext<ToastContextValue | null>(null);
const icons = { success: CheckCircle2, error: AlertCircle, warning: AlertTriangle, info: Info };

export function FeedbackProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const dismiss = useCallback((id: number) => setToasts(items => items.filter(item => item.id !== id)), []);
  const notify = useCallback((toast: ToastInput) => {
    const id = Date.now() + Math.random();
    setToasts(items => [...items.slice(-3), { ...toast, id }]);
  }, []);
  const value = useMemo(() => ({ notify }), [notify]);
  return <ToastContext.Provider value={value}>{children}<div className="gaia-toast-region" aria-live="polite" aria-label="Notificaciones">{toasts.map(toast => <Toast key={toast.id} toast={toast} dismiss={dismiss} />)}</div></ToastContext.Provider>;
}

function Toast({ toast, dismiss }: { toast: ToastItem; dismiss: (id: number) => void }) {
  const [paused, setPaused] = useState(false);
  const Icon = icons[toast.tone];
  useEffect(() => {
    if (toast.persistent || paused) return;
    const timer = window.setTimeout(() => dismiss(toast.id), toast.tone === "error" ? 7000 : 4500);
    return () => window.clearTimeout(timer);
  }, [dismiss, paused, toast]);
  return <article className={`gaia-toast gaia-toast-${toast.tone}`} onMouseEnter={() => setPaused(true)} onMouseLeave={() => setPaused(false)} role={toast.tone === "error" ? "alert" : "status"}>
    <span className="gaia-toast-icon"><Icon size={19} /></span><div><strong>{toast.title}</strong>{toast.description && <p>{toast.description}</p>}</div><IconButton label="Cerrar notificación" onClick={() => dismiss(toast.id)}><X size={16} /></IconButton>
  </article>;
}

export function useFeedback() {
  const context = useContext(ToastContext);
  if (!context) throw new Error("useFeedback debe utilizarse dentro de FeedbackProvider.");
  return context;
}
