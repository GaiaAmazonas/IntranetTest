"use client";

import type { ButtonHTMLAttributes, ReactNode } from "react";
import { Inbox } from "lucide-react";
import Image from "next/image";

export function Avatar({ name, imageUrl }: { name: string; imageUrl?: string | null }) {
  const initials = name.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join("").toUpperCase();
  return imageUrl
    ? <Image alt="" className="gaia-avatar" height={36} src={imageUrl} unoptimized width={36} />
    : <span aria-hidden="true" className="gaia-avatar">{initials || "GA"}</span>;
}

export function IconButton({ label, children, className = "", ...props }:
  ButtonHTMLAttributes<HTMLButtonElement> & { label: string; children: ReactNode }) {
  return <button aria-label={label} className={`gaia-icon-button ${className}`} title={label} type="button" {...props}>{children}</button>;
}

export function Button({ variant = "primary", children, className = "", ...props }:
  ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "secondary" | "danger"; children: ReactNode }) {
  return <button className={`gaia-button gaia-button-${variant} ${className}`} type="button" {...props}>{children}</button>;
}

export function Badge({ tone = "neutral", children }: { tone?: "success" | "warning" | "danger" | "neutral"; children: ReactNode }) {
  return <span className={`gaia-badge gaia-badge-${tone}`}>{children}</span>;
}

export function PageHeader({ eyebrow, title, description, actions }: { eyebrow?: string; title: string; description?: string; actions?: ReactNode }) {
  return <div className="gaia-page-header"><div>{eyebrow && <p>{eyebrow}</p>}<h2>{title}</h2>{description && <span>{description}</span>}</div>{actions && <div className="gaia-page-actions">{actions}</div>}</div>;
}

export function EmptyState({ title, description }: { title: string; description: string }) {
  return <div className="gaia-empty-state"><Inbox size={25} /><strong>{title}</strong><span>{description}</span></div>;
}

export function Skeleton({ className = "" }: { className?: string }) {
  return <span aria-hidden="true" className={`gaia-skeleton ${className}`} />;
}
