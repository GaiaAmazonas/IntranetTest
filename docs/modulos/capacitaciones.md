# Capacitaciones

> Estado: **implementado; requiere validación ambiental integral** · Verificado: **2026-10-09**
> Código principal: `Gaia.Modules.Training`, adaptadores Dataverse y rutas de administración/participación.

## Propósito y modelo

Training administra catálogo, versiones inmutables, contenido, audiencias, asignaciones, progreso, evaluaciones, intentos y resultados. La capacitación es un encabezado estable; cada publicación fija una versión para no alterar la historia de participantes.

El modelo vigente contempla categoría, capacitación, versión, destinatario, sección, recurso, evaluación, pregunta, opción, bloque, asignación, progreso, intento, respuesta, respuesta-opción e historial.

## Administración

1. Crear ficha y seleccionar categoría, unidad y responsable.
2. Crear versión en borrador con objetivo, disponibilidad y vencimiento.
3. Agregar secciones, bloques y materiales.
4. Configurar evaluaciones/encuestas, preguntas y opciones.
5. Resolver participantes mediante grupos y excepciones.
6. Previsualizar contenido y preguntas.
7. Publicar directamente o enviar a revisión según la configuración.
8. Generar asignaciones; si falla la generación posterior, sincronizarlas sin duplicar la versión.

Contenido, evaluación, participantes y revisión/publicación son pasos contextuales de la capacitación seleccionada. Catálogo, Seguimiento y Resultados son vistas globales. Una versión publicada no se edita: se duplica como nuevo borrador.

Cuando una ficha recién creada todavía no tiene versiones, la gestión muestra directamente la acción **Crear primera versión** y mantiene ocultos los pasos que dependen de ella. Al guardar el primer borrador se habilitan Contenido, Evaluación, Participantes y Revisión/publicación; el título público se propone inicialmente a partir del nombre de la capacitación y puede editarse.

El indicador **Capacitación activa** clasifica la vigencia administrativa de la ficha y permite conservar registros retirados sin perderlos. No publica ni oculta por sí solo una capacitación en la Intranet: la experiencia del participante depende de la versión publicada, su vigencia y las asignaciones generadas. Una ficha inactiva continúa siendo administrable y puede recibir su primera versión.

Una capacitación sin versiones puede eliminarse definitivamente con `CAP.CATALOGO.ELIMINAR`. Desde que existe cualquier versión —incluido un borrador— se conserva la ficha y debe inactivarse en lugar de eliminarse, para proteger contenido, publicación, asignaciones, intentos y resultados. La API comprueba esta regla aunque la interfaz oculte o deshabilite la acción.

## Estados editoriales

Sin revisión: Borrador → Publicada. Con revisión: Borrador → En revisión → Publicada, con devolución motivada a Borrador. La revisión editorial no es aprobación del aprendizaje y no genera automáticamente correo ni selecciona un aprobador específico.

La publicación valida contenido y audiencia, registra historia, inmoviliza la versión y crea asignaciones sin duplicados.

## Audiencias

La administración incluye/excluye toda la organización, unidades con descendientes o personas. La vista previa no crea asignaciones. Una exclusión conserva la persona visible, con motivo y opción de revertir; no elimina al tercero. La API calcula el conjunto efectivo con asignaciones organizacionales activas.

## Evaluaciones

- Obligatoria: debe responderse para finalizar.
- Define aprobación: exige el mínimo propio; en su ausencia usa el mínimo general o 70%.
- El puntaje se basa en puntos, no en número de preguntas.
- Texto con revisión manual queda pendiente hasta que una persona con `CAP.REVISAR` califique.
- Una escala no calificable recoge opinión.
- Máximo de intentos incluye el primero; vacío significa sin límite.
- Un intento aprobado o pendiente de revisión no se repite.
- El servidor controla el tiempo y un envío vencido conserva respuestas, pero no aprueba.
- La aleatorización es estable dentro de un intento y puede cambiar entre intentos.
- Resultados, respuestas correctas y retroalimentación solo se muestran según configuración después de calificar.
- Exigir encuesta obliga a disponer de una evaluación de Satisfacción.

Tipos de presentación respetados: selección única con radios, lista desplegable única, Sí/No, texto y escala. Los códigos de opciones se generan en backend.

## Participante

`Mis capacitaciones` ofrece búsqueda, paginación, pendientes, historial y acceso a una asignación propia. Inicio y calendario consumen la misma asignación; un error no se representa como “sin capacitaciones”.

- Contenido secuencial bloquea material obligatorio previo tanto en UI como en API.
- Documentos se sirven por una ruta autorizada; no se accede a archivos de otra asignación.
- PDF, Word, Excel y PowerPoint se visualizan dentro del recorrido y de la previsualización administrativa mediante una URL temporal del visor de Microsoft 365. La URL se solicita solamente después de autorizar la asignación o el permiso de contenido, no sustituye la referencia estable y no se persiste.
- La descarga del archivo original es una acción independiente de la visualización.
- Videos usan archivo institucional o YouTube; la visualización informada no constituye control antifraude.
- Borradores de respuestas pueden vivir en `sessionStorage`; solo el envío persiste en Dataverse.
- Una capacitación finaliza al completar contenido obligatorio, evaluaciones requeridas, aprobaciones y encuesta exigida.
- 100% de contenido no implica por sí solo aprobación.

## Persistencia y concurrencia

Intentos, respuestas y opciones se guardan en Dataverse. El servidor valida pertenencia, preguntas, opciones, obligatoriedad, rangos, intentos y tiempo. El envío usa un changeset y ETag para impedir cierres o revisiones concurrentes.

Los recursos binarios usan la infraestructura compartida de archivos. Archivos grandes se transfieren con almacenamiento temporal acotado y soporte de rango; el temporal se elimina al finalizar y no sustituye el archivo de SharePoint.

## Seguridad

Las familias `CAP.*` separan lectura de catálogo, administración, eliminación de fichas sin uso, contenido, publicación, revisión y participación. La API limita las consultas personales al tercero autenticado. Ocultar una acción no reemplaza autorización.

## Prueba funcional mínima

1. Publicar una versión con dos secciones, documento, video y evaluación.
2. Verificar aparición en Inicio, Mis capacitaciones y calendario.
3. Probar orden secuencial, visor embebido de PDF/DOCX/XLSX/PPTX, descarga autorizada del original y condición mínima de video.
4. Fallar y aprobar una evaluación respetando intentos.
5. Calificar una respuesta abierta con otra cuenta autorizada.
6. Probar encuesta obligatoria y tiempo vencido.
7. Probar revisión editorial y devolución.
8. Verificar búsqueda, resultados, exportación, escritorio/móvil y rechazo de recursos ajenos.

## Límites y pendientes

- No incluye certificados, correo/Teams, sesiones presenciales, asistencia, banco global, firma, geolocalización ni gamificación.
- Deben validarse etiquetas publicadas y el conjunto real de tablas/keys en el ambiente.
- Las pruebas automáticas no sustituyen identidades, permisos y archivos reales.
