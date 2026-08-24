import {
  ArrowRight,
  BookOpenCheck,
  CircleCheck,
  Clock3,
  FileQuestion,
  Headphones,
  Laptop,
  MessageSquareText,
  Plus,
  Search,
  ShieldCheck,
  UsersRound,
} from "lucide-react";

const categories = [
  { title: "Tecnología", detail: "Equipos, accesos y soporte", icon: Laptop, tone: "blue" },
  { title: "Talento Humano", detail: "Certificados y novedades", icon: UsersRound, tone: "purple" },
  { title: "Administrativo", detail: "Compras, viajes y servicios", icon: FileQuestion, tone: "coral" },
  { title: "Orientación", detail: "Consulta guías y respuestas", icon: BookOpenCheck, tone: "teal" },
] as const;

export function IntranetHelpdesk() {
  return (
    <section className="intranet-helpdesk">
      <header className="helpdesk-hero">
        <div>
          <p>Centro de ayuda</p>
          <h1>¿Cómo podemos ayudarte?</h1>
          <span>Encuentra respuestas o inicia una solicitud con el equipo adecuado.</span>
          <label>
            <Search aria-hidden="true" size={19} />
            <input aria-label="Buscar en el centro de ayuda" placeholder="Buscar guías, servicios o temas de ayuda" type="search" />
          </label>
        </div>
        <div aria-hidden="true" className="helpdesk-hero-art"><Headphones size={46} /><span>HELP<br /><strong>DESK</strong></span></div>
      </header>

      <div className="helpdesk-overview">
        <article><i className="tone-blue"><Clock3 size={18} /></i><span><strong>0</strong><small>Solicitudes abiertas</small></span></article>
        <article><i className="tone-purple"><MessageSquareText size={18} /></i><span><strong>0</strong><small>En seguimiento</small></span></article>
        <article><i className="tone-green"><CircleCheck size={18} /></i><span><strong>0</strong><small>Resueltas recientemente</small></span></article>
        <details>
          <summary><Plus size={18} /> Nueva solicitud</summary>
          <div className="helpdesk-temporary-form">
            <span><ShieldCheck size={16} /> Prototipo visual temporal</span>
            <label>Tipo de solicitud<select defaultValue=""><option disabled value="">Selecciona una categoría</option><option>Tecnología</option><option>Talento Humano</option><option>Administrativo</option></select></label>
            <label>Asunto<input placeholder="Describe brevemente tu necesidad" /></label>
            <label>Descripción<textarea placeholder="Incluye la información necesaria para ayudarte" rows={4} /></label>
            <button disabled type="button">Enviar cuando se habilite el servicio</button>
          </div>
        </details>
      </div>

      <div className="helpdesk-layout">
        <div>
          <header className="helpdesk-section-heading"><span><p>Servicios</p><h2>¿Sobre qué necesitas ayuda?</h2></span></header>
          <div className="helpdesk-categories">
            {categories.map(({ detail, icon: Icon, title, tone }) => (
              <article key={title}><i className={`tone-${tone}`}><Icon size={21} /></i><span><strong>{title}</strong><small>{detail}</small></span><ArrowRight size={16} /></article>
            ))}
          </div>
        </div>
        <aside>
          <header className="helpdesk-section-heading"><span><p>Actividad</p><h2>Mis solicitudes</h2></span><button disabled type="button">Ver todas</button></header>
          <div className="helpdesk-empty"><MessageSquareText size={28} /><strong>Aún no tienes solicitudes</strong><p>Cuando el servicio esté conectado, podrás consultar aquí su estado y trazabilidad.</p></div>
        </aside>
      </div>
    </section>
  );
}
