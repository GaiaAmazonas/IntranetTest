"use client";

import { Suspense, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { AccessState } from "@/components/route-access-gate";
import { useSecurity } from "@/components/security-context";
import { startLogin } from "@/lib/api-client";

export default function Home() {
  return <Suspense fallback={null}><HomeContent /></Suspense>;
}

function HomeContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const security = useSecurity();
  const logoutSuccess = searchParams.get("logout") === "success";

  useEffect(() => {
    if (!security.loading && security.user) router.replace("/intranet");
  }, [router, security.loading, security.user]);

  if (security.loading || security.user) return <AccessState icon="loading" title={security.user ? "Abriendo la Intranet Gaia…" : "Verificando tu acceso…"} />;

  return <AccessState action="Iniciar sesión" description="Ingresa con tu cuenta institucional para continuar." icon="login" notice={logoutSuccess ? "Sesión cerrada correctamente." : undefined} onAction={() => startLogin(`${window.location.origin}/intranet`)} title="" />;
}
