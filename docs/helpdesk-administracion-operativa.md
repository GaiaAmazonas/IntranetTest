# GAIA Helpdesk — administración operativa de solicitudes

Fecha de revisión: 2026-09-28

## Qué está construido

### Configuración

- Catálogo de servicios y formularios de radicación.
- Flujos versionados por servicio.
- Etapas iniciales, finales y de reapertura.
- Asignación por unidad, persona o responsable de la solicitud.
- Activación por cualquier conexión o por todas las conexiones.
- Rutas condicionadas por resultado: completado, aprobado, rechazado, devuelto, requiere aprobación y no aplica.
- Formularios dinámicos propios de cada etapa.
- Reglas de observación, archivo, decisión y devolución al solicitante.

### Ejecución

- Al radicar se conserva la versión publicada del flujo y se crea su instancia.
- Las etapas iniciales crean gestiones disponibles para sus responsables.
- Al completar una gestión, el motor activa únicamente las rutas compatibles.
- Se soportan recorridos lineales, ramas paralelas, convergencia, reapertura y espera del solicitante.
- Las respuestas de formularios, observaciones y adjuntos quedan asociados a la gestión concreta.
- La finalización de una etapa final resuelve la instancia y conserva el resumen de solución.

### Administración existente

- Bandeja global de solicitudes con búsqueda, filtros, estados, plazos y reasignación.
- Bandejas operativas: mis gestiones, mi unidad, aprobaciones y espera del solicitante.
- Consulta de la solicitud, conversación, archivos, estado y recorrido del flujo.
- Toma de gestiones disponibles de una unidad.
- Formularios de gestión y decisiones de aprobar, rechazar, completar o pedir información.

## Experiencia objetivo

Cuando una solicitud pasa, por ejemplo, de una coordinación a otra unidad, el nuevo responsable debe abrir una sola pantalla y encontrar:

1. **Qué debe hacer ahora:** etapa actual, instrucciones, plazo, requisitos, formulario y decisiones permitidas.
2. **Qué se solicitó:** número, servicio, solicitante, asunto, descripción y respuestas del formulario inicial.
3. **Qué ocurrió antes:** línea de tiempo de etapas, responsable, resultado, observación, fechas, respuestas y archivos de cada gestión.
4. **Qué sigue:** próximos destinos posibles, expresados en lenguaje comprensible sin exponer códigos técnicos.
5. **Trazabilidad transversal:** conversación, notas internas, historial de estados y adjuntos generales.

Las etapas anteriores son de solo lectura. Una persona únicamente puede editar la gestión activa que le corresponde.

## Brechas pendientes

### Prioridad alta

- Convertir el modal actual en un espacio de gestión integrado y amplio, con la gestión actual como acción principal y el recorrido como contexto.
- Mostrar nombres de etapa, unidad y responsable; hoy el contrato operativo expone principalmente código e identificadores.
- Exponer en modo lectura las respuestas de formularios de gestiones completadas.
- Mostrar los archivos agrupados por gestión y diferenciar archivos internos de visibles al solicitante.
- Mostrar las rutas siguientes posibles antes de confirmar una decisión.
- Actualizar inmediatamente las bandejas después de tomar o completar una gestión.
- Validar la existencia real de adjuntos obligatorios; el cliente no debe afirmar que existe un archivo sin comprobarlo.

### Prioridad media

- Indicadores de SLA por gestión, no solo por solicitud.
- Historial funcional unificado de asignaciones, decisiones y cambios de estado.
- Una única vista general de operación, adaptada por permisos y alcance de datos, sin crear pantallas distintas por cargo o rol.
- Acciones masivas y métricas operativas.
- Verificación visual autenticada y pruebas de concurrencia contra Dataverse de Pruebas.

## Fases de implementación

1. **Recorrido operativo confiable:** corregir acciones en curso, refresco de bandejas, validación de archivos y presentación clara de resultados anteriores.
2. **Expediente completo:** ampliar el contrato de lectura con nombres, respuestas y adjuntos por gestión.
3. **Pantalla de gestión:** sustituir la superposición de paneles por una vista integrada con pestañas Resumen, Gestión actual, Recorrido, Conversación y Archivos.
4. **Decisiones guiadas:** anticipar destinos y explicar qué ocurrirá al aprobar, rechazar, completar o devolver.
5. **Supervisión:** SLA, métricas, reasignaciones, auditoría y validación autenticada integral.

## Primera fase iniciada

- Las gestiones disponibles y en curso permiten continuar su atención.
- La bandeja operativa se refresca después de tomar o completar una gestión.
- El cliente informa `hasRelatedFile` solamente cuando existe un adjunto asociado a la gestión o se cargó uno desde el formulario de etapa.
- El recorrido diferencia estado, resultado, fechas y observaciones de las gestiones anteriores.

## Segunda fase iniciada

- El contrato del expediente devuelve el título comprensible del formulario de etapa.
- Cada gestión identifica la unidad y la persona responsable por nombre.
- Las respuestas ya guardadas se muestran dentro de la etapa correspondiente, incluidas opciones y valores Sí/No explícitos.
- Los archivos se agrupan por la gestión que los originó.
- La bandeja Aprobaciones filtra en Dataverse únicamente etapas que requieren decisión, antes de aplicar el límite de resultados.
- Los nombres de responsables y las respuestas internas solo se entregan a usuarios con acceso administrativo; no se filtran al portal del solicitante.

## Tercera fase iniciada

- El expediente administrativo usa un espacio de trabajo de ancho amplio en lugar de dos paneles superpuestos.
- Se organiza en Resumen, Gestión actual, Recorrido, Conversación y Archivos.
- Gestión actual incluye etapas disponibles, en curso y en espera del solicitante.
- Resumen conserva los cambios administrativos de estado separados de las decisiones propias de cada etapa.
- Conversación permite publicar respuestas y notas internas sin abandonar el expediente.

## Cuarta fase iniciada

- La API entrega, por cada resultado permitido, las etapas que se activarán y su condición de etapa final.
- Antes de confirmar una decisión, el gestor ve en lenguaje claro qué ocurrirá y hacia dónde continuará el recorrido.
- Solicitar información explica que la gestión quedará en espera del solicitante.
- Aprobar, rechazar, completar, reasignar, comentar, tomar una gestión, adjuntar y cambiar el estado producen una confirmación visible.
- Los errores y datos obligatorios se presentan con mensajes específicos, conservando el contexto del expediente.
- Mientras una acción se guarda, sus controles quedan bloqueados y muestran su estado de procesamiento.

## Quinta fase iniciada

- Cada gestión expone la meta de días hábiles configurada en su etapa.
- Las tarjetas activas muestran días transcurridos, tiempo restante y una alerta destacada cuando la meta fue superada.
- Las etapas completadas conservan una lectura histórica del cumplimiento de su meta.
- La fecha objetivo excluye sábados, domingos y los días no laborables activos configurados en Dataverse, por lo que el indicador corresponde al calendario institucional.
- Los controles principales incluyen estados de interacción y mantienen separados los resultados del flujo de los cambios administrativos de la solicitud.
- La cabecera de la única vista general resume gestiones a cargo, carga de las unidades autorizadas, decisiones pendientes y solicitudes esperando respuesta.
- Cada indicador abre directamente su bandeja correspondiente y el panorama puede actualizarse sin recargar la aplicación.
- La carga disponible se agrupa por unidad organizacional para facilitar la supervisión y el reparto de trabajo.
- El expediente incorpora un panel único de actividad con cambios de estado, asignaciones, decisiones, comentarios, archivos y eventos del flujo.
- Cada movimiento identifica fecha, actor, origen y visibilidad para diferenciar información pública de notas o acciones internas.
- La lectura del historial respeta el acceso: la administración recibe la trazabilidad completa y el solicitante únicamente movimientos marcados como visibles.
- Las bandejas priorizan primero las gestiones vencidas y después las fechas objetivo más próximas.
- Cada tarjeta usa el nombre comprensible de la etapa, unidad responsable, estado y fecha objetivo institucional, dejando el código técnico fuera de la información principal.
- El panorama evita duplicar una gestión presente en varias bandejas al calcular el total de metas vencidas.
- Quien tenga permiso de reasignación puede redistribuir una etapa activa sin cambiar el responsable general de la solicitud.
- La etapa puede asignarse a una persona concreta o regresar a la bandeja disponible de su unidad; el motivo es obligatorio y queda en la trazabilidad.
- El formulario de reasignación muestra la asignación actual, bloquea acciones durante el guardado y confirma visualmente el resultado.
- La búsqueda de responsables muestra únicamente personas con asignación organizacional vigente en la unidad de la etapa.
- La API valida nuevamente esa pertenencia para impedir asignaciones inconsistentes aunque se intente modificar la solicitud manualmente.
