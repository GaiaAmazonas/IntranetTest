"use client";

import { Building2, ChevronDown, ChevronRight, Mail, MapPin, Phone, Search, Users } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { apiRequest } from "@/lib/api-client";

type Person = { id:string; fullName:string; jobTitle:string|null; organizationUnitId:string|null; organizationUnit:string|null; organizationUnitCode:string|null; site:string|null; institutionalEmail:string|null; visiblePhone:string|null; photoUrl:string|null };
type PeoplePage = { items:Person[]; page:number; pageSize:number; total:number };
type OrganizationUnit = { id:string; code:string; name:string; parentId:string|null; level:number; visualOrder:number };

export function IntranetPeople() {
  const [search,setSearch]=useState("");
  const [query,setQuery]=useState("");
  const [allPeople,setAllPeople]=useState<Person[]>([]);
  const [visibleCount,setVisibleCount]=useState(24);
  const [loading,setLoading]=useState(true);
  const [error,setError]=useState("");
  const [units,setUnits]=useState<OrganizationUnit[]>([]);
  const [selectedUnitId,setSelectedUnitId]=useState("");
  const [includeDescendants,setIncludeDescendants]=useState(false);
  const [collapsed,setCollapsed]=useState(()=>new Set<string>());

  const unitById=useMemo(()=>new Map(units.map(unit=>[unit.id,unit])),[units]);
  const tree=useMemo(()=>organizationTree(units,collapsed),[units,collapsed]);
  const selectedUnit=unitById.get(selectedUnitId);
  const result=useMemo<PeoplePage>(()=>{const scope=selectedUnitId?new Set([selectedUnitId,...(includeDescendants?descendantsOf(selectedUnitId,units):[])]):null;const term=normalize(query);const filtered=allPeople.filter(person=>(!scope||(person.organizationUnitId&&scope.has(person.organizationUnitId)))&&(!term||[person.fullName,person.jobTitle,person.organizationUnit,person.organizationUnitCode,person.site,person.institutionalEmail,person.visiblePhone].some(value=>normalize(value).includes(term))));return{items:filtered.slice(0,visibleCount),page:1,pageSize:visibleCount,total:filtered.length};},[allPeople,includeDescendants,query,selectedUnitId,units,visibleCount]);

  useEffect(()=>{const timer=window.setTimeout(()=>{setQuery(search.trim());setVisibleCount(24);},250);return()=>window.clearTimeout(timer);},[search]);
  useEffect(()=>{let active=true;Promise.all([apiRequest<PeoplePage>(peoplePath(1,500,"","",false)),apiRequest<OrganizationUnit[]>("/api/intranet/people/organization-units")]).then(([people,data])=>{if(!active)return;setAllPeople(people.items);setUnits(data);setCollapsed(new Set(data.filter(candidate=>data.some(unit=>unit.parentId===candidate.id)).map(unit=>unit.id)));}).catch(reason=>{if(active)setError(reason instanceof Error?reason.message:"No fue posible cargar Personas.");}).finally(()=>{if(active)setLoading(false);});return()=>{active=false;};},[]);

  function loadMore(){setVisibleCount(current=>current+24);}

  return <section className="intranet-directory">
    <header className="intranet-section-hero intranet-section-hero-people"><div><p>Nuestro equipo</p><h1>Personas</h1><span>Encuentra y conecta con quienes hacen posible el trabajo de Fundación Gaia Amazonas.</span></div><label><Search aria-hidden="true" size={18}/><input aria-label="Buscar por persona, cargo o unidad" onChange={event=>setSearch(event.target.value)} placeholder="Buscar persona, cargo, unidad, sede…" value={search}/></label></header>
    <div className="intranet-directory-layout">
      <aside className="intranet-directory-units"><header><Building2 size={17}/><span><strong>Unidades organizacionales</strong><small>Explora las personas de cada equipo.</small></span></header><div role="tree"><button aria-current={!selectedUnitId?"true":undefined} className="intranet-directory-unit is-all" onClick={()=>{setSelectedUnitId("");setVisibleCount(24);}} type="button"><Users size={15}/><span><strong>Toda la organización</strong><small>Directorio completo</small></span></button>{tree.map(({unit,depth,hasChildren})=><div className="intranet-directory-tree-row" key={unit.id} style={{"--tree-depth":depth} as React.CSSProperties}>{hasChildren?<button aria-label={`${collapsed.has(unit.id)?"Expandir":"Contraer"} ${unit.name}`} className="intranet-directory-tree-toggle" onClick={()=>setCollapsed(current=>toggle(current,unit.id))} type="button">{collapsed.has(unit.id)?<ChevronRight size={14}/>:<ChevronDown size={14}/>}</button>:<span className="intranet-directory-tree-spacer"/>}<button aria-current={selectedUnitId===unit.id?"true":undefined} className="intranet-directory-unit" onClick={()=>{setSelectedUnitId(unit.id);setVisibleCount(24);}} type="button"><span><strong>{unit.name}</strong><small>{unit.code}</small></span></button></div>)}</div></aside>
      <div className="intranet-directory-results">
        <div className="intranet-directory-summary"><span><Users size={16}/><strong>{result?.total??0}</strong> {selectedUnit?`personas en ${selectedUnit.name}`:"colaboradores activos"}</span><small>Solo se muestran datos institucionales autorizados.</small></div>
        {selectedUnit&&<label className="intranet-directory-descendants"><input checked={includeDescendants} onChange={event=>{setIncludeDescendants(event.target.checked);setVisibleCount(24);}} type="checkbox"/><span><strong>Incluir unidades descendientes</strong><small>Agrega los equipos que dependen de esta unidad.</small></span></label>}
        {error&&<div className="intranet-data-state is-error"><strong>No fue posible cargar Personas</strong><p>{error}</p></div>}
        {!error&&loading&&<div className="intranet-data-state"><strong>Cargando directorio…</strong></div>}
        {!error&&!loading&&result.items.length===0&&<div className="intranet-data-state"><strong>No encontramos personas</strong><p>Prueba con otro nombre, cargo, unidad, código, sede, correo o teléfono.</p></div>}
        {!loading&&result.items.length>0&&<div className="intranet-people-grid">{result.items.map(person=><PersonCard key={person.id} person={person}/>)}</div>}
        {result.items.length<result.total&&<button className="intranet-load-more" disabled={loading} onClick={loadMore} type="button">Mostrar más personas</button>}
      </div>
    </div>
  </section>;
}

function peoplePath(page:number,pageSize:number,search:string,unitId:string,includeDescendants:boolean){const params=new URLSearchParams({page:String(page),pageSize:String(pageSize),search,includeDescendants:String(includeDescendants)});if(unitId)params.set("organizationUnitId",unitId);return `/api/intranet/people?${params}`;}
function PersonCard({person}:{person:Person}){return <article className="intranet-person-card"><header className="intranet-person-identity"><span className="intranet-person-avatar" style={person.photoUrl?{backgroundImage:`url("${person.photoUrl}")`}:undefined}>{!person.photoUrl&&initials(person.fullName)}</span><div><h2>{title(person.fullName)}</h2><p>{person.jobTitle??"Colaborador Gaia"}</p></div></header><div className="intranet-person-team"><Building2 size={15}/><span><strong>{person.organizationUnit??"Equipo Gaia"}</strong>{person.site&&<small><MapPin size={12}/>{person.site}</small>}</span></div><footer><span>Contacto corporativo</span><div>{person.institutionalEmail&&<a aria-label={`Enviar correo a ${person.fullName}`} href={`mailto:${person.institutionalEmail}`} title={person.institutionalEmail}><Mail size={16}/><span>{person.institutionalEmail}</span></a>}{person.visiblePhone&&<a aria-label={`Llamar a ${person.fullName}`} href={`tel:${person.visiblePhone}`} title={person.visiblePhone}><Phone size={16}/><span>{person.visiblePhone}</span></a>}{!person.institutionalEmail&&!person.visiblePhone&&<small>Contacto corporativo aún no publicado</small>}</div></footer></article>}
function normalize(value:string|null|undefined){return (value??"").normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es").trim();}
function descendantsOf(parentId:string,units:OrganizationUnit[]){const children=new Map<string,string[]>();units.forEach(unit=>{if(unit.parentId)children.set(unit.parentId,[...(children.get(unit.parentId)??[]),unit.id]);});const result:string[]=[];const pending=[...(children.get(parentId)??[])];const visited=new Set<string>();while(pending.length){const id=pending.pop()!;if(visited.has(id))continue;visited.add(id);result.push(id);pending.push(...(children.get(id)??[]));}return result;}
function toggle(current:Set<string>,id:string){const next=new Set(current);if(next.has(id))next.delete(id);else next.add(id);return next;}
function organizationTree(units:OrganizationUnit[],collapsed:Set<string>){const ids=new Set(units.map(unit=>unit.id));const children=new Map<string,OrganizationUnit[]>();for(const unit of units){const parent=unit.parentId&&ids.has(unit.parentId)?unit.parentId:"";children.set(parent,[...(children.get(parent)??[]),unit]);}children.forEach(rows=>rows.sort((a,b)=>a.visualOrder-b.visualOrder||a.name.localeCompare(b.name,"es")));const result:{unit:OrganizationUnit;depth:number;hasChildren:boolean}[]=[];const visit=(parent:string,depth:number)=>{for(const unit of children.get(parent)??[]){const hasChildren=(children.get(unit.id)?.length??0)>0;result.push({unit,depth,hasChildren});if(hasChildren&&!collapsed.has(unit.id))visit(unit.id,depth+1);}};visit("",0);return result;}

function initials(name:string){return name.trim().split(/\s+/).slice(0,2).map(value=>value[0]).join("").toUpperCase();}
function title(name:string){return name.toLocaleLowerCase("es").replace(/(^|\s)\p{L}/gu,value=>value.toLocaleUpperCase("es"));}
