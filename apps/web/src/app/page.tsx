"use client";

import { Suspense, useEffect } from "react";
import { useSearchParams } from "next/navigation";
import { AccessState } from "@/components/route-access-gate";
import { useSecurity } from "@/components/security-context";
import { startLogin } from "@/lib/api-client";

export default function Home() {
  return <Suspense fallback={null}><HomeContent /></Suspense>;
}

function HomeContent() {
  const searchParams = useSearchParams();
  const security = useSecurity();
  const logoutReason = searchParams.get("logout");
  const logoutNotice = logoutReason === "inactivity"
    ? "Cerramos tu sesión después de 40 minutos sin actividad para proteger tu cuenta."
    : logoutReason === "success" ? "Sesión cerrada correctamente." : undefined;

  useEffect(() => {
    if (!security.loading && security.user) window.location.replace("/intranet/");
  }, [security.loading, security.user]);

  if (security.user) return <AccessState icon="loading" title="Abriendo la Intranet Gaia…" />;

  return <AccessState action={security.loading ? undefined : "Iniciar sesión"} description={security.loading ? "Estamos comprobando tu sesión mientras preparamos el acceso institucional." : "Ingresa con tu cuenta institucional para continuar."} icon="login" notice={logoutNotice} onAction={security.loading ? undefined : () => startLogin(`${window.location.origin}/intranet`)} title="" />;
}
