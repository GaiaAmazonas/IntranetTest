"use client";

import { AlertTriangle, Save, X } from "lucide-react";
import { useEffect, useId, useRef, type ReactNode } from "react";
import { Button, IconButton } from "./ui";

export function FormDialog({ open, title, subtitle, children, onClose, formId, submitLabel = "Guardar", loading = false, error }: {
  open: boolean; title: string; subtitle?: string; children: ReactNode; onClose: () => void;
  formId: string; submitLabel?: string; loading?: boolean; error?: string | null;
}) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const onCloseRef = useRef(onClose);
  useEffect(() => { onCloseRef.current = onClose; }, [onClose]);
  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    const frame = requestAnimationFrame(() => dialogRef.current?.querySelector<HTMLElement>("input,select,textarea,button")?.focus());
    function keydown(event: KeyboardEvent) { if (event.key === "Escape" && !loading) onCloseRef.current(); }
    document.addEventListener("keydown", keydown); document.body.style.overflow = "hidden";
    return () => { cancelAnimationFrame(frame); document.removeEventListener("keydown", keydown); document.body.style.overflow = ""; previous?.focus(); };
  }, [loading, open]);
  if (!open) return null;
  return <div className="gaia-dialog-backdrop" role="presentation" onMouseDown={event => { if (event.target === event.currentTarget && !loading) onClose(); }}>
    <div aria-labelledby={`${formId}-title`} aria-modal="true" className="gaia-form-dialog" ref={dialogRef} role="dialog">
      <header className="gaia-dialog-header"><div><p>Plataforma Gaia</p><h2 id={`${formId}-title`}>{title}</h2>{subtitle && <span>{subtitle}</span>}</div><IconButton label="Cerrar formulario" disabled={loading} onClick={onClose}><X size={19} /></IconButton></header>
      <div className="gaia-dialog-content">{error && <div className="gaia-form-error" role="alert"><AlertTriangle size={17} /><span>{error}</span></div>}{children}</div>
      <footer className="gaia-dialog-footer"><Button disabled={loading} onClick={onClose} variant="secondary">Cancelar</Button><Button disabled={loading} form={formId} type="submit"><Save size={17} />{loading ? "Guardando…" : submitLabel}</Button></footer>
    </div>
  </div>;
}

export function ConfirmDialog({ open, title, description, confirmLabel, destructive = false, loading = false, onCancel, onConfirm }: {
  open: boolean; title: string; description: string; confirmLabel: string; destructive?: boolean;
  loading?: boolean; onCancel: () => void; onConfirm: () => void;
}) {
  const dialogRef = useRef<HTMLDivElement>(null);
  const onCancelRef = useRef(onCancel);
  useEffect(() => { onCancelRef.current = onCancel; }, [onCancel]);
  const titleId = useId(); const descriptionId = useId();
  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    const frame = requestAnimationFrame(() => dialogRef.current?.querySelector<HTMLElement>("button")?.focus());
    function keydown(event: KeyboardEvent) { if (event.key === "Escape" && !loading) onCancelRef.current(); }
    document.addEventListener("keydown", keydown);
    return () => { cancelAnimationFrame(frame); document.removeEventListener("keydown", keydown); previous?.focus(); };
  }, [loading, open]);
  if (!open) return null;
  return <div className="gaia-dialog-backdrop" role="presentation"><div aria-describedby={descriptionId} aria-labelledby={titleId} aria-modal="true" className="gaia-confirm-dialog" ref={dialogRef} role="alertdialog"><span aria-hidden="true" className={`gaia-confirm-icon ${destructive ? "is-danger" : ""}`}><AlertTriangle size={22} /></span><h2 id={titleId}>{title}</h2><p id={descriptionId}>{description}</p><div><Button disabled={loading} onClick={onCancel} variant="secondary">Cancelar</Button><Button disabled={loading} onClick={onConfirm} variant={destructive ? "danger" : "primary"}>{loading ? "Procesando…" : confirmLabel}</Button></div></div></div>;
}
