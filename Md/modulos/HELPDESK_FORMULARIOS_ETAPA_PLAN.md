# Formularios dinámicos por etapa — análisis y plan por fases

## Resultado de la fase 1

La ampliación es compatible con el modelo actual. El resultado de una gestión continuará en
`gaia_gestionsolicitud.gaia_resultado`; los formularios de etapa solo capturarán evidencia y datos de
cada ejecución. Las rutas seguirán evaluando exclusivamente los valores `299540170–299540175`.

No se detectó la necesidad de una sexta tabla, otro lookup ni un nuevo Choice. El modelo solicitado de
cinco tablas y un lookup opcional en `gaia_adjuntosolicitud` es suficiente.

## Separación funcional obligatoria

| Contexto | Formulario | Respuestas | Dueño de la ejecución |
|---|---|---|---|
| Radicación | `gaia_formularioservicio` | `gaia_respuestacampo` | `gaia_solicitud` |
| Gestión interna | `gaia_formulariopaso` | `gaia_respuestacampogestion` | `gaia_gestionsolicitud` |

No se reutilizará `gaia_respuestacampo` para gestiones. Cada nueva ejecución de una etapa tiene otra
`gaia_gestionsolicitud`, de modo que la clave gestión + campo conserva las respuestas históricas.

## Hallazgos del código actual

1. El diseñador de radicación está concentrado en `FormDesigner`, dentro de
   `apps/web/src/features/helpdesk/helpdesk-administration.tsx`. Se debe extraer un constructor neutral
   reutilizable; no se copiará el componente completo.
2. El diseñador actual permite agregar, editar, previsualizar y ordenar campos, pero su UI solo expone
   texto corto/largo, número, correo, fecha, fecha-hora y lista. El modelo ya contempla teléfono, URL,
   radio, checkbox, grupo de checkbox y archivo, que deberán incorporarse sin cambiar la semántica del
   formulario de radicación.
3. Los contratos actuales no transportan patrón, mensaje de validación, valor predeterminado ni
   configuración JSON, aunque esas columnas existen en `gaia_campoformulario`. El contrato compartido
   debe incorporarlas para reproducir realmente las capacidades del modelo.
4. La validación de respuestas de radicación vive de forma privada en `HelpdeskRequestApplication`.
   Debe extraerse una regla reutilizable para los dos tipos de formulario, manteniendo separados sus
   almacenes de respuestas.
5. La gestión se completa actualmente mediante `CompleteHelpdeskManagement`; el frontend usa un prompt
   y envía el resultado directamente. La nueva operación debe validar y guardar primero las respuestas
   de la gestión y solo después completar la gestión con su resultado.
6. `DataverseHelpdeskWorkflowExecutionWriter.CreateDraftAsync` crea una versión vacía. La nueva versión
   deberá clonar pasos, rutas y formularios/opciones, remapeando todos los identificadores internos.
7. La publicación actual valida solamente pasos y rutas. Se ampliará para validar formularios y campos,
   pero no cambiará el motor de decisión.
8. Los adjuntos ya soportan `gaia_gestionsolicitud`. Se añadirá la referencia a
   `gaia_respuestacampogestion` y se validará que solicitud, gestión y respuesta pertenezcan a la misma
   ejecución antes de escribir en SharePoint/Dataverse.

## Componentes previstos

### Modelo y contratos

- `src/Modules/Helpdesk/Gaia.Modules.Helpdesk/HelpdeskFormContracts.cs`
- Nuevo contrato específico de formularios/respuestas de gestión en el mismo módulo.
- `src/Modules/Helpdesk/Gaia.Modules.Helpdesk/HelpdeskWorkflow.cs`
- `src/Modules/Helpdesk/Gaia.Modules.Helpdesk/HelpdeskAttachmentContracts.cs`

### API y Dataverse

- `src/Modules/Helpdesk/Gaia.Modules.Helpdesk/HelpdeskEndpoints.cs`
- `src/Gaia.Api/Infrastructure/Dataverse/Helpdesk/DataverseHelpdeskFormAdministration.cs`
- `src/Gaia.Api/Infrastructure/Dataverse/Helpdesk/DataverseHelpdeskWorkflowDefinitionReader.cs`
- `src/Gaia.Api/Infrastructure/Dataverse/Helpdesk/DataverseHelpdeskWorkflowExecutionWriter.cs`
- `src/Gaia.Api/Infrastructure/Dataverse/Helpdesk/DataverseHelpdeskAttachmentStore.cs`
- Nuevo adaptador Dataverse para formularios y respuestas de gestión, separado del formulario de
  radicación aunque comparta contratos y validadores.
- Registro de dependencias en `src/Gaia.Api/Program.cs` si el nuevo puerto lo requiere.

### Frontend

- Extraer el constructor desde
  `apps/web/src/features/helpdesk/helpdesk-administration.tsx` a componentes reutilizables.
- Integrarlo en `apps/web/src/features/helpdesk/helpdesk-workflow-manager.tsx` mediante la acción
  **Configurar formulario** en cada etapa.
- Renderizar y diligenciar el formulario en
  `apps/web/src/features/helpdesk/helpdesk-management.tsx` antes de completar/aprobar/rechazar.
- Mantener intacta la experiencia cuando una etapa no tenga formulario.

### Pruebas

- Validación de formularios y campos de etapa.
- Inmutabilidad después de publicar.
- Clonado completo con remapeo de IDs.
- Respuestas tipadas por ejecución y opciones múltiples.
- Reejecución sin sobrescribir respuestas anteriores.
- Archivos vinculados al campo correcto y a la misma gestión.
- La ruta depende de `gaia_resultado`, nunca de una respuesta arbitraria.
- Regresión del formulario de radicación y del flujo sin formulario.

## Fases de implementación

### Fase 1 de 6 — Análisis y contrato

Completada con este documento. No modifica Dataverse ni el comportamiento de ejecución.

### Fase 2 de 6 — Manifiesto y preflight no destructivo

- Incorporar las cinco tablas y el lookup al manifiesto documental/provisionable.
- Validar nombres, tipos, Choices, claves y relaciones.
- Ejecutar solamente comprobación contra Dataverse.
- Si faltan componentes, detenerse y entregar la lista exacta; no crearlos automáticamente.

Estado: completada el 23 de septiembre de 2026. El inventario remoto confirmó que no existían
conflictos; después se aprovisionaron las cinco tablas, 5 claves, 8 relaciones y el lookup de adjuntos en
`GAIAHelpdesk`. La verificación posterior confirmó propiedad de Organización, auditoría, seguimiento de
cambios, claves activas, relaciones `Restrict` e inclusión correcta en la solución. Una segunda ejecución
en modo `Plan` fue idempotente y no propuso creaciones.

### Fase 3 de 6 — Backend administrativo

- CRUD del formulario de etapa, campos y opciones.
- Reglas de configuración y de publicación.
- Inmutabilidad de flujos publicados.
- Clonado de pasos, rutas, formularios, campos y opciones al crear una versión.

Estado: implementación de código completada el 23 de septiembre de 2026. Incluye API administrativa,
persistencia Dataverse, validación conjunta al publicar, protección mediante flujo borrador y clonado
con remapeo de identificadores. Compilación sin advertencias y 11 pruebas focalizadas superadas. Su
prueba integrada requiere que los componentes de la fase 2 existan previamente en Dataverse.

### Fase 4 de 6 — Ejecución y archivos

- Lectura del formulario correspondiente a la gestión.
- Carga y guardado tipado de respuestas por `gaia_gestionsolicitud`.
- Relación de archivos con `gaia_respuestacampogestion`.
- Operación coordinada: validar respuestas y después completar con `gaia_resultado`.

Estado: implementación de backend completada el 23 de septiembre de 2026. La API entrega el formulario
de la gestión, persiste valores tipados y opciones por ejecución, devuelve los identificadores necesarios
para cargar archivos y valida todos los campos obligatorios antes de completar. Los adjuntos de campo se
comprueban contra la misma gestión. `gaia_resultado` continúa siendo el único dato que activa rutas. La
compilación terminó sin advertencias y las 347 pruebas del backend fueron superadas.

### Fase 5 de 6 — UI reutilizable

- Constructor compartido para radicación y etapa.
- Acción **Configurar formulario** fuera del modal de edición de etapa.
- Previsualización, ordenamiento, opciones, archivos y validaciones.
- Formulario funcional en la bandeja de gestión.

Estado: implementación completada el 23 de septiembre de 2026. Cada etapa de un flujo borrador tiene
una acción independiente **Configurar formulario**; el diseñador permite crear y editar campos,
previsualizarlos con controles funcionales y reordenarlos arrastrando, con persistencia inmediata del
orden. El mismo renderizador dinámico se usa en la vista previa y en la gestión real. Al completar una
gestión, las respuestas y archivos del formulario se guardan antes de registrar `gaia_resultado`; el
backend verifica directamente los adjuntos relacionados y no confía en una afirmación del navegador.
Las etapas sin formulario conservan el comportamiento anterior. ESLint y TypeScript terminaron sin
errores; las 54 pruebas activas del frontend y las 347 pruebas del backend fueron superadas.

### Fase 6 de 6 — Regresión, aceptación y documentación

- Pruebas de backend/frontend y escenarios de reejecución.
- Prueba de carga real a SharePoint sin exponer secretos.
- Verificación de compatibilidad para servicios y etapas sin formulario.
- Actualización del modelo consolidado y documentación funcional.

Estado: cierre de código y documentación completado el 23 de septiembre de 2026. Se añadieron
regresiones para la vista previa funcional, persistencia del orden, secuencia respuestas→archivos→resultado,
compatibilidad sin formulario, aislamiento histórico por gestión y validación de adjuntos calculada en
el servidor. El resultado final fue de 350 pruebas de backend superadas, junto con las 54 pruebas
activas de frontend, TypeScript y ESLint superados en la fase anterior. La guía funcional y de
aceptación quedó en `Md/modulos/HELPDESK_FORMULARIOS_ETAPA_IMPLEMENTACION.md`.

La aceptación ambiental de Dataverse y del flujo hasta la segunda etapa fue completada el 23 de
septiembre de 2026. Se publicó el flujo v1 y el formulario de radicación de `Prueba de servicio`, se creó
`HD-0001018`, se comprobó la obligatoriedad del formulario interno y se activó correctamente la etapa
`APROBACION_DIRECTIVA`. El cierre total queda pendiente de una sesión autorizada de Dirección, porque
la sesión de Tecnología no puede aprobar esa etapa. También queda pendiente una prueba específica de
archivo/SharePoint en una nueva versión de aceptación que incluya un campo de archivo.

## Condición para iniciar la fase 3

Dataverse debe contener exactamente las cinco tablas y el lookup solicitados, con relaciones Restrict,
propiedad de Organización, auditoría y seguimiento de cambios. La fase 2 comprobará esa condición sin
realizar mutaciones.
