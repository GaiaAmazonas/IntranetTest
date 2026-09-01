export type IntranetApplication = {
  code: string; name: string; description: string; category: string; href: string;
  permission?: string; external: boolean; initials: string; logoUrl?: string;
  tone: "green" | "blue" | "purple" | "coral";
};

export const intranetApplications: readonly IntranetApplication[] = [{
  code: "MICROSOFT_TEAMS", name: "Microsoft Teams",
  description: "Reuniones, conversaciones y colaboración institucional.",
  category: "Microsoft 365", href: "msteams://", external: false,
  initials: "T", logoUrl: "/applications/microsoft-teams.svg", tone: "purple",
}, {
  code: "MICROSOFT_OUTLOOK", name: "Microsoft Outlook",
  description: "Correo y calendario institucional de Microsoft 365.",
  category: "Microsoft 365", href: "mailto:", external: false,
  initials: "O", logoUrl: "/applications/microsoft-outlook.svg", tone: "blue",
}, {
  code: "GOOGLE_DRIVE", name: "Drive Gaia",
  description: "Acceso directo a tu unidad personal de Google Drive.",
  category: "Productividad", href: "https://drive.google.com/drive/my-drive", external: true,
  initials: "D", logoUrl: "/applications/google-drive.svg", tone: "green",
}] as const;

type ApplicationModule = { code: string; name: string; description?: string | null; route: string; icon?: string | null; order: number };
const tones: IntranetApplication["tone"][] = ["green", "blue", "purple", "coral"];

export function applicationsFromModules(modules: readonly ApplicationModule[]): IntranetApplication[] {
  return modules
    .filter(module => module.code.toUpperCase().startsWith("INT.APP.") && Boolean(module.route?.trim()))
    .sort((left, right) => left.order - right.order || left.name.localeCompare(right.name, "es"))
    .map((module, index) => ({
      code: module.code, name: module.name,
      description: module.description?.trim() || "Aplicación institucional autorizada.",
      category: "Aplicaciones institucionales", href: module.route.trim(),
      external: /^https?:\/\//i.test(module.route),
      initials: module.name.split(/\s+/).filter(Boolean).slice(0, 2).map(part => part[0]).join("").toUpperCase(),
      logoUrl: temporaryApplicationLogo(module.code, module.name),
      tone: tones[index % tones.length],
    }));
}

function temporaryApplicationLogo(code: string, name: string) {
  const key = `${code} ${name}`.toLocaleLowerCase("es");
  if (key.includes("admincore")) return "/applications/admincore-temporary.svg";
  if (key.includes("plan view") || key.includes("planview")) return "/applications/planview-temporary.svg";
  return undefined;
}

export function authorizedApplications(applications: readonly IntranetApplication[], can: (permission: string) => boolean) {
  return applications.filter(application => !application.permission || can(application.permission));
}

export function filterApplications(applications: readonly IntranetApplication[], search: string, category: string) {
  const query = search.trim().toLocaleLowerCase("es");
  return applications.filter(application =>
    (category === "Todas" || application.category === category) &&
    (!query || `${application.name} ${application.description} ${application.category}`.toLocaleLowerCase("es").includes(query)));
}
