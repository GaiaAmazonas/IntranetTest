# Helpdesk — levantamiento funcional previo a implementación

## Estado confirmado

Helpdesk será el espacio de autoservicio de los colaboradores dentro de la Intranet Gaia. Debe permitir crear solicitudes, consultar las propias, atender devoluciones o correcciones y revisar su trazabilidad.

El prototipo histórico contiene una representación visual con solicitudes y estados ficticios. No constituye una definición funcional ni autoriza la creación de tablas, estados, responsables o automatizaciones.

En la solución actual solamente existen:

- la ruta protegida `/intranet/helpdesk`;
- el permiso `INT.HELPDESK.VER`;
- la navegación condicional por permiso;
- una pantalla informativa sin datos simulados.

No existe todavía un módulo Helpdesk de backend, API, almacenamiento en Dataverse ni proceso aprobado.

## Decisiones obligatorias antes de diseñar el modelo

### 1. Alcance inicial

Definir cuáles solicitudes entrarán en la primera versión. Para cada una se requiere:

- nombre y objetivo;
- área responsable;
- quién puede crearla;
- campos obligatorios y opcionales;
- documentos adjuntos requeridos;
- reglas de validación;
- tiempos esperados de atención;
- resultado o documento que recibe el solicitante.

No se recomienda comenzar con un motor genérico. La primera versión debe resolver de extremo a extremo entre uno y tres trámites reales y frecuentes.

### 2. Participantes

Definir las responsabilidades reales:

- solicitante;
- responsable de revisión;
- aprobador, si aplica;
- ejecutor o área resolutora;
- observadores o personas notificadas;
- administrador funcional del catálogo.

Debe precisarse si la asignación se realiza por persona, cargo, unidad organizacional o una combinación.

### 3. Ciclo de vida

Los estados deben surgir de los trámites seleccionados. Como referencia de análisis, no como definición aprobada, se debe determinar si son necesarias acciones equivalentes a:

- guardar borrador;
- radicar;
- devolver para corrección;
- corregir y reenviar;
- aprobar o rechazar;
- iniciar atención;
- finalizar;
- cancelar.

Cada transición debe establecer quién puede ejecutarla, desde qué estado, qué información exige y qué evento de auditoría genera.

### 4. Trazabilidad

Cada solicitud deberá conservar como mínimo:

- identificador legible;
- autor y fecha de creación;
- fecha de radicación;
- estado actual;
- historial de estados;
- actor, fecha y comentario de cada acción;
- modificaciones relevantes;
- archivos adjuntos y sus versiones;
- responsable vigente;
- fecha de cierre y resultado.

El historial debe ser inmutable desde la interfaz funcional.

### 5. Datos y privacidad

Antes de crear tablas se debe clasificar la información capturada: institucional, personal, sensible o reservada. También se debe definir:

- quién puede ver cada solicitud;
- si el jefe o la unidad puede consultar solicitudes de otras personas;
- política de conservación;
- tamaño y tipos de archivo permitidos;
- campos que no deben aparecer en listados, notificaciones o registros técnicos.

### 6. Integraciones y notificaciones

Confirmar si la primera versión requiere:

- correo institucional;
- Teams;
- Power Automate;
- generación de PDF;
- firma o aprobación Microsoft;
- Power BI;
- integración financiera, documental o de talento humano.

Estas integraciones no deben asumirse solamente porque Microsoft 365 esté disponible.

## Seguridad propuesta para evaluar

La capacidad general `INT.HELPDESK.VER` controla la entrada a Helpdesk. Las operaciones reales requerirán permisos explícitos separados, cuyos códigos se aprobarán junto con el alcance, por ejemplo crear solicitudes propias, consultar solicitudes propias, corregir solicitudes propias, gestionar solicitudes asignadas y administrar catálogos.

Los permisos no deben depender de nombres de roles ni limitarse a ocultar botones: rutas y endpoints deberán aplicar las mismas reglas.

## Arquitectura recomendada

Cuando se aprueben los requerimientos, Helpdesk debe incorporarse como un módulo del monolito modular existente, con contratos, endpoints, servicios y adaptadores propios. El frontend reutilizará autenticación, autorización, cliente API, feedback y Design System actuales.

Dataverse puede utilizarse como almacenamiento si el modelo aprobado encaja con sus relaciones, auditoría, archivos y volumen. La decisión se tomará después de definir los trámites y revisar las tablas existentes; no se crearán tablas preventivas.

## Primera entrega recomendada

Seleccionar un trámite real, frecuente y de complejidad controlada. Implementar verticalmente:

1. creación y validación;
2. radicación;
3. bandeja `Mis solicitudes`;
4. detalle y trazabilidad;
5. devolución y corrección, si el trámite la necesita;
6. atención y cierre;
7. autorización, auditoría y pruebas.

Después se evaluará qué elementos son reutilizables para el segundo trámite. Esto evita diseñar un motor abstracto basado en supuestos.

## Información que debe entregar el responsable funcional

Para iniciar la implementación se necesita responder:

1. ¿Cuáles son los primeros uno a tres trámites que debe manejar Helpdesk?
2. ¿Cómo se gestionan hoy y quién es responsable de cada uno?
3. ¿Qué formulario, Excel, correo o documento utilizan actualmente?
4. ¿Qué campos y anexos exige cada trámite?
5. ¿Quién revisa, aprueba, ejecuta y cierra?
6. ¿Cuándo se devuelve, rechaza, cancela o corrige?
7. ¿Qué estados y tiempos utilizan realmente hoy?
8. ¿Quién puede ver las solicitudes además del solicitante?
9. ¿Qué notificaciones son indispensables?
10. ¿Qué resultado debe recibir el colaborador al finalizar?

## Criterio de inicio

No se iniciará la creación de tablas, endpoints ni formularios hasta aprobar al menos un trámite con sus campos, participantes, transiciones, visibilidad y resultado. La maqueta histórica se conservará únicamente como referencia de composición visual.
