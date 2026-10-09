# Guía para agentes en Gaia

Antes de modificar el repositorio, leer:

1. `docs/README.md`;
2. `docs/01-arquitectura.md`;
3. `docs/02-seguridad-e-integraciones.md`;
4. `docs/07-desarrollo-extension.md`;
5. `docs/10-matriz-trazabilidad.md`;
6. el documento del módulo afectado.

Reglas esenciales:

- conservar el monolito modular, Dataverse y la separación frontend/API;
- no acceder ni imprimir secretos o configuraciones locales reales;
- no inventar nombres lógicos de Dataverse;
- aplicar autorización en servidor, aunque la UI oculte acciones;
- mantener Intranet y AdminCore como superficies distintas;
- respetar cambios no relacionados ya presentes en el árbol de trabajo;
- usar únicamente la documentación oficial bajo `docs`; consultar Git solo si se necesita investigar una decisión histórica;
- actualizar documentación y pruebas cuando cambie el comportamiento.

Si una fuente contradice el código vigente o una decisión oficial, registrar la evidencia y pedir una decisión cuando cambie el alcance; no escoger silenciosamente una arquitectura nueva.
