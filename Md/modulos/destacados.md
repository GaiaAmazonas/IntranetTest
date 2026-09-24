# Módulo Destacados de Intranet

Actualizado: 23 de septiembre de 2026.

## Objetivo y superficies

Destacados administra las piezas del carrusel principal de la Intranet. La gestión vive en AdminCore bajo `/comunicaciones/destacados`; la lectura pública para usuarios autenticados se realiza desde `/api/intranet/banners` y sus imágenes desde `/api/intranet/banners/{id}/images/{variant}`.

## Arquitectura vigente

- Dataverse conserva contenido, vigencia, orden, estado, relaciones y referencias externas.
- SharePoint conserva los binarios de escritorio y móvil mediante `IFileStorage`.
- La lectura y escritura funcional de `gaia_promocionbanner` usa el cliente delegado de Dataverse.
- La carga y descarga de archivos usa la identidad de aplicación Graph configurada en `FileStorage`; no reutiliza el token delegado del usuario.
- El navegador recibe imágenes por streaming desde la API. No recibe IDs de SharePoint, credenciales ni URLs de transferencia.
- La portada usa las URLs entregadas por `PublicBannerDto`; no existe asignación de imágenes locales por posición del carrusel.

## Datos y referencias de archivo

Tabla validada por el adaptador: `gaia_promocionbanner`.

Las columnas `gaia_ImagenEscritorio` y `gaia_ImagenMovil` son columnas de texto de 1000 caracteres capaces de almacenar la referencia externa segura generada por el servidor. El adaptador consulta metadata y rechaza la carga con un mensaje explícito si la referencia supera la longitud publicada. No se debe guardar una URL de SharePoint ni un secreto en estas columnas.

Cada carga utiliza el ámbito lógico `Destacados/{bannerId}` y un nombre técnico generado. Al reemplazar una imagen se actualiza la referencia de Dataverse. Al eliminarla, primero se retira la referencia y después se solicita la eliminación controlada mediante `IFileStorageMaintenance`; SharePoint conserva su semántica de papelera.

## Estados y reglas

- 1 Creada: puede enviarse a diseño o rechazarse.
- 2 En diseño: puede publicarse o rechazarse.
- 3 Publicada: puede cerrarse.
- 4 Cerrada.
- 5 Rechazada.
- Publicar exige imagen de escritorio, estado En diseño y registro activo.
- La portada muestra únicamente registros publicados, activos y vigentes. Si están relacionados con un evento, este debe estar publicado o finalizado.
- La variante móvil usa la imagen de escritorio como respaldo cuando no existe una imagen móvil.

## Administración y experiencia

La vista principal es una tabla responsive pensada para decenas de registros. La búsqueda filtra contenido y nombres de estado; `Solo activos` conserva su función. Editar/Gestionar abre el formulario con contenido e imágenes. Las operaciones sensibles usan `ConfirmDialog`; rechazo usa un formulario con motivo; éxito y error usan `useFeedback`. No se permiten `alert`, `confirm` ni `prompt` nativos.

## Seguridad

Se conservan las políticas `COM.DESTACADOS.VER`, `COM.DESTACADOS.CREAR`, `COM.DESTACADOS.ACTUALIZAR` y `COM.DESTACADOS.ADMINISTRAR`. La eliminación física usa exclusivamente `COM.DESTACADOS.ELIMINAR`; se presenta en Roles y permisos como una acción independiente y no se hereda de Inactivar. Los endpoints vuelven a autorizar cada operación; ocultar o deshabilitar un botón no reemplaza la autorización del servidor.

Eliminar un destacado borra la fila de Dataverse. Después, el servidor solicita la eliminación de los archivos referenciados en SharePoint. Si la limpieza remota falla después de confirmar el borrado de la fila, la operación no informa un falso fallo: el registro permanece eliminado y el archivo huérfano puede ser depurado operativamente.

## Verificación y despliegue

- Validar carga, lectura, reemplazo y eliminación de ambas variantes.
- Recargar el administrador y confirmar que las referencias persisten.
- Publicar un registro vigente y verificar la imagen real en Inicio.
- Comprobar búsqueda, `Solo activos`, 0/1/30 registros y vista móvil.
- El perfil de almacenamiento y la fila de Configuración SharePoint deben estar disponibles para el entorno. Los secretos permanecen en User Secrets o variables de entorno.

## Puesta en servicio del permiso nuevo

El catálogo de Seguridad obtiene sus permisos desde `AdminCorePermissions.All`. Para que `COM.DESTACADOS.ELIMINAR` aparezca en Roles y permisos del ambiente, un administrador autorizado debe ejecutar el mecanismo existente de inicialización/sincronización de Seguridad. Después debe asignarlo únicamente a los roles autorizados y publicar los cambios. El código no ejecuta ese bootstrap ni concede el permiso por su cuenta.
