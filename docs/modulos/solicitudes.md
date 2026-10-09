# Solicitudes

> Estado: **implementado; no declarado listo para producción** · Verificado: **2026-10-09**
> Código principal: `Gaia.Modules.Solicitudes`, adaptadores `Infrastructure/Dataverse/Solicitudes`, infraestructura de archivos y features de Solicitudes.

## Superficies

- Intranet: `/intranet/solicitudes`, catálogo, radicación, bandeja personal, expediente y respuesta.
- AdminCore: `/solicitudes`, bandejas y gestión.
- Configuración: `/solicitudes/servicios-y-flujos` y rutas relacionadas.
- API: `/api/solicitudes`.

## Configuración

Un servicio pertenece a una unidad y define visibilidad, vigencia, plazo y responsable predeterminado. Los formularios de radicación y flujos son versionados. Publicar fija una versión; editar requiere borrador o nueva versión según su estado.

El diseñador de flujo soporta:

- etapas iniciales, finales y de reapertura;
- asignación por unidad, persona o responsable de la solicitud;
- activación por cualquier conexión o por todas;
- ramas lineales, paralelas y convergencia;
- rutas por completado, aprobado, rechazado, devuelto, requiere aprobación o no aplica;
- observación, devolución, archivo y decisión;
- formularios dinámicos propios por etapa.

Los códigos técnicos de servicios, campos y opciones se generan internamente. El orden se deriva de la posición configurada, no se exige al usuario como dato manual.

## Radicación

El solicitante selecciona un servicio visible, recibe su formulario publicado, confirma y radica. La solicitud nace en estado Radicada, conserva la versión publicada y congela los días hábiles aplicables. Cambios futuros del servicio no recalculan solicitudes existentes.

Los formularios dinámicos muestran etiqueta, ayuda, ejemplo, tipo, presentación, obligatoriedad y visibilidad. Los adjuntos configurables controlan el mismo cargador existente; no existe un sistema paralelo.

## Ejecución del flujo

```mermaid
stateDiagram-v2
    [*] --> Radicada
    Radicada --> EnGestion: activar etapas iniciales
    EnGestion --> EnEspera: pedir información
    EnEspera --> EnGestion: respuesta del solicitante
    EnGestion --> EnGestion: activar siguiente/ramas
    EnGestion --> Cerrada: completar etapa final
    Cerrada --> EnGestion: reapertura autorizada
```

Al radicar se crea la instancia del flujo y las gestiones iniciales. Completar una gestión activa únicamente rutas compatibles. Una convergencia “todas” espera todas las entradas; “cualquiera” activa con la primera compatible. La etapa final cierra cuando no queda trabajo pendiente y conserva el resumen de solución.

Una reejecución crea otra gestión y no sobrescribe respuestas anteriores. Pedir información relaciona comentario y gestión; la respuesta del solicitante reanuda esa misma gestión. Reabrir conserva versión e historia y crea la ejecución prevista.

## Formularios de etapa

Administración:

- `GET/PUT /administration/workflow-steps/{stepId}/form`;
- `POST/PUT/DELETE .../form/fields`.

Ejecución:

- `GET /management/workflows/{managementId}/form`;
- `PUT /management/workflows/{managementId}/form/responses`.

Se guardan respuestas tipadas, después archivos de campo y finalmente el resultado. El servidor rechaza campos ajenos/ocultos, opciones de otro campo, valores inválidos y archivos no asociados a la misma solicitud/gestión. La finalización vuelve a comprobar obligatoriedad y archivos. Los valores de formulario no activan rutas directamente; el motor usa el resultado autorizado de la gestión.

## Bandejas y alcance

La operación usa una sola vista general adaptada por permisos:

- Mis pendientes: asignadas a la persona o disponibles sin responsable en **su propia unidad organizacional**.
- Mi unidad: trabajo de unidades autorizadas.
- Aprobaciones: etapas que requieren decisión.
- Esperando respuesta: gestiones suspendidas por información del solicitante.
- En gestión, cerradas y todas según estado.

La bandeja pagina y filtra en Dataverse mediante `@odata.nextLink`; no usa `$skip`. Búsqueda, servicio, estado, plazo y orden forman la consulta. Las gestiones vencidas y fechas próximas tienen prioridad.

El rol puede tener `gaia_administracionglobal`; esta propiedad amplía alcance organizacional solo junto con el permiso funcional correspondiente. No se deduce del nombre del rol. Los roles departamentales permanecen restringidos a sus unidades.

## Expediente operativo

Una persona autorizada ve:

1. gestión actual: etapa, instrucciones, plazo, requisitos, formulario y decisiones;
2. solicitud: número, servicio, solicitante y respuestas del formulario inicial;
3. recorrido: etapas, unidad, responsable, resultado, observación, fechas, respuestas y archivos;
4. próximos destinos explicados sin códigos técnicos;
5. conversación, notas internas, historial y adjuntos.

Etapas anteriores son de solo lectura. Solo la gestión activa autorizada se modifica. El historial distingue información interna de visible para el solicitante.

## SLA y reasignación

Cada gestión puede tener meta en días hábiles. La fecha objetivo excluye fines de semana y días no laborables activos. Se conservan lectura histórica, transcurrido, restante y vencimiento.

Reasignar una etapa requiere permiso, motivo y pertenencia vigente a la unidad de la etapa. Puede asignarse a una persona o volver a la bandeja de unidad sin cambiar el responsable general. El backend valida la pertenencia y registra la trazabilidad.

## Adjuntos

- metadatos y asociaciones en Dataverse; binario en SharePoint;
- cargas múltiples secuenciales;
- una radicación puede quedar creada si un adjunto falla, informando resultado parcial;
- carga de gestión autorizada al responsable o miembro habilitado de la unidad;
- inactivación lógica y eliminación física solo mediante política explícita;
- conciliación reporta faltantes/huérfanos sin borrarlos automáticamente;
- historial registra actor, origen, visibilidad, fecha e ID de operación.

## Seguridad

- portal: `INT.SOLICITUDES.VER`;
- bandeja: `HD.SOLICITUDES.VER`;
- reasignación: `HD.SOLICITUDES.REASIGNAR`;
- catálogos: `HD.CATALOGOS.VER` y `HD.CATALOGOS.ADMINISTRAR`;
- eliminación de datos de prueba: permiso específico.

El portal no concede administración. Toda toma, respuesta, transición, archivo y reasignación se reautoriza en API.

## Integridad y producción

El motor usa IDs de operación, claves alternativas, ETag y ChangeSets para evitar duplicados y confirmar de forma atómica activaciones, devoluciones y cierre. Antes de producción siguen siendo obligatorias pruebas reales de concurrencia/colisión en un Dataverse aislado, verificación visual autenticada y recorridos con identidades de varias unidades.
