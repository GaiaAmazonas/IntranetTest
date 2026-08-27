"use client";

import { Mail, MapPin, Phone, Search, Users } from "lucide-react";
import { useEffect, useState } from "react";
import { apiRequest } from "@/lib/api-client";

type Person = { id:string; fullName:string; jobTitle:string|null; organizationUnit:string|null; site:string|null; institutionalEmail:string|null; visiblePhone:string|null; photoUrl:string|null };
type PeoplePage = { items:Person[]; page:number; pageSize:number; total:number };

export function IntranetPeople() {
  const [search,setSearch]=useState("");
  const [query,setQuery]=useState("");
  const [result,setResult]=useState<PeoplePage|null>(null);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState("");

  useEffect(()=>{const timer=window.setTimeout(()=>{setLoading(true);setError("");setQuery(search.trim());},300);return()=>window.clearTimeout(timer);},[search]);
  useEffect(()=>{let active=true;apiRequest<PeoplePage>(`/api/intranet/people?page=1&pageSize=24&search=${encodeURIComponent(query)}`).then(data=>{if(active)setResult(data);}).catch(reason=>{if(active)setError(reason instanceof Error?reason.message:"No fue posible cargar Personas.");}).finally(()=>{if(active)setLoading(false);});return()=>{active=false;};},[query]);

  async function loadMore(){if(!result||loading)return;setLoading(true);try{const next=await apiRequest<PeoplePage>(`/api/intranet/people?page=${result.page+1}&pageSize=${result.pageSize}&search=${encodeURIComponent(query)}`);setResult({...next,items:[...result.items,...next.items]});}catch(reason){setError(reason instanceof Error?reason.message:"No fue posible cargar más personas.");}finally{setLoading(false);}}

  return <section className="intranet-directory">
    <header className="intranet-section-hero intranet-section-hero-people"><div><p>Nuestro equipo</p><h1>Personas</h1><span>Encuentra y conecta con quienes hacen posible el trabajo de Fundación Gaia Amazonas.</span></div><label><Search aria-hidden="true" size={18}/><input aria-label="Buscar personas" onChange={event=>setSearch(event.target.value)} placeholder="Buscar una persona por nombre" value={search}/></label></header>
    <div className="intranet-directory-summary"><span><Users size={16}/><strong>{result?.total??0}</strong> colaboradores activos</span><small>Solo se muestran datos institucionales autorizados.</small></div>
    {error&&<div className="intranet-data-state is-error"><strong>No fue posible cargar Personas</strong><p>{error}</p></div>}
    {!error&&loading&&!result&&<div className="intranet-data-state"><strong>Cargando directorio…</strong></div>}
    {!error&&result?.items.length===0&&<div className="intranet-data-state"><strong>No encontramos personas</strong><p>Prueba con otro nombre.</p></div>}
    {result&&result.items.length>0&&<div className="intranet-people-grid">{result.items.map(person=><article key={person.id}><div className="intranet-person-cover"/><span className="intranet-person-avatar">{initials(person.fullName)}</span><h2>{title(person.fullName)}</h2><p>{person.jobTitle??"Cargo pendiente de vinculación organizacional"}</p><div className="intranet-person-meta"><span><Users size={13}/>{person.organizationUnit??"Unidad no vinculada"}</span><span><MapPin size={13}/>{person.site??"Sede no vinculada"}</span></div><footer>{person.institutionalEmail?<a href={`mailto:${person.institutionalEmail}`}><Mail size={14}/><span>{person.institutionalEmail}</span></a>:<small>Sin correo corporativo visible</small>}{person.visiblePhone?<a href={`tel:${person.visiblePhone}`}><Phone size={14}/><span>{person.visiblePhone}</span></a>:<small>Sin teléfono corporativo visible</small>}</footer></article>)}</div>}
    {result&&result.items.length<result.total&&<button className="intranet-load-more" disabled={loading} onClick={()=>void loadMore()} type="button">{loading?"Cargando…":"Mostrar más personas"}</button>}
  </section>;
}

function initials(name:string){return name.trim().split(/\s+/).slice(0,2).map(value=>value[0]).join("").toUpperCase();}
function title(name:string){return name.toLocaleLowerCase("es").replace(/(^|\s)\p{L}/gu,value=>value.toLocaleUpperCase("es"));}
