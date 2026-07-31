# Prompt maestro para iniciar en Codex

Analiza y continúa el desarrollo de la intranet de Fundación Gaia Amazonas usando este repositorio y los documentos `.md` adjuntos como especificación inicial.

También recibirás:

- El archivo `RelacionEquipos.xlsx`.
- Una imagen del organigrama consolidado.
- Imágenes de las pantallas proyectadas para la intranet.
- El toolkit visual y organizacional de Gaia Amazonas.

## Forma de trabajo obligatoria

1. No empieces escribiendo código.
2. Inspecciona primero todo el repositorio: stack, estructura, dependencias, base de datos, autenticación, permisos, componentes, estilos, pruebas y estado de ejecución.
3. Lee los archivos `00` a `11` en orden.
4. Contrasta la especificación con el código y con los insumos suministrados.
5. Entrega un diagnóstico preciso que contenga:
   - Arquitectura encontrada.
   - Funcionalidad ya existente.
   - Diferencias contra la especificación.
   - Riesgos de modificación.
   - Modelo de datos actual y modelo objetivo.
   - Elementos reutilizables.
   - Decisiones pendientes.
   - Plan de implementación por fases.
6. No reemplaces tecnologías existentes ni reestructures el proyecto completo sin justificación y autorización.
7. No tomes el Excel como modelo de datos literal. Es una fuente operativa y de migración.
8. Mantén separados los siguientes conceptos:
   - Tercero o persona.
   - Vinculación.
   - Asignación organizacional.
   - Cargo.
   - Rol.
   - Unidad organizacional.
   - Producto.
   - Elemento físico.
   - Movimiento.
   - Asignación de inventario.
   - Acta.
9. Implementa reglas críticas en backend y base de datos, no solo en la interfaz.
10. Conserva auditoría e historial.
11. No uses nulos con significado implícito, como `estado vacío = activo`, en el modelo final.
12. No migres valores ambiguos sin reportarlos.
13. Usa el toolkit visual proporcionado y evita duplicar colores o estilos rígidos.
14. Añade pruebas automatizadas para invariantes y flujos críticos.
15. Documenta cada migración, endpoint y decisión relevante.

## Primera respuesta esperada

Entrega únicamente el diagnóstico y el plan. Incluye los archivos del repositorio que revisaste y las preguntas estrictamente necesarias para resolver decisiones bloqueantes. No generes todavía migraciones destructivas ni una reescritura completa.

## Primera implementación sugerida después de aprobar el diagnóstico

Construye una entrega vertical del módulo organizacional:

- Tipos de unidad.
- Unidades jerárquicas.
- Validación de ciclos.
- Administración mediante API y pantalla.
- Organigrama dinámico.
- Auditoría.
- Permisos.
- Pruebas.

Antes de ejecutar cualquier cambio destructivo, muestra el plan de migración y el impacto esperado.
