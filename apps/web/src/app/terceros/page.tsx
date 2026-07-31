"use client";

import { AppHeader } from "@/components/app-header";
import { FormEvent, useEffect, useMemo, useState } from "react";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";

type PartySummary = {
  id: string; fullName: string; documentType: string; documentNumber: string;
  personType: string; isActive: boolean; needsNameReview: boolean;
};
type Detail = {
  party: PartySummary & Record<string, string | boolean | null>;
  engagements: Array<Record<string, string>>;
  assignments: Array<Record<string, string>>;
  studies: Array<Record<string, string>>;
  languages: Array<Record<string, string>>;
  trainings: Array<Record<string, string>>;
  experiences: Array<Record<string, string>>;
  emergencyContacts: Array<Record<string, string>>;
};
type Tab = "basic" | "links" | "assignments" | "studies" | "languages" | "trainings" | "experiences" | "emergency" | "inventory" | "history";

async function api<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${apiUrl}${path}`, {
    credentials: "include", ...options,
    headers: { "Content-Type": "application/json", ...options?.headers },
  });
  if (response.status === 401) { window.location.href = "/"; throw new Error("Sesión finalizada."); }
  if (!response.ok) throw new Error("No fue posible completar la operación.");
  return (await response.json()) as T;
}

const tabs: Array<[Tab, string]> = [
  ["basic", "Datos básicos"], ["links", "Vinculaciones"], ["assignments", "Asignaciones"],
  ["studies", "Estudios"], ["languages", "Idiomas"], ["trainings", "Formación"],
  ["experiences", "Experiencia"], ["emergency", "Emergencia"], ["inventory", "Inventario"],
  ["history", "Historial"],
];

export default function ThirdPartiesPage() {
  const [parties, setParties] = useState<PartySummary[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<Detail | null>(null);
  const [search, setSearch] = useState("");
  const [tab, setTab] = useState<Tab>("basic");
  const [adding, setAdding] = useState(false);
  const [message, setMessage] = useState("");

  useEffect(() => {
    void api<PartySummary[]>("/api/third-parties").then((items) => {
      setParties(items);
      setSelectedId(items[0]?.id ?? null);
    });
  }, []);
  useEffect(() => {
    if (selectedId) void api<Detail>(`/api/third-parties/${selectedId}`).then(setDetail);
  }, [selectedId]);

  const visible = useMemo(() => {
    const value = search.toLocaleLowerCase("es").trim();
    return value ? parties.filter((item) =>
      item.fullName.toLocaleLowerCase("es").includes(value) || item.documentNumber.includes(value)) : parties;
  }, [parties, search]);

  async function refresh() {
    if (selectedId) setDetail(await api<Detail>(`/api/third-parties/${selectedId}`));
  }

  return (
    <main className="min-h-screen bg-[#eef3eb] text-[#193522]">
      <AppHeader title="Gestión de terceros" />

      <div className="mx-auto grid max-w-[1500px] gap-4 px-4 py-4 lg:grid-cols-[310px_1fr]">
        <aside className="overflow-hidden rounded-3xl bg-white shadow-sm">
          <div className="border-b border-[#e4ebe1] p-4">
            <div className="flex items-end justify-between"><div><p className="text-xs font-bold uppercase tracking-widest text-[#66804e]">Directorio</p><p className="mt-1 text-2xl font-semibold">{parties.length}</p></div><button className="rounded-xl bg-[#294f35] px-3 py-2 text-sm font-bold text-white">+ Nuevo</button></div>
            <input className="mt-4 w-full rounded-xl border border-[#d7e1d4] bg-[#f8faf7] px-4 py-2.5 outline-none focus:border-[#66804e]" onChange={(e) => setSearch(e.target.value)} placeholder="Nombre o documento" value={search} />
          </div>
          <div className="max-h-[calc(100vh-260px)] overflow-y-auto">
            {visible.map((party) => (
              <button className={`w-full border-b border-[#edf1eb] p-4 text-left transition ${selectedId === party.id ? "bg-[#eaf2e6] shadow-[inset_4px_0_0_#386037]" : "hover:bg-[#f7f9f6]"}`} key={party.id} onClick={() => { setSelectedId(party.id); setTab("basic"); }} type="button">
                <div className="flex gap-3"><span className="grid size-10 shrink-0 place-items-center rounded-full bg-[#dce8d7] text-sm font-bold">{initials(party.fullName)}</span><div className="min-w-0"><p className="truncate text-sm font-semibold">{title(party.fullName)}</p><p className="mt-1 text-xs text-[#77857b]">{party.documentType} · {party.documentNumber}</p></div></div>
              </button>
            ))}
          </div>
        </aside>

        <section className="min-w-0">
          {detail ? (
            <>
              <div className="relative overflow-hidden rounded-2xl bg-white p-5 shadow-sm">
                <div className="absolute right-0 top-0 h-full w-52 bg-[radial-gradient(circle_at_top_right,#dcebd6,transparent_65%)]" />
                <div className="relative flex flex-wrap items-center gap-5">
                  <span className="grid size-16 place-items-center rounded-2xl bg-[#294f35] text-xl font-bold text-white">{initials(detail.party.fullName)}</span>
                  <div className="flex-1"><div className="flex flex-wrap items-center gap-3"><h2 className="text-3xl font-semibold tracking-tight">{title(detail.party.fullName)}</h2><Status active={Boolean(detail.party.isActive)} /></div><p className="mt-2 text-sm text-[#718077]">{detail.party.documentType} {detail.party.documentNumber} · Persona {String(detail.party.personType).toLowerCase()}</p></div>
                  {detail.party.needsNameReview && <span className="rounded-xl bg-[#fff4df] px-3 py-2 text-xs font-semibold text-[#8a6328]">Nombre pendiente de separar</span>}
                </div>
              </div>

              <div className="mt-5 overflow-hidden rounded-3xl bg-white shadow-sm">
                <nav className="flex overflow-x-auto border-b border-[#e5ebe3] px-4">
                  {tabs.map(([value, label]) => <button className={`whitespace-nowrap border-b-2 px-4 py-4 text-sm font-semibold ${tab === value ? "border-[#386037] text-[#294f35]" : "border-transparent text-[#78857d]"}`} key={value} onClick={() => { setTab(value); setAdding(false); }} type="button">{label}</button>)}
                </nav>
                <div className="min-h-[420px] p-5">
                  <TabContent detail={detail} tab={tab} onAdd={() => setAdding(true)} />
                  {adding && selectedId && <RelatedForm id={selectedId} tab={tab} onDone={async () => { setAdding(false); setMessage("Información agregada correctamente."); await refresh(); }} />}
                  {message && <p className="mt-5 rounded-xl bg-[#eaf4e6] px-4 py-3 text-sm text-[#345a3c]">{message}</p>}
                </div>
              </div>
            </>
          ) : <div className="grid min-h-[600px] place-items-center rounded-3xl bg-white text-[#77857b]">Selecciona una persona</div>}
        </section>
      </div>
    </main>
  );
}

function TabContent({ detail, tab, onAdd }: { detail: Detail; tab: Tab; onAdd: () => void }) {
  if (tab === "basic") return <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">{[
    ["Nombre registrado", detail.party.fullName], ["Documento", `${detail.party.documentType} ${detail.party.documentNumber}`],
    ["Correo personal", detail.party.personalEmail], ["Teléfono", detail.party.primaryPhone],
    ["Ciudad", detail.party.city], ["Dirección", detail.party.address],
  ].map(([label, value]) => <Info key={String(label)} label={String(label)} value={value ? String(value) : "Sin información"} />)}</div>;
  const mapping: Record<Exclude<Tab, "basic">, [keyof Detail | null, string]> = {
    links: ["engagements", "Vinculación"], assignments: ["assignments", "Asignación"],
    studies: ["studies", "Estudio"], languages: ["languages", "Idioma"], trainings: ["trainings", "Formación"],
    experiences: ["experiences", "Experiencia"], emergency: ["emergencyContacts", "Contacto"],
    inventory: [null, "Elemento"], history: [null, "Evento"],
  };
  const [key, singular] = mapping[tab];
  const records = key ? detail[key] as Array<Record<string, unknown>> : [];
  const canAdd = ["studies", "languages", "trainings", "experiences", "emergency"].includes(tab);
  return <><div className="flex items-center justify-between"><div><p className="text-xs font-bold uppercase tracking-[.2em] text-[#66804e]">{singular}</p><h3 className="mt-1 text-2xl font-semibold">{tabs.find(([value]) => value === tab)?.[1]}</h3></div>{canAdd && <button className="rounded-xl bg-[#294f35] px-4 py-2.5 text-sm font-semibold text-white" onClick={onAdd}>+ Agregar</button>}</div>
    <div className="mt-6 grid gap-3 md:grid-cols-2">{records.map((record, index) => <article className="rounded-2xl border border-[#e1e9de] p-5" key={String(record.id ?? index)}><p className="font-semibold">{primary(record)}</p><p className="mt-2 text-sm leading-6 text-[#758179]">{secondary(record)}</p></article>)}</div>
    {!records.length && <div className="mt-10 rounded-2xl border border-dashed border-[#cfdacb] py-14 text-center text-sm text-[#7b887f]">No hay información registrada en esta sección.</div>}</>;
}

function RelatedForm({ id, tab, onDone }: { id: string; tab: Tab; onDone: () => void }) {
  const [values, setValues] = useState<Record<string, string>>({});
  const configs: Partial<Record<Tab, { path: string; fields: Array<[string, string]>; extras?: Record<string, unknown> }>> = {
    languages: { path: "languages", fields: [["language", "Idioma"], ["overallLevel", "Nivel general"], ["certification", "Certificación"]], extras: { readingLevel: null, writingLevel: null, speakingLevel: null } },
    studies: { path: "studies", fields: [["academicLevel", "Nivel académico"], ["title", "Título"], ["institution", "Institución"]], extras: { graduated: false } },
    trainings: { path: "trainings", fields: [["type", "Tipo"], ["name", "Nombre"], ["institution", "Institución"]], extras: { completionDate: null } },
    experiences: { path: "experiences", fields: [["organization", "Organización"], ["role", "Cargo o rol"], ["description", "Descripción"]], extras: { startDate: null, endDate: null } },
    emergency: { path: "emergency-contacts", fields: [["fullName", "Nombre"], ["relationship", "Parentesco"], ["phone", "Teléfono"], ["alternatePhone", "Teléfono alterno"]], extras: { isPrimary: true } },
  };
  const config = configs[tab];
  if (!config) return null;
  async function submit(event: FormEvent) { event.preventDefault(); await api(`/api/third-parties/${id}/${config!.path}`, { method: "POST", body: JSON.stringify({ ...values, ...config!.extras }) }); onDone(); }
  return <form className="mt-6 rounded-2xl bg-[#f4f7f2] p-5" onSubmit={submit}><p className="mb-4 font-semibold">Nuevo registro</p><div className="grid gap-4 md:grid-cols-2">{config.fields.map(([name, label]) => <label className="text-sm font-semibold" key={name}>{label}<input className="mt-2 w-full rounded-xl border border-[#d3ddd0] bg-white px-4 py-3 font-normal outline-none" onChange={(e) => setValues({ ...values, [name]: e.target.value })} required={!["certification", "institution", "description", "alternatePhone"].includes(name)} /></label>)}</div><button className="mt-5 rounded-xl bg-[#294f35] px-5 py-3 font-semibold text-white">Guardar</button></form>;
}

function Info({ label, value }: { label: string; value: string }) { return <div className="rounded-2xl bg-[#f7f9f6] p-5"><p className="text-xs font-bold uppercase tracking-wider text-[#829087]">{label}</p><p className="mt-2 font-semibold">{value}</p></div>; }
function Status({ active }: { active: boolean }) { return <span className={`rounded-full px-3 py-1 text-xs font-bold ${active ? "bg-[#e6f2e1] text-[#386037]" : "bg-[#eee] text-[#777]"}`}>{active ? "Activo" : "Inactivo"}</span>; }
function initials(name: string) { return name.trim().split(/\s+/).slice(0, 2).map((part) => part[0]).join("").toUpperCase(); }
function title(name: string) { return name.toLocaleLowerCase("es").replace(/(^|\s)\p{L}/gu, (letter) => letter.toLocaleUpperCase("es")); }
function primary(record: Record<string, unknown>) { return String(record.language ?? record.title ?? record.name ?? record.roleName ?? record.type ?? record.organization ?? record.fullName ?? "Registro"); }
function secondary(record: Record<string, unknown>) { return String(record.overallLevel ?? record.institution ?? record.corporateEmail ?? record.role ?? record.relationship ?? record.sourceAreaCode ?? "Información registrada"); }
