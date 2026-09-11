# Capacitaciones

## Objetivo

Capacitaciones administra contenidos formativos versionados, audiencias, asignaciones, progreso y evaluaciones. La capacitación funciona como encabezado estable; cada publicación conserva una versión inmutable para no alterar asignaciones históricas.

## Alcance acordado

El diseño toma como fuente el modelo de Dataverse y los diagramas funcional y relacional entregados. En esta etapa no incluye certificados, contratos, inducción, correo o Teams, sesiones presenciales, asistencia, banco global de preguntas, firma, geolocalización, gamificación ni reglas condicionales avanzadas.

## Convenciones actuales de interfaz

- La edición sigue cuatro pasos: Contenido, Evaluación, Participantes y Revisión/publicación.
- Las secciones de contenido empiezan expandidas, se contraen individualmente y muestran hover. `Agregar contenido` es una acción principal Gaia.
- Las evaluaciones y preguntas empiezan expandidas y pueden contraerse de forma independiente.
- La selección de participantes se presenta como **grupos y excepciones**, no como reglas técnicas. El listado efectivo se pagina y permite excluir una persona con motivo sin eliminarla de Dataverse.
- Una persona excluida permanece visible con estado **Excluida**, motivo y acción **Incluir**. La tabla permite filtrar Todos, Incluidos y Excluidos; revertir una exclusión retira su excepción individual, nunca elimina a la persona.
- Las unidades organizacionales se buscan por código o nombre, conservan la jerarquía visual y se ordenan ascendentemente por código tanto en Participantes como al editar una capacitación.
- Revisión y publicación usa tabla en escritorio y tarjetas operables en móvil; nunca obliga a desplazamiento horizontal para encontrar la acción.
- `Gestionar` usa la variante principal o secundaria del toolkit, nunca un botón genérico con borde negro.
- Selectores de versión y barras de acciones deben ocupar como máximo el ancho disponible y reorganizarse en móvil.
- El estándar completo de botones, tokens, hover, foco y responsive está en `Md/10_INTERFAZ_Y_EXPERIENCIA.md` y es obligatorio para cualquier chat o desarrollador.
- Las preguntas de selección muestran letras automáticas `a), b), c)…` durante su configuración. **Selección única con botones** presenta radios; **Lista desplegable** presenta un selector y admite una sola respuesta; **Sí o No** genera y conserva únicamente esas dos opciones.
- Texto corto y Texto largo no almacenan opciones y muestran una vista previa del control final. Escala exige mínimo menor que máximo y etiquetas comprensibles para ambos extremos.
- El contenido de tipo Video ofrece dos orígenes explícitos: archivo MP4/WebM/MOV almacenado en SharePoint (máximo 250 MB) o enlace público de YouTube. Los enlaces de YouTube se validan y se previsualizan antes de guardar; nunca se tratan como archivos adjuntos.
- Las eliminaciones son bajas lógicas; toda lectura debe filtrar `statecode eq 0` para impedir que Dataverse vuelva a presentar registros retirados.

## Publicación y aprobación

En la versión de trabajo se define si la capacitación requiere revisión previa. Si no la requiere, el borrador validado puede publicarse directamente. Si la requiere, el flujo es Borrador → En revisión → Publicada, con posibilidad de devolución a borrador. La publicación valida contenido, evaluación y participantes, registra trazabilidad, vuelve inmutable la versión y genera las asignaciones.

El modelo vigente contiene 16 tablas: categoría, capacitación, versión, destinatario, sección, recurso, evaluación, pregunta, opción, bloque, asignación, progreso, intento, respuesta, respuesta-opción e historial.

## Navegación de AdminCore

- El menú principal muestra un único acceso: Capacitaciones.
- Dentro del módulo, Catálogo, Seguimiento y Resultados son vistas globales.
- Contenido, versiones, evaluación y Audiencia aparecen como pestañas de la capacitación seleccionada.
- Las categorías se administran en una vista independiente dentro del Catálogo, sin reducir el ancho de su tabla.
- Los permisos y endpoints permanecen separados aunque la navegación sea más compacta.

## Fases cortas de implementación

1. Base técnica, permisos, navegación y lectura del catálogo.
2. Gestión de categorías y encabezado de capacitación.
3. Creación, edición y duplicación de versiones.
4. Constructor de secciones, bloques y recursos.
5. Constructor de evaluación, preguntas y opciones.
6. Audiencias y vista previa de destinatarios.
7. Revisión, devolución, aprobación y publicación.
8. Generación de asignaciones y experiencia inicial del participante.
9. Progreso, intentos, respuestas y resultados.
10. Exportación, pruebas de permisos, accesibilidad y endurecimiento.

Cada fase debe cerrar con validaciones frontend y .NET antes de avanzar. La publicación de una versión debe generar asignaciones a partir de una audiencia resuelta y conservar instantáneas suficientes para que los cambios futuros no modifiquen solicitudes activas.

## Estado actual

Las fases 1 a 7 incorporan el proyecto modular `Gaia.Modules.Training`, permisos, navegación y administración conectada a Dataverse. Categorías, encabezados y versiones ya se gestionan desde AdminCore. El constructor permite organizar secciones y bloques de título, texto, destacados y enlaces externos, respetando el orden y bloqueando cambios fuera de borrador. También permite crear evaluaciones de conocimiento, diagnóstico o satisfacción, configurar intentos, tiempo, aprobación y retroalimentación, y construir preguntas con sus opciones y puntajes. Los códigos técnicos de las opciones se generan en backend. La administración de audiencias admite inclusión y exclusión de toda la organización, unidades con subunidades y personas específicas; la API calcula una vista previa efectiva con base en asignaciones organizacionales activas. El flujo editorial permite enviar a revisión, devolver con motivo, aprobar y publicar, cerrar y archivar. Las transiciones se autorizan y validan en la API, la publicación exige contenido y audiencia mínima, y cada cambio se registra en el historial funcional.

La base operativa de las fases 8 y 9 ya genera asignaciones al publicar, evita duplicados por versión y participante, calcula el vencimiento según la regla congelada de la versión y permite resincronizar una audiencia publicada. AdminCore dispone de seguimiento con filtros, avance, vencimientos e intentos, y de resultados consolidados con finalizaciones, aprobación y promedio. La carga binaria de imágenes, video, audio y documentos queda separada para conectarla al almacenamiento institucional sin mezclar metadatos incompletos. No se ejecutó aprovisionamiento ni se modificó Dataverse durante el desarrollo.

La primera fase de experiencia del participante incorpora `Mis capacitaciones` en la Intranet. La API limita cada consulta a las asignaciones del tercero autenticado, entrega la estructura publicada y registra bloques completados en `gaia_progresobloque`. El avance se recalcula en backend, se respeta el orden secuencial y una capacitación sin evaluación obligatoria queda aprobada al completar sus bloques; si existe evaluación obligatoria queda pendiente de evaluación. La presentación, envío y calificación de evaluaciones sigue siendo la siguiente fase funcional.

## Ajuste pendiente del modelo

Los archivos de modelo y aprovisionamiento existentes contienen varios nombres visibles de columnas fragmentados (por ejemplo, letras separadas), aunque los nombres lógicos y de esquema son utilizables. Antes de una validación funcional deben corregirse las etiquetas en los artefactos fuente y, si las tablas ya existen, actualizarse de manera controlada en Dataverse. También debe conservarse como fuente de verdad el total de 16 tablas, evitando referencias antiguas a 14.

## Reglas técnicas

- Resolver siempre entity sets, atributos y navegaciones mediante metadatos de Dataverse.
- Proteger los endpoints en backend; ocultar opciones en frontend no sustituye autorización.
- No editar una versión publicada: duplicarla como borrador de la siguiente versión.
- Mantener este módulo aislado de Helpdesk y del resto de módulos, salvo contratos explícitos.
