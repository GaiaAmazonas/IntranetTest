# Módulo de inventarios

## 1. Lectura del Excel actual

### Hoja `Productos`

Catálogo de productos con ID, clase, subcategoría, elemento, clasificación `Alta/Baja` y estado. El estado vacío se ha interpretado operativamente como activo, pero esta regla debe formalizarse durante la migración.

### Hoja `Marcas`

Catálogo de marcas existentes.

### Hoja `RegistroElementos`

Contiene unidades físicas con producto, marca, modelo, serial, condición, observaciones, centro de costos, financiador, valor y estado de disponibilidad.

### Hoja `Asignaciones`

Contiene la relación histórica o proyectada entre elementos y terceros. Incluye datos descriptivos redundantes que deben normalizarse.

### Hoja `ActaEntrega`

Plantilla documental para entrega de recursos tecnológicos.

### Hoja `Resumen`

Vista agregada del inventario asignado por área, colaborador y clase. En la intranet debe construirse como reporte, no como tabla maestra.

## 2. Catálogo de productos

### `clase_producto`

Ejemplos actuales: Equipos de Cómputo, Periféricos y Accesorios, Redes y Telecomunicaciones, Equipos Audiovisuales.

### `categoria_producto`

Corresponde a la subcategoría actual: Cómputo, Entrada, Almacenamiento, Comunicación, Video, entre otras. Debe depender de una clase.

### `producto`

| Campo | Descripción |
|---|---|
| `id` | Llave técnica. |
| `codigo` | ID visible actual, por ejemplo 1001. |
| `clase_producto_id` | Clase. |
| `categoria_producto_id` | Categoría o subcategoría. |
| `nombre` | Portátil, mouse, GPS, etc. |
| `nivel_control_inventario` | Reemplaza el nombre ambiguo `Categoría` para valores Alta/Baja. |
| `requiere_serial` | Regla por producto. |
| `requiere_placa` | Regla por producto. |
| `es_consumible` | Distingue activos unitarios de consumibles. |
| `vida_util_referencial` | Opcional, no implica depreciación contable automática. |
| `estado` | Activo o inactivo. |
| metadatos | Auditoría. |

La clasificación Alta/Baja debe documentarse con una definición funcional definitiva. No debe confundirse con alta o baja física del activo.

## 3. Elemento físico

### `elemento_inventario`

| Campo | Descripción |
|---|---|
| `id` | Llave técnica. |
| `codigo_interno` | Placa o consecutivo visible. |
| `producto_id` | Producto. |
| `marca_id` | Marca. |
| `modelo` | Modelo. |
| `serial` | Serial, cuando exista. |
| `fecha_ingreso` | Fecha de incorporación. |
| `estado_conservacion_id` | Bueno, regular, malo u otros. |
| `estado_operativo_id` | Disponible, asignado, mantenimiento, baja, perdido, etc. |
| `ubicacion_actual_id` | Ubicación física actual. |
| `centro_costo_id` | Centro de costo. |
| `financiador_id` | Financiador. |
| `valor_adquisicion` | Valor y moneda. |
| `fecha_adquisicion` | Cuando esté disponible. |
| `garantia_hasta` | Opcional. |
| `observaciones` | Notas generales. |
| metadatos | Auditoría. |

Los campos de clase, categoría y nombre del producto no deben repetirse en esta tabla.

## 4. Ubicaciones y custodios

### `ubicacion`

Debe admitir estructura jerárquica: sede, oficina, bodega, ciudad, espacio físico u otra ubicación definida.

Un elemento puede estar:

- Disponible en una bodega.
- Asignado a una persona.
- Asignado a una unidad.
- En préstamo temporal.
- En mantenimiento con un tercero.
- En tránsito.

La ubicación física y el responsable no siempre son lo mismo.

## 5. Movimientos

### `tipo_movimiento_inventario`

Catálogo inicial sugerido:

- Ingreso.
- Entrega o asignación.
- Devolución.
- Traslado.
- Préstamo.
- Retorno de préstamo.
- Envío a mantenimiento.
- Retorno de mantenimiento.
- Ajuste autorizado.
- Pérdida.
- Hurto.
- Daño.
- Baja definitiva.

### `movimiento_inventario`

Debe registrar:

- Elemento.
- Tipo de movimiento.
- Fecha efectiva.
- Estado anterior y nuevo, cuando aplique.
- Ubicación origen y destino.
- Responsable origen y destino.
- Asignación organizacional relacionada, cuando aplique.
- Motivo.
- Observaciones.
- Usuario que registra.
- Usuario que aprueba, cuando aplique.
- Documento o acta soporte.
- Identificador de operación para agrupar movimientos de varios elementos.

Los movimientos confirmados deben ser inmutables. Las correcciones se realizan mediante movimientos compensatorios o anulaciones auditadas.

## 6. Asignaciones

### `asignacion_inventario`

Representa la responsabilidad vigente o histórica sobre un elemento.

| Campo | Descripción |
|---|---|
| `id` | Llave técnica. |
| `elemento_inventario_id` | Elemento. |
| `tercero_id` | Persona receptora, si aplica. |
| `asignacion_organizacional_id` | Contexto laboral vigente, si aplica. |
| `unidad_organizacional_id` | Asignación institucional, si no es personal. |
| `ubicacion_id` | Ubicación de uso o custodia. |
| `fecha_inicio` / `fecha_fin` | Vigencia. |
| `movimiento_entrega_id` | Movimiento que la origina. |
| `movimiento_cierre_id` | Movimiento que la finaliza. |
| `estado` | Activa, cerrada, pendiente de aceptación, etc. |
| `observaciones` | Notas. |

Solo debe existir una asignación activa por elemento, salvo que el producto sea consumible o se modele por cantidad.

## 7. Actas

### `acta`

- Número o serie.
- Tipo: entrega, devolución, traslado u otro.
- Fecha y ciudad.
- Estado: borrador, generada, enviada, aceptada, anulada.
- Entregado por y recibido por.
- Datos de instantánea documental.
- Archivo generado.
- Fechas de generación y aceptación.
- Observaciones.

### `acta_detalle`

Relaciona el acta con movimientos o elementos y conserva la descripción histórica mostrada en el documento.

La plantilla actual incluye:

- Datos generales del receptor.
- Cargo o rol.
- Área o dependencia.
- Lugar de entrega.
- Descripción de bienes.
- Estado y observaciones.
- Condiciones de responsabilidad.
- Firmas o aceptación.

## 8. Baja y clasificación Alta/Baja

Se deben distinguir tres conceptos:

1. `nivel_control_inventario`: Alta/Baja en el catálogo de productos.
2. `estado_operativo`: Disponible, asignado, mantenimiento, etc.
3. `proceso_baja`: acto formal para retirar un elemento del inventario.

Un mouse clasificado como `Baja` puede seguir estando activo y asignado. Un portátil clasificado como `Alta` puede ser dado de baja formalmente por daño o fin de vida útil.

## 9. Reportes mínimos

- Inventario general por estado.
- Elementos asignados por tercero.
- Elementos por unidad organizacional.
- Elementos disponibles por ubicación.
- Inventario por clase, categoría, producto y marca.
- Elementos sin serial cuando deberían tenerlo.
- Asignaciones sin acta.
- Elementos en mantenimiento.
- Bajas por periodo y motivo.
- Historial completo por elemento.
- Conciliación entre elementos y asignaciones.
