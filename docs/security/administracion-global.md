# Alcance organizacional para Servicios y flujos

## Campo requerido en Dataverse

Agregar una columna a la tabla de roles `gaia_rol` con esta configuración:

- Nombre para mostrar: `Administración global`
- Nombre de esquema: `gaia_AdministracionGlobal`
- Nombre lógico esperado: `gaia_administracionglobal`
- Tipo: Sí/No (booleano)
- Valor predeterminado: No
- Requerimiento: Obligatorio
- Descripción: `Permite administrar recursos de todas las áreas de la organización, de acuerdo con los permisos funcionales asignados al rol.`
- Auditoría: habilitada

También debe estar habilitada la auditoría de la tabla `gaia_rol` en el entorno. Publicar la personalización después de crear la columna.

## Inicialización segura

El sistema no concede alcance global por el código, nombre o descripción de un rol. Por eso, el primer rol global debe marcarse explícitamente en Dataverse por un administrador de la plataforma. Los roles existentes deben quedar diligenciados expresamente en `No`, salvo aquellos que realmente deban administrar toda la organización. Después de ese paso, un usuario con una asignación vigente de dicho rol podrá administrar esta propiedad desde **Seguridad > Roles y permisos**.

No marcar globalmente roles de administración departamental. Los permisos `HD.CATALOGOS.VER` y `HD.CATALOGOS.ADMINISTRAR` continúan determinando las acciones funcionales; la nueva propiedad únicamente amplía el alcance organizacional de esas acciones.

## Modelo utilizado

- Definición de roles: `gaia_rol`.
- Asignación usuario–rol: `gaia_usuariorol` mediante `gaia_Usuario` y `gaia_Rol`, respetando estado y fechas de vigencia.
- Unidad del usuario: asignaciones vigentes de `gaia_asignacionorganizacional` para el tercero asociado a `gaia_usuarioaplicacion`.
- Unidad propietaria del servicio: relación `gaia_UnidadResponsable` de `gaia_servicio` hacia `gaia_organizacion`.

La autorización del backend cruza estas relaciones y devuelve `403` cuando un usuario intenta consultar o modificar un recurso perteneciente a otra unidad.
