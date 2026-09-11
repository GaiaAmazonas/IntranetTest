# Módulo Helpdesk

Revisado: 2026-09-07.

## Estado implementado

Helpdesk funciona como módulo del monolito modular: autoservicio en `/intranet/helpdesk`, gestión en `/helpdesk/solicitudes` y configuración en `/helpdesk/catalogos`. Dataverse conserva servicios, formularios versionados, solicitudes, respuestas tipadas, conversación, estados, SLA, calificación, adjuntos e historial. SharePoint conserva únicamente los archivos mediante el puerto neutral de almacenamiento.

El portal presenta Helpdesk como Centro de servicios con un hero botánico propio, búsqueda sobre las solicitudes reales y una bandeja personal escalable. Las solicitudes se organizan en pestañas por estado, filtros por servicio, orden temporal, tabla en escritorio, tarjetas en móvil y paginación de diez registros. El formulario se abre desde el CTA principal como diálogo y marca los campos obligatorios con un asterisco discreto.

## Flujo

`React/Next.js → API Helpdesk → aplicación y reglas → adaptadores Dataverse/SharePoint`.

El solicitante selecciona un servicio, recibe su formulario publicado, radica y consulta la trazabilidad. El equipo autorizado usa la bandeja global, filtra, reasigna, comenta y ejecuta transiciones. La administración crea servicios, borradores, campos y opciones; al publicar se retira la versión anterior y el servicio apunta a la nueva.

La solicitud se crea directamente en estado **Radicada**; el estado Creada no forma parte del flujo actual de Intranet. La ventana confirma antes de radicar, bloquea acciones durante el guardado, se cierra al terminar y actualiza el listado sin recarga manual. El formulario no aparece hasta seleccionar un servicio y al deseleccionarlo se descartan sus respuestas.

Los campos dinámicos muestran su etiqueta funcional, texto de ayuda, marcador de obligatoriedad y opciones comprensibles. Asunto y Descripción se persisten en la propia solicitud. En AdminCore, la observación de una transición se registra también como comentario de la conversación; si existen transiciones no se presenta un segundo formulario de comentario que duplique el flujo. Desde Radicada puede devolverse directamente y el solicitante puede responder en estados Devuelta o En espera del solicitante.

## Adjuntos

- Los metadatos y relaciones viven en Dataverse; el contenido binario vive en la biblioteca SharePoint configurada.
- Las cargas múltiples se procesan secuencialmente para respetar límites y facilitar resultados parciales.
- El almacenamiento reintenta conflictos transitorios de versión y la escritura de historial también reintenta fallos transitorios antes de compensar.
- Una radicación puede existir aunque falle un adjunto; el portal informa el resultado parcial y el gestor ve los adjuntos confirmados.
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

- Entrada de Intranet: `INT.HELPDESK.VER`.
- Bandeja: `HD.SOLICITUDES.VER`.
- Reasignación: `HD.SOLICITUDES.REASIGNAR`.
- Configuración: `HD.CATALOGOS.VER` y `HD.CATALOGOS.ADMINISTRAR`.
- Toda operación se valida también en API. Los permisos deben materializarse mediante el bootstrap de Seguridad y asignarse a roles autorizados; no se conceden automáticamente a usuarios.

## Responsabilidades principales

- `src/Modules/Helpdesk/Gaia.Modules.Helpdesk`: contratos, reglas, endpoints y aplicaciones.
- `src/Gaia.Api/Infrastructure/Dataverse/Helpdesk`: consultas y persistencia física.
- `src/BuildingBlocks/Gaia.BuildingBlocks/Files` y `src/Gaia.Api/Infrastructure/Files`: almacenamiento documental reusable.
- `apps/web/src/features/intranet/intranet-helpdesk.tsx`: portal del solicitante.
- `apps/web/src/features/helpdesk`: bandeja y administración.
- `model/gaia-helpdesk-model.json`: contrato canónico de Dataverse.

## Verificación

Backend: 241 pruebas aprobadas. Frontend: 41 pruebas aprobadas y una omitida intencionalmente. TypeScript, ESLint y compilación de producción Next.js aprobados.

Última validación local: ESLint sin hallazgos, 41 pruebas frontend superadas (1 omitida), build Next.js correcto y 241 pruebas .NET superadas. Estas cifras deben actualizarse cuando cambie el conjunto de pruebas.

## Activación de entorno

Un administrador funcional debe ejecutar el bootstrap controlado de Seguridad, asignar los nuevos permisos a roles y garantizar privilegios Dataverse de mínimo alcance sobre las tablas Helpdesk. Esta activación externa no modifica el código ni requiere secretos en el repositorio.
