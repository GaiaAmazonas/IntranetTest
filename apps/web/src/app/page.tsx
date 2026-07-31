"use client";

import { FormEvent, useEffect, useState } from "react";
import { AppHeader } from "@/components/app-header";

type CurrentUser = {
  id: string;
  displayName: string;
  email: string;
  roles: string[];
  permissions: string[];
};

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";

export default function Home() {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [email, setEmail] = useState("emunar@gaiaamazonas.org");
  const [password, setPassword] = useState("");
  const [status, setStatus] = useState<"loading" | "anonymous" | "authenticated">(
    "loading",
  );
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    async function loadSession() {
      try {
        const response = await fetch(`${apiUrl}/api/auth/me`, {
          credentials: "include",
        });
        if (response.ok) {
          setUser((await response.json()) as CurrentUser);
          setStatus("authenticated");
          return;
        }
      } catch {
        setError("No fue posible conectar con el servicio de Gaia.");
      }
      setStatus("anonymous");
    }

    void loadSession();
  }, []);

  async function login(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitting(true);
    setError("");

    try {
      const response = await fetch(`${apiUrl}/api/auth/login`, {
        method: "POST",
        credentials: "include",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });

      if (!response.ok) {
        setError("El correo o la contraseña no son válidos.");
        return;
      }

      setUser((await response.json()) as CurrentUser);
      setPassword("");
      setStatus("authenticated");
    } catch {
      setError("No fue posible conectar con el servicio de Gaia.");
    } finally {
      setSubmitting(false);
    }
  }

  if (status === "loading") {
    return (
      <main className="grid min-h-screen place-items-center bg-[#f4f7f2]">
        <p className="text-sm font-medium tracking-wide text-[#34523a]">
          Preparando Gaia…
        </p>
      </main>
    );
  }

  if (status === "authenticated" && user) {
    return (
      <main className="min-h-screen bg-[linear-gradient(145deg,#eef4e9_0%,#f7f3e8_48%,#e9f1ec_100%)] text-[#193522]">
        <AppHeader title="Plataforma empresarial" user={user} />
        <div className="mx-auto max-w-7xl px-4 py-5 lg:px-6">
          <section>
            <p className="text-sm font-semibold text-[#66804e]">Inicio</p>
            <h2 className="mt-1 text-3xl font-semibold tracking-tight">
              Hola, {user.displayName.split(" ")[0]}
            </h2>
            <p className="mt-2 max-w-2xl text-sm text-[#607065]">
              Esta es la base operativa desde la que gestionaremos la organización,
              los terceros y el inventario institucional.
            </p>
          </section>

          <section className="mt-6 grid gap-4 md:grid-cols-3">
            {[
              ["Estructura organizacional", "Sedes, áreas, equipos y cargos", "01", "/organizacion", "from-[#214d38] to-[#3c838c]", "Jerarquía · Equipos"],
              ["Terceros", "Personas y organizaciones relacionadas", "02", "/terceros", "from-[#6f3873] to-[#a0384d]", "Talento · Relaciones"],
              ["Inventario", "Elementos, asignaciones y movimientos", "03", "/inventario", "from-[#52685e] to-[#66804e]", "Activos · Custodia"],
            ].map(([title, description, number, href, color, motif]) => (
              <a
                className={`group relative min-h-56 overflow-hidden rounded-3xl bg-gradient-to-br ${color} p-5 text-white shadow-[0_18px_45px_rgba(31,65,42,.16)] transition hover:-translate-y-1 hover:shadow-2xl`}
                href={href}
                key={number}
              >
                <span className="relative text-xs font-bold tracking-[0.2em] text-white/75">
                  MÓDULO {number}
                </span>
                <div className="absolute -bottom-20 -right-14 size-52 rounded-full border-[34px] border-white/10 transition group-hover:scale-110" />
                <p className="relative mt-10 text-[10px] font-bold uppercase tracking-[.24em] text-white/60">{motif}</p>
                <h3 className="relative mt-2 text-xl font-semibold">{title}</h3>
                <p className="relative mt-2 max-w-xs text-sm leading-6 text-white/75">{description}</p>
                <p className="relative mt-5 text-sm font-semibold text-white">
                  Abrir módulo →
                </p>
              </a>
            ))}
          </section>
        </div>
      </main>
    );
  }

  return (
    <main className="grid min-h-screen bg-[#edf2ea] lg:grid-cols-[1.08fr_0.92fr]">
      <section className="relative hidden overflow-hidden bg-[#193f2c] p-14 text-white lg:flex lg:flex-col lg:justify-between">
        <div className="absolute -right-28 -top-20 size-96 rounded-full border-[70px] border-[#688450]/25" />
        <div className="absolute -bottom-36 -left-20 size-[32rem] rounded-full border-[90px] border-[#b76d49]/20" />
        <p className="relative text-sm font-bold uppercase tracking-[0.3em]">
          Fundación Gaia Amazonas
        </p>
        <div className="relative max-w-xl">
          <p className="mb-5 text-sm font-semibold uppercase tracking-[0.22em] text-[#b7c9a8]">
            Territorio · Conocimiento · Futuro
          </p>
          <h1 className="text-6xl font-semibold leading-[1.05] tracking-tight">
            Una plataforma para cuidar lo que somos.
          </h1>
          <p className="mt-7 max-w-lg text-lg leading-8 text-[#d9e4d4]">
            Información institucional conectada, segura y preparada para acompañar
            el trabajo de Gaia durante los próximos años.
          </p>
        </div>
        <p className="relative text-sm text-[#b7c9a8]">Gaia Enterprise Platform</p>
      </section>

      <section className="flex items-center justify-center p-6 sm:p-12">
        <div className="w-full max-w-md rounded-[2rem] bg-white p-8 shadow-[0_30px_80px_rgba(30,65,40,0.12)] sm:p-10">
          <div className="mb-9">
            <h2 className="text-3xl font-semibold tracking-tight text-[#193522]">
              Bienvenido a Gaia
            </h2>
            <p className="mt-2 text-sm leading-6 text-[#68766c]">
              Ingresa con tu cuenta autorizada para acceder a la plataforma.
            </p>
          </div>

          <form className="space-y-5" onSubmit={login}>
            <label className="block">
              <span className="text-sm font-semibold text-[#34473a]">
                Correo electrónico
              </span>
              <input
                autoComplete="username"
                className="mt-2 w-full rounded-xl border border-[#d6dfd3] bg-[#fbfcfa] px-4 py-3 text-[#193522] outline-none transition focus:border-[#66804e] focus:ring-4 focus:ring-[#66804e]/10"
                onChange={(event) => setEmail(event.target.value)}
                required
                type="email"
                value={email}
              />
            </label>
            <label className="block">
              <span className="text-sm font-semibold text-[#34473a]">Contraseña</span>
              <input
                autoComplete="current-password"
                className="mt-2 w-full rounded-xl border border-[#d6dfd3] bg-[#fbfcfa] px-4 py-3 text-[#193522] outline-none transition focus:border-[#66804e] focus:ring-4 focus:ring-[#66804e]/10"
                onChange={(event) => setPassword(event.target.value)}
                required
                type="password"
                value={password}
              />
            </label>

            {error && (
              <p
                className="rounded-xl bg-[#fff0eb] px-4 py-3 text-sm text-[#8a3f25]"
                role="alert"
              >
                {error}
              </p>
            )}

            <button
              className="w-full rounded-xl bg-[#294f35] px-5 py-3.5 font-semibold text-white transition hover:bg-[#193f2c] disabled:cursor-wait disabled:opacity-60"
              disabled={submitting}
              type="submit"
            >
              {submitting ? "Validando…" : "Ingresar"}
            </button>
          </form>

          <p className="mt-7 text-center text-xs leading-5 text-[#819087]">
            Acceso exclusivo para personal autorizado. Los intentos de acceso quedan
            registrados por seguridad.
          </p>
        </div>
      </section>
    </main>
  );
}
