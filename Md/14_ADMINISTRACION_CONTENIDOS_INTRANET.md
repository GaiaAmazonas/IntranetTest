# Intranet Gaia — administración de contenidos

## Resultado de la evaluación

La intranet necesita administración editorial, pero la solución actual no contiene un módulo de contenidos, endpoints ni tablas confirmadas en Dataverse. El Home utiliza información marcada explícitamente como referencia visual en `intranet-home.preview.ts`; no debe tratarse como contenido institucional publicado.

No se encontraron fuentes existentes para noticias, eventos o capacitaciones. Los cumpleaños sí tienen una fuente real y no requieren administración editorial: se obtienen de la fecha de nacimiento autorizada del módulo de colaboradores.

## Capacidades que sí requieren administración

### 1. Comunicaciones

Contenido para el bloque `Lo que está pasando` y una futura vista de comunicaciones.

Información mínima a confirmar:

- título y resumen;
- cuerpo o enlace de destino;
- categoría;
- imagen o recurso visual;
- autor o área responsable;
- fecha de publicación y retiro;
- audiencia;
- prioridad o contenido destacado;
- estado editorial;
- adjuntos;
- texto alternativo de imágenes.

### 2. Eventos y capacitaciones

Fuente para el calendario, agenda próxima y actividades institucionales.

Información mínima a confirmar:

- título y descripción;
- categoría;
- inicio y finalización;
- zona horaria;
- modalidad;
- lugar o enlace virtual;
- organizador;
- audiencia;
- cupo e inscripción, si aplican;
- estado y cancelación;
- recurrencia, si realmente se necesita.

Un evento puede ser una capacitación mediante categoría o tipo. No se recomienda crear dos modelos separados antes de confirmar diferencias funcionales reales.

### 3. Catálogo de aplicaciones

El acceso y la autorización ya se apoyan en el catálogo de Seguridad. No debe crearse una segunda fuente para decidir quién puede abrir AdminCore.

Para administrar aplicaciones internas y externas desde AdminCore se debe evaluar la ampliación controlada del registro de módulos existente con datos editoriales que hoy no están confirmados: categoría, descripción corta, icono, URL externa, orden y disponibilidad. La autorización continuará mediante permisos explícitos.

No deben persistirse favoritos hasta definir si son personales y dónde se guardarán. Las aplicaciones frecuentes pueden calcularse posteriormente a partir de uso real o configurarse por usuario, pero no deben simularse con datos globales.

## Capacidades que no requieren un catálogo editorial propio

- Cumpleaños: provienen de Colaboradores y respetan visibilidad.
- Personas: provienen de Terceros y Asignación Organizacional.
- Navegación: proviene del modelo de Seguridad.
- Accesos rápidos: deben dirigir a capacidades existentes; no deben duplicar aplicaciones.
- Identidad visual y bienvenida: pertenecen a configuración del producto, no a una noticia editable.
- Helpdesk: tendrá su propio dominio cuando se aprueben sus requerimientos.
- Documentos: continúa fuera del alcance actual.

## Flujo editorial que debe aprobarse

Antes de construir el CRUD se debe decidir si la Fundación necesita:

1. borrador;
2. revisión;
3. publicación programada;
4. publicación;
5. retiro o archivo;
6. devolución con observaciones.

Si una sola persona crea y publica, puede utilizarse un flujo inicial más simple. No debe implementarse una aprobación compleja sin necesidad comprobada.

## Audiencias

Debe confirmarse si todas las publicaciones son generales o si se segmentarán por sede, unidad, rol o grupos específicos. La segmentación exige relaciones verificables con la estructura organizacional y reglas claras para evitar que contenido reservado aparezca durante la carga.

## Archivos e imágenes

La decisión de almacenamiento debe tomarse con el responsable de infraestructura. Se debe definir:

- Dataverse File/Image, SharePoint u otra fuente corporativa;
- tamaños máximos;
- formatos permitidos;
- análisis de archivos;
- versiones;
- política de conservación;
- permisos de descarga;
- URL estables y acceso autenticado.

No se deben guardar imágenes en campos de texto ni publicar enlaces anónimos sin política institucional.

## Seguridad propuesta para evaluar

La lectura pública interna y la administración deben quedar separadas. La nomenclatura definitiva se aprobará antes de incorporarla al inicializador idempotente. Como capacidades funcionales se necesitarán, al menos:

- consultar contenidos publicados;
- consultar borradores;
- crear;
- actualizar;
- publicar o retirar;
- administrar categorías, únicamente si estas serán configurables.

Los endpoints de lectura de la intranet deberán devolver exclusivamente contenido publicado, vigente y autorizado para la audiencia del usuario.

## Ubicación en AdminCore

Se recomienda incorporar posteriormente un módulo `Comunicaciones` dentro de AdminCore, sin mezclarlo con Tecnología ni Seguridad. Su primera navegación podría contener:

- Publicaciones;
- Eventos;
- Categorías, solo si son administrables;
- Aplicaciones, reutilizando el registro de Seguridad cuando se aprueben sus metadatos adicionales.

La intranet seguirá siendo una experiencia de consumo y no ofrecerá controles editoriales aunque el usuario sea administrador.

## Secuencia recomendada de implementación

1. Confirmar responsables editoriales y flujo de publicación.
2. Confirmar campos, audiencias y almacenamiento de imágenes.
3. Auditar en Dataverse si ya existen tablas equivalentes.
4. Presentar el modelo físico y las relaciones antes de modificar Dataverse.
5. Crear módulo, contratos y adaptadores.
6. Implementar administración en AdminCore.
7. Implementar endpoints de lectura para la intranet.
8. Reemplazar completamente `intranet-home.preview.ts`.
9. Probar programación, vencimiento, autorización, responsive y accesibilidad.

## Decisiones pendientes del responsable funcional

1. ¿Quién crea y quién autoriza comunicaciones?
2. ¿Se necesita aprobación o basta crear/publicar?
3. ¿Qué categorías reales se utilizarán?
4. ¿Se publicará para todos o por audiencias?
5. ¿Dónde se almacenan hoy imágenes y documentos institucionales?
6. ¿Los eventos provienen de un calendario Microsoft existente?
7. ¿Se requiere inscripción a capacitaciones?
8. ¿Quién puede cancelar o modificar eventos publicados?
9. ¿Qué aplicaciones externas están aprobadas y cuáles son sus URL?
10. ¿Debe conservarse historial de versiones visible para auditoría?

## Criterio de cierre

La Fase 8 de análisis queda cerrada con la identificación de capacidades y dependencias. La implementación administrativa permanece condicionada a estas decisiones y a la auditoría previa de Dataverse. No se crean tablas ni datos a partir del prototipo visual.
