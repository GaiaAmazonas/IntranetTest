"use client";

import {useEffect} from "react";
import Link from "next/link";
import {useRouter} from "next/navigation";

export default function LegacyHelpdeskFormPage(){
 const router=useRouter(),destination="/helpdesk/servicios-y-flujos/formulario/";
 useEffect(()=>{router.replace(`${destination}${window.location.search}`)},[router]);
 return <main className="grid min-h-[50vh] place-items-center p-6"><div className="text-center"><p className="text-sm text-[var(--gaia-ink-500)]">Abriendo el formulario del servicio…</p><Link className="mt-3 inline-block text-sm font-semibold text-[var(--brand-primary)]" href={destination}>Continuar</Link></div></main>;
}
