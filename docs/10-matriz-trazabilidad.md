# Matriz de trazabilidad técnica y funcional

> Estado: **vigente** · Verificado: **2026-10-09**
> Propósito: localizar desde una capacidad su interfaz, contrato HTTP, implementación, datos, autorización y evidencia.

## Cómo leerla

- **Implementada:** existe recorrido funcional en código.
- **Parcial:** existe una parte operativa y otra devuelve indisponibilidad o requiere completar integración.
- **Pendiente:** existe contrato/intención, pero no una operación utilizable.
- Los permisos de la tabla son los principales; un endpoint puede exigir además sesión, acceso base a Intranet/AdminCore, propiedad o alcance organizacional.
- Las tablas son agregados principales, no un diccionario físico exhaustivo. Los nombres exactos se resuelven por metadata.
- Las pruebas citadas demuestran reglas concretas, no certifican producción ni cobertura total.

## Plataforma, identidad y seguridad

| ID | Capacidad / estado | Interfaz y ruta | API / implementación | Datos e integración | Permisos principales | Evidencia |
|---|---|---|---|---|---|---|
| IDN-01 | Inicio/cierre de sesión · Implementada | `/` y navegación protegida | `/api/auth/login`, `/logout`, `/me`; `IdentityEndpoints`, `IdentityModule` | Entra OIDC, cookie `__Host-Gaia.Session`, token delegado | Sesión; retorno limitado al origen | `SecurityAuthorizationTests`, `DataverseReauthenticationTests` |
| SEC-01 | Contexto y navegación autorizada · Implementada | Todos los shells; `SecurityProvider`, `RouteAccessGate` | `/api/security/me`; `SecurityModule`, `DataverseSecurityStore` | usuarios, roles, permisos y módulos Gaia | `INTRANET.VER`, `INT.APP.ADMINCORE.VER` y permiso de ruta | `route-access.test.ts`, `security-permissions.test.ts`, `SecurityAuthorizationTests` |
| SEC-02 | Usuarios y asignaciones de rol · Implementada | `/seguridad/usuarios` | familia `/api/security/users`; `SecurityEndpoints` | `gaia_usuarioaplicacion`, `gaia_usuariorol`, `gaia_rol` | `TI.USUARIOS.*` | `SecurityAssignmentRulesTests`, `SecurityEndpointAuthorizationTests` |
| SEC-03 | Roles y permisos · Implementada | `/seguridad/roles` | familia `/api/security/roles`; `SecurityEndpoints` | `gaia_rol`, `gaia_permiso`, `gaia_rolpermiso` | `TI.ROLES.*` | `security-admin-rules.test.ts`, `SecurityModuleRulesTests` |
| SEC-04 | Árbol de módulos · Implementada | `/seguridad/modulos` | familia `/api/security/modules`; `SecurityEndpoints` | `gaia_modulo` | `TI.MODULOS.*` | `SecurityModuleRulesTests`, `route-access.test.ts` |
| SEC-05 | Administración global organizacional · Implementada | Roles y permisos | actualización de propiedad de rol; autorización en adaptadores | `gaia_rol.gaia_administracionglobal` y asignaciones vigentes | permiso funcional + propiedad global | `SecurityAuthorizationTests`; [Seguridad](02-seguridad-e-integraciones.md) |

## Organización, personas e Intranet

| ID | Capacidad / estado | Interfaz y ruta | API / implementación | Datos e integración | Permisos principales | Evidencia |
|---|---|---|---|---|---|---|
| ORG-01 | Tipos, sedes, unidades y cargos · Implementada | `/organizacion` | `/api/organization/unit-types`, `/sites`, `/units`, `/positions`; `OrganizationEndpoints` | organización, sede y cargo en Dataverse | `ORG.SEDES_TIPOS.*`, `ORG.UNIDADES.*`, `ORG.CARGOS.*` | `DataverseOrganization*Tests`, `organizational-search.test.ts` |
| ORG-02 | Asignaciones organizacionales · Implementada | `/organizacion`, `/talento-humano/vinculaciones` | `/api/third-parties/organizational-assignments`; contracts Organization/ThirdParties | `gaia_asignacionorganizacional`, terceros, unidad, cargo | `ORG.ASIGNACIONES.*`, `TH.VINCULACIONES.*` | `OrganizationalAssignmentTests`, `organizational-assignments-explorer.test.ts` |
| TH-01 | Colaboradores y contactos · Implementada | `/talento-humano/colaboradores`, `/terceros` | `/api/third-parties`, `/emails`, `/phones`; `ThirdPartiesEndpoints` | terceros, tipos de documento, correos y teléfonos | `TH.COLABORADORES.*`, `TH.COLABORADORES.INFO.*`, permisos de correo/teléfono | `DataverseThirdPartyTests`, `DataverseCollaboratorContactTests` |
| TH-02 | Importación administrativa · Implementada | administración de colaboradores/asignaciones | `/administrative-import/validate|execute`, `/organizational-assignments/import/*` | staging lógico, metadata y Dataverse | `TH.COL.IMPORT.ADMINISTRAR` | `PersonnelWorkbookImportTests`, `OrganizationalAssignmentTests` |
| TH-03 | Estudios, idiomas, experiencia y emergencia · Parcial | ficha de colaborador | endpoints heredados devuelven `503` | modelo pendiente de adaptador Dataverse | `TH.COL.INFO.ACTUALIZAR` | [Organización y terceros](modulos/organizacion-terceros.md) |
| INT-01 | Inicio y contenido agregado · Implementada | `/intranet` | `/api/intranet/home` y endpoints de Communications/ThirdParties | eventos, banners, cumpleaños, módulos autorizados | `INTRANET.VER`, `INT.INICIO.VER` | `intranet-birthdays.test.ts`, `intranet-navigation.test.ts` |
| INT-02 | Directorio de personas · Implementada | `/intranet/personas` | `/api/intranet/people`, unidades y foto | ThirdParties, Organization y Graph delegado | `INT.PERSONAS.VER` | `DataverseIntranetDirectoryTests`, `person-avatar.test.ts` |
| INT-03 | Perfil propio · Implementada | `/intranet/perfil` | `/api/profile/`, `/api/profile/photo` | tercero asociado a sesión y Graph | `INTRANET.VER`; propiedad del perfil | `ProfilePhotoContractsTests`, `IntranetProfileObservationRegressionTests` |
| INT-04 | Calendario · Implementada | `/intranet/calendario` | `/api/intranet/events` más fechas autorizadas | eventos y asignaciones de capacitación | `INT.CALENDARIO.VER` | reglas Communications/Training; validación funcional requerida |
| INT-05 | Catálogo de aplicaciones · Implementada | `/intranet/aplicaciones`, Mi espacio | contexto de seguridad/módulos | `gaia_modulo` y permisos `INT.APP.*` | `INT.APLICACIONES.VER` + permiso individual | `intranet-applications.test.ts`, `route-access.test.ts` |

## Comunicaciones

| ID | Capacidad / estado | Interfaz y ruta | API / implementación | Datos e integración | Permisos principales | Evidencia |
|---|---|---|---|---|---|---|
| COM-01 | Tipos de evento y eventos · Implementada | `/comunicaciones/tipos-evento`, `/comunicaciones/eventos` | `/api/communications/event-types`, `/events`; `CommunicationsModule` | Dataverse; lectura Intranet | `COM.TIPOS_EVENTO.*`, `COM.EVENTOS.*` | `CommunicationsRulesTests` |
| COM-02 | Destacados · Implementada | `/comunicaciones/destacados`, Inicio | `/api/communications/highlights`, `/api/intranet/banners` | `gaia_promocionbanner`; imágenes en SharePoint | `COM.DESTACADOS.*` | `CommunicationsRulesTests`, `CommunicationsImageStorageRegressionTests` |
| COM-03 | Login institucional · Implementada | `/configuracion/login`, pantalla pública | `/api/communications/login-configurations`, `/api/public/login-configuration` | Dataverse, instantánea pública y archivos | actualmente políticas `COM.DESTACADOS.*`; lectura pública limitada | pruebas Communications e integración ambiental |
| COM-04 | Ambientación visual · Implementada | `/configuracion/ambientacion`, capas Intranet/AdminCore | `/api/communications/visual-ambiences`, `/active-visual-ambience` | `gaia_ambientacionvisual`; imágenes SharePoint | actualmente políticas `COM.DESTACADOS.*` | `CommunicationsRulesTests`; [Comunicaciones](modulos/comunicaciones.md) |

## Capacitaciones

| ID | Capacidad / estado | Interfaz y ruta | API / implementación | Datos e integración | Permisos principales | Evidencia |
|---|---|---|---|---|---|---|
| CAP-01 | Catálogo y versiones · Implementada | `/capacitaciones/catalogo`, `/contenido` | `/api/training/administration` categorías, capacitaciones, versiones y contenido | tablas Training en Dataverse | `CAP.CATALOGO.*`, `CAP.CONTENIDO.*` | `training-content-rules.test.ts`, tests de operaciones Training |
| CAP-02 | Audiencias y publicación · Implementada | `/capacitaciones/audiencias`, revisión contextual | administración de destinatarios, preview, revisión y publicación | destinatarios, asignaciones e historial | `CAP.AUDIENCIAS.*`, `CAP.REVISAR`, `CAP.PUBLICAR`, `CAP.ARCHIVAR` | `TrainingParticipantOperationsTests`, `TrainingParticipantAuthorizationTests` |
| CAP-03 | Evaluaciones e intentos · Implementada | constructor; `/intranet/capacitaciones` | `/api/training/my/assignments/.../evaluations|attempts` | evaluación, pregunta, opción, intento y respuesta | `INT.CAPACITACIONES.VER`; revisión `CAP.REVISAR` | `TrainingEvaluationPersistenceTests`, `TrainingEvaluationScoringTests`, `training-evaluation-builder.test.ts` |
| CAP-04 | Participación y progreso · Implementada | `/intranet/capacitaciones` | asignaciones, recursos, progreso y finalización | asignación, bloque, progreso y archivos | `INT.CAPACITACIONES.VER`; propiedad del participante | `TrainingCompletionPolicyTests`, `TrainingParticipantAuthorizationTests` |
| CAP-05 | Seguimiento, resultados y exportación · Implementada | `/capacitaciones/seguimiento`, `/resultados` | overview, pendientes, calificación y exportación | asignaciones, intentos y resultados | `CAP.SEGUIMIENTO.*`, `CAP.RESULTADOS.*`, `CAP.REVISAR` | tests Training; prueba ambiental multiusuario pendiente |

## Solicitudes

| ID | Capacidad / estado | Interfaz y ruta | API / implementación | Datos e integración | Permisos principales | Evidencia |
|---|---|---|---|---|---|---|
| SOL-01 | Catálogo, formulario y radicación · Implementada | `/intranet/solicitudes` | `/portal/catalog`, `/services/{id}/form`, `POST /requests`; `SolicitudesRequestApplication` | servicios, formularios, campos, solicitudes y respuestas | `INT.SOLICITUDES.VER` | `SolicitudesRequestApplicationTests`, `SolicitudesFormDesignerRegressionTests` |
| SOL-02 | Bandeja personal y expediente · Implementada | `/intranet/solicitudes` | `/portal`, `/requests/{id}` y workflow | solicitud, historial, comentarios y gestiones visibles | `INT.SOLICITUDES.VER`; propiedad del solicitante | tests Request/Observation/Conversation |
| SOL-03 | Bandejas operativas · Implementada | `/solicitudes` | `/management/queue`, `/workflow-queue`, `/management/catalog` | solicitudes y gestiones paginadas con `nextLink` | `HD.SOLICITUDES.VER` + alcance organizacional | `DataverseSolicitudesQueueTests`, `solicitudes-queue.test.ts` |
| SOL-04 | Toma, gestión, respuesta y reasignación · Implementada | expediente AdminCore | `/take`, `/complete`, `/resume`, `/assignment`, transiciones | gestión, dependencias, comentarios e historial | `HD.SOLICITUDES.VER`; `HD.SOLICITUDES.REASIGNAR` | `SolicitudesManagementApplicationTests`, `SolicitudesWorkflowApplicationTests` |
| SOL-05 | Servicios, formularios y flujos · Implementada | `/solicitudes/servicios-y-flujos` | `/administration/services|forms|workflows|steps|routes` | definiciones versionadas y publicadas | `HD.CATALOGOS.VER`, `HD.CATALOGOS.ADMINISTRAR`; alcance de unidad/global | `SolicitudesWorkflowRulesTests`, `SolicitudesFormDesignerRegressionTests` |
| SOL-06 | Formularios de etapa · Implementada | diseñador y completar etapa | `/workflow-steps/{id}/form`, `/management/workflows/{id}/form/responses` | formularios/campos/opciones y respuestas por gestión | permisos de catálogo o gestión autorizada | tests FormDesigner/Management/Workflow |
| SOL-07 | Adjuntos y conciliación · Implementada | radicación, gestión y pestaña Archivos | `/requests/{id}/attachments`, `/attachments/{id}/content`, reconciliation | `gaia_adjuntosolicitud`, metadata Dataverse y binario SharePoint | propiedad/gestión autorizada + permiso funcional | `SolicitudesAttachmentApplicationTests`, `SolicitudesAttachmentRulesTests` |
| SOL-08 | Concurrencia y cierre del flujo · Implementada; validación real pendiente | acciones de expediente | ChangeSets, ETag e IDs de operación | gestión, instancia, rutas, dependencias e historia | autorización de la gestión | `DataverseChangeSetTests`, `SolicitudesWorkflowRulesTests`; carrera real pendiente |

## Archivos, diagnóstico e inventario

| ID | Capacidad / estado | Interfaz y ruta | API / implementación | Datos e integración | Permisos principales | Evidencia |
|---|---|---|---|---|---|---|
| FILE-01 | Almacenamiento SharePoint · Implementada | Consumida por módulos | `IFileStorage`, `SharePointFileStorage`, Graph transport | `gaia_configuracionsharepoint`, Graph/SharePoint | permiso del módulo + identidad técnica | `FileStorageFoundationTests`, `SharePointFileStorageTests`, `SharePointRepositoryConfigurationTests` |
| FILE-02 | Diagnóstico y prueba controlada · Implementada | operación técnica | `/api/infrastructure/files/diagnostics`, `write-probe` | configuración/credencial autorizada | AdminCore + `TI.MODULOS.ADMINISTRAR` | `FileStorageDiagnosticAuthorizationTests` |
| FILE-03 | Migración de repositorio · Base implementada | sin UI masiva general | `IFileStorageMigration` | copia, ETag, longitud y SHA-256 | operación técnica autorizada | `FileStorageMigrationTests`; lote por módulo pendiente |
| INV-01 | Gestión de inventario · Pendiente | `/inventario` | `/api/inventory/{**path}` devuelve `503` | modelo físico/adaptador no implementado | `INV.VER`, futuros permisos de operación | [Inventario](modulos/inventario.md) |

## Trazabilidad de documentos

| Si cambia... | Actualizar como mínimo |
|---|---|
| Ruta o permiso | esta matriz, Seguridad y Frontend/diseño |
| Endpoint/DTO | esta matriz, API/datos y módulo |
| Tabla, columna, choice o relación | esta matriz, Modelo funcional, API/datos y módulo |
| Regla funcional o estado | módulo, Modelo funcional y pruebas |
| Identidad o integración Microsoft | Seguridad/integraciones, Operación y ADR si cambia la decisión |
| Componente/tokens compartidos | Frontend/diseño y módulos consumidores |
| Estado implementado/parcial/pendiente | esta matriz, mapa de módulos y Auditoría |

## Regla de mantenimiento

Cada entrega funcional revisa las filas afectadas. Si una capacidad nueva no puede ubicarse en esta matriz, todavía no está completamente documentada. Una prueba nueva se enlaza por archivo o familia estable, no por un total numérico que quede obsoleto.
