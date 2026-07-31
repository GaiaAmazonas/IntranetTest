# Contexto, alcance y vocabulario del sistema

## 1. Contexto

Fundación Gaia Amazonas requiere consolidar en su intranet procesos actualmente proyectados o administrados mediante Excel, Power Apps y otras fuentes. Los primeros dominios priorizados son:

- Estructura organizacional.
- Terceros o personas.
- Inventario y asignación de recursos tecnológicos.
- Gestión documental y trazabilidad.

La solución debe evitar replicar literalmente la estructura plana del Excel. El Excel contiene datos útiles para migración, validación y prototipos de pantalla, pero mezcla entidades y valores derivados que deben separarse en el modelo de datos.

## 2. Alcance funcional inicial

### Incluido

- Catálogo y jerarquía de unidades organizacionales.
- Tipos de unidad organizacional.
- Visualización del organigrama consolidado.
- Registro maestro de terceros.
- Vinculaciones de terceros con la organización.
- Asignaciones organizacionales, cargos, roles y jefaturas.
- Estudios, idiomas, formaciones, experiencias, contactos y documentos.
- Catálogo de productos y marcas.
- Registro unitario de elementos de inventario.
- Ubicaciones, estados físicos y estados de disponibilidad.
- Movimientos de entrada, salida, entrega, devolución, traslado, mantenimiento y baja.
- Asignaciones de elementos a terceros, unidades o ubicaciones.
- Generación y conservación de actas.
- Auditoría de creación, modificación, aprobación e inactivación.
- Importación inicial desde el Excel suministrado.

### Fuera de alcance hasta confirmación

- Contabilidad completa de activos fijos.
- Depreciación financiera o fiscal.
- Compras, órdenes de compra y cuentas por pagar completas.
- Firma electrónica certificada.
- Integraciones automáticas con proveedores externos no identificados.
- Nómina y liquidación contractual.

Estos puntos podrán añadirse posteriormente, pero no deben asumirse como incluidos en el primer desarrollo.

## 3. Vocabulario común

### Tercero

Persona natural o registro especial capaz de participar en procesos internos. El Excel contiene un registro `BODEGA` dentro de terceros. Este caso debe revisarse porque una bodega es una ubicación o custodio institucional, no una persona. Durante la migración no debe tratarse automáticamente como persona natural.

### Vinculación

Relación contractual, laboral, institucional o de prestación de servicios entre un tercero y Gaia Amazonas durante un periodo.

### Asignación organizacional

Ubicación funcional de una vinculación dentro de una unidad, con cargo, roles, jefatura, dedicación y vigencia.

### Unidad organizacional

Nodo de la estructura jerárquica institucional. Puede depender de otra unidad y tiene un tipo de unidad.

### Producto

Definición genérica de un tipo de bien, por ejemplo: portátil, monitor, celular, GPS o cámara.

### Elemento de inventario

Unidad física identificable perteneciente a un producto. Puede tener marca, modelo, serial, placa, valor, estado y ubicación.

### Clasificación de control

Criterio operativo del catálogo de productos que actualmente se expresa como `Alta` o `Baja` en Excel. No significa que el activo esté dado de alta o de baja. Se propone nombrarlo `nivel_control_inventario` o equivalente.

### Baja de inventario

Proceso formal por el cual un elemento deja de formar parte del inventario activo. No debe confundirse con la clasificación de control del producto.

### Movimiento

Evento inmutable que cambia o documenta la situación de un elemento: ingreso, entrega, devolución, traslado, mantenimiento, pérdida, baja, entre otros.

## 4. Principios de diseño

- Separar identidad, relación laboral y posición organizacional.
- Mantener historial en lugar de sobrescribir datos relevantes.
- Usar catálogos controlados para valores que se repiten.
- Evitar nulos con significado implícito.
- Aplicar eliminación lógica en información con trazabilidad.
- Registrar origen de datos para migraciones y sincronizaciones.
- Derivar nombres, resúmenes y niveles cuando sea posible.
- No duplicar datos descriptivos cuando exista una llave foránea confiable.
