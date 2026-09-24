# GAIA Helpdesk — aceptación del motor de flujos

Fecha de revisión: 2026-09-21

## Resultado automatizado

- Backend `.NET`: 339 pruebas aprobadas, 0 fallidas.
- Frontend `Vitest`: 52 aprobadas, 1 omitida preexistente.
- TypeScript: compilación `--noEmit` correcta.
- ESLint: 0 errores; permanece una advertencia preexistente en `intranet-helpdesk.tsx`.

## Escenarios cubiertos

| Criterio | Estado | Evidencia |
|---|---|---|
| Flujo lineal A → B → C | Automatizado | `HelpdeskWorkflowRulesTests.LinearFlowActivatesOneStepAtATime` |
| Dos pasos iniciales paralelos | Automatizado | `TwoInitialStepsAreActivatedInParallel` |
| Dos ramas y convergencia TODAS | Automatizado | `ParallelConvergenceWaitsForAllIncomingRoutes` |
| Convergencia CUALQUIERA | Automatizado | `AnyIncomingEnablesOnFirstCompatibleRoute` |
| Aprobación y rechazo | Automatizado | `RejectedResultOnlyExecutesCompatibleRoute` y validaciones de resultados |
| Segunda ejecución del mismo paso | Automatizado | `RepeatedStepUsesNextExecutionWithoutMutatingHistory` |
| Espera del solicitante con otra rama activa | Automatizado | `WaitingBranchDoesNotPauseRequestWhileAnotherBranchCanWork` |
| Paso con observación/archivo obligatorio | Automatizado | `RequiredFileAndObservationAreEnforced` |
| Devolución no permitida por configuración | Automatizado | `CompletionRejectsRequesterReturnWhenStepDoesNotAllowIt` |
| Autorización por persona y UO corporativa | Automatizado | `UnitQueueUsesExistingOrganizationalMembership`, `AssignedManagementIsRestrictedToItsResponsible` |
| Publicación inválida | Automatizado | `PublicationRejectsUnreachableAndInvalidAssignments`, pruebas de aplicación |
| Cierre de instancia | Automatizado | `InstanceCompletesOnlyAfterFinalStepAndNoPendingWork` |
| Idempotencia de comando | Implementado | `gaia_operacionid` y búsqueda previa antes de completar |
| Idempotencia de gestión/dependencia | Implementado | búsqueda previa y claves alternativas de Dataverse |
| Servicio sin flujo | Implementado | el inicio solo ocurre cuando `gaia_flujovigente` tiene valor |
| Conservación de versión | Implementado | solicitud e instancia guardan el flujo publicado utilizado |
| Reapertura | Implementado | reutiliza instancia/versión y crea nueva ejecución del paso de reapertura |
| Comentario de solicitud de información | Implementado | comentario visible relacionado con `gaia_gestionsolicitud` |
| Respuesta del solicitante | Implementado | comentario relacionado y reanudación de la misma gestión |
| Tomar gestión de UO | Implementado | valida estado disponible, ausencia de responsable y membresía corporativa |
| Adjunto asociado a gestión | Implementado | `managementId` validado contra la solicitud y persistido en `gaia_gestionsolicitud` |
| Bandejas operativas de flujo | Implementado | consultas autorizadas para mi UO, mis gestiones, aprobaciones y espera del solicitante |
| Resolución funcional del flujo | Implementado | usa la transición activa de resolución, exige y conserva el resumen de solución y genera historial |
| Devolución atómica al solicitante | Implementado | gestión, historial, comentario y estado/SLA se confirman en un único ChangeSet `$batch` |
| Resolución atómica del flujo | Implementado | gestión final, historial, instancia, transición de resolución, solución e historial de estado se confirman en un único ChangeSet `$batch` |
| Activación atómica de etapas | Implementado | gestión origen, historial, gestiones destino, dependencias y estado agregado se confirman en un único ChangeSet `$batch` |
| Transporte ChangeSet | Automatizado | valida multipart, encabezados de concurrencia, errores internos 409/412/500 y error exterior del lote |

## Pendientes antes de producción

1. Ejecutar en un entorno Dataverse aislado las carreras concurrentes y colisiones reales de claves. El transporte HTTP, la detección de errores internos del ChangeSet y la expectativa de rollback ya tienen pruebas automatizadas reproducibles.
2. Completar la verificación visual autenticada de AdminCore y del portal en un host con TLS/SSPI operativo. El 2026-09-21 la aplicación web y la API iniciaron correctamente por HTTP, pero el middleware OpenID no pudo descargar la configuración de Microsoft por el error local de Windows `No hay credenciales disponibles en el paquete de seguridad`; no se eludió ni automatizó la autenticación.

## Decisión de salida

El modelo, aprovisionamiento, motor de reglas, persistencia básica, administración, adjuntos por gestión y vistas operativas —incluidas las cuatro bandejas dedicadas— están implementados y pasan las pruebas disponibles. El cambio todavía no debe declararse listo para producción hasta resolver los pendientes anteriores, especialmente la atomicidad transaccional.
