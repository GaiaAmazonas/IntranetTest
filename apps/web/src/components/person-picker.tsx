"use client";

import { Check, ChevronDown, Search, UserRound } from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";

type PersonOption = { id: string; name: string; unitIds: string[] };
type UnitOption = { id: string; name: string };

export function PersonPicker({ people, units, value, onChange, required = false, disabled = false, placeholder = "Buscar persona por nombre o unidad" }: {
  people: PersonOption[];
  units: UnitOption[];
  value: string;
  onChange: (id: string) => void;
  required?: boolean;
  disabled?: boolean;
  placeholder?: string;
}) {
  const root = useRef<HTMLDivElement>(null), [open, setOpen] = useState(false), [search, setSearch] = useState("");
  const unitNames = useMemo(() => new Map(units.map((unit) => [unit.id, unit.name])), [units]);
  const ordered = useMemo(() => [...people].sort((a, b) => a.name.localeCompare(b.name, "es")), [people]);
  const selected = ordered.find((person) => person.id === value);
  const visible = useMemo(() => {
    const query = normalize(search);
    if (!query) return ordered;
    return ordered.filter((person) => normalize(`${person.name} ${person.unitIds.map((id) => unitNames.get(id) ?? "").join(" ")}`).includes(query));
  }, [ordered, search, unitNames]);
  useEffect(() => {
    const close = (event: MouseEvent) => { if (!root.current?.contains(event.target as Node)) setOpen(false); };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, []);
  const areas = (person: PersonOption) => person.unitIds.map((id) => unitNames.get(id)).filter((name): name is string => Boolean(name));
  return <div className="relative" ref={root}>
    <button aria-expanded={open} aria-haspopup="listbox" className="flex min-h-11 w-full items-center justify-between gap-3 rounded-xl border border-[var(--gaia-line-strong)] bg-white px-3 text-left text-sm outline-none focus:border-[var(--brand-primary)] disabled:bg-[var(--surface-muted)]" disabled={disabled} onClick={() => setOpen((current) => !current)} type="button">
      <span className={selected ? "min-w-0" : "text-[var(--gaia-muted)]"}>{selected ? <><strong className="block truncate">{selected.name}</strong>{areas(selected).length > 0 && <small className="block truncate text-[var(--gaia-muted)]">{areas(selected).join(" · ")}</small>}</> : placeholder}</span><ChevronDown className="shrink-0" size={16}/>
    </button>
    {required && <input aria-hidden="true" className="pointer-events-none absolute h-px w-px opacity-0" required tabIndex={-1} value={value} onChange={() => {}}/>}
    {open && !disabled && <div className="absolute left-0 right-0 top-[calc(100%+.4rem)] z-[100] overflow-hidden rounded-xl border border-[var(--gaia-line)] bg-white shadow-xl">
      <label className="flex items-center gap-2 border-b border-[var(--gaia-line)] px-3"><Search size={15}/><input autoFocus className="min-h-11 w-full border-0 bg-transparent text-sm outline-none" onChange={(event) => setSearch(event.target.value)} placeholder="Escribe un nombre o una unidad" value={search}/></label>
      <div className="max-h-60 overflow-y-auto p-1.5" role="listbox">{visible.map((person) => <button aria-selected={person.id === value} className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm hover:bg-[var(--surface-muted)]" key={person.id} onClick={() => { onChange(person.id); setOpen(false); setSearch(""); }} role="option" type="button"><UserRound className="shrink-0 text-[var(--brand-primary)]" size={16}/><span className="min-w-0 flex-1"><strong className="block truncate">{person.name}</strong>{areas(person).length > 0 && <small className="block truncate text-[var(--gaia-muted)]">{areas(person).join(" · ")}</small>}</span>{person.id === value && <Check className="shrink-0" size={15}/>}</button>)}{visible.length === 0 && <p className="px-3 py-5 text-center text-sm text-[var(--gaia-muted)]">No se encontraron personas.</p>}</div>
    </div>}
  </div>;
}

function normalize(value: string) { return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase("es").trim(); }
