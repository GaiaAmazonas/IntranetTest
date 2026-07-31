# Migración y calidad de datos

## 1. Fuentes iniciales

- Hoja `Organizacional`.
- Hoja `Terceros`.
- Hoja `Productos`.
- Hoja `Marcas`.
- Hoja `RegistroElementos`.
- Hoja `Asignaciones`.
- Hoja `ActaEntrega` como plantilla.
- Hoja `Resumen` como mecanismo de conciliación.
- Exportaciones futuras de Power Apps.

## 2. Estrategia

### Fase A: perfilado

- Conteos por hoja.
- Campos vacíos.
- Duplicados.
- Tipos de dato inconsistentes.
- Valores fuera de catálogo.
- Relaciones rotas.
- Seriales repetidos.
- Terceros inexistentes en asignaciones.
- Elementos inexistentes en asignaciones.

### Fase B: staging

Crear tablas o almacenamiento temporal con el valor original y el valor normalizado. Cada fila debe conservar:

- Hoja y número de fila.
- Lote de importación.
- Valor original.
- Resultado de validación.
- Advertencias.
- Decisión de migración.

### Fase C: normalización

- Convertir códigos organizacionales a texto.
- Resolver padres de unidades.
- Separar nombres cuando sea viable; mantener el original para revisión.
- Diferenciar correo personal y corporativo.
- Crear catálogos sin duplicados por mayúsculas, tildes o espacios.
- Convertir estados vacíos de Productos a `Activo` solo mediante una regla explícita y documentada.
- Convertir `0` como padre organizacional a nulo.
- Interpretar fechas seriales de Excel correctamente.

### Fase D: carga

Orden sugerido:

1. Catálogos.
2. Tipos y unidades organizacionales.
3. Terceros.
4. Vinculaciones y asignaciones organizacionales.
5. Productos y marcas.
6. Elementos.
7. Ubicaciones.
8. Movimientos y asignaciones.
9. Actas o referencias documentales.

### Fase E: conciliación

- Conteo origen vs. destino.
- Elementos sin producto.
- Asignaciones sin elemento.
- Asignaciones sin tercero.
- Elementos duplicados.
- Totales por clase y estado.
- Comparación con la hoja Resumen.

## 3. Incidencias ya observadas

- `BODEGA` aparece como tercero con cédula 1; requiere reclasificación.
- Hay códigos de área `PENDIENTE` o `INACTIVO` en una columna que debería contener códigos.
- Hay áreas con `#N/A`.
- Hay correos `No tiene` en lugar de nulo o estado de ausencia.
- El Excel mezcla roles, cargos y descripciones de funciones.
- La hoja Productos usa estado vacío para representar activo.
- La imagen y la hoja Organizacional presentan una posible diferencia de códigos alrededor de Presidencia Ejecutiva y Equipo Asesor.
- La hoja Asignaciones repite datos del tercero y de la organización, generando riesgo de inconsistencias.
- Algunos seriales pueden ser numéricos en Excel y perder formato; deben importarse como texto.

## 4. Informe de migración obligatorio

Codex debe generar un reporte con:

- Registros procesados.
- Insertados.
- Actualizados.
- Omitidos.
- Rechazados.
- Advertencias.
- Duplicados detectados.
- Relaciones no resueltas.
- Archivo de errores descargable.

## 5. Prohibiciones

- No limpiar datos directamente en producción sin respaldo.
- No transformar silenciosamente valores ambiguos.
- No crear terceros ficticios para resolver asignaciones.
- No eliminar filas con errores sin reportarlas.
- No usar nombres descriptivos como llaves de relación.
