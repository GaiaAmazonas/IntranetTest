export type RouteAccessRule = {
  prefix: string;
  exact?: boolean;
  requirements: string[];
};

export const routeAccessRules: RouteAccessRule[] = [
  { prefix: "/intranet/personas", requirements: ["INTRANET.VER", "INT.PERSONAS.VER"] },
  { prefix: "/intranet/calendario", requirements: ["INTRANET.VER", "INT.CALENDARIO.VER"] },
  { prefix: "/intranet/aplicaciones", requirements: ["INTRANET.VER", "INT.APLICACIONES.VER"] },
  { prefix: "/intranet/helpdesk", requirements: ["INTRANET.VER", "INT.HELPDESK.VER"] },
  { prefix: "/intranet/capacitaciones", requirements: ["INTRANET.VER", "INT.CAPACITACIONES.VER"] },
  { prefix: "/intranet/perfil", requirements: ["INTRANET.VER"] },
  { prefix: "/intranet", requirements: ["INTRANET.VER", "INT.INICIO.VER"], exact: true },
  { prefix: "/admincore", requirements: ["INT.APP.ADMINCORE.VER", "INICIO.VER"] },
  { prefix: "/helpdesk/solicitudes", requirements: ["INT.APP.ADMINCORE.VER", "HD.SOLICITUDES.VER"] },
  { prefix: "/helpdesk/servicios-y-flujos", requirements: ["INT.APP.ADMINCORE.VER", "HD.CATALOGOS.VER"] },
  { prefix: "/helpdesk/catalogos", requirements: ["INT.APP.ADMINCORE.VER", "HD.CATALOGOS.VER"] },
  { prefix: "/capacitaciones/catalogo", requirements: ["INT.APP.ADMINCORE.VER", "CAP.CATALOGO.VER"] },
  { prefix: "/capacitaciones/contenido", requirements: ["INT.APP.ADMINCORE.VER", "CAP.CONTENIDO.VER"] },
  { prefix: "/capacitaciones/audiencias", requirements: ["INT.APP.ADMINCORE.VER", "CAP.AUDIENCIAS.VER"] },
  { prefix: "/capacitaciones/seguimiento", requirements: ["INT.APP.ADMINCORE.VER", "CAP.SEGUIMIENTO.VER"] },
  { prefix: "/capacitaciones/resultados", requirements: ["INT.APP.ADMINCORE.VER", "CAP.RESULTADOS.VER"] },
  { prefix: "/organizacion", requirements: ["INT.APP.ADMINCORE.VER", "ORG.ORGANIGRAMA.VER|ORG.UNIDADES.VER|ORG.ASIGNACIONES.VER|ORG.CARGOS.VER|ORG.SEDES_TIPOS.VER"] },
  { prefix: "/talento-humano/vinculaciones", requirements: ["INT.APP.ADMINCORE.VER", "TH.VINCULACIONES.VER"] },
  { prefix: "/talento-humano", requirements: ["INT.APP.ADMINCORE.VER", "TH.COLABORADORES.VER"] },
  { prefix: "/terceros", requirements: ["INT.APP.ADMINCORE.VER", "TH.COLABORADORES.VER"] },
  { prefix: "/inventario", requirements: ["INT.APP.ADMINCORE.VER", "INV.VER"] },
  { prefix: "/comunicaciones/eventos", requirements: ["INT.APP.ADMINCORE.VER", "COM.EVENTOS.VER"] },
  { prefix: "/comunicaciones/tipos-evento", requirements: ["INT.APP.ADMINCORE.VER", "COM.TIPOS_EVENTO.VER"] },
  { prefix: "/comunicaciones/destacados", requirements: ["INT.APP.ADMINCORE.VER", "COM.DESTACADOS.VER"] },
  { prefix: "/configuracion/login", requirements: ["INT.APP.ADMINCORE.VER", "COM.DESTACADOS.VER"] },
  { prefix: "/configuracion/ambientacion", requirements: ["INT.APP.ADMINCORE.VER", "COM.DESTACADOS.VER"] },
  { prefix: "/seguridad/usuarios", requirements: ["INT.APP.ADMINCORE.VER", "TI.USUARIOS.VER"] },
  { prefix: "/seguridad/roles", requirements: ["INT.APP.ADMINCORE.VER", "TI.ROLES.VER"] },
  { prefix: "/seguridad/modulos", requirements: ["INT.APP.ADMINCORE.VER|TI.MODULOS.ADMINISTRAR", "TI.MODULOS.VER"] },
];

export function routeRuleFor(pathname: string) {
  const normalizedPathname = pathname.length > 1
    ? pathname.replace(/\/+$/, "")
    : pathname;

  return routeAccessRules
    .filter(rule => rule.exact
      ? normalizedPathname === rule.prefix
      : normalizedPathname === rule.prefix || normalizedPathname.startsWith(`${rule.prefix}/`))
    .sort((first, second) => second.prefix.length - first.prefix.length)[0] ?? null;
}
