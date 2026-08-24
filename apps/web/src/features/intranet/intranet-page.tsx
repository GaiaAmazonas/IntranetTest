import { Construction } from "lucide-react";

export function IntranetPage({
  description,
  eyebrow,
  title,
}: {
  description: string;
  eyebrow: string;
  title: string;
}) {
  return (
    <section className="intranet-page-shell">
      <header className="intranet-page-heading">
        <p>{eyebrow}</p>
        <h1>{title}</h1>
        <span>{description}</span>
      </header>
      <div className="intranet-phase-placeholder">
        <Construction aria-hidden="true" size={24} />
        <div>
          <strong>Estructura preparada</strong>
          <p>El contenido y la conexión con datos reales se incorporarán en las fases aprobadas.</p>
        </div>
      </div>
    </section>
  );
}
