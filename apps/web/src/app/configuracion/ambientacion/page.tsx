import { AppHeader } from "@/components/app-header";
import { VisualAmbienceAdmin } from "@/features/communications/visual-ambience-admin";

export default function Page() {
  return <main className="gaia-app-page"><AppHeader title="Configuración · Ambientación visual"/><VisualAmbienceAdmin/></main>;
}
