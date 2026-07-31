# Módulo de terceros

## 1. Principio del módulo

`Tercero` representa a la persona. Cargo, rol, unidad, jefe, correo corporativo y tipo de vinculación pertenecen a relaciones temporales y no deben almacenarse como atributos permanentes del tercero.

La hoja `Terceros` es una proyección operativa. Sus columnas actuales deben redistribuirse durante la migración.

## 2. Entidad `tercero`

Campos propuestos:

| Grupo | Campos |
|---|---|
| Identidad | `id`, `tipo_persona`, `tipo_documento_id`, `numero_documento`, `primer_nombre`, `segundo_nombre`, `primer_apellido`, `segundo_apellido`, `nombre_preferido`. |
| Datos básicos | `fecha_nacimiento`, `sexo_id` o `genero_id` solo si el proceso lo requiere, `nacionalidad_id`. |
| Contacto personal | `correo_personal`, `telefono_principal`, `telefono_alternativo`. |
| Residencia | `direccion`, `pais_id`, `departamento_id`, `municipio_id`. |
| Control | `estado_tercero_id`, `observaciones`, metadatos de auditoría. |

`nombre_completo` debe calcularse o mantenerse mediante una estrategia controlada. No debe sustituir los campos de nombre.

## 3. Vinculación con Gaia

### `tipo_vinculacion`

Catálogo: empleado, contratista, consultor, voluntario, practicante, miembro de junta, proveedor persona natural u otros definidos por el negocio.

### `vinculacion_tercero`

| Campo | Descripción |
|---|---|
| `id` | Llave técnica. |
| `tercero_id` | Persona vinculada. |
| `tipo_vinculacion_id` | Naturaleza de la relación. |
| `numero_referencia` | Contrato, convenio o identificador. |
| `fecha_inicio` / `fecha_fin` | Vigencia. |
| `modalidad_trabajo_id` | Presencial, híbrida, remota u otra. |
| `sede_principal_id` | Cuando aplique. |
| `correo_corporativo` | Pertenece a la vinculación o cuenta institucional, no a la identidad personal. |
| `estado` | Borrador, activa, suspendida, finalizada u otro flujo aprobado. |
| `motivo_finalizacion_id` | Cuando aplique. |
| `observaciones` | Información adicional. |
| metadatos | Auditoría. |

## 4. Asignación organizacional

### `cargo`

Catálogo formal con código, nombre, descripción, familia, nivel y estado.

### `rol_organizacional`

Catálogo de responsabilidades funcionales. Un rol no necesariamente equivale a un cargo.

### `asignacion_organizacional`

| Campo | Descripción |
|---|---|
| `id` | Llave técnica. |
| `vinculacion_id` | Relación de la persona con Gaia. |
| `unidad_organizacional_id` | Unidad donde se desempeña. |
| `cargo_id` | Cargo formal, cuando aplique. |
| `jefe_asignacion_id` | Autorreferencia a la asignación organizacional del jefe. |
| `fecha_inicio` / `fecha_fin` | Vigencia. |
| `es_principal` | Define asignación principal. |
| `porcentaje_dedicacion` | Opcional para múltiples asignaciones. |
| `centro_costo_id` | Cuando sea necesario. |
| `estado` | Estado del registro. |
| `observaciones` | Notas. |
| metadatos | Auditoría. |

### `asignacion_rol`

Permite múltiples roles por asignación organizacional, cada uno con vigencia, prioridad y estado.

## 5. Formación y perfil

### `estudio_tercero`

- Nivel académico.
- Título.
- Institución.
- País.
- Fechas de inicio, finalización y grado.
- Graduado.
- Tarjeta profesional, cuando aplique.
- Estado de validación.
- Soportes.

### `idioma_tercero`

- Idioma.
- Nivel general.
- Lectura, escritura, conversación y comprensión.
- Certificación, puntaje y vigencia.
- Estado de validación.
- Soportes.

### `formacion_tercero`

Cursos, diplomados, talleres, seminarios y certificaciones.

### `experiencia_tercero`

Experiencia previa o complementaria, separada de la vinculación interna con Gaia.

### `contacto_emergencia`

Uno o varios contactos, con indicador principal, parentesco y estado.

### `documento_tercero`

Relación entre el tercero y sus documentos o soportes, utilizando el subsistema común de archivos.

## 6. Metadatos funcionales equivalentes a Power Apps

Además de la auditoría estándar, las entidades que requieran validación deben incluir o relacionar:

- Estado de validación.
- Validado por.
- Fecha de validación.
- Motivo de rechazo.
- Comentario de revisión.
- Fuente de captura.
- Identificador del registro en el sistema de origen.
- Lote de importación.

No añadir campos de aprobación a todas las tablas sin necesidad. Deben aplicarse solo a procesos que realmente se revisan o aprueban.

## 7. Reglas específicas

1. Documento único por tipo de documento, con excepciones controladas.
2. Una persona puede tener varias vinculaciones históricas.
3. Una vinculación puede tener varias asignaciones organizacionales.
4. Solo una asignación principal vigente por vinculación, salvo excepción aprobada.
5. Un jefe debe ser una asignación vigente y válida en el contexto.
6. No sobrescribir cargo, unidad ni jefe anteriores.
7. Cerrar una vinculación debe cerrar o exigir tratamiento de asignaciones activas, accesos e inventarios pendientes.
8. Datos sensibles requieren permisos específicos.
9. `BODEGA` no debe migrarse automáticamente como persona; se debe convertir en ubicación o custodio institucional según definición final.
10. Valores `PENDIENTE`, `#N/A`, `No tiene` o similares deben convertirse en incidencias de calidad, no en datos maestros.

## 8. Interfaces mínimas

- Directorio de terceros.
- Ficha integral con pestañas: datos básicos, vinculaciones, asignaciones, formación, idiomas, experiencia, documentos, inventario e historial.
- Línea de tiempo de cambios organizacionales.
- Flujo de validación de estudios y soportes.
- Alertas por documentos o certificaciones próximos a vencer.
