export type IntranetApplication = {
  code: string; name: string; description: string; category: string; href: string;
  permission: string; external: boolean; initials: string;
  tone: "green" | "blue" | "purple" | "coral";
};

export const intranetApplications: readonly IntranetApplication[] = [{
  code: "ADMINCORE", name: "AdminCore",
  description: "Administración de organización, talento humano, inventario y seguridad.",
  category: "Administración", href: "/admincore", permission: "INT.APP.ADMINCORE.VER",
  external: true, initials: "AC", tone: "green",
}] as const;

export function authorizedApplications(applications: readonly IntranetApplication[], can: (permission: string) => boolean) {
  return applications.filter(application => can(application.permission));
}

export function filterApplications(applications: readonly IntranetApplication[], search: string, category: string) {
  const query = search.trim().toLocaleLowerCase("es");
  return applications.filter(application =>
    (category === "Todas" || application.category === category) &&
    (!query || `${application.name} ${application.description} ${application.category}`.toLocaleLowerCase("es").includes(query)));
}
