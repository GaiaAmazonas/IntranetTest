# Preparación de adjuntos de Helpdesk

Actualizado: 2026-09-04. Documento de preparación; no implementa ni modifica funcionalidades de Helpdesk.

## Estado y límite de esta etapa

La infraestructura reusable de Microsoft Graph/SharePoint ya existe y fue validada contra Staging. Helpdesk deberá consumir los puertos actuales `IFileStorage`, `IFileStorageMaintenance` e `IFileStorageMigration`; no debe crear en paralelo `IDocumentStorage`, `SharePointDocumentStorage` ni dependencias directas del SDK de Graph.

El módulo backend `Gaia.Modules.Helpdesk`, el adaptador de metadatos `gaia_adjuntosolicitud` y los endpoints autenticados de carga, listado, descarga e inactivación ya están incorporados. La identidad del tercero se resuelve desde la sesión de GAIA y no se acepta desde el navegador. La pantalla actual de Intranet Helpdesk continúa siendo temporal.

El contrato canónico del modelo está en `model/gaia-helpdesk-model.json`. Contiene las 14 tablas de la solución `GAIAHelpdesk` y debe ser la única fuente para nombres lógicos, tipos, opciones, relaciones y claves del módulo. Los adaptadores funcionales futuros dependerán de este contrato; no se inferirán nombres físicos desde prompts ni desde la interfaz.

## Correspondencia confirmada con el modelo

| Resultado neutral de almacenamiento | Campo declarado de `gaia_adjuntosolicitud` |
|---|---|
| `StoredFile.OriginalName` | `gaia_nombre` |
| `StoredFile.StoredName` | `gaia_nombrealmacenado` |
| `StoredFile.ContentType` | `gaia_tipomime` |
| `StoredFile.Length` | `gaia_tamanobytes` |
| Hash SHA-256 futuro | `gaia_hashsha256` |
| `StoredFile.Id.Provider` | `gaia_proveedoralmacenamiento` |
| `StoredFile.Id.RepositoryId` | `gaia_repositorioexternoid` |
| `StoredFile.Id.ContainerId` | `gaia_contenedorexternoid` |
| `StoredFile.Id.FileId` | `gaia_archivoexternoid` |
| `StoredFile.ETag` | `gaia_etag` |
| `StoredFile.WebUrl` | `gaia_urlweb` |
| `StoredFile.LogicalPath` | `gaia_rutalogica` |

La identidad técnica se compone de proveedor, repositorio, contenedor y archivo. URL y ruta son informativas. Las relaciones declaradas con solicitud, comentario, respuesta de campo y usuario cargador solo se implementarán después de confirmar metadata y autorización funcional.

## Flujo por completar en las siguientes fases

1. Las reglas ya validan solicitud activa, solicitante o responsable, visibilidad, habilitación del servicio, cantidad máxima y tamaño máximo antes de escribir. La siguiente fase debe obtener el tercero actor desde la identidad autenticada, nunca desde datos confiados del cliente.
2. El servicio de aplicación ya genera previamente el GUID estable del registro de adjunto.
3. Ya construye un `LogicalFileScope` sin datos personales y carga mediante `IFileStorage`.
4. Ya crea el registro Dataverse usando exclusivamente los metadatos devueltos.
5. Si Dataverse falla, ya compensa mediante `IFileStorageMaintenance` con ID externo y ETag.
6. La descarga ya usa el ID interno, reautoriza contra la solicitud y abre el stream por los identificadores externos.
7. El endpoint de eliminación ya inactiva lógicamente en Dataverse; una eliminación física posterior exige política y operación explícita.
8. Para cambio de repositorio, usar `IFileStorageMigration`, guardar primero la nueva referencia y conservar el origen hasta confirmar el corte.

Cada alta e inactivación registra una entrada en `gaia_historialsolicitud` con actor, origen, visibilidad, fecha e identificador de operación. La conciliación por solicitud compara los registros activos de Dataverse con los archivos físicos de SharePoint y reporta faltantes y huérfanos; por seguridad no elimina automáticamente ninguno.

## Infraestructura preparada para conectar Helpdesk

- El contrato recibe el GUID técnico y la fecha de carga suministrados por el consumidor; el nombre físico es estable y se devuelve el SHA-256 calculado durante la carga.
- Existe comprobación explícita de existencia; la eliminación física sigue protegida por identificador externo, ETag y razón obligatoria.
- Completar validación básica de firmas de contenido sin presentarla como antivirus.
- Evaluar `Lists.SelectedOperations.Selected` mediante una concesión real sobre las bibliotecas; el entorno validado actualmente utiliza `Sites.Selected` sobre el sitio de Staging.
- Extender la prueba real para eliminar también la carpeta temporal, no solamente el archivo.

Estas brechas pertenecen a la infraestructura actual y se ejecutarán únicamente con autorización expresa. No requieren crear todavía funcionalidades de Helpdesk.

## Decisiones pendientes antes de desarrollar

- Confirmación de permisos funcionales para crear, listar, descargar e inactivar adjuntos internos y visibles al solicitante.
- Política organizacional de retención y eliminación física.
- Decisión y configuración del servicio antimalware; hasta entonces no se declarará contenido como libre de malware.
