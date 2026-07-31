# Reglas de negocio consolidadas

## 1. Organización

- ORG-001: Cada unidad tiene un tipo de unidad activo.
- ORG-002: Una unidad puede tener cero o una unidad padre.
- ORG-003: La jerarquía no admite ciclos.
- ORG-004: El nivel se deriva de la jerarquía.
- ORG-005: No se elimina una unidad referenciada.
- ORG-006: Los cambios de estructura deben conservar historia y auditoría.
- ORG-007: El código organizacional es único y no actúa como llave primaria.
- ORG-008: Una unidad inactiva no recibe nuevas asignaciones, salvo permiso excepcional.

## 2. Terceros y vinculaciones

- TER-001: La identidad de una persona no depende de su cargo o área.
- TER-002: No se admiten duplicados de documento sin una excepción documentada.
- TER-003: Una persona puede tener varias vinculaciones históricas.
- TER-004: Cargo, unidad, jefe y rol se gestionan mediante asignaciones organizacionales.
- TER-005: Los cambios organizacionales no sobrescriben periodos anteriores.
- TER-006: Solo una asignación principal puede estar vigente por vinculación, salvo configuración aprobada.
- TER-007: La jefatura debe referenciar una asignación organizacional válida.
- TER-008: Finalizar una vinculación exige revisar elementos, accesos y tareas pendientes.
- TER-009: Estudios, idiomas y documentos pueden requerir validación.
- TER-010: Valores de Excel como `PENDIENTE`, `#N/A` y `No tiene` no crean catálogos automáticamente.

## 3. Productos y elementos

- INV-001: Un producto describe un tipo de bien; un elemento representa una unidad física.
- INV-002: Todo elemento pertenece a un producto activo o histórico válido.
- INV-003: Marca y modelo pertenecen al elemento, salvo que posteriormente se cree un catálogo de modelos.
- INV-004: El serial es obligatorio solo para productos configurados con esa regla.
- INV-005: Serial o placa no pueden duplicarse cuando sean identificadores únicos.
- INV-006: `Alta/Baja` del producto es nivel de control, no estado del elemento.
- INV-007: No se debe asumir estado activo a partir de un nulo después de la migración.

## 4. Movimientos y asignaciones

- MOV-001: Todo cambio de responsable, ubicación o estado relevante genera un movimiento.
- MOV-002: Los movimientos confirmados son inmutables.
- MOV-003: Un elemento solo puede tener una asignación activa.
- MOV-004: No se asigna un elemento inactivo, dado de baja, perdido o en mantenimiento.
- MOV-005: La entrega debe registrar receptor, fecha, ubicación y estado físico.
- MOV-006: La devolución cierra la asignación activa y define el destino del elemento.
- MOV-007: Un traslado no puede tener el mismo origen y destino.
- MOV-008: Una baja requiere motivo, autorización y soporte.
- MOV-009: Un acta no puede referenciar movimientos inexistentes.
- MOV-010: Anular un acta no borra los movimientos; debe aplicar el flujo definido.

## 5. Actas

- ACT-001: El número de acta debe ser único dentro de su serie.
- ACT-002: El documento emitido conserva instantáneas de datos relevantes.
- ACT-003: Una acta aceptada no se modifica; se anula y reemplaza mediante proceso auditado.
- ACT-004: El sistema debe conservar el archivo final y su hash.
- ACT-005: La aceptación o firma debe registrar fecha, identidad y mecanismo utilizado.

## 6. Auditoría y datos

- AUD-001: Toda modificación sensible registra usuario y valores cambiados.
- AUD-002: Las importaciones registran lote y origen.
- AUD-003: No se eliminan físicamente movimientos ni auditorías.
- AUD-004: Los permisos se validan en backend.
- AUD-005: La información personal sensible no aparece en reportes sin autorización.

## 7. Reglas de cierre de vinculación

Antes de finalizar una vinculación, el sistema debe verificar:

1. Elementos de inventario activos a cargo.
2. Actas pendientes de aceptación o devolución.
3. Asignaciones organizacionales activas.
4. Documentos o procesos pendientes según módulos integrados.

La aplicación debe bloquear el cierre o exigir una excepción autorizada, según la política que defina Gaia.
