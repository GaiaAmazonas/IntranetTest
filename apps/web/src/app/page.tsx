"use client";

import { FormEvent, useEffect, useState } from "react";

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

  async function logout() {
    await fetch(`${apiUrl}/api/auth/logout`, {
      method: "POST",
      credentials: "include",
    });
    setUser(null);
    setStatus("anonymous");
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
      <main className="min-h-screen bg-[#f4f7f2] p-6 text-[#193522] lg:p-10">
        <div className="mx-auto max-w-7xl">
          <header className="flex items-center justify-between rounded-3xl bg-white px-6 py-5 shadow-[0_18px_60px_rgba(29,67,40,0.08)]">
            <div>
              <p className="text-xs font-bold uppercase tracking-[0.24em] text-[#66804e]">
                Fundación Gaia Amazonas
              </p>
              <h1 className="mt-1 text-2xl font-semibold">Plataforma empresarial</h1>
            </div>
            <div className="flex items-center gap-4">
              <div className="hidden text-right sm:block">
                <p className="font-semibold">{user.displayName}</p>
                <p className="text-sm text-[#607065]">{user.email}</p>
              </div>
              <button
                className="rounded-full border border-[#d8e2d5] px-4 py-2 text-sm font-semibold hover:bg-[#f4f7f2]"
                onClick={() => void logout()}
                type="button"
              >
                Cerrar sesión
              </button>
            </div>
          </header>

          <section className="mt-8">
            <p className="text-sm font-semibold text-[#66804e]">Inicio</p>
            <h2 className="mt-2 text-4xl font-semibold tracking-tight">
              Hola, {user.displayName.split(" ")[0]}
            </h2>
            <p className="mt-3 max-w-2xl text-[#607065]">
              Esta es la base operativa desde la que gestionaremos la organización,
              los terceros y el inventario institucional.
            </p>
          </section>

          <section className="mt-10 grid gap-5 md:grid-cols-3">
            {[
              ["Estructura organizacional", "Sedes, áreas, equipos y cargos", "01"],
              ["Terceros", "Personas y organizaciones relacionadas", "02"],
              ["Inventario", "Elementos, asignaciones y movimientos", "03"],
            ].map(([title, description, number]) => (
              <article
                className="rounded-3xl border border-[#e2e9df] bg-white p-6 transition hover:-translate-y-1 hover:shadow-xl"
                key={number}
              >
                <span className="text-xs font-bold tracking-[0.2em] text-[#a25b3c]">
                  MÓDULO {number}
                </span>
                <h3 className="mt-8 text-xl font-semibold">{title}</h3>
                <p className="mt-2 text-sm leading-6 text-[#607065]">{description}</p>
                <p className="mt-8 text-sm font-semibold text-[#66804e]">
                  Próximamente →
                </p>
              </article>
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
            <span className="inline-flex size-12 items-center justify-center rounded-2xl bg-[#e8efe3] text-xl font-bold text-[#34523a]">
              G
            </span>
            <h2 className="mt-7 text-3xl font-semibold tracking-tight text-[#193522]">
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
