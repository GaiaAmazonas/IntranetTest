# Infraestructura de archivos Graph / SharePoint

Actualizado: 2026-09-03. Implementación independiente de Helpdesk. No se han modificado Dataverse, GAIAHelpdesk, modelos de solicitudes ni repositorios de configuración de datos.

## Entrega por fases y punto de continuación

El usuario solicita autorización entre fases y respuestas finales breves, sin detalles salvo que los pida.

1. **Fundación técnica:** puertos, DTO, errores controlados, reglas de nombres/tipos/tamaños y configuración tipada. Implementada; pruebas automatizadas en `FileStorageFoundationTests.cs`.
2. **Adaptador y autenticación:** implementados. Credenciales de aplicación Graph (identidad administrada/certificado/secreto externo solo Development), streaming simple/reanudable, metadatos, carpetas, descarga y limpieza controlada; reintentos transitorios con Retry-After y pruebas con HTTP simulado.
3. **Diagnóstico protegido y prueba real:** completada. Lectura, carga, descarga, metadatos y eliminación se validaron contra Staging; el ejecutor protegido quedó disponible y deshabilitado por configuración después de la prueba.
4. **Configuración administrable:** completada para selección, consumo y registro de validación. El lector usa la tabla real «Configuración SharePoint», `gaia_configuracionsharepoint` (NO `gaia_repositorioarchivo`), y el modo Dataverse está activo en desarrollo local. La administración del registro se realiza en Dataverse.
5. **Base de migración transparente:** implementada. Copia desde la referencia histórica hacia el repositorio predeterminado actual, verifica ETag, longitud y SHA-256, limpia destinos incompletos y conserva el origen hasta que el módulo confirme el cambio de referencia. La migración masiva requiere el inventario de referencias del futuro módulo.

El adaptador y el diagnóstico están registrados y **validados contra SharePoint Staging real**. `IFileStorage` ya puede ser consumido por futuros módulos, pero cada módulo debe implementar primero su autorización y persistencia de referencias externas. No se escribe ni se crean carpetas al arrancar. El modo Dataverse requiere activación explícita y no hace fallback silencioso al servidor si falta una fila válida.

## Archivos y responsabilidades

- `src/BuildingBlocks/Gaia.BuildingBlocks/Files/FileStorageContracts.cs`: puerto sin dependencias Graph, streaming de carga/descarga, metadatos, disponibilidad, carpetas, scanner y puerto separado de mantenimiento.
- `src/BuildingBlocks/Gaia.BuildingBlocks/Files/FileUploadRules.cs`: validación de segmentos/ruta, extensiones/MIME permitidos y tamaño declarado; nombre técnico GUID independiente del original.
- `src/Gaia.Api/Infrastructure/Files/SharePointStorageConfiguration.cs`: opciones sin contener secretos, validación local al iniciar.
- `src/Gaia.Api/Infrastructure/Files/GraphApplicationTokenProvider.cs`: Azure.Identity 1.17.2, credencial app-only y scope Graph .default; nunca reutiliza identidad delegada ni hace fallback a credenciales de desarrollador.
- `src/Gaia.Api/Infrastructure/Files/GraphFileTransport.cs`: HTTP, destinos permitidos, errores sanitizados, reintentos y Retry-After.
- `src/Gaia.Api/Infrastructure/Files/SharePointFileStorage.cs`: adaptador, autorización de raíz, cargas, metadatos, descargas y mantenimiento.
- `src/Gaia.Api/Infrastructure/Files/FileTransferStreams.cs`: spool temporal y ownership de streams/respuestas HTTP.
- `src/Gaia.Api/Infrastructure/Files/FileStorageMigration.cs`: copia verificada entre repositorios, conciliación segura y limpieza del destino no confirmado; nunca elimina automáticamente el origen.
- `src/Gaia.Api/Infrastructure/Files/FileStorageRegistration.cs` y `src/Gaia.Api/Program.cs`: DI y validación local; no llaman servicios remotos al iniciar. Redirecciones automáticas, cookies y logging del cliente HTTP deshabilitados.
- `tests/Gaia.ArchitectureTests/FileStorageFoundationTests.cs` y `SharePointFileStorageTests.cs`: pruebas sin credenciales reales ni red.
- `src/Gaia.Api/Infrastructure/Files/FileStorageDiagnostics.cs`: diagnóstico de solo lectura y endpoint protegido. `FileStorageDiagnosticAuthorizationTests.cs`: política y metadatos del endpoint.

## Contrato y límites de responsabilidad

`StoredFile.Id`: Provider, RepositoryId (siteId), ContainerId (driveId), FileId (driveItemId). También ETag, OriginalName, StoredName, ContentType, Length, WebUrl y LogicalPath opcionales. Ni URL ni ruta sustituyen los identificadores estables.

`IFileStorage`: UploadAsync, DownloadAsync, GetMetadataAsync, EnsureFolderAsync, CheckAvailabilityAsync. `IFileStorageMaintenance.DeletePhysicallyAsync` exige ID, ETag esperado y motivo; el adaptador valida repositorio, biblioteca y ascendencia hasta la raíz, y envía If-Match. Separar interfaces no constituye autorización por sí mismo: la API debe autorizar al usuario antes de invocarlas.

`IFileStorageMigration.CopyAndVerifyAsync` descarga desde el repositorio indicado en la referencia histórica y carga en el predeterminado vigente. Retorna una nueva referencia verificada; el módulo consumidor debe guardarla atómicamente antes de autorizar la retirada posterior del origen. Esto permite cambiar de SharePoint sin romper IDs internos de Gaia y evita pérdida de datos durante el corte.

La eliminación lógica pertenece a la entidad del módulo futuro, no a este proveedor físico. El DELETE del proveedor no es una purga irreversible: conserva la semántica de papelera de SharePoint. No se ha creado tabla de adjuntos ni endpoint de negocio. `FileDownload` dispone el stream y libera su respuesta HTTP.

`OriginalName` se entrega al cargar; el módulo futuro debe persistirlo junto con los IDs y ETag. Al releer metadatos vale null porque SharePoint solo conoce el nombre técnico GUID. No inferir ni reconstruir el original a partir de ese nombre.

Scope y CorrelationId son segmentos genéricos seguros bajo RootFolder. No introducir nombres, contraseñas o sesiones de usuarios en la autenticación de aplicación. No exponer URLs firmadas/tokens/secretos al navegador.

`IFileContentScanner` es un punto de extensión, **no un antivirus implementado**. No hay implementación permisiva declarando Clean. Con RequireContentScan=true, un scanner ausente/no disponible bloquea la carga; cualquier scanner registrado debe devolver Clean. Allowlist MIME/extensión no prueba que el contenido binario corresponda al tipo declarado.

La carga se copia en streaming a un archivo temporal DeleteOnClose, comprobando la longitud real antes de escribir remotamente. Esto permite escaneo y relectura incluso de entradas no seekables sin almacenar el archivo completo en memoria. No es almacenamiento local permanente ni fallback. Dimensionar/proteger el disco temporal del servicio; pueden quedar residuos si el proceso termina abruptamente. Los fragmentos usan memoria acotada por UploadChunkBytes. La reanudación es interna a una llamada activa, no entre reinicios del proceso; una sesión fallida intenta cancelarse con un presupuesto separado de cinco segundos. Las cargas simples usan nombre GUID y conflicto fail; una respuesta perdida después de guardar puede requerir conciliación administrativa, nunca se anuncia éxito sin metadatos válidos.

## Configuración (sin valores reales)

Sección `FileStorage:SharePoint` (variables de entorno con `__` en lugar de `:`):

| Clave | Uso |
|---|---|
| Enabled | false/ausente: integración inactiva. true: exige configuración válida. |
| TenantId / ClientId | Identidad de aplicación; no usuario Microsoft ni credencial Dataverse delegada. |
| AuthenticationMethod | ManagedIdentity, Certificate o ClientSecret. Este último rechazado fuera de Development. |
| CertificatePath / CertificateThumbprint | Exactamente una referencia: PKCS12 con clave privada o certificado vigente en CurrentUser/My del usuario del servicio. |
| UseSystemAssignedManagedIdentity | false: identidad asignada por usuario mediante ClientId; true: identidad del sistema. Sin fallback automático. |
| SiteId / DriveId | Repositorio y biblioteca autorizados. |
| LibraryName | Referencia administrativa, no ID de operaciones. |
| RootFolder | Ruta relativa segura, p. ej. Gaia/Files. No se crea al iniciar. |
| MaximumFileBytes | Máximo de Gaia, entero positivo. |
| SimpleUploadThresholdBytes | Umbral configurable, positivo y menor o igual al máximo. |
| UploadChunkBytes | Predeterminado 3276800; múltiplo de 327680, máximo interno de memoria 10 MiB. No es el máximo de archivo. |
| TransferAllowedHosts | Hosts DNS HTTPS exactos de transferencia, sin comodines. Por defecto se infiere el host del SiteId compuesto; añadir otros solo tras verificarlos con el administrador del tenant. |
| RequireContentScan | false por defecto; activar y registrar un scanner real para exigir revisión antes de subir contenido. |
| AllowedExtensions | Lista explícita como .pdf; sin comodines. |
| AllowedMimeTypes | Lista explícita como application/pdf; sin comodines. |
| RealTestsEnabled / RealTestsEnvironment | Habilita explícitamente la prueba remota protegida solo en el entorno indicado; nunca se admite en Production. Debe volver a `false` al finalizar. |

ClientSecret y CertificatePassword se leen exclusivamente de User Secrets/variables de entorno, no de JSON versionado. No registrar sus valores. El inicio valida parámetros y material de credencial local; no solicita tokens ni confirma acceso efectivo. En producción preferir identidad administrada o certificado; ClientSecret se rechaza fuera de Development.

## Restricciones implementadas y que deben preservarse

- Graph app-only con `Sites.Selected` más concesión explícita al sitio. No solicitar Sites.ReadWrite.All ni Files.ReadWrite.All.
- No reutilizar `DataverseDelegatedClientFactory`: su identidad es delegada y resuelve otro problema.
- Usar HttpClient/DI existentes; establecer ownership de streams y cancelación. Evitar logging de URLs de sesión de carga/descarga.
- Cargas con nombre técnico y conflicto fail; ETag para evitar eliminación de versiones modificadas; limitar toda operación al site/drive/root configurado, incluso cuando llega un ID.
- Sesiones de carga: umbral de negocio configurable; distinguirlo de requisitos del protocolo Graph (fragmentos alineados). No codificar el máximo de archivo como un límite de Graph. Reanudar consultando rangos aceptados, sin repetir escrituras a ciegas.
- Readiness sin escrituras; permiso de escritura permanece Unknown hasta que una operación de prueba controlada lo verifique. No afirmar escritura disponible solo porque listar funcionó.
- Reintentos para 429/errores transitorios, respetando Retry-After, presupuesto y cancelación. No reintentar 401/403/404 ni validaciones.
- No devolver URL temporal de descarga; hacer streaming por API autorizada. Validar destinos HTTPS de redirecciones/sesiones y evitar propagar Bearer fuera de Graph.

## Fuentes técnicas verificadas

- [Permisos Selected de Microsoft Graph](https://learn.microsoft.com/en-us/graph/permissions-selected-overview): consentimiento y concesión explícita al recurso son pasos separados.
- [Sesión de carga de driveItem](https://learn.microsoft.com/en-us/graph/api/driveitem-createuploadsession?view=graph-rest-1.0): protocolo de fragmentos, rangos y reanudación; PUT de transferencia sin Bearer.
- [Descarga de contenido](https://learn.microsoft.com/en-us/graph/api/driveitem-get-content?view=graph-rest-1.0): redirección a una URL temporal que el adaptador no expone al navegador.

## Diagnóstico protegido implementado

`GET /api/infrastructure/files/diagnostics`, con la sesión/autenticación habitual de la API. Exige usuario autenticado y ambos permisos existentes: acceso a AdminCore (`AdminCorePermissions.IntranetAdminCoreVer`) y `TI.MODULOS.ADMINISTRAR`. No hay excepción para usuarios anónimos ni para Development. No se crean nuevos permisos automáticamente en Dataverse.

Devuelve Configuration, Authentication, Repository, Container, RootFolder, Read, Write y ErrorCode (serializados con la convención JSON de la API). Los estados son Available, Unavailable o Unknown. Write siempre es Unknown: el diagnóstico no prueba escritura. Read significa lectura de los metadatos de la raíz, no que se hayan descargado todos sus archivos. Una carpeta inexistente se informa sin crearla. Los fallos transitorios no se interpretan como denegación de permisos.

HTTP 200 significa que se obtuvo un informe, no que todos sus componentes estén disponibles. Consultar los estados y ErrorCode. Las denegaciones de autenticación/autorización del endpoint corresponden al middleware de seguridad. Presupuesto de consulta: 30 segundos, cancelable por el cliente; respuesta no cacheable. No incluye IDs, rutas, URLs de transferencia, credenciales ni cuerpos de errores Graph. No modifica el endpoint público `/health` ni realiza consultas al arrancar.

## Preparación del destino real (sin ejecutar todavía)

1. El administrador elige un sitio y biblioteca institucionales, separados de los archivos personales de un empleado. Compartir solo URL, nombres e identificadores, nunca secretos en el chat.
2. Preparar una identidad de aplicación y conceder `Sites.Selected` con autorización explícita de escritura únicamente sobre ese sitio. El aprovisionamiento de permisos se hace con identidad administrativa separada; la aplicación de ejecución no debe poder concederse permisos.
3. Un administrador autorizado puede resolver el sitio por su ruta mediante `GET https://graph.microsoft.com/v1.0/sites/{hostname}:/{ruta-del-sitio}?$select=id,webUrl`. Conservar el `id` compuesto completo. Consultar [sitio por ruta](https://learn.microsoft.com/en-us/graph/api/site-getbypath?view=graph-rest-1.0).
4. Consultar `GET https://graph.microsoft.com/v1.0/sites/{siteId}/drives?$select=id,name,webUrl`, elegir la biblioteca exacta y guardar su `id`. Consultar [bibliotecas de un sitio](https://learn.microsoft.com/en-us/graph/api/drive-list?view=graph-rest-1.0). No ampliar permisos globales de la aplicación para hacer descubrimiento; usar el administrador de aprovisionamiento si hace falta.
5. Preparar certificado/identidad administrada en el servidor; los secretos solo se admiten en Development. Registrar destinos y referencias administrativas; no activar un registro incompleto ni pegar credenciales en Dataverse.
6. Ejecutar primero el diagnóstico con usuario administrativo. La prueba protegida `POST /api/infrastructure/files/diagnostics/write-probe` exige los flags explícitos, crea un archivo técnico único, verifica su contenido, lo elimina con ETag y confirma que no exista. El 3 de septiembre de 2026 se validó exitosamente en Staging: carga, descarga y eliminación disponibles, limpieza sin residuos. Los flags quedaron deshabilitados después de la ejecución.

## Configuración administrable y migración futura

El usuario entregó capturas de la tabla creada y autorizó continuar. La implementación solo lee: no crea/modifica columnas, opciones, registros ni resultados de validación. EntitySetName, clave primaria y nombres efectivos se resuelven por metadata; no se inventa el plural. El nombre lógico de tabla se infiere de `gaia_ConfiguracionSharePointId` y se confirma al consultar metadata; puede precisarse mediante ConfigurationTable si la tabla publicada difiere.

La tabla real es `gaia_configuracionsharepoint`. El campo de método observado en las capturas es **`gaia_metodoautenticacio`**, sin n final. Entorno: 1 Desarrollo, 2 Pruebas, 3 Producción; proveedor: 1 SharePoint Online. Método: 1 identidad del sistema, 2 identidad asignada por usuario, 3 certificado, 4 secreto solo Development. La opción 5 «solo desarrollo» fue creada accidentalmente como método: se rechaza y debe retirarse si no tiene referencias. Un método vacío también se rechaza aunque Dataverse permita guardarlo. Estado operativo: 1 Borrador, 2 Disponible, 3 Solo lectura, 4 En migración, 5 Retirado. Resultado: 1 Sin validar, 2 Lectura verificada, 3 Lectura y escritura verificadas, 4 Error. La prueba explícita registra resultado, detalle controlado y fecha; nunca persiste respuestas Graph, identificadores sensibles ni secretos.

El lector filtra filas activas, proveedor y entorno (Development=1, Staging/Testing=2, Production=3; otros nombres se rechazan). Para nuevas cargas/diagnóstico exige un único predeterminado. Cero o múltiples coincidencias fallan de forma cerrada. Para un archivo existente busca su siteId/driveId, NO el predeterminado actual; mantiene la validación de ascendencia a la raíz. Dos configuraciones coincidentes para el mismo site/drive son ambiguas y se rechazan. No permite escribir salvo Disponible; lecturas en Disponible/Solo lectura; diagnóstico también en Borrador. En migración/Retirado se bloquean. No se consume el resultado histórico de validación como prueba de permisos actuales.

Archivos nuevos: `DataverseSharePointConfigurationReader.cs` (metadata y consulta paginada sin $skip), `RepositoryStorageConfiguration.cs` (opciones y límites de confianza) y `DataverseConfiguredFileStorage.cs` (puertos con recursos y snapshot de configuración por operación, liberados al terminar el scope de la petición). No existe caché global de filas/credenciales entre usuarios. Descargar y disponer el stream dentro de la petición que lo creó.

### Activación del modo Dataverse

Para activar otro despliegue, configurar `FileStorage:SharePoint:ConfigurationSource=Dataverse` y `Enabled=true`; opcionalmente ConfigurationTable. Desarrollo local ya usa este modo contra el registro de Pruebas mediante `RepositoryEnvironment=2`. Mantener MaximumFileBytes, SimpleUploadThresholdBytes, AllowedExtensions, AllowedMimeTypes y las políticas de fragmentación/escaneo en el servidor. La fila y credencial se validan al usarlas, no se consulta Dataverse al arrancar. Server conserva el comportamiento anterior de validación local inicial.

`gaia_referenciacredencial` contiene un alias (solo letras ASCII, números, guion y guion bajo), nunca una ruta de archivo o secreto. Para identidad administrada sin alias se utiliza `Default`, que igualmente debe existir como perfil aprobado. Cada perfil `FileStorage:Credentials:{alias}:` define TenantId, ClientId, AuthenticationMethod (ManagedIdentity/Certificate/ClientSecret), UseSystemAssignedManagedIdentity, AllowedSiteIds (lista exacta), AllowedTransferHosts (lista exacta) y, cuando aplica, CertificatePath o CertificateThumbprint. Las identidades, sitios y hosts de Dataverse deben coincidir con ese perfil; un administrador de la tabla no puede redirigir arbitrariamente una credencial del servidor. Añadir un nuevo sitio requiere aprobarlo en este perfil y conceder Sites.Selected; cambiar de tenant/identidad requiere preparar otra credencial segura.

ClientSecret/CertificatePassword se leen desde la misma ruta del perfil, exclusivamente mediante User Secrets o variables de entorno del servidor, nunca de JSON versionado. Ejemplo de nombre de variable: `FileStorage__Credentials__GaiaTest__ClientSecret` (sin escribir aquí su valor). Los demás valores administrativos pueden configurarse por el mecanismo habitual del servidor. Los hosts de Dataverse van uno por línea y deben ser subconjunto de AllowedTransferHosts.

La lectura de esta tabla reutiliza **el cliente delegado existente de Dataverse**, no para Graph: Graph sigue usando identidad de aplicación. El usuario administrativo que ejecuta el diagnóstico necesita lectura sobre la tabla. Antes de habilitar adjuntos para usuarios comunes habrá que definir la lectura de configuración con una identidad técnica Dataverse de mínimo privilegio, o el modelo autorizado equivalente; NO ampliar indiscriminadamente la lectura de esta tabla ni suponer que una identidad Graph concede permisos en Dataverse. Este punto bloquea su consumo general por módulos, no el diagnóstico administrativo.

Para una migración transparente, no basta editar SiteId/DriveId: conservar el ID interno del archivo Gaia y registrar su nueva ubicación/ETag después de copiar y verificar contenido. El puerto de migración ya realiza la copia y verificación individual sin borrar el origen. Cuando exista la tabla funcional de adjuntos faltará construir el manifiesto/lote, bloquear cambios concurrentes por registro, guardar cada nueva referencia, conciliar reintentos y retirar el origen solo después de confirmar el corte. Los IDs externos no son globalmente estables entre repositorios.

## Pruebas

Ejecutar `dotnet test tests/Gaia.ArchitectureTests/Gaia.ArchitectureTests.csproj --no-restore`. Fundación, adaptador, diagnóstico, configuración y migración verifican tipos/tamaños, credenciales, streams, reanudación, permisos, selección por entorno, duplicados, estados operativos, registro de validación, copia íntegra, limpieza y alias/destinos autorizados. Ejecución del 2026-09-03: **212 aprobadas, 0 fallidas, 0 omitidas**. Además, la prueba real protegida confirmó carga, descarga y eliminación en Staging sin archivos residuales, y actualizó el registro a «Lectura y escritura verificadas»; no forma parte de la suite automática y quedó deshabilitada.

La documentación operativa para obtener siteId/driveId, conceder Sites.Selected, cargar credenciales y ejecutar la prueba real se completará con el adaptador y diagnóstico. No configurar adjuntos funcionales antes de eso.
