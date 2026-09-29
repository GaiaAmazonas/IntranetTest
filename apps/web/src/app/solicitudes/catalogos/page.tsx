"use client";

import {useEffect} from "react";
import Link from "next/link";
import {useRouter} from "next/navigation";

export default function LegacySolicitudesCatalogsPage(){
 const router=useRouter();
 useEffect(()=>{router.replace("/solicitudes/servicios-y-flujos/")},[router]);
 return <main className="grid min-h-[50vh] place-items-center p-6"><div className="text-center"><p className="text-sm text-[var(--gaia-ink-500)]">Abriendo Servicios y flujos…</p><Link className="mt-3 inline-block text-sm font-semibold text-[var(--brand-primary)]" href="/solicitudes/servicios-y-flujos/">Continuar</Link></div></main>;
}
