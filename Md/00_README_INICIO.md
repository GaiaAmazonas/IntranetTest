# Especificación inicial para Codex — Intranet Gaia Amazonas

## Propósito

Este paquete define la base funcional, técnica y de datos para continuar el desarrollo de la intranet de Fundación Gaia Amazonas. Codex debe usar estos documentos como fuente de verdad inicial y contrastarlos con los archivos suministrados por el usuario:

- `RelacionEquipos.xlsx`.
- Imagen del organigrama consolidado.
- Imágenes de referencia de las interfaces proyectadas.
- Toolkit visual y organizacional que se cargará junto con este paquete.
- Código fuente existente de la intranet, cuando sea suministrado.

El Excel es una **proyección funcional y una fuente de datos de referencia**, pero no representa por sí mismo el modelo relacional definitivo. Las columnas repetidas o descriptivas de las hojas deben normalizarse según este paquete.

## Objetivos de la solución

1. Administrar la estructura organizacional mediante una jerarquía dinámica.
2. Administrar terceros o personas sin mezclar sus datos personales con cargos, unidades, contratos o asignaciones.
3. Administrar inventario, productos, elementos físicos, estados, ubicaciones, asignaciones, devoluciones y actas.
4. Conservar trazabilidad completa de los cambios, equivalente o superior a la que actualmente ofrece Power Apps.
5. Permitir migración controlada desde Excel, Power Apps y otras fuentes.
6. Aplicar identidad gráfica de Gaia Amazonas sin acoplar colores o recursos visuales al modelo de negocio.

## Documentos incluidos

| Archivo | Contenido |
|---|---|
| `01_CONTEXTO_Y_ALCANCE.md` | Contexto, alcance, conceptos y límites del proyecto. |
| `02_ARQUITECTURA_Y_CONVENCIONES.md` | Lineamientos técnicos y convenciones de implementación. |
| `03_MODELO_DATOS_GENERAL.md` | Modelo relacional propuesto y relaciones principales. |
| `04_MODULO_ORGANIZACIONAL.md` | Estructura organizacional, tipos de unidad y organigrama. |
| `05_MODULO_TERCEROS.md` | Personas, vinculaciones, asignaciones, estudios, idiomas y soportes. |
| `06_MODULO_INVENTARIOS.md` | Catálogo, activos, movimientos, asignaciones, actas y bajas. |
| `07_AUDITORIA_METADATOS_Y_DOCUMENTOS.md` | Metadatos técnicos, historial y archivos. |
| `08_REGLAS_DE_NEGOCIO.md` | Reglas transversales y por módulo. |
| `09_MIGRACION_Y_CALIDAD_DATOS.md` | Estrategia para migrar y depurar los datos actuales. |
| `10_INTERFAZ_Y_EXPERIENCIA.md` | Navegación y criterios visuales para las pantallas. |
| `11_PLAN_IMPLEMENTACION_Y_PRUEBAS.md` | Fases, entregables, pruebas y criterios de aceptación. |
| `12_PROMPT_MAESTRO_CODEX.md` | Instrucción consolidada para iniciar el trabajo en Codex. |

## Orden recomendado de lectura para Codex

1. Leer este archivo.
2. Analizar el repositorio existente sin modificar código.
3. Leer `01`, `02`, `03` y `08`.
4. Leer el documento específico del módulo que se vaya a implementar.
5. Revisar el Excel y las imágenes únicamente como insumos de contraste.
6. Presentar un diagnóstico de brechas antes de generar migraciones o componentes.

## Decisiones ya tomadas

- La tabla denominada anteriormente `Clasificación` no se utilizará para el módulo organizacional.
- Para organización se conserva un catálogo de `TipoUnidad`.
- La entidad de personas se denomina `Tercero`.
- Cargo, rol, unidad, jefe y tipo de vinculación no son atributos permanentes de `Tercero`.
- Los datos laborales y organizacionales deben mantener historial por periodos de vigencia.
- `Producto` es un catálogo; `ElementoInventario` representa cada unidad física o activo.
- La clasificación `Alta/Baja` de la hoja Productos no equivale al estado operativo del elemento.
- Los estados vacíos de Excel no deben mantenerse como nulos en el sistema; durante la migración se aplicarán reglas explícitas.
- Los movimientos de inventario no se sobrescriben ni se eliminan físicamente.

## Aspectos pendientes de confirmación

Codex no debe inventar estos puntos:

- Tecnología exacta del frontend, backend, ORM y base de datos, salvo que ya esté definida en el repositorio.
- Proveedor definitivo para almacenamiento de archivos.
- Flujo jurídico de firma electrónica o aceptación de actas.
- Catálogo definitivo de tipos de vinculación, cargos, roles, sedes, centros de costo y financiadores.
- Política de retención documental y datos personales.
- Campos exactos existentes en Power Apps que todavía no hayan sido exportados.

## Resultado esperado de la primera ejecución

Codex debe entregar:

1. Inventario del repositorio y arquitectura encontrada.
2. Comparación entre el modelo existente y esta especificación.
3. Propuesta de implementación por fases.
4. Lista precisa de decisiones pendientes.
5. Primer conjunto de migraciones o modelos solo después de validar que no destruyen funcionalidad existente.
