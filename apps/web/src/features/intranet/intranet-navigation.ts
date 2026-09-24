import {
  CalendarDays,
  CircleHelp,
  Grid2X2,
  GraduationCap,
  Home,
  LayoutDashboard,
  LifeBuoy,
  Search,
  Users,
  type LucideIcon,
} from "lucide-react";
import type { SecurityNavigationModule } from "@/components/security-context";

export type IntranetNavigationItem = {
  id: string;
  code: string;
  href: string;
  label: string;
  description: string;
  icon: LucideIcon;
  exact?: boolean;
  group?: "services";
  order: number;
};

const icons: Record<string, LucideIcon> = {
  applications: Grid2X2, calendar: CalendarDays, helpdesk: LifeBuoy, home: Home,
  intranet: LayoutDashboard, people: Users, search: Search, training: GraduationCap,
};
const primaryCodes = new Set(["INT.INICIO", "INT.PERSONAS", "INT.CALENDARIO"]);

export function intranetNavigationFromModules(modules: readonly SecurityNavigationModule[]): IntranetNavigationItem[] {
  return modules
    .filter(module => {
      const code = module.code.trim().toUpperCase();
      return code.startsWith("INT.") && !code.startsWith("INT.APP.") && Boolean(module.route?.trim());
    })
    .map(module => {
      const code = module.code.trim().toUpperCase();
      const href = module.route.trim().replace(/\/$/, "") || "/intranet";
      return {
        id: module.id, code, href, label: module.name.trim(),
        description: module.description?.trim() || "Acceso institucional autorizado.",
        icon: icons[module.icon?.trim().toLowerCase() || ""] ?? CircleHelp,
        exact: href === "/intranet",
        group: primaryCodes.has(code) ? undefined : "services" as const,
        order: module.order,
      };
    })
    .sort((left, right) => left.order - right.order || left.label.localeCompare(right.label, "es"));
}

export function isIntranetRouteActive(
  pathname: string,
  item: Pick<IntranetNavigationItem, "href" | "exact">,
) {
  return item.exact ? pathname === item.href : pathname.startsWith(item.href);
}
