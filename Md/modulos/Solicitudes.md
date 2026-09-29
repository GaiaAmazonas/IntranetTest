# Módulo Solicitudes

Revisado: 2026-09-29.

## Estado implementado

Solicitudes funciona como módulo del monolito modular: autoservicio en `/intranet/solicitudes`, gestión general en `/solicitudes` y configuración en `/solicitudes/catalogos`. La API se publica bajo `/api/solicitudes`. Dataverse conserva servicios, formularios versionados, solicitudes, respuestas tipadas, conversación, estados, SLA, calificación, adjuntos e historial. SharePoint conserva únicamente los archivos mediante el puerto neutral de almacenamiento.

El portal presenta Solicitudes como Centro de servicios con un hero botánico propio, búsqueda sobre las solicitudes reales y una bandeja personal escalable. Las solicitudes se organizan en pestañas por estado, filtros por servicio, orden temporal, tabla en escritorio, tarjetas en móvil y paginación de diez registros. El formulario se abre desde el CTA principal como diálogo y marca los campos obligatorios con un asterisco discreto.

## Flujo

`React/Next.js → API Solicitudes → aplicación y reglas → adaptadores Dataverse/SharePoint`.

El solicitante selecciona un servicio, recibe su formulario publicado, radica y consulta la trazabilidad. El equipo autorizado usa la bandeja global, filtra, reasigna, comenta y ejecuta transiciones. La administración crea servicios, borradores, campos y opciones; al publicar se retira la versión anterior y el servicio apunta a la nueva.

La administración utiliza una sola vista general, no una pantalla por rol ni por área. Cada etapa determina la unidad que debe atenderla y sus usuarios autorizados pueden tomar la gestión disponible. La etapa activa resalta claramente el área responsable; cuando alguien la toma, la bandeja muestra **Responsable en gestión**. El expediente conserva las personas que atendieron las etapas anteriores para consultar el recorrido completo sin confundirlas con quien tiene el turno actual.

La solicitud se crea directamente en estado **Radicada**; el estado Creada no forma parte del flujo actual de Intranet. La ventana confirma antes de radicar, bloquea acciones durante el guardado, se cierra al terminar y actualiza el listado sin recarga manual. El formulario no aparece hasta seleccionar un servicio y al deseleccionarlo se descartan sus respuestas.

Los campos dinámicos muestran su etiqueta funcional, texto de ayuda, marcador de obligatoriedad y opciones comprensibles. Asunto y Descripción se persisten en la propia solicitud. En AdminCore, la observación de una transición se registra también como comentario de la conversación; si existen transiciones no se presenta un segundo formulario de comentario que duplique el flujo. Desde Radicada puede devolverse directamente y el solicitante puede responder en estados Devuelta o En espera del solicitante.

Cuando el solicitante atiende una observación, la gestión anterior vuelve a quedar operable para el responsable correspondiente, con sus decisiones de aprobar, rechazar o devolver. Al avanzar a otra unidad no se conserva artificialmente la toma de la etapa anterior: la nueva etapa debe ser tomada por una persona autorizada de su unidad. En el último paso, si no existe una transición explícita de resolución, el backend admite como respaldo una única transición activa hacia `RESUELTA`; si hay cero o más de una, informa la inconsistencia de configuración.

Los detalles se presentan como diálogos superpuestos sobre la aplicación y no como una segunda página visual. En Intranet solo se abre un diálogo de acción a la vez. Al completar una etapa, el diálogo permanece en estado de procesamiento hasta actualizar el expediente, evitando que parezca que la operación no se ejecutó.

## Adjuntos

- Los metadatos y relaciones viven en Dataverse; el contenido binario vive en la biblioteca SharePoint configurada.
- Las cargas múltiples se procesan secuencialmente para respetar límites y facilitar resultados parciales.
- El almacenamiento reintenta conflictos transitorios de versión y la escritura de historial también reintenta fallos transitorios antes de compensar.
- Una radicación puede existir aunque falle un adjunto; el portal informa el resultado parcial y el gestor ve los adjuntos confirmados.
- Una persona puede adjuntar a la gestión activa cuando es su responsable directo o pertenece a la unidad que debe atenderla. La interfaz muestra un estado de carga visible mientras se transfiere el archivo y bloquea acciones incompatibles.
- La configuración activa de Staging usa `Sites.Selected`, acceso `Write` al sitio y una referencia de credencial; nunca documentar el valor del secreto.

## Administración funcional

- Servicios y formularios se gestionan en una sola experiencia maestro-detalle.
- Unidad se selecciona antes del responsable; las unidades se ordenan ascendentemente por código y se presenta el nombre oficial.
- Los códigos técnicos de servicios y campos se generan automáticamente para no exponer convenciones internas al administrador.
- Un formulario publicado se consulta en modo de solo lectura. Las modificaciones deben realizarse en una nueva versión/borrador y publicarse explícitamente.
- Los días hábiles se fijan en la solicitud al radicar; cambiar después el servicio no recalcula solicitudes existentes.
- **Activo** controla la vigencia administrativa; **Visible** controla si el servicio aparece al solicitante.
- La bandeja usa 10 registros por página. El backend filtra, ordena y pagina en Dataverse mediante `@odata.nextLink`, encapsulado en un token protegido; nunca usa `$skip` ni devuelve detalles de Dataverse al navegador.
- La caché vive en memoria mientras la página está montada y conserva varias páginas para cada combinación de búsqueda, servicio, estado, plazo y orden. Volver a una página consultada no genera una nueva solicitud HTTP. Actualizar resultados o realizar una mutación invalida los datos que pueden haber quedado obsoletos.
- Búsqueda y filtros se reflejan en la URL. El total y la última página solo se muestran cuando Dataverse entrega un conteo fiable; de lo contrario se usa `hasNextPage`.

## Seguridad

- Entrada de Intranet: `INT.SOLICITUDES.VER`.
- Bandeja: `HD.SOLICITUDES.VER`.
- Reasignación: `HD.SOLICITUDES.REASIGNAR`.
- Configuración: `HD.CATALOGOS.VER` y `HD.CATALOGOS.ADMINISTRAR`.
- Toda operación se valida también en API. Los permisos deben materializarse mediante el bootstrap de Seguridad y asignarse a roles autorizados; no se conceden automáticamente a usuarios.

## Responsabilidades principales

- `src/Modules/Solicitudes/Gaia.Modules.Solicitudes`: contratos, reglas, endpoints y aplicaciones.
- `src/Gaia.Api/Infrastructure/Dataverse/Solicitudes`: consultas y persistencia física.
- `src/BuildingBlocks/Gaia.BuildingBlocks/Files` y `src/Gaia.Api/Infrastructure/Files`: almacenamiento documental reusable.
- `apps/web/src/features/intranet/intranet-solicitudes.tsx`: portal del solicitante.
- `apps/web/src/features/solicitudes`: bandeja y administración.
- `model/gaia-solicitudes-model.json`: contrato canónico de Dataverse.

## Verificación

Última validación local: **356 pruebas .NET aprobadas**, TypeScript sin errores y ESLint sin hallazgos. Estas cifras deben actualizarse cuando cambie el conjunto de pruebas. En esta revisión no se volvió a ejecutar el conjunto completo de pruebas frontend ni la exportación de producción, por lo que no se declara un resultado nuevo para esos dos pasos.

## Activación de entorno

Un administrador funcional debe ejecutar el bootstrap controlado de Seguridad, asignar los nuevos permisos a roles y garantizar privilegios Dataverse de mínimo alcance sobre las tablas Solicitudes. Esta activación externa no modifica el código ni requiere secretos en el repositorio.
