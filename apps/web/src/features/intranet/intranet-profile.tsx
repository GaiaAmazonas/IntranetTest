"use client";
import {useEffect,useState} from "react";
import {Building2,BriefcaseBusiness,Mail,MapPin,Phone,UserRound,ShieldCheck} from "lucide-react";
import {useSecurity} from "@/components/security-context";
import {PersonAvatar} from "@/components/person-avatar";
import {apiRequest} from "@/lib/api-client";
import "./intranet-profile.css";
type Profile={id:string;fullName:string;jobTitle:string|null;organizationUnit:string|null;organizationUnitCode:string|null;site:string|null;institutionalEmail:string|null;visiblePhone:string|null};
export function IntranetProfile(){
 const {user}=useSecurity(),[person,setPerson]=useState<Profile|null>(null),[loading,setLoading]=useState(true),[error,setError]=useState("");
 useEffect(()=>{let active=true;apiRequest<Profile>("/api/profile/").then(value=>{if(active)setPerson(value)}).catch(reason=>{if(active)setError(reason instanceof Error?reason.message:"No fue posible cargar tu ficha.")}).finally(()=>{if(active)setLoading(false)});return()=>{active=false}},[]);
 const email=person?.institutionalEmail||user?.email;
 return <section className="intranet-page-shell intranet-profile-page"><header className="intranet-page-heading"><p>Cuenta institucional</p><h1>Mi perfil</h1><span>Tu ficha personal y tu lugar dentro del equipo Gaia.</span></header>
 {loading&&<p className="intranet-profile-notice" role="status">Consultando tu información institucional…</p>}{error&&<p className="intranet-profile-notice" role="alert">{error}</p>}
 <div className="intranet-profile-layout"><aside className="intranet-profile-identity"><PersonAvatar currentUser name={person?.fullName||user?.name||"Mi perfil"} size={144}/><small>Fundación Gaia Amazonas</small><h2>{person?.fullName||user?.name||"Colaborador Gaia"}</h2><p>{person?.jobTitle||"Cargo no registrado"}</p><span><ShieldCheck size={15}/>{user?.isActive?"Cuenta institucional activa":"Cuenta institucional"}</span>{email&&<a href={`mailto:${email}`}><Mail size={16}/>Enviar correo</a>}</aside>
 <div className="intranet-profile-sections"><article><header><UserRound size={21}/><div><small>Tu información</small><h2>Datos generales</h2></div></header><dl><div><dt>Nombre completo</dt><dd>{person?.fullName||user?.name||"No registrado"}</dd></div><div><dt>Documento</dt><dd>{user?.documentNumber||"No registrado"}</dd></div><div><dt>Correo institucional</dt><dd>{email?<a href={`mailto:${email}`}>{email}</a>:"No registrado"}</dd></div><div><dt><Phone size={14}/>Teléfono corporativo</dt><dd>{person?.visiblePhone?<a href={`tel:${person.visiblePhone}`}>{person.visiblePhone}</a>:"No registrado"}</dd></div></dl></article>
 <article><header><Building2 size={21}/><div><small>Tu equipo</small><h2>Ubicación organizacional</h2></div></header><dl><div><dt>Organización</dt><dd>Fundación Gaia Amazonas</dd></div><div><dt>Unidad organizacional</dt><dd>{person?.organizationUnit||"No registrada"}</dd></div><div><dt>Código de la unidad</dt><dd>{person?.organizationUnitCode||"No registrado"}</dd></div><div><dt><BriefcaseBusiness size={14}/>Cargo</dt><dd>{person?.jobTitle||"No registrado"}</dd></div><div><dt><MapPin size={14}/>Sede</dt><dd>{person?.site||"No registrada"}</dd></div></dl></article><p className="intranet-profile-footnote">Ficha de consulta. Si algún dato requiere actualización, solicita el ajuste al equipo responsable de Talento Humano.</p></div></div></section>
}
