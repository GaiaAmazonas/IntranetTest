"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";
type User = { displayName: string; email: string };

export function AppHeader({ title, user: suppliedUser }: { title: string; user?: User }) {
  const [user, setUser] = useState<User | null>(suppliedUser ?? null);
  useEffect(() => {
    if (suppliedUser) return;
    void fetch(`${apiUrl}/api/auth/me`, { credentials: "include" }).then(async response => {
      if (response.status === 401) { window.location.href = "/"; return; }
      if (response.ok) setUser(await response.json() as User);
    });
  }, [suppliedUser]);
  async function logout() {
    await fetch(`${apiUrl}/api/auth/logout`, { method: "POST", credentials: "include" });
    window.location.href = "/";
  }
  return <header className="app-header bg-[#173f2b] text-white shadow-[0_10px_35px_rgba(20,55,36,.16)]">
    <div className="mx-auto flex max-w-[1500px] flex-wrap items-center justify-between gap-3 px-5 py-3.5">
      <div><p className="text-[10px] font-bold uppercase tracking-[.25em] text-[#b9d0ae]">Fundación Gaia Amazonas</p><h1 className="text-lg font-semibold">{title}</h1></div>
      <div className="flex items-center gap-4">
        <nav className="flex gap-4 text-xs font-semibold text-[#d7e6d1]"><Link href="/">Inicio</Link><Link className="hidden md:inline" href="/organizacion">Organización</Link><Link className="hidden md:inline" href="/terceros">Terceros</Link><Link className="hidden md:inline" href="/inventario">Inventario</Link></nav>
        <span className="hidden h-7 w-px bg-white/20 sm:block" />
        {user && <div className="hidden text-right sm:block"><p className="text-sm font-semibold">{user.displayName}</p><p className="text-[11px] text-[#b9cdb5]">{user.email}</p></div>}
        <button className="rounded-full border border-white/25 px-3 py-1.5 text-xs font-semibold hover:bg-white/10" onClick={() => void logout()} type="button">Cerrar sesión</button>
      </div>
    </div>
    <div className="h-1 bg-gradient-to-r from-[#a0384d] via-[#3c838c] to-[#66804e]" />
  </header>;
}
