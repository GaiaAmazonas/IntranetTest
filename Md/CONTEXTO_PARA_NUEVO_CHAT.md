# Gaia: contexto de continuidad para un nuevo chat

Fecha de revisión documental: **22 de septiembre de 2026**. Documento basado en el código local, no solamente en conversaciones anteriores. Raíz del proyecto: `Proyecto Gaia Aplicacion`.

## Cómo usar este documento

Lee primero [Gobierno documental y arquitectura canónica](00_GOBIERNO_DOCUMENTAL_Y_ARQUITECTURA.md), luego este archivo y los documentos enlazados abajo antes de crear un módulo. Después inspecciona solamente los archivos afectados y un módulo de referencia. No es necesario volver a explorar todo el repositorio, pero estos documentos no sustituyen verificar el código que vas a cambiar.

1. [Arquitectura, convenciones y conexiones](continuidad/01_ARQUITECTURA.md).
2. [Seguridad, autenticación y tokens](continuidad/02_SEGURIDAD.md).
3. [Receta para módulos, diseño y verificación](continuidad/03_CREAR_MODULO.md).
4. Para infraestructura de archivos: [Estado por fases de Graph / SharePoint](continuidad/04_ARCHIVOS_SHAREPOINT.md). Fundación, adaptador, diagnóstico, prueba controlada, configuración Dataverse y copia verificada entre repositorios están implementados; Staging fue validado. Falta integrar los puertos desde cada módulo y construir lotes cuando exista su inventario de referencias. Consultar allí nombres de tabla, opciones y credenciales por alias antes de continuar.
5. Para Helpdesk y sus adjuntos: [Módulo Helpdesk](modulos/helpdesk.md) y [Preparación de adjuntos](continuidad/05_PREPARACION_ADJUNTOS_HELPDESK.md). Los endpoints, la persistencia funcional y la integración con SharePoint ya existen; el segundo documento conserva las decisiones de infraestructura. La fuente contractual del modelo sigue siendo `model/gaia-helpdesk-model.json`.
6. Para fotos institucionales de perfil: [Microsoft Entra / Graph](continuidad/06_FOTOS_PERFIL_ENTRA.md). No almacenar fotos en Dataverse, no usar nombres para resolver identidades y completar primero el consentimiento delegado mínimo de Graph.
7. Para separar identidades de Dataverse y SharePoint, y especialmente para configuración anterior al login: [Autenticación Dataverse y SharePoint](continuidad/07_AUTENTICACION_DATAVERSE_SHAREPOINT.md).
8. Para campañas decorativas de Intranet y AdminCore: [Ambientación visual](modulos/ambientacion-visual.md). Dataverse conserva configuración y vigencia; SharePoint conserva imágenes; el tema es texto abierto y los efectos respetan reducción de movimiento.
9. Para banners de la portada: [Destacados de Intranet](modulos/destacados.md). Dataverse conserva contenido, vigencia, estado y referencias; SharePoint conserva las variantes de imagen y la portada las consume mediante streaming autorizado desde la API.

Son una fotografía del estado revisado. La solicitud actual del usuario delimita el trabajo; los documentos históricos describen también aspiraciones. Si el código contradice esta guía, informa la diferencia y actualiza la documentación al terminar, sin sustituir silenciosamente la implementación.

## Resumen ejecutivo

- Fundación Gaia Amazonas tiene **dos experiencias**: Intranet para colaboradores y AdminCore para administración. Comparten backend y seguridad, pero no son el mismo diseño ni tienen los mismos accesos.
- Stack: C# / ASP.NET Core .NET 10; Next.js 16.2.12, React 19.2.4, TypeScript 5, Tailwind CSS 4 y CSS propio. Iconos Lucide. Dataverse Web API OData como integración de datos activa.
- Arquitectura: monolito modular. Contratos y reglas en `src/Modules`; implementaciones Dataverse en `src/Gaia.Api/Infrastructure/Dataverse`; composición y middleware en `src/Gaia.Api/Program.cs`.
- Autenticación Microsoft Entra ID con OpenID Connect y cookie de sesión. La API obtiene tokens **delegados del usuario** para Dataverse. El navegador no debe almacenar tokens de Dataverse.
- Autorización funcional mediante usuarios, roles, permisos y módulos propios almacenados en Dataverse. Separada de los roles técnicos de seguridad del entorno Power Platform.
- El frontend es una **exportación estática** (`output: "export"`), no un servidor Next con API routes. La API ASP.NET es un proceso independiente.

## Estado real y decisiones que no se deben perder

| Área | Estado / restricción |
|---|---|
| Organización | Unidades, tipos, sedes, cargos y asignaciones con adaptadores Dataverse. |
| Talento humano / terceros | Identidad de la persona, contactos y asignaciones. No confundir persona, cargo, vinculación y rol de seguridad. |
| Seguridad | Usuarios, asignaciones temporales de roles, permisos y árbol de módulos. Proteger también cada endpoint, no solo los botones. |
| Comunicaciones | Eventos, tipos de evento y destacados/banners con estados, vigencia e imágenes. |
| Inventario | Existe estructura, pero sus endpoints responden 503: aún no tiene implementación Dataverse operativa. No presentarlo como terminado. |
| Helpdesk | Módulo funcional en Intranet y AdminCore: catálogos, formularios versionados, radicación, bandeja, reasignación, conversación, transiciones, SLA y adjuntos en SharePoint. Consultar `Md/modulos/helpdesk.md` para estado, límites y pruebas vigentes. |
| Banners de Inicio | Textos, vigencia y acciones desde Dataverse. La administración usa una tabla responsive; las imágenes se almacenan en SharePoint mediante `IFileStorage` y la portada consume las URLs de API. Ver `modulos/destacados.md` para el contrato de referencias y la validación de columnas. |
| Cumpleaños | Nombres y fechas desde Dataverse. Ilustraciones SVG de prueba en `public/people/temporary-avatar-*.svg`; no son fotos reales. No inventar cumpleaños ni personas. |
| Aplicaciones | Configuradas según módulos autorizados `INT.APP.*`. Accesos fijos Teams, Outlook y Drive; logos temporales de AdminCore/Plan View. Drive abre la cuenta que esté iniciada en Google, no garantiza por sí mismo la cuenta institucional. |
| Navegación | Apps configuradas también aparecen en el desplegable del usuario. Accesos institucionales abren aparte. Respetar las decisiones vigentes del código al modificar menú o catálogo. |
| Personas | Búsqueda por nombre, cargo, unidad, sede y contactos; filtro jerárquico y paginación. Panel de unidades compacto y sticky: **se rechazó estirarlo hasta toda la altura**. |
| Fotos de perfil | API delegada hacia Microsoft Graph, con `PersonAvatar` reutilizable y alternativa de iniciales. Ver `continuidad/06_FOTOS_PERFIL_ENTRA.md` antes de cambiar permisos, caché o resolución de identidad. |
| Calendario | Vistas día/semana/mes. Mantener la agenda lateral en escritorio; textos dentro de sus celdas; cumpleaños agrupados por día. |

## Preferencias de trabajo y diseño

- No rediseñar otras pantallas ni cambiar autorizaciones por un ajuste visual.
- Reutilizar colores, tipografía, radios, sombras, iconos y contenedores existentes; Intranet debe sentirse humana, no como tablas de AdminCore.
- Inicio usa banner de ancho completo, texto y controles en el eje izquierdo y sombra ligera. Bloques inferiores y páginas Personas/Calendario aprovechan hasta 1600 px con márgenes responsive.
- Cumpleaños deben transmitir celebración: felicitación, presencia visual y confeti repetible; no una lista administrativa de nombres. Respetar movimiento reducido.
- Nombres, correos, teléfonos y textos de eventos no deben desbordar. No sacrificar información necesaria con recortes indiscriminados.
- Después de guardar o retirar un rol, actualizar el registro afectado sin recargar toda la página, perder búsqueda o cerrar el panel. Confirmar persistencia real, no solamente un toast de éxito.
- No publicar a Git, modificar Dataverse, restaurar privilegios de un usuario ni ejecutar bootstrap por iniciativa propia. Requieren estar dentro de la solicitud actual.
- Revisar `git status` antes de editar y preservar cambios ajenos. El árbol local puede contener trabajo no publicado.

## Alertas conocidas: no copiar como buenas prácticas

- `intranet.css` acumula overrides históricos; leer también el final del archivo. Añadir otro override sin entender la cascada puede deshacer responsive, hover o dimensiones.
- Hay componentes TSX antiguos compactados en líneas muy largas. Para código nuevo usar formato legible; no convertir esa compactación en una convención.
- Inicio usa un endpoint agregado que espera varias consultas. No prometer que cada bloque ya carga independientemente ni que todas las demoras están resueltas.
- Personas tiene caché en memoria del módulo frontend y refresco de fondo. No está diseñada como caché multiusuario ni tiene TTL explícito: revisar aislamiento/invalidación antes de extenderla a otros módulos. Una autorización fallida nunca debe justificar mostrar datos antiguos indefinidamente.
- Buscar/filtrar personas puede leer y enriquecer un conjunto amplio desde Dataverse. Optimizar consultas antes de añadir más cachés; medir tiempos reales.
- La transición de login tiene 300 ms adicionales; la cookie dura hasta 8 horas con renovación deslizante, mientras que el cierre por 40 minutos de inactividad es control frontend. No confundir estos mecanismos.
- Ante 400 `Skip Clause is not supported in CRM`, no añadir `$skip`: usar paginación compatible y `@odata.nextLink`.
- Ante 403 de metadatos, no asignar Administrador del sistema a todos. Diagnosticar los permisos técnicos con una cuenta de prueba.

## Prompt para el nuevo chat

> Lee `Md/CONTEXTO_PARA_NUEVO_CHAT.md` y los tres documentos de continuidad que enlaza. Revisa el estado del repositorio y solo los archivos relacionados con mi solicitud. Crea el módulo [NOMBRE] para [INTRANET / ADMINCORE] con estas capacidades: [REQUISITOS]. Reutiliza arquitectura, seguridad, cliente Dataverse y diseño; no cambies módulos ajenos. Identifica cualquier tabla/columna/privilegio que deba preparar antes de depender de él. Verifica permisos, compilación, pruebas y responsive. Actualiza esta documentación con las decisiones nuevas.

Para continuar específicamente Helpdesk en otro chat puede usarse:

> Trabaja en `C:\Users\Edgar Munar\Documents\Proyecto Gaia Aplicacion`. Antes de modificar código, lee completos `Md/CONTEXTO_PARA_NUEVO_CHAT.md` y `Md/modulos/helpdesk.md`; luego revisa `git status` y el último commit de `main`. Mi cambio solicitado es: [DESCRIBIR CAMBIO]. Conserva la arquitectura modular, seguridad, Dataverse y SharePoint documentados; no modifiques otros módulos, no publiques sin mi autorización y ejecuta las validaciones indicadas en la documentación.

## Mantenimiento de la memoria

Al cerrar cada módulo, actualizar: fecha de revisión, mapa de carpetas, contratos/endpoints, códigos de permisos, tablas/relaciones, configuración nueva sin secretos, estado implementado y pendientes. No registrar tokens, datos personales ni contraseñas. Esta actualización mantiene útil el contexto para futuras conversaciones.
