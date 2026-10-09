# Auditoría documental, hallazgos y riesgos

> Estado: **auditoría consolidada** · Fecha: **2026-10-09**
> Referencia de código: `2d77af4` más el árbol documental de esta entrega.
> Alcance: inventario de Markdown, solución, manifiestos, composición, módulos, endpoints, frontend, configuración de ejemplo, integraciones y pruebas.

## Resultado de la consolidación

La documentación heredada fue revisada antes de retirarse. Sus reglas vigentes se integraron en:

- arquitectura y convenciones;
- identidad, autorización y alcance organizacional;
- API, Dataverse y almacenamiento SharePoint;
- frontend, navegación y sistema de diseño;
- instalación, despliegue y diagnóstico;
- guía de desarrollo;
- modelo funcional, integridad y migraciones;
- capítulos oficiales de Organización/Terceros, Inventario, Comunicaciones, Capacitaciones y Solicitudes.

Los planes, prompts, actas, informes fechados y documentos duplicados se eliminaron del árbol de trabajo para evitar fuentes competidoras. Su historia continúa disponible en Git. `docs/README.md` es el único índice oficial.

## Material integrado

| Tema heredado | Destino oficial |
|---|---|
| Gobierno, arquitectura y continuidad | `docs/01-arquitectura.md`, ADR y `AGENTS.md` |
| Seguridad, sesiones y permisos | `docs/02-seguridad-e-integraciones.md` |
| SharePoint, Graph, adjuntos y migración | `docs/04-api-datos-archivos.md` |
| Diseño, navegación y responsive | `docs/05-frontend-diseno.md` |
| Montaje de servidor y operación | `docs/06-instalacion-operacion.md` |
| Receta de módulos y pruebas | `docs/07-desarrollo-extension.md` |
| Reglas transversales y calidad de datos | `docs/09-modelo-funcional-datos.md` |
| Relación capacidad–pantalla–API–datos–permisos–pruebas | `docs/10-matriz-trazabilidad.md` |
| Organización, terceros y fotos | `docs/modulos/organizacion-terceros.md` |
| Inventario objetivo | `docs/modulos/inventario.md` |
| Eventos, destacados, login y ambientación | `docs/modulos/comunicaciones.md` |
| Capacitaciones, evaluaciones y participación | `docs/modulos/capacitaciones.md` |
| Solicitudes, flujos, formularios y bandejas | `docs/modulos/solicitudes.md` |

## Hallazgos verificados

1. Gaia es un monolito modular con frontend exportado estáticamente y una API ASP.NET Core.
2. Dataverse Web API v9.2 es la persistencia activa; no existen referencias operativas actuales a EF Core/Npgsql.
3. `appsettings.Development.example.json` conserva una cadena PostgreSQL obsoleta. No se modificó por tratarse de configuración y requerir una tarea separada.
4. Inventario está registrado, pero responde `503`; su documento describe modelo objetivo, no funcionalidad disponible.
5. Algunas operaciones avanzadas de ThirdParties también responden `503` y quedaron identificadas.
6. OpenAPI solo se publica en desarrollo.
7. El frontend no contiene un backend Next alterno.
8. La autorización frontend es experiencia; las políticas de la API son la autoridad.
9. Dataverse usa identidad delegada y SharePoint una identidad técnica independiente.
10. No existe una topología productiva vigente suficientemente verificada para declarar un método definitivo de despliegue.
11. Los totales de pruebas de actas anteriores variaban por fecha; la documentación oficial exige reportar comando/fecha en cada entrega, no un total permanente.
12. Se creó `AGENTS.md` como entrada compacta para futuras sesiones.

## Riesgos y deuda

| Prioridad | Riesgo | Acción recomendada |
|---|---|---|
| Alta | Cadena PostgreSQL obsoleta en el ejemplo | Retirarla en una tarea de configuración y comprobar ambientes consumidores |
| Alta | Despliegue productivo sin definición verificable | Aprobar topología, TLS, secretos, monitoreo, respaldo, recuperación y runbook |
| Alta | Metadatos/permisos reales no auditados en todos los ambientes | Generar inventario seguro desde Dataverse/Entra/SharePoint objetivo |
| Alta | Inventario no operativo | Confirmar modelo físico e implementar adaptador Dataverse sin base paralela |
| Media | Operaciones de perfil en transición | Priorizar por necesidad, datos y permisos; conservar `503` explícito hasta completar |
| Media | Runtime Node/pnpm no fijado formalmente | Añadir política/versiones en una tarea técnica aprobada |
| Media | Caché de tokens en memoria | Diseñar caché distribuida y Data Protection antes de varias instancias |
| Media | Configuración SharePoint leída con identidad delegada en algunos flujos | Definir acceso técnico mínimo para consumo general antes de ampliarlo |
| Media | CSS histórico de Intranet acumula overrides | Reducirlo gradualmente con pruebas visuales, sin refactor colateral |
| Baja | Cifras de pruebas caducan | Registrar evidencia de ejecución por entrega |

## Revisión de seguridad

Se verificaron controles documentables: cookie segura, retorno restringido, CORS explícito, autorización servidor, alcance organizacional, separación de identidades, destinos Graph permitidos y errores saneados.

Requieren validación ambiental: consentimiento y roles Entra, privilegios Dataverse, concesión `Sites.Selected`, almacén/rotación de secretos, protección de datos compartida, políticas de retención y observabilidad.

No existe en el repositorio una ubicación con control de acceso independiente para hallazgos ofensivos. Una vulnerabilidad reproducible debe registrarse en el sistema restringido que defina la organización, no en Markdown versionado. Este documento conserva categorías y acciones sin secretos ni instrucciones de explotación.

## Limitaciones

- No se consultó en esta auditoría un tenant o Dataverse real.
- No se validaron permisos efectivos, bibliotecas, retención ni datos productivos.
- No se ejecutó un despliegue productivo.
- No se abrieron archivos locales ignorados que pudieran contener secretos.
- No se infiere cobertura porcentual del número de pruebas.
- El estado corresponde al checkout y fecha indicados; debe actualizarse cuando cambie la implementación.

## Criterio de mantenimiento

Un cambio de arquitectura, seguridad, persistencia, rutas, permisos, esquema o comportamiento funcional actualiza el capítulo correspondiente y, si cambia una decisión estructural, un ADR. No se crean documentos paralelos de plan/acta que repitan la fuente oficial; la evidencia fechada se registra en el mismo capítulo o en el sistema de trabajo del equipo.
