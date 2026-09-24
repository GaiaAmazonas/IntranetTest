"use client";

import Link from "next/link";
import type { IntranetNavigationItem } from "./intranet-navigation";

export function IntranetFooter({ navigation }: { navigation: readonly IntranetNavigationItem[] }) {
  return (
    <footer className="intranet-footer">
      <strong>Gaia Amazonas · Intranet institucional</strong>
      <nav aria-label="Enlaces de la Intranet">
        {navigation.map(item => (
          <Link href={item.href} key={item.id}>{item.label}</Link>
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
