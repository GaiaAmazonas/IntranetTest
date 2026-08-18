"use client";

import { Suspense, useEffect, useState } from "react";
import { AppHeader } from "@/components/app-header";
import { useSearchParams } from "next/navigation";
import { ArrowRight } from "lucide-react";
import Image from "next/image";

type CurrentUser = {
  id: string;
  displayName: string;
  email: string;
  roles: string[];
  permissions: string[];
};

const apiUrl = process.env.NEXT_PUBLIC_GAIA_API_URL ?? "https://localhost:7168";

export default function Home() {
  return <Suspense fallback={null}><HomeContent /></Suspense>;
}

function HomeContent() {

  const searchParams = useSearchParams();
  const logoutSuccess = searchParams.get("logout") === "success";

  const [user, setUser] = useState<CurrentUser | null>(null);
  const [status, setStatus] = useState<"loading" | "anonymous" | "authenticated">(
    "loading",
  );

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
        // Si no existe una sesión disponible, se muestra la pantalla de acceso.
      }

      setStatus("anonymous");
    }

    void loadSession();
  }, []);

    function loginWithMicrosoft() {
      const returnUrl = encodeURIComponent(window.location.origin);
      window.location.href = `${apiUrl}/api/auth/login?returnUrl=${returnUrl}`;
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
      <main className="gaia-app-page min-h-screen bg-[linear-gradient(145deg,#eef4e9_0%,#f7f3e8_48%,#e9f1ec_100%)] text-[#193522]">
        <AppHeader title="Plataforma empresarial" user={user} />
        <div className="mx-auto max-w-6xl px-5 py-8 lg:px-8">
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

          <section className="mt-8 grid gap-4 md:grid-cols-3">
            {[
              { title: "Estructura organizacional", description: "Sedes, áreas, equipos y cargos", number: "01", href: "/organizacion", tone: "organization", motif: "Jerarquía · Equipos", icon: "/brand/icons/organization.png" },
              { title: "Talento Humano", description: "Colaboradores y sus datos de contacto", number: "02", href: "/talento-humano/colaboradores", tone: "third-parties", motif: "Personas · Colaboradores", icon: "/brand/icons/third-parties.png" },
              { title: "Inventario", description: "Elementos, asignaciones y movimientos", number: "03", href: "/inventario", tone: "inventory", motif: "Activos · Custodia", icon: "/brand/icons/inventory.png" },
            ].map(({ title, description, number, href, tone, motif, icon }) => (
              <a
                className={`gaia-module-card gaia-module-${tone}`}
                href={href}
                key={number}
              >
                <div className="gaia-module-card-head"><span><Image alt="" height={42} src={icon} width={42} /></span><small>Módulo {number}</small></div>
                <p className="gaia-module-motif">{motif}</p><h3>{title}</h3><p className="gaia-module-description">{description}</p>
                <span className="gaia-module-action">Abrir módulo <ArrowRight size={16} /></span>
              </a>
            ))}
          </section>
        </div>
      </main>
    );
  }

  return (
    <main className="grid min-h-screen bg-[#edf2ea] lg:grid-cols-[1.08fr_0.92fr]">
      <section className="gaia-login-hero relative hidden overflow-hidden bg-[#193f2c] p-14 text-white lg:flex lg:flex-col lg:justify-between">
        <div aria-hidden="true" className="gaia-login-symbols">
          <span className="symbol-maloca"><Image alt="" height={150} src="/brand/icons/login-maloca.png" width={150} /></span>
          <span className="symbol-jaguar"><Image alt="" height={118} src="/brand/icons/login-jaguar.png" width={118} /></span>
          <span className="symbol-yarumo"><Image alt="" height={130} src="/brand/icons/login-yarumo.png" width={130} /></span>
          <span className="symbol-boa"><Image alt="" height={112} src="/brand/icons/login-boa.png" width={112} /></span>
          <span className="symbol-canangucho"><Image alt="" height={105} src="/brand/icons/login-canangucho.png" width={105} /></span>
        </div>
        <div className="gaia-login-brand">
          <Image alt="Gaia Amazonas" height={38} priority src="/brand/logo-gaia.svg" width={70} />
          <p className="text-sm font-bold uppercase tracking-[0.3em]">Fundación Gaia Amazonas</p>
        </div>
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
        <div className="gaia-login-signature"><span /><p>Gaia Enterprise Platform</p><small>Amazonía · conocimiento vivo</small></div>
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

          <div className="space-y-5">
            {logoutSuccess && (
              <p
                className="rounded-xl bg-[#eaf4e6] px-4 py-3 text-sm text-[#345a3c]"
                role="status"
              >
                Sesión cerrada correctamente.
              </p>
            )}
            <button
              className="flex w-full items-center justify-center gap-3 rounded-xl bg-[#294f35] px-5 py-3.5 font-semibold text-white transition hover:bg-[#193f2c]"
              onClick={loginWithMicrosoft}
              type="button"
            >
              <span className="grid grid-cols-2 gap-0.5" aria-hidden="true">
                <span className="size-2.5 bg-[#f35325]" />
                <span className="size-2.5 bg-[#81bc06]" />
                <span className="size-2.5 bg-[#05a6f0]" />
                <span className="size-2.5 bg-[#ffba08]" />
              </span>
              Continuar con Microsoft
            </button>
          </div>

          <p className="mt-7 text-center text-xs leading-5 text-[#819087]">
            Acceso exclusivo para personal autorizado. Los intentos de acceso quedan
            registrados por seguridad.
          </p>
        </div>
      </section>
    </main>
  );
}
