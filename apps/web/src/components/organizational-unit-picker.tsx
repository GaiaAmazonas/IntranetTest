"use client";

import { Check, ChevronDown, Search } from "lucide-react";
import { useEffect, useMemo, useRef, useState, type CSSProperties } from "react";

export type OrganizationalUnitOption = { id:string; code:string; name:string; parentId?:string|null; level?:number; isActive?:boolean };

export function OrganizationalUnitPicker({units,value,onChange,required=false,placeholder="Seleccionar unidad"}:{units:OrganizationalUnitOption[];value:string;onChange:(id:string)=>void;required?:boolean;placeholder?:string}){
  const root=useRef<HTMLDivElement>(null),[open,setOpen]=useState(false),[search,setSearch]=useState("");
  const ordered=useMemo(()=>treeOrder(units.filter(unit=>unit.isActive!==false)),[units]);
  const selected=ordered.find(unit=>unit.id===value);
  const visible=useMemo(()=>{const query=normalize(search);if(!query)return ordered;const byId=new Map(ordered.map(unit=>[unit.id,unit])),included=new Set<string>();for(const unit of ordered.filter(item=>normalize(`${item.code} ${item.name}`).includes(query))){let current:OrganizationalUnitOption|undefined=unit;while(current&&!included.has(current.id)){included.add(current.id);current=current.parentId?byId.get(current.parentId):undefined;}}return ordered.filter(unit=>included.has(unit.id));},[ordered,search]);
  useEffect(()=>{const close=(event:MouseEvent)=>{if(!root.current?.contains(event.target as Node))setOpen(false)};document.addEventListener("mousedown",close);return()=>document.removeEventListener("mousedown",close)},[]);
  return <div className="gaia-unit-picker" ref={root}>
    <button aria-expanded={open} aria-haspopup="listbox" className="gaia-unit-picker-trigger" onClick={()=>setOpen(current=>!current)} type="button"><span className={selected?"":"is-placeholder"}>{selected?<><strong>{selected.name}</strong><small>{selected.code}</small></>:placeholder}</span><ChevronDown size={16}/></button>
    {required&&<input aria-hidden="true" className="gaia-unit-picker-required" required tabIndex={-1} value={value} onChange={()=>{}}/>}
    {open&&<div className="gaia-unit-picker-popover"><label className="gaia-unit-picker-search"><Search size={15}/><input autoFocus onChange={event=>setSearch(event.target.value)} placeholder="Buscar por código o nombre" value={search}/></label><div className="gaia-unit-picker-tree" role="listbox">{visible.map(unit=><button aria-selected={unit.id===value} className="gaia-unit-picker-option" key={unit.id} onClick={()=>{onChange(unit.id);setOpen(false);setSearch("")}} role="option" style={{"--unit-depth":unit.depth} as CSSProperties} type="button"><span className="gaia-unit-picker-branch"/><span><strong>{unit.name}</strong><small>{unit.code}</small></span>{unit.id===value&&<Check size={15}/>}</button>)}{!visible.length&&<p className="gaia-unit-picker-empty">No se encontraron unidades.</p>}</div></div>}
  </div>;
}

function treeOrder(units:OrganizationalUnitOption[]){const ids=new Set(units.map(unit=>unit.id)),children=new Map<string|null,OrganizationalUnitOption[]>();for(const unit of units){const parent=unit.parentId&&ids.has(unit.parentId)?unit.parentId:null;children.set(parent,[...(children.get(parent)??[]),unit]);}const sort=(items:OrganizationalUnitOption[])=>items.sort((left,right)=>left.code.localeCompare(right.code,"es",{numeric:true})||left.name.localeCompare(right.name,"es"));const result:Array<OrganizationalUnitOption&{depth:number}>=[],visited=new Set<string>();const visit=(parent:string|null,depth:number)=>{for(const unit of sort(children.get(parent)??[])){if(visited.has(unit.id))continue;visited.add(unit.id);result.push({...unit,depth});visit(unit.id,depth+1)}};visit(null,0);for(const unit of sort(units.filter(item=>!visited.has(item.id))))result.push({...unit,depth:Math.max(0,unit.level??0)});return result;}
function normalize(value:string){return value.normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLocaleLowerCase("es").trim()}
