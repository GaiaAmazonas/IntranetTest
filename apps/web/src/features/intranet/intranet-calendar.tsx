"use client";

import { Cake, CalendarDays, ChevronLeft, ChevronRight } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { apiRequest } from "@/lib/api-client";

type Birthday={id:string;fullName:string;day:number;month:number;photoUrl:string|null};
const weekdays=["Lun","Mar","Mié","Jue","Vie","Sáb","Dom"];

export function IntranetCalendar(){
  const today=new Date();const[current,setCurrent]=useState(()=>new Date(today.getFullYear(),today.getMonth(),1));const[selected,setSelected]=useState(today.getDate());const[birthdays,setBirthdays]=useState<Birthday[]>([]);const[loading,setLoading]=useState(true);const[error,setError]=useState("");
  useEffect(()=>{let active=true;apiRequest<Birthday[]>(`/api/intranet/birthdays?month=${current.getMonth()+1}`).then(rows=>{if(active)setBirthdays(rows);}).catch(reason=>{if(active)setError(reason instanceof Error?reason.message:"No fue posible cargar el calendario.");}).finally(()=>{if(active)setLoading(false);});return()=>{active=false;};},[current]);
  const days=useMemo(()=>calendarDays(current),[current]);const selectedBirthdays=birthdays.filter(item=>item.day===selected);const monthName=new Intl.DateTimeFormat("es-CO",{month:"long",year:"numeric"}).format(current);
  function move(offset:number){const next=new Date(current.getFullYear(),current.getMonth()+offset,1);setLoading(true);setError("");setCurrent(next);setSelected(1);}
  return <section className="intranet-calendar-page"><header className="intranet-section-hero intranet-section-hero-calendar"><div><p>Agenda institucional</p><h1>Calendario</h1><span>Encuentros, celebraciones y momentos que conectan a nuestro equipo.</span></div><button onClick={()=>{setLoading(true);setError("");setCurrent(new Date(today.getFullYear(),today.getMonth(),1));setSelected(today.getDate());}} type="button">Ir a hoy</button></header>
    {error&&<div className="intranet-data-state is-error"><strong>No fue posible cargar el calendario</strong><p>{error}</p></div>}
    <div className="intranet-calendar-layout"><div className="intranet-month"><header><button aria-label="Mes anterior" onClick={()=>move(-1)} type="button"><ChevronLeft size={18}/></button><h2>{monthName}</h2><button aria-label="Mes siguiente" onClick={()=>move(1)} type="button"><ChevronRight size={18}/></button></header><div className="intranet-weekdays">{weekdays.map(day=><span key={day}>{day}</span>)}</div><div className="intranet-days">{days.map((day,index)=>day===null?<span key={`empty-${index}`}/>:<button aria-pressed={selected===day} className={selected===day?"is-selected":undefined} key={day} onClick={()=>setSelected(day)} type="button"><strong>{day}</strong>{birthdays.some(item=>item.day===day)&&<i aria-label="Cumpleaños"/>}</button>)}</div></div>
      <aside className="intranet-day-detail"><p>Tu agenda del día</p><h2>{selected} de {monthName.split(" ")[0]}</h2>{loading?<div className="intranet-day-empty">Cargando celebraciones…</div>:selectedBirthdays.length?<div className="intranet-birthday-list">{selectedBirthdays.map(person=><article key={person.id}><span>{initials(person.fullName)}</span><div><small><Cake size={13}/>Cumpleaños</small><strong>{title(person.fullName)}</strong><em>Fundación Gaia Amazonas</em></div></article>)}</div>:<div className="intranet-day-empty"><CalendarDays size={22}/><strong>Un día disponible</strong><span>No hay celebraciones visibles para esta fecha.</span></div>}<section><h3>Eventos institucionales</h3><p>Los próximos encuentros aparecerán aquí cuando se conecte su fuente institucional.</p></section></aside>
    </div></section>;
}

function calendarDays(date:Date){const first=new Date(date.getFullYear(),date.getMonth(),1);const count=new Date(date.getFullYear(),date.getMonth()+1,0).getDate();const offset=(first.getDay()+6)%7;return [...Array<null>(offset).fill(null),...Array.from({length:count},(_,index)=>index+1)];}
function initials(name:string){return name.trim().split(/\s+/).slice(0,2).map(value=>value[0]).join("").toUpperCase();}
function title(name:string){return name.toLocaleLowerCase("es").replace(/(^|\s)\p{L}/gu,value=>value.toLocaleUpperCase("es"));}
