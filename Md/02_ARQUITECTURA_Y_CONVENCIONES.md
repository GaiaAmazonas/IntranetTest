# Arquitectura y convenciones de implementación

## 1. Regla principal

Codex debe adaptarse a la arquitectura real del repositorio. No debe reemplazar el framework, el ORM, la estrategia de autenticación ni la estructura del proyecto sin demostrar una necesidad concreta.

## 2. Separación por capas

La implementación debe conservar, con los nombres propios del stack existente, las siguientes responsabilidades:

1. **Dominio:** entidades, invariantes y reglas de negocio.
2. **Aplicación:** casos de uso, comandos, consultas y validaciones.
3. **Infraestructura:** persistencia, archivos, correo, importaciones e integraciones.
4. **Presentación:** API, controladores y componentes de interfaz.

No se deben ubicar reglas críticas únicamente en el frontend.

## 3. Convenciones de base de datos

Usar una sola convención de nombres en todo el esquema. Preferencia sugerida si el proyecto no tiene una:

- Tablas en `snake_case` y plural o singular de manera consistente.
- Llaves primarias técnicas: `id` tipo UUID o entero de acuerdo con el estándar existente.
- Códigos de negocio separados de las llaves primarias.
- Llaves foráneas con sufijo `_id`.
- Fechas en UTC para metadatos técnicos.
- Fechas funcionales sin hora cuando el negocio solo requiere día.
- Restricciones únicas y de integridad declaradas en base de datos, no solo en la aplicación.

## 4. Convenciones de estado

No usar texto libre para estados transaccionales. Cada entidad debe utilizar:

- Enum controlado cuando el catálogo sea pequeño, estable y propio del código.
- Tabla catálogo cuando sea administrable o pueda crecer.

No usar `NULL = Activo`. Todo registro debe tener un estado explícito.

## 5. Vigencia e historial

Para relaciones con historia se utilizará el patrón:

- `fecha_inicio` obligatoria.
- `fecha_fin` opcional mientras esté vigente.
- Restricción para impedir periodos incoherentes.
- Indicador derivado de vigencia; evitar duplicar `activo` cuando pueda inferirse con seguridad.

Cuando el sistema requiera cierre manual, conservar además un estado de proceso.

## 6. Eliminación

- Catálogos y maestros referenciados: inactivación o eliminación lógica.
- Movimientos y auditoría: no se eliminan físicamente.
- Archivos: aplicar política de retención y eliminación controlada.
- Datos de prueba: usar mecanismos propios del entorno, no borrados manuales en producción.

## 7. API

Cada recurso debe soportar, según corresponda:

- Paginación.
- Ordenamiento.
- Filtros explícitos.
- Búsqueda por texto normalizado.
- Respuestas de error estructuradas.
- Control de concurrencia para actualizaciones sensibles.
- Validación de permisos en backend.

Las operaciones que ejecutan procesos de negocio deben modelarse como acciones, no como simples cambios arbitrarios de estado. Ejemplos:

- `asignar elemento`.
- `devolver elemento`.
- `trasladar elemento`.
- `dar de baja elemento`.
- `cerrar vinculación`.

## 8. Seguridad y permisos

Como mínimo, prever permisos por capacidad:

- Consultar organización.
- Administrar organización.
- Consultar terceros.
- Administrar terceros.
- Consultar información sensible de terceros.
- Validar estudios y documentos.
- Consultar inventario.
- Administrar catálogos de inventario.
- Registrar movimientos.
- Aprobar bajas.
- Generar o consultar actas.
- Consultar auditoría.

No asumir que todo usuario autenticado puede consultar datos personales o históricos.

## 9. Importaciones

Las importaciones deben ejecutarse mediante un proceso de staging:

1. Cargar archivo.
2. Validar formato.
3. Normalizar valores.
4. Mostrar errores y advertencias.
5. Permitir correcciones o exclusiones.
6. Confirmar ejecución.
7. Registrar lote, origen, usuario y resultado.

No insertar directamente todas las filas de Excel en tablas productivas.

## 10. Pruebas mínimas

- Pruebas unitarias para invariantes.
- Pruebas de integración para persistencia y restricciones.
- Pruebas de API para permisos y errores.
- Pruebas funcionales para flujos críticos.
- Pruebas de migración con conteos, duplicados y reconciliación.
