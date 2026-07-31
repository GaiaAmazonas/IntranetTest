# Modelo de datos general

## 1. Dominios

El modelo se divide en cuatro dominios principales:

1. Organización.
2. Terceros y talento.
3. Inventarios.
4. Plataforma transversal: auditoría, documentos, catálogos e importación.

## 2. Relaciones de alto nivel

```mermaid
erDiagram
    TIPO_UNIDAD ||--o{ UNIDAD_ORGANIZACIONAL : clasifica
    UNIDAD_ORGANIZACIONAL ||--o{ UNIDAD_ORGANIZACIONAL : contiene

    TERCERO ||--o{ VINCULACION_TERCERO : posee
    TIPO_VINCULACION ||--o{ VINCULACION_TERCERO : clasifica
    VINCULACION_TERCERO ||--o{ ASIGNACION_ORGANIZACIONAL : genera
    UNIDAD_ORGANIZACIONAL ||--o{ ASIGNACION_ORGANIZACIONAL : ubica
    CARGO ||--o{ ASIGNACION_ORGANIZACIONAL : define
    ASIGNACION_ORGANIZACIONAL ||--o{ ASIGNACION_ORGANIZACIONAL : supervisa
    ASIGNACION_ORGANIZACIONAL ||--o{ ASIGNACION_ROL : tiene
    ROL_ORGANIZACIONAL ||--o{ ASIGNACION_ROL : clasifica

    TERCERO ||--o{ ESTUDIO_TERCERO : registra
    TERCERO ||--o{ IDIOMA_TERCERO : registra
    TERCERO ||--o{ FORMACION_TERCERO : registra
    TERCERO ||--o{ EXPERIENCIA_TERCERO : registra
    TERCERO ||--o{ CONTACTO_EMERGENCIA : registra

    CLASE_PRODUCTO ||--o{ CATEGORIA_PRODUCTO : contiene
    CATEGORIA_PRODUCTO ||--o{ PRODUCTO : clasifica
    PRODUCTO ||--o{ ELEMENTO_INVENTARIO : instancia
    MARCA ||--o{ ELEMENTO_INVENTARIO : identifica
    UBICACION ||--o{ ELEMENTO_INVENTARIO : contiene

    ELEMENTO_INVENTARIO ||--o{ MOVIMIENTO_INVENTARIO : afecta
    TIPO_MOVIMIENTO ||--o{ MOVIMIENTO_INVENTARIO : clasifica
    TERCERO ||--o{ ASIGNACION_INVENTARIO : recibe
    UNIDAD_ORGANIZACIONAL ||--o{ ASIGNACION_INVENTARIO : puede_recibir
    ELEMENTO_INVENTARIO ||--o{ ASIGNACION_INVENTARIO : se_asigna
    ACTA ||--o{ ACTA_DETALLE : contiene
    MOVIMIENTO_INVENTARIO ||--o{ ACTA_DETALLE : soporta

    ARCHIVO ||--o{ ENTIDAD_ARCHIVO : relaciona
    LOTE_IMPORTACION ||--o{ FILA_IMPORTACION : contiene
```

## 3. Criterios de normalización

### No duplicar datos de organización en asignaciones de inventario

La hoja `Asignaciones` contiene nombre del tercero, área, subdirección, rol y correo. En el modelo final deben persistirse las llaves del elemento, la persona o asignación organizacional y el movimiento. Las descripciones se consultan mediante relaciones o se conservan únicamente como instantánea documental cuando sea necesario.

### Datos calculados o de presentación

No deben ser la fuente principal:

- Nombre completo del tercero.
- Resumen del elemento.
- Nombre del área junto al código.
- Subdirección derivada de la jerarquía.
- Nivel organizacional, salvo que se decida persistirlo como optimización controlada.

### Instantáneas históricas

Las actas sí pueden conservar una instantánea de nombre, documento, cargo, unidad y descripción del elemento en el momento de emisión. Esta instantánea no reemplaza las relaciones normalizadas; sirve para que el documento histórico no cambie si posteriormente se actualizan los maestros.

## 4. Llaves de negocio relevantes

- `unidad_organizacional.codigo` debe ser único por vigencia o globalmente, según la decisión final.
- `tercero + tipo_documento + numero_documento` debe evitar duplicados.
- `producto.codigo` debe ser único.
- Seriales o placas de inventario deben ser únicos cuando existan y aplique.
- Número de acta debe ser único dentro de su tipo o serie documental.
- Una asignación de inventario activa por elemento como máximo.

## 5. Catálogos transversales sugeridos

- Países, departamentos y municipios.
- Tipos de documento.
- Estados de validación.
- Tipos de archivo o soporte.
- Tipos de vinculación.
- Modalidades de trabajo.
- Sedes y ubicaciones.
- Centros de costo.
- Financiadores.
- Monedas.
- Estados de conservación.
- Estados operativos de inventario.
- Motivos de baja, pérdida, devolución y traslado.

Codex debe reutilizar catálogos existentes en el repositorio y evitar crear duplicados semánticos.
