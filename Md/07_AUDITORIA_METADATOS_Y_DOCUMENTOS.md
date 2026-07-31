# Auditoría, metadatos y gestión documental

## 1. Objetivo

La intranet debe capturar, como mínimo, la trazabilidad disponible actualmente en Power Apps y mejorarla donde existan vacíos. La auditoría no puede depender únicamente de un campo `updated_at`.

## 2. Metadatos estándar

Aplicar según el tipo de entidad:

- `created_at`.
- `created_by`.
- `updated_at`.
- `updated_by`.
- `deleted_at`.
- `deleted_by`.
- `is_deleted` o estrategia equivalente del proyecto.
- `version` para control de concurrencia.
- `source_system`.
- `source_record_id`.
- `import_batch_id`.

No todos los registros deben permitir eliminación lógica. Movimientos, actas emitidas y auditorías requieren reglas más restrictivas.

## 3. Auditoría detallada

### `audit_event`

Campos sugeridos:

- ID.
- Entidad y registro afectado.
- Tipo de operación.
- Fecha y hora.
- Usuario.
- Origen o módulo.
- Identificador de sesión o correlación.
- Dirección IP cuando sea legal y técnicamente pertinente.
- Valores anteriores y nuevos en JSON estructurado o tabla de detalle.
- Motivo u observación.

Eventos mínimos:

- Creación.
- Actualización.
- Inactivación.
- Reactivación.
- Aprobación.
- Rechazo.
- Importación.
- Generación documental.
- Asignación y devolución.
- Anulación.

## 4. Archivos

### `archivo`

- ID.
- Nombre original.
- Nombre de almacenamiento.
- Extensión y MIME.
- Tamaño.
- Hash.
- Proveedor y ruta o clave de almacenamiento.
- Fecha de carga.
- Usuario que carga.
- Estado de seguridad o análisis, si aplica.
- Metadatos de origen.

### `entidad_archivo`

Relación genérica o relaciones específicas según el estándar del proyecto:

- Entidad.
- Registro.
- Archivo.
- Tipo documental.
- Vigencia.
- Es principal.
- Estado de validación.

Codex debe evitar almacenar binarios directamente en la base de datos salvo que el sistema existente ya lo requiera y esté justificado.

## 5. Validación documental

Para estudios, certificados, contratos y soportes:

- Pendiente.
- En revisión.
- Aprobado.
- Rechazado.
- Vencido.
- Reemplazado.

Registrar quién valida, cuándo, comentario y motivo de rechazo.

## 6. Privacidad

- Aplicar autorización por rol y propósito.
- No exponer documentos de identidad, direcciones o contactos personales en listados generales.
- Registrar acceso a información sensible si la política lo exige.
- Definir retención antes de implementar eliminación automática.
- No copiar datos sensibles en logs de aplicación.
