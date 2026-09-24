# Helpdesk — formularios dinámicos por etapa

## Estado

Implementación de aplicación y aprovisionamiento ambiental terminados el 23 de septiembre de 2026.
Las cinco tablas, sus claves y relaciones, y el lookup de adjuntos fueron creados dentro de
`GAIAHelpdesk` y publicados en Dataverse. El aprovisionador continúa siendo idempotente: una segunda
ejecución en modo `Plan` no propuso crear componentes adicionales.

## Funcionamiento

1. El administrador abre un servicio, entra en **Paso 3. Definir flujo** y selecciona
   **Configurar formulario** en una etapa de un flujo en borrador.
2. Puede crear el encabezado, agregar campos, opciones y ayudas, previsualizar controles funcionales y
   cambiar el orden arrastrando. Cada cambio de orden se persiste.
3. Al publicar, el backend valida conjuntamente flujo, etapas, rutas y formularios. Un flujo publicado
   es inmutable; una nueva versión clona etapas, rutas, formularios, campos y opciones con nuevos IDs.
4. Cuando una persona completa una gestión, la aplicación carga el formulario asociado a esa etapa.
   Si no existe formulario, conserva el proceso anterior.
5. Primero se guardan las respuestas tipadas; después se cargan los archivos asociados a la respuesta
   correspondiente; finalmente se registra el resultado de la gestión.
6. El motor de rutas solo consulta `gaia_gestionsolicitud.gaia_resultado`. Ningún valor ingresado en el
   formulario puede activar directamente una ruta.

## Persistencia

| Propósito | Tabla |
|---|---|
| Encabezado del formulario de una etapa | `gaia_formulariopaso` |
| Definición de campos | `gaia_campoformulariopaso` |
| Opciones de campos | `gaia_opcioncampoformulariopaso` |
| Respuesta de una ejecución concreta | `gaia_respuestacampogestion` |
| Opciones elegidas en una respuesta | `gaia_respuestaopciongestion` |
| Archivo de un campo | `gaia_adjuntosolicitud.gaia_respuestacampogestionid` |

Las respuestas se identifican por gestión y campo. Una reejecución genera otra
`gaia_gestionsolicitud`, por lo que no sobrescribe el historial de ejecuciones anteriores.

## Operaciones HTTP

### Administración

- `GET /api/helpdesk/administration/workflow-steps/{stepId}/form`
- `PUT /api/helpdesk/administration/workflow-steps/{stepId}/form`
- `POST /api/helpdesk/administration/workflow-steps/{stepId}/form/fields`
- `PUT /api/helpdesk/administration/workflow-steps/{stepId}/form/fields/{fieldId}`

Solo se permite editar formularios pertenecientes a flujos en borrador.

### Ejecución

- `GET /api/helpdesk/management/workflows/{managementId}/form`
- `PUT /api/helpdesk/management/workflows/{managementId}/form/responses`
- `POST /api/helpdesk/requests/{requestId}/attachments`
- Operación existente de finalización de gestión, ejecutada después de respuestas y archivos.

## Reglas de seguridad e integridad

- El actor debe estar autorizado para atender la gestión.
- No se aceptan respuestas para campos ajenos, ocultos o pertenecientes a otro formulario.
- Las opciones deben pertenecer al campo enviado.
- Las respuestas se validan según tipo, obligatoriedad, longitud y rango.
- Un archivo de campo debe referenciar una respuesta de la misma gestión y solicitud.
- La exigencia de archivos se calcula en el servidor consultando Dataverse; no se confía en un indicador
  enviado por el navegador.
- La finalización vuelve a comprobar campos y archivos obligatorios.
- Los identificadores de operación mantienen la finalización idempotente.

## Compatibilidad

- Los formularios de radicación continúan en `gaia_formularioservicio` y
  `gaia_respuestacampo`.
- Los formularios internos no reutilizan ni mezclan esas respuestas.
- Servicios, flujos y etapas sin formulario siguen funcionando como antes.
- Los adjuntos generales y comentarios existentes mantienen sus relaciones actuales.

## Validación realizada

- ESLint: sin errores.
- TypeScript: sin errores.
- Frontend: 54 pruebas activas superadas y 1 omitida.
- Backend antes del cierre documental: 347 pruebas superadas.
- Regresiones adicionales verifican interacción de vista previa, persistencia de orden, secuencia de
  guardado, aislamiento por gestión y comprobación de archivos en servidor.
- Cierre final de backend: 350 pruebas superadas.
- Manifiesto local: 25 tablas consolidadas, 5 tablas nuevas, 1 tabla extendida, 8 relaciones nuevas,
  5 claves nuevas y 0 errores; apto para ejecutar el preflight remoto de solo lectura.
- Verificación remota posterior al aprovisionamiento: 25 tablas con propiedad de Organización; las 5
  tablas nuevas tienen auditoría y seguimiento de cambios; 5 claves activas; 8 relaciones con
  eliminación `Restrict`; lookup de adjuntos presente; todos los componentes incorporados en
  `GAIAHelpdesk` (incluidos los hijos heredados por el componente raíz de cada tabla).
- Aceptación funcional en `Prueba de servicio`: flujo v1 publicado con dos etapas y una ruta; formulario
  interno obligatorio configurado en `REVISION_INICIAL`; versión 1 del formulario de radicación
  publicada; solicitud `HD-0001018` creada desde la Intranet con cuatro respuestas dinámicas.
- La etapa inicial rechazó correctamente el envío sin el campo obligatorio, guardó después la respuesta
  y la observación, cambió la solicitud a **En gestión** y activó `APROBACION_DIRECTIVA`.

## Resultado y validaciones pendientes

La validación de Dataverse y del recorrido hasta la segunda etapa quedó completada. Permanecen dos
comprobaciones ambientales que requieren condiciones distintas a la sesión actual:

1. `APROBACION_DIRECTIVA` está asignada a Dirección. La sesión usada pertenece a Tecnología y la
   interfaz deshabilita correctamente **Tomar / Aprobar / Rechazar**. Una persona autorizada de Dirección
   debe aprobarla o rechazarla para validar el cierre completo sin eludir permisos.
2. El formulario publicado de la etapa inicial solo contiene un campo de texto. No se hizo una carga
   real de archivo a SharePoint ni una reejecución, para no alterar un flujo ya publicado. Esas pruebas
   deben hacerse en una versión de flujo de aceptación que incluya un campo archivo y usando el perfil
   local de SharePoint, sin registrar secretos en Git, documentos ni conversaciones.

## Archivos de referencia

- Plan: `Md/modulos/HELPDESK_FORMULARIOS_ETAPA_PLAN.md`
- Modelo consolidado: repositorio de aprovisionamiento, `model/gaia-helpdesk-model.json`
- Extensión: repositorio de aprovisionamiento, `model/gaia-helpdesk-stage-forms-extension.json`
