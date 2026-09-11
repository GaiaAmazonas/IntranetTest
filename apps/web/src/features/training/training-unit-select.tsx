"use client";

import { Check, ChevronDown, ChevronRight, Search } from "lucide-react";
import { type KeyboardEvent, useEffect, useMemo, useRef, useState } from "react";
import type { Reference } from "./training-administration";

export function TrainingUnitSelect({ units, value, onChange }: { units: Reference[]; value: string; onChange: (value: string) => void }) {
  const root = useRef<HTMLDivElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [active, setActive] = useState(0);
  const ordered = useMemo(() => [...units].sort((a, b) => a.code.localeCompare(b.code, "es", { numeric: true })), [units]);
  const selected = units.find((item) => item.id === value);
  const term = query.trim().toLocaleLowerCase("es");
  const options = term ? ordered.filter((item) => `${item.code} ${item.name}`.toLocaleLowerCase("es").includes(term)) : ordered;

  useEffect(() => {
    const close = (event: MouseEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false); };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);
  useEffect(() => { if (open) requestAnimationFrame(() => input.current?.focus()); }, [open]);

  function choose(item: Reference) { onChange(item.id); setOpen(false); setQuery(""); setActive(0); }
  function keyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === "ArrowDown") { event.preventDefault(); setActive((index) => Math.min(index + 1, options.length - 1)); }
    if (event.key === "ArrowUp") { event.preventDefault(); setActive((index) => Math.max(index - 1, 0)); }
    if (event.key === "Enter" && options[active]) { event.preventDefault(); choose(options[active]); }
    if (event.key === "Escape") setOpen(false);
  }

  return <div className="gaia-hierarchy-select" ref={root}>
    <button aria-expanded={open} className="gaia-hierarchy-trigger" onClick={() => setOpen((current) => !current)} type="button">
      <span>{selected ? <><strong>{selected.name}</strong><small>{selected.code}</small></> : <><strong>Seleccionar unidad</strong><small>Buscar por código o nombre</small></>}</span>
      <ChevronDown className={open ? "is-open" : ""} size={18} />
    </button>
    {open && <div className="gaia-hierarchy-popover">
      <div className="gaia-hierarchy-search"><Search size={16} /><input onChange={(event) => { setQuery(event.target.value); setActive(0); }} onKeyDown={keyDown} placeholder="Buscar unidad" ref={input} value={query} /></div>
      <div className="gaia-hierarchy-options" role="listbox">
        {options.map((item, index) => <button aria-selected={item.id === value} className={`gaia-hierarchy-option ${index === active ? "is-highlighted" : ""}`} key={item.id} onClick={() => choose(item)} onMouseEnter={() => setActive(index)} role="option" style={{ paddingLeft: `${12 + Math.max(0, item.level - 1) * 20}px` }} type="button"><span className={`gaia-hierarchy-branch ${units.some((child) => child.parentId === item.id) ? "has-children" : ""}`}><ChevronRight size={14} /></span><span className="gaia-hierarchy-option-label"><strong>{item.name}</strong><small>{item.code}</small></span>{item.id === value && <Check className="gaia-hierarchy-check" size={16} />}</button>)}
        {!options.length && <p className="gaia-hierarchy-empty">No se encontraron unidades.</p>}
      </div>
      <p className="gaia-hierarchy-help">Ordenadas por código · la sangría representa la jerarquía</p>
    </div>}
  </div>;
}
