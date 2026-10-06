# Navegación de Intranet y AdminCore

## Separación de experiencias

La Intranet y AdminCore tienen navegaciones independientes porque atienden propósitos distintos:

- La Intranet usa `apps/web/src/features/intranet/intranet-shell.tsx` y construye sus accesos visibles desde el catálogo de módulos autorizado en Dataverse.
- AdminCore usa `apps/web/src/components/app-shell.tsx` y construye su estructura, grupos, rutas y nombres desde el catálogo de módulos autorizado en Dataverse.

No debe reutilizarse el menú de AdminCore dentro de la Intranet ni el menú de la Intranet dentro de AdminCore. El vínculo entre ambos es la aplicación autorizada `INT.APP.ADMINCORE`.

## Configuración

El grupo **Configuración** de AdminCore (Login institucional y Ambientación visual) también se obtiene desde Dataverse. Agregar una nueva página requiere crear el módulo, asociar sus permisos y configurar nombre, ruta, icono, orden y visibilidad.

## Capacitaciones

El menú de AdminCore presenta **Capacitaciones** como un único acceso al catálogo. Contenido, evaluación, participantes, revisión, seguimiento y resultados son pasos contextuales de una capacitación seleccionada y no páginas globales independientes.
