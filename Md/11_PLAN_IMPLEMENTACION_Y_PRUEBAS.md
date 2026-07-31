# Plan de implementación y pruebas

## Fase 0 — Diagnóstico

Entregables:

- Inventario del repositorio.
- Diagrama de arquitectura actual.
- Modelo de datos existente.
- Módulos reutilizables.
- Brechas contra esta especificación.
- Riesgos técnicos.

No modificar código productivo en esta fase.

## Fase 1 — Base transversal

- Convenciones de auditoría.
- Permisos.
- Gestión de archivos.
- Catálogos comunes.
- Importaciones con staging.

Criterios de aceptación:

- Auditoría probada.
- Acceso restringido por permisos.
- Carga y consulta segura de archivos.
- Importación de prueba con informe de errores.

## Fase 2 — Organización

- Tipos de unidad.
- Unidades y jerarquía.
- Organigrama dinámico.
- Historial y administración.

Pruebas críticas:

- Impedir ciclos.
- Calcular niveles.
- Inactivar sin perder historia.
- Detectar códigos duplicados.
- Renderizar ramas extensas.

## Fase 3 — Terceros

- Maestro de personas.
- Vinculaciones.
- Asignaciones organizacionales.
- Cargos, roles y jefaturas.
- Estudios, idiomas y documentos.

Pruebas críticas:

- Evitar duplicados de documento.
- Conservar cambios de cargo y área.
- Controlar una asignación principal.
- Restringir datos sensibles.
- Validar soportes.

## Fase 4 — Inventarios

- Catálogo de productos y marcas.
- Elementos.
- Ubicaciones.
- Movimientos.
- Asignaciones.
- Actas.
- Bajas y reportes.

Pruebas críticas:

- No asignar un elemento no disponible.
- Una asignación activa por elemento.
- Devolución y traslado con trazabilidad.
- Generación de acta consistente.
- Historial reconstruible.

## Fase 5 — Migración

- Perfilado completo.
- Staging.
- Limpieza asistida.
- Carga.
- Conciliación.
- Aprobación del negocio.

## Criterios de terminado por historia

Una funcionalidad no se considera terminada hasta contar con:

- Regla de negocio implementada en backend.
- Migración o esquema versionado.
- Validación de permisos.
- Pruebas automatizadas pertinentes.
- Manejo de errores.
- Auditoría.
- Interfaz consistente con toolkit.
- Documentación técnica actualizada.

## Riesgos principales

1. Replicar la estructura plana de Excel en la base de datos.
2. Migrar valores ambiguos como datos válidos.
3. Confundir clasificación Alta/Baja con estado o baja formal.
4. Perder historial al actualizar cargos, áreas o asignaciones.
5. Generar actas sin relación transaccional con movimientos.
6. Exponer datos personales por permisos insuficientes.
7. Cambiar arquitectura existente sin diagnóstico.

## Siguiente entrega recomendada de Codex

Después del diagnóstico, Codex debe implementar primero un esqueleto vertical pequeño:

- Catálogo de tipos de unidad.
- Unidad organizacional jerárquica.
- API y pantalla administrativa.
- Organigrama básico dinámico.
- Auditoría y pruebas.

Esta entrega valida arquitectura, permisos, diseño visual y estrategia de datos antes de abordar terceros e inventarios.
