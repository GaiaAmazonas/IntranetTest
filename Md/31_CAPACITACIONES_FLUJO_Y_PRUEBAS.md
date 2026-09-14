# Capacitaciones: administración y participación

## Flujo del administrador

1. Crea la ficha de capacitación en Catálogo y elige categoría, unidad y responsable funcional.
2. Crea una versión en borrador: título público, objetivo, disponibilidad y fecha límite individual o general.
3. Agrega secciones y materiales. Se pueden editar o retirar solo mientras la versión esté en borrador.
4. Configura evaluaciones y encuestas. Selección única y Sí/No usan radios; Lista desplegable permite una sola opción; texto y escala usan sus controles correspondientes.
5. Define participantes mediante grupos y excepciones. Esta vista previa no es una asignación.
6. Abre Vista previa en Revisión y publicación; revisa orden, textos, documentos, videos y preguntas.
7. Publica: sin revisión editorial, usa Publicar capacitación. Con Requiere revisión antes de publicar activo, usa Enviar a revisión. Una cuenta con CAP.PUBLICAR podrá aprobar y publicar; CAP.REVISAR permite devolver a borrador con un motivo.
8. La publicación crea asignaciones. Si esa parte falla después de publicarse, usa Sincronizar asignaciones en Seguimiento. No dupliques la versión para recuperarla.

La revisión editorial no es la aprobación del aprendizaje. Activar revisión no obliga a tener un examen. Cada evaluación que define la aprobación sí debe tener preguntas calificables.

La revisión no envía correos ni elige un aprobador específico: las personas autorizadas encuentran las versiones en AdminCore. No presentar ese flujo como una notificación automática.

## Configuración de evaluaciones

- Obligatoria: debe responderse para finalizar, incluso si no cuenta para la nota.
- Define la aprobación: debe alcanzar su mínimo. El mínimo propio prevalece; si está vacío se usa el mínimo general de la versión, o 70% si tampoco está configurado.
- Puntaje máximo: se ponderan los puntos de las preguntas, no la cantidad de preguntas. Sin puntos explícitos, una pregunta calificable usa al menos un punto. Una opción con puntaje explícito conserva ese puntaje, limitado al máximo de la pregunta.
- Texto con revisión manual: el revisor asigna puntos. Activa Requiere revisión manual y configura el máximo. Una escala no calificable recoge opinión, no genera una nota.
- Máximo de intentos: incluye el primero; vacío significa sin límite. Permitir reintento debe estar activo para repetir. Un intento aprobado o pendiente de revisión no se repite.
- Tiempo: empieza al iniciar y no se pausa al salir. El servidor controla el vencimiento. Un envío fuera de tiempo conserva las respuestas, pero no aprueba y recibe cero puntos.
- Aleatorización: el orden es estable al reanudar el mismo intento y cambia entre intentos.
- Mostrar resultado, respuestas correctas y retroalimentación: se aplican a intentos calificados, nunca al cuestionario inicial. Activar respuestas correctas con reintentos revela las respuestas para intentos posteriores; úsalo deliberadamente.
- Encuesta obligatoria de versión: exige una evaluación de Satisfacción respondida. La API impide publicar una versión que exija encuesta sin tenerla configurada.

## Experiencia del participante

- Inicio incluye un resumen y acceso a la próxima capacitación pendiente. Un error de carga no se muestra como ausencia de asignaciones.
- Mis capacitaciones tiene búsqueda, paginación, pendientes e historial, y acceso desde el menú.
- El calendario muestra la fecha límite de las asignaciones pendientes; no crea eventos duplicados en Dataverse. Las asignaciones sin fecha límite se consultan en Mis capacitaciones.
- Las secciones se abren por defecto y permiten contraer/expandir individualmente o todas.
- El recorrido secuencial bloquea el material hasta completar el obligatorio anterior. La API también valida el orden.
- Los videos usan reproductor HTML5 o la [API oficial de YouTube](https://developers.google.com/youtube/iframe_api_reference). La visualización mínima es opcional y se configura en el contenido de video; saltar posiciones no equivale a haber visto esas partes.
- La visualización se mide en el navegador, no es una prueba antifraude de atención. Al confirmar se guardan el porcentaje informado y el tiempo. Una recarga puede requerir volver a visualizar si aún no se confirmó.
- Los documentos se abren o descargan mediante una ruta autorizada de la API; un participante no puede solicitar archivos de otra asignación.
- Los borradores de respuesta se conservan en sessionStorage por asignación e intento, cuando el navegador lo permite. Solo el envío persiste respuestas en Dataverse; cerrar la sesión/pestaña puede perder un borrador no enviado.
- Si una respuesta abierta requiere calificación, el intento queda Pendiente de revisión. En AdminCore → Resultados se califican las respuestas; requiere CAP.REVISAR y acceso a Resultados.
- La capacitación finaliza al completar el material obligatorio, las evaluaciones obligatorias, aprobar las que definen aprobación y responder la encuesta exigida. Un 100% de contenido no significa por sí solo capacitación aprobada.

## Datos y seguridad

Se usan las tablas existentes gaia_intentoevaluacion, gaia_respuestapregunta y gaia_respuestaopcion; no una memoria temporal del servidor. El servidor calcula puntos y valida pertenencia, preguntas, opciones, obligatoriedad, escalas, intentos y tiempos. Las respuestas y el cierre del intento se guardan en un changeset transaccional; se usa ETag para impedir envíos/revisiones concurrentes.

No se agregaron permisos nuevos. No ejecutar scripts de provisión contra el Dataverse compartido sin autorización. El despliegue necesita las tablas y claves del modelo publicadas y permisos de lectura/escritura adecuados; SharePoint requiere su configuración de almacenamiento independiente.

Los archivos MP4/PDF remotos de hasta 250 MB usan un archivo temporal acotado para soportar peticiones de rango; se elimina automáticamente al cerrar el flujo. No se borran los archivos originales.

## Prueba funcional después de reiniciar API y frontend

1. Publica un borrador sin revisión, con dos secciones, un documento, un video y una evaluación calificable.
2. Entra como una persona incluida; comprueba Inicio, menú Mis capacitaciones y calendario.
3. Verifica contraer/expandir, orden secuencial, documento y video. Si fijaste mínimo de video, intenta confirmar antes de verlo y después de alcanzar el mínimo.
4. Responde una lista y confirma que solo permite una opción. Envía respuestas incorrectas, consulta la nota y repite dentro del máximo.
5. Aprueba la evaluación; verifica finalización e historial después de recargar.
6. Prueba una pregunta abierta manual: el participante debe quedar pendiente; califica desde Resultados con otra cuenta autorizada y verifica la finalización.
7. Prueba una encuesta obligatoria y un intento con tiempo. Verifica que no se finalice antes de responder la encuesta y que un intento vencido no apruebe.
8. Activa revisión editorial en otro borrador: publicar directamente debe rechazarse; usa revisión y después una cuenta autorizada para publicar.
9. Revisa búsqueda/paginación, resultados/exportación, escritorio y móvil (390 px). Intenta abrir una asignación o archivo ajeno: debe rechazarse.

Las pruebas automáticas no sustituyen esta prueba con identidades reales y la configuración efectiva de Dataverse/SharePoint. No publicar a GitHub ni al servidor sin solicitarlo.
