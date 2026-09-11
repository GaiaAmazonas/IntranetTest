# Fotografías de perfil de Microsoft Entra

Fecha de revisión: **11 de septiembre de 2026**.

## Arquitectura implementada

Las fotografías institucionales se consultan exclusivamente desde la API con el token **delegado** del usuario autenticado. El navegador no recibe tokens de Microsoft Graph ni se guardan imágenes binarias/Base64 en Dataverse.

- `GET /api/profile/photo?size=96`: fotografía del usuario autenticado; obtiene su claim `oid` y usa Graph `/me/photos/{size}x{size}/$value`.
- `GET /api/intranet/people/{personId}/photo?size=96`: fotografía de una persona del directorio. Exige el permiso `INTRANET.PERSONAS.VER`; el servidor resuelve la relación antes de consultar Graph.
- Tamaños permitidos: `48`, `64`, `96`, `120` y `240` píxeles. La API devuelve 404 si no hay foto, 401/403 por autenticación/autorización y 503 controlado cuando Graph no está disponible.
- Las respuestas positivas se almacenan en memoria de API hasta 12 horas (renovación deslizante de 4); las ausencias, 15 minutos. El frontend carga fotografías visibles de forma diferida, con un máximo de cuatro solicitudes simultáneas, y mantiene iniciales estables durante la carga o ante cualquier error.

`PersonAvatar` es el componente reutilizable para el menú, Inicio y directorio Personas. No usar etiquetas `img` directas contra Graph ni crear almacenamiento paralelo de fotos.

## Resolución de identidad

Nunca se usa el nombre de una persona como identificador. Para el perfil actual se usa `oid` (con `NameIdentifier` como compatibilidad). Para una persona del directorio la API busca, en este orden:

1. `gaia_terceros.gaia_EntraObjectId`, si la columna existe.
2. Un usuario activo `gaia_usuarioaplicacion` relacionado por `gaia_Tercero`, usando su `gaia_EntraObjectId`.
3. Correo institucional corporativo `@gaiaamazonas.org` del usuario o de `gaia_correocolaborador` como compatibilidad temporal.

No se presume que el GUID del tercero en Dataverse sea el Object ID de Entra.

### Columna recomendada, no creada automáticamente

Para cubrir también colaboradores que todavía no tienen usuario de aplicación, crear manualmente en la tabla **`gaia_terceros`** una columna de texto de una línea, opcional, de 36 caracteres, con nombre de esquema/lógico según el publicador: **`gaia_EntraObjectId` / `gaia_entraobjectid`**. Debe contener el Object ID de Entra en formato GUID y, si Dataverse lo permite, tener una clave alternativa única. Poblarla desde Entra; no con nombres.

## Configuración manual de Entra

En el registro de aplicación usado por `MicrosoftEntra:ClientId`:

1. Agregar en **Microsoft Graph → Delegated permissions** el permiso mínimo `ProfilePhoto.Read.All`.
2. Ejecutar **Grant admin consent** para el tenant.
3. Cerrar sesión e iniciar sesión de nuevo para que se emita el consentimiento/token con el nuevo scope.

No se requiere `Directory.Read.All`, `User.Read.All` ni un secreto adicional. La configuración no concede el permiso por sí sola: `MicrosoftGraph:ProfilePhotoScope` solo indica a la API el scope que solicitará.

## Pruebas y operación

No automatizar llamadas reales a Graph. Validar con una cuenta que tenga fotografía y otra que no la tenga: el menú, Inicio y Personas deben mantener las iniciales si la foto falta o Graph falla. Probar 401/403 desde una sesión sin permisos y la carga de varias tarjetas para confirmar que no se produzcan solicitudes masivas.
