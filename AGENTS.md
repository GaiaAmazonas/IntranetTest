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

## Publicación personal y empresarial

Cuando el propietario solicite **publicar en el personal, copiar al espejo o directorio 2 y publicar en la empresa**, ejecutar el procedimiento completo en este orden:

1. validar el árbol de trabajo y publicar la rama `main` del repositorio personal `C:\Users\Edgar Munar\Documents\Proyecto Gaia Aplicacion` en su remoto `origin`;
2. sincronizar únicamente el contenido versionado hacia `C:\ProyectoIntranetGaia\IntranetTest`, conservando sin modificación su directorio `.git`, historial, remoto, rama, secretos, configuraciones locales, dependencias y archivos ignorados;
3. verificar mediante el hash del árbol Git que el contenido versionado personal y empresarial sea exactamente el mismo;
4. crear el commit equivalente en la rama empresarial activa y publicarlo en su remoto `origin`;
5. confirmar que ambos directorios queden limpios y que los commits locales coincidan con sus respectivos remotos.

Este procedimiento no autoriza por sí solo hacer merge a otra rama, publicar en `main` empresarial, desplegar en servidores, reiniciar procesos ni modificar configuraciones ambientales. Esas acciones requieren una solicitud explícita adicional.
