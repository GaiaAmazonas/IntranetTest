"use client";

import Link from "@/components/document-link";
import { useSecurity } from "@/components/security-context";
import { intranetNavigation } from "./intranet-navigation";

export function IntranetFooter() {
  const security = useSecurity();
  return (
    <footer className="intranet-footer">
      <strong>Gaia Amazonas · Intranet institucional</strong>
      <nav aria-label="Enlaces de la Intranet">
        {intranetNavigation.filter(item => security.can(item.permission)).map(item => (
          <Link href={item.href} key={item.href}>{item.label}</Link>
        ))}
      </nav>
      <span>
        <a href="https://gaiaamazonas.org/politica-de-datos/" rel="noopener noreferrer" target="_blank">Política de tratamiento de datos</a>
        <Link href="/intranet/helpdesk">Ayuda técnica</Link>
        <small>© Fundación Gaia Amazonas</small>
      </span>
    </footer>
  );
}
