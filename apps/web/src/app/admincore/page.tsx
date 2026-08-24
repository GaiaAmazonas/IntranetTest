"use client";

import Link from "next/link";
import { ArrowRight, Building2, LockKeyhole, PackageSearch, ShieldCheck, Sparkles, Users } from "lucide-react";
import { AppHeader } from "@/components/app-header";
import { useSecurity } from "@/components/security-context";

const workspaceModules = [
  { title:"Organización", description:"Estructura, cargos, sedes y asignaciones organizacionales.", href:"/organizacion", permission:"ORG.ORGANIGRAMA.VER|ORG.UNIDADES.VER|ORG.ASIGNACIONES.VER|ORG.CARGOS.VER|ORG.SEDES_TIPOS.VER", eyebrow:"Estructura institucional", icon:Building2, accent:"#317c87", surface:"from-[#e8f4f4] to-[#f8fbfa]" },
  { title:"Talento Humano", description:"Colaboradores, información de contacto y vinculaciones.", href:"/talento-humano/colaboradores", permission:"TH.COLABORADORES.VER|TH.VINCULACIONES.VER", eyebrow:"Personas y relaciones", icon:Users, accent:"#8b3c72", surface:"from-[#f8edf4] to-[#fcf9fb]" },
  { title:"Inventario", description:"Elementos institucionales, asignaciones y movimientos.", href:"/inventario", permission:"INV.VER", eyebrow:"Activos y custodia", icon:PackageSearch, accent:"#55754b", surface:"from-[#edf4e9] to-[#fafcf9]" },
  { title:"Seguridad", description:"Usuarios, roles, permisos y catálogo de recursos protegidos.", href:"/seguridad/usuarios", permission:"TI.USUARIOS.VER|TI.ROLES.VER|TI.MODULOS.VER", eyebrow:"Identidad y acceso", icon:LockKeyhole, accent:"#9a384d", surface:"from-[#faecef] to-[#fcfaf9]" },
] as const;

export default function AdminCoreHomePage() {
  const security = useSecurity();
  const { user } = security;
  if (!user) return null;
  const availableModules = workspaceModules.filter(module => security.can(module.permission));
  const firstName = user.name.split(" ").filter(Boolean)[0] ?? "";

  return <main className="gaia-app-page min-h-screen bg-[#f7f9f6] text-[#193522]">
    <AppHeader title="AdminCore · Inicio" user={{ displayName:user.name, email:user.email }}/>
    <div className="mx-auto max-w-[1400px] px-5 py-7 lg:px-8 lg:py-10">
      <section className="relative overflow-hidden rounded-[30px] bg-[#123e2d] px-6 py-8 text-white shadow-[0_22px_55px_rgba(18,62,45,.16)] sm:px-9 lg:grid lg:grid-cols-[1fr_auto] lg:items-end lg:px-12 lg:py-11">
        <div className="relative z-10 max-w-3xl"><p className="flex items-center gap-2 text-[10px] font-bold uppercase tracking-[.18em] text-[#bfd29d]"><Sparkles size={14}/>Centro de gestión institucional</p><h1 className="mt-4 text-3xl font-semibold tracking-[-.035em] sm:text-4xl">Hola, {firstName}.</h1><p className="mt-3 max-w-2xl text-sm leading-6 text-[#dce8df] sm:text-base">Aquí encuentras únicamente las herramientas que tienes autorizadas para gestionar. Tu espacio se adapta automáticamente a tus responsabilidades.</p></div>
        <div className="relative z-10 mt-7 flex items-center gap-3 rounded-2xl border border-white/15 bg-white/10 px-4 py-3 backdrop-blur lg:mt-0"><span className="grid size-10 place-items-center rounded-xl bg-[#c9dc9f] text-[#174631]"><ShieldCheck size={20}/></span><div><strong className="block text-xl">{availableModules.length}</strong><small className="text-[#d6e2da]">módulos disponibles</small></div></div>
        <span className="pointer-events-none absolute -right-20 -top-32 size-80 rounded-full border-[55px] border-white/[.055]"/><span className="pointer-events-none absolute -bottom-36 right-40 size-60 rounded-full border-[42px] border-[#b6ce8b]/[.07]"/>
      </section>

      <section className="mt-9"><div className="flex flex-wrap items-end justify-between gap-3"><div><p className="text-[10px] font-bold uppercase tracking-[.15em] text-[#66804e]">Tu espacio de trabajo</p><h2 className="mt-2 text-2xl font-semibold tracking-tight text-[#153729]">¿Qué necesitas gestionar hoy?</h2></div><p className="max-w-md text-xs leading-5 text-[#6b7b72]">Las opciones se muestran según los permisos asignados a tu cuenta en Seguridad.</p></div>
        {availableModules.length ? <div className="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-4">{availableModules.map((module,index)=>{const Icon=module.icon;return <Link className={`group relative flex min-h-[255px] flex-col overflow-hidden rounded-[24px] border border-[#dfe7dc] bg-gradient-to-br ${module.surface} p-5 transition duration-200 hover:-translate-y-1 hover:border-[#becfba] hover:shadow-[0_18px_38px_rgba(30,70,48,.11)]`} href={module.href} key={module.href}>
          <div className="flex items-start justify-between"><span className="grid size-12 place-items-center rounded-2xl border border-white/80 bg-white shadow-sm" style={{color:module.accent}}><Icon size={22}/></span><small className="text-[10px] font-bold tracking-[.14em] text-[#87948c]">{String(index+1).padStart(2,"0")}</small></div>
          <p className="mt-7 text-[10px] font-bold uppercase tracking-[.13em]" style={{color:module.accent}}>{module.eyebrow}</p><h3 className="mt-2 text-xl font-semibold tracking-tight text-[#153729]">{module.title}</h3><p className="mt-2 flex-1 text-xs leading-5 text-[#66776e]">{module.description}</p><span className="mt-5 flex items-center gap-2 text-xs font-bold text-[#214d38]">Abrir módulo <ArrowRight className="transition group-hover:translate-x-1" size={15}/></span>
        </Link>;})}</div>:<div className="mt-5 grid min-h-52 place-items-center rounded-3xl border border-dashed border-[#cfdccc] bg-white text-center"><div><LockKeyhole className="mx-auto text-[#66804e]"/><h3 className="mt-3 font-semibold">No tienes módulos habilitados</h3><p className="mt-1 text-xs text-[#718077]">Solicita a un administrador la revisión de tus permisos.</p></div></div>}
      </section>
    </div>
  </main>;
}
