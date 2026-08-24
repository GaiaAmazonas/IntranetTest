import type { Metadata } from "next";
import { IntranetShell } from "@/features/intranet/intranet-shell";
import "@/features/intranet/intranet.css";

export const metadata: Metadata = {
  title: "Intranet Gaia | Fundación Gaia Amazonas",
  description: "Espacio institucional para colaboradores de la Fundación Gaia Amazonas.",
};

export default function IntranetLayout({ children }: { children: React.ReactNode }) {
  return <IntranetShell>{children}</IntranetShell>;
}
