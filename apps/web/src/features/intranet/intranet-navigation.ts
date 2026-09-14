import {
  CalendarDays,
  Grid2X2,
  GraduationCap,
  Home,
  LifeBuoy,
  Users,
  type LucideIcon,
} from "lucide-react";

export type IntranetNavigationItem = {
  href: string;
  label: string;
  icon: LucideIcon;
  permission: string;
  exact?: boolean;
  group?: "services";
};

export const intranetNavigation: IntranetNavigationItem[] = [
  { href: "/intranet", label: "Inicio", icon: Home, permission: "INT.INICIO.VER", exact: true },
  { href: "/intranet/personas", label: "Personas", icon: Users, permission: "INT.PERSONAS.VER" },
  { href: "/intranet/calendario", label: "Calendario", icon: CalendarDays, permission: "INT.CALENDARIO.VER" },
  { href: "/intranet/aplicaciones", label: "Mis aplicaciones", group: "services", icon: Grid2X2, permission: "INT.APLICACIONES.VER" },
  { href: "/intranet/helpdesk", label: "Mis solicitudes de ayuda", group: "services", icon: LifeBuoy, permission: "INT.HELPDESK.VER" },
  { href: "/intranet/capacitaciones", label: "Mis capacitaciones", group: "services", icon: GraduationCap, permission: "INT.CAPACITACIONES.VER" },
];

export function isIntranetRouteActive(
  pathname: string,
  item: Pick<IntranetNavigationItem, "href" | "exact">,
) {
  return item.exact ? pathname === item.href : pathname.startsWith(item.href);
}
