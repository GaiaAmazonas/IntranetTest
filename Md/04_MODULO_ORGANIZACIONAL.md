# Módulo organizacional

## 1. Fuente de referencia

La hoja `Organizacional` del Excel contiene las columnas:

- Código.
- Nombre.
- Tipo de Unidad.
- Unidad Padre.
- Estado.
- Nivel.

La imagen del organigrama muestra la representación visual consolidada y los grupos cromáticos por tipo de unidad.

## 2. Entidades

### `tipo_unidad`

| Campo | Descripción |
|---|---|
| `id` | Llave técnica. |
| `codigo` | Código interno opcional y único. |
| `nombre` | Directivos, Subdirección, Asesoría Estratégica, Coordinación Directa, Coordinación Transversal u Operativa. |
| `descripcion` | Alcance funcional. |
| `color_token` | Referencia al token visual, no color rígido si existe toolkit. |
| `orden_visual` | Orden de presentación. |
| `estado` | Activo o inactivo. |
| metadatos | Auditoría estándar. |

No crear una tabla adicional `clasificacion_organizacional` mientras no exista una necesidad funcional distinta.

### `unidad_organizacional`

| Campo | Descripción |
|---|---|
| `id` | Llave técnica. |
| `codigo` | Código organizacional visible. |
| `nombre` | Nombre oficial. |
| `nombre_corto` | Opcional para tarjetas o navegación. |
| `tipo_unidad_id` | Tipo de unidad. |
| `unidad_padre_id` | Autorreferencia; nula para raíces. |
| `nivel` | Puede calcularse; si se persiste, debe sincronizarse automáticamente. |
| `descripcion` | Propósito o alcance. |
| `orden_visual` | Orden entre unidades hermanas. |
| `fecha_inicio` | Inicio de vigencia. |
| `fecha_fin` | Fin de vigencia. |
| `estado` | Estado de administración. |
| metadatos | Auditoría estándar. |

## 3. Decisiones de modelado

- El código no debe utilizarse como llave primaria ni como llave foránea.
- `unidad_padre_id` debe apuntar a la llave técnica.
- La jerarquía debe admitir más de cuatro niveles aunque la estructura actual tenga niveles 1 a 4.
- El organigrama debe generarse desde datos, no construirse manualmente en HTML.
- Los colores dependen del tipo de unidad y del toolkit visual.
- Una unidad inactiva debe seguir disponible en consultas históricas.

## 4. Reglas específicas

1. No puede existir un ciclo jerárquico.
2. Una unidad no puede ser su propio padre.
3. El padre debe estar vigente durante la vigencia de la unidad hija, salvo reglas de reorganización formal.
4. No se permite eliminar una unidad con hijas, asignaciones organizacionales o movimientos históricos.
5. El nivel se calcula a partir de la ruta jerárquica.
6. Cambiar una unidad de padre debe registrar auditoría y advertir el impacto histórico.
7. Los códigos deben validarse como texto para no perder ceros ni limitar futuros formatos.
8. Una reorganización no debe reescribir silenciosamente actas o asignaciones históricas.

## 5. Casos que requieren revisión en los datos actuales

- La imagen muestra `Presidencia Ejecutiva` con código `2001`, mientras la hoja suministrada contiene `20` para Presidencia Ejecutiva y `2001` para Equipo Asesor. Codex debe reportar esta diferencia y no elegir una versión sin confirmación.
- La imagen y el Excel deben reconciliarse para códigos, padres y unidades activas.
- Valores como `0` en Unidad Padre son representación de Excel; en base de datos deben convertirse en `NULL` para nodos raíz.

## 6. Interfaces

### Lista administrativa

- Filtros por tipo, estado, nivel y unidad padre.
- Búsqueda por código o nombre.
- Acciones de crear, editar, inactivar y consultar historial.

### Árbol organizacional

- Expansión y contracción de nodos.
- Búsqueda y enfoque del nodo.
- Colores por `tipo_unidad`.
- Indicador de estado.
- Vista de ficha lateral con código, nombre, tipo, padre y responsables vigentes.

### Organigrama consolidado

- Exportación a imagen o PDF, si el stack lo permite.
- Diseño responsivo y navegación por zoom.
- Leyenda basada en tipos de unidad activos.
