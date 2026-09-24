# Autenticación e identidades para Dataverse y SharePoint

## Propósito

Este documento define cómo debe acceder la aplicación GAIA a Microsoft Entra ID, Dataverse y SharePoint. Sirve como contrato técnico para otros ambientes de desarrollo y para agentes Codex que continúen el proyecto.

La regla principal es que **iniciar sesión, consultar Dataverse y operar archivos en SharePoint no son necesariamente la misma identidad**. No se deben reutilizar aplicaciones, permisos o secretos por conveniencia sin una decisión de arquitectura explícita.

## Resumen ejecutivo

| Operación | Identidad esperada | Autorización |
|---|---|---|
| Inicio de sesión | Usuario de Microsoft Entra ID | OpenID Connect |
| Dataverse después del inicio de sesión | Usuario autenticado, mediante token delegado | Roles de seguridad de ese usuario en Dataverse |
| SharePoint/Graph para archivos | Aplicación técnica del backend | Permisos Graph de mínimo privilegio, preferiblemente `Sites.Selected` |
| Configuración pública antes del inicio de sesión | No puede depender de un token delegado | Identidad técnica dedicada y restringida, o configuración pública publicada fuera de Dataverse |

## 1. Acceso autenticado a Dataverse

Para operaciones realizadas después de que el usuario inicia sesión, el patrón actual es acceso delegado:

1. El usuario inicia sesión con Microsoft Entra ID.
2. El backend obtiene un token para Dataverse en nombre del usuario.
3. El alcance solicitado es `Dynamics CRM/user_impersonation`.
4. Dataverse evalúa los roles y privilegios del usuario que inició sesión.

En el código, este acceso debe reutilizar `IDataverseDelegatedClientFactory` y la adquisición de token delegado existente. No debe reemplazarse silenciosamente por credenciales de aplicación.

Consecuencia importante: si Dataverse devuelve `403` en este flujo, asignar permisos a un **Application User** distinto no resolverá el problema. Se deben revisar los roles del usuario autenticado, el privilegio requerido y el nivel de acceso a la tabla involucrada.

## 2. Acceso técnico a SharePoint y Microsoft Graph

Las operaciones de archivos se realizan desde el backend con una identidad de aplicación independiente. La configuración se resuelve mediante `FileStorage:Credentials:<alias>` y la implementación de almacenamiento obtiene un token de Microsoft Graph.

Métodos de credencial admitidos según el ambiente:

- Identidad administrada, cuando esté disponible.
- Certificado, recomendado para aplicaciones registradas.
- Secreto de cliente solo cuando sea necesario en desarrollo; nunca debe almacenarse en Git ni en este documento.

El permiso recomendado en Graph es `Sites.Selected`, concediendo acceso únicamente al sitio requerido. No se deben exponer tokens, secretos, `siteId`, `driveId` ni `driveItemId` al navegador.

## 3. Lectura de la configuración de SharePoint

La tabla confirmada en el proyecto es:

- `gaia_configuracionsharepoint`

En los módulos autenticados, `DataverseSharePointConfigurationReader` consulta esta configuración mediante el cliente delegado de Dataverse. Después, la identidad técnica de Graph realiza la operación sobre SharePoint. Son dos pasos y pueden usar identidades diferentes:

```text
Usuario autenticado
  -> token delegado de Dataverse
  -> lectura de gaia_configuracionsharepoint
  -> resolución del alias de almacenamiento
  -> token de aplicación de Microsoft Graph
  -> operación sobre SharePoint
```

Por tanto, la aplicación registrada para Graph no necesita convertirse automáticamente en Application User de Dataverse.

## 4. Caso especial: configuración anterior al inicio de sesión

Una página de acceso todavía no tiene un usuario autenticado. Por ello, la configuración visual del login, redes sociales o imágenes mostradas antes de iniciar sesión **no puede depender del patrón delegado**.

Se debe elegir conscientemente una de estas alternativas:

### Alternativa A: configuración pública fuera de Dataverse

Publicar únicamente los datos no sensibles que necesita la pantalla inicial en configuración del backend, caché controlada o almacenamiento público diseñado para ese fin.

Esta alternativa evita dar acceso de aplicación a Dataverse, pero exige un mecanismo administrativo de publicación o sincronización.

### Alternativa B: Application User dedicado en Dataverse

Crear o reutilizar una aplicación técnica destinada expresamente al arranque público y registrarla como Application User en el entorno de Power Platform. Su rol debe tener solo lectura sobre las tablas y columnas estrictamente necesarias.

Si la fila de configuración no pertenece a un usuario o equipo concreto y debe ser visible globalmente, el privilegio de lectura debe tener nivel Organización. Esto no autoriza a conceder privilegios de escritura, personalización ni acceso general a otras tablas.

Usar para este fin la misma aplicación de SharePoint solo es válido si se adopta como decisión explícita, se documenta y sus permisos permanecen mínimos. La mera existencia de un Client ID en la configuración no demuestra que esa sea la identidad correcta para Dataverse.

## 5. Tablas de configuración de login

En otra rama o ambiente se han mencionado los nombres:

- `gaia_configuracionlogin`
- `gaia__redsociallogin`

Estos nombres **no están confirmados en el código actual de este repositorio**. Antes de configurar permisos o escribir consultas se deben validar mediante los metadatos de Dataverse:

- `LogicalName`
- `EntitySetName`
- clave primaria
- columnas realmente utilizadas
- propiedad y nivel de acceso requerido

No se debe asumir que un nombre con doble guion bajo es correcto ni crear una tabla nueva para hacer coincidir un nombre supuesto.

## 6. Diagnóstico de errores 403

Antes de cambiar roles, identificar exactamente qué solicitud falla:

1. Confirmar la URL de destino: Dataverse o Microsoft Graph.
2. Identificar la identidad contenida en el token sin imprimir el token ni secretos.
3. Confirmar si el flujo es delegado o de aplicación.
4. Confirmar la tabla, operación y nivel de acceso solicitados.

### Si el 403 proviene de Dataverse con token delegado

- Revisar el usuario conectado y sus roles de seguridad.
- Verificar lectura sobre la tabla requerida.
- Verificar el nivel de acceso requerido por la propiedad del registro.
- No modificar el Application User de Graph, porque no participa en esa llamada.

### Si el 403 proviene de Dataverse con token de aplicación

- Verificar que el Client ID esté registrado como Application User en el entorno correcto.
- Asignar un rol de mínimo privilegio.
- Conceder lectura a nivel Organización solamente cuando el caso de configuración global lo requiera.
- No conceder creación, actualización, eliminación ni personalización si el proceso solo lee configuración.

### Si el 403 proviene de Microsoft Graph

- Revisar permisos de aplicación y consentimiento administrativo.
- Si se usa `Sites.Selected`, comprobar la concesión sobre el sitio exacto.
- Revisar que el sitio, biblioteca y ruta correspondan al ambiente.

## 7. Configuración esperada sin secretos

El ambiente debe suministrar externamente:

- Tenant de Microsoft Entra ID.
- Client ID de la aplicación web para autenticación.
- Credencial de esa aplicación, fuera del repositorio.
- URL de Dataverse y alcance delegado `user_impersonation`.
- Uno o más alias `FileStorage:Credentials` para Graph.
- Credencial de Graph mediante identidad administrada, certificado o secreto externo.
- Si existe arranque público desde Dataverse, Client ID del Application User dedicado y su rol restringido.

Nunca se deben inventar identificadores, compartir secretos entre cuentas de Codex ni copiar secretos a archivos Markdown, variables versionadas o mensajes.

## 8. Validación mínima

La implementación se considera validada cuando se comprueba, sin revelar credenciales:

1. Inicio de sesión exitoso.
2. Consulta autenticada a un endpoint como `/api/security/me`.
3. Lectura de la configuración de SharePoint con la identidad prevista.
4. Obtención del token de Graph con la identidad técnica.
5. Prueba controlada de lectura y, si aplica, escritura y eliminación del archivo de diagnóstico.
6. Acceso a la configuración pública del login sin depender de una sesión inexistente.
7. Un usuario o aplicación sin privilegios recibe denegación, demostrando mínimo privilegio.

## 9. Instrucción para otro agente Codex

Se puede entregar este texto junto con el repositorio:

> Alinea la implementación con `Md/continuidad/07_AUTENTICACION_DATAVERSE_SHAREPOINT.md`. Primero identifica si cada llamada usa un token delegado o de aplicación. No concedas permisos de Dataverse a la aplicación de SharePoint sin demostrar que esa identidad realiza la llamada. Para módulos autenticados reutiliza el cliente delegado existente. Para información requerida antes del login, usa una fuente pública controlada o un Application User dedicado de solo lectura y mínimo privilegio. Valida los nombres lógicos mediante metadatos; no crees ni modifiques tablas y no solicites, imprimas ni almacenes secretos.

## 10. Estado actual y límites

El proyecto ya documenta y utiliza la separación entre acceso delegado a Dataverse y acceso técnico a Graph. Esto no significa que todos los usuarios tengan automáticamente lectura sobre `gaia_configuracionsharepoint`: sus roles deben autorizarla cuando el módulo use acceso delegado.

Tampoco está resuelto automáticamente el acceso previo al login para tablas nuevas creadas en otra rama o máquina virtual. Ese caso debe implementar una de las alternativas de la sección 4 y validar los nombres lógicos reales antes de asignar permisos.

## Documentos y componentes relacionados

- `Md/continuidad/02_SEGURIDAD.md`
- `Md/continuidad/04_ARCHIVOS_SHAREPOINT.md`
- `Md/continuidad/06_FOTOS_PERFIL_ENTRA.md`
- `IDataverseDelegatedClientFactory`
- `DataverseSharePointConfigurationReader`
- `GraphApplicationTokenProvider`
