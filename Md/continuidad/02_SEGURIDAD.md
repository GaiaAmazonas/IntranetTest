# Seguridad, sesión, tokens y permisos

Revisado: 2026-09-02. Documentación de la implementación, no una autorización para cambiar permisos del entorno.

## Tres capas diferentes

| Capa | Responsabilidad |
|---|---|
| Microsoft Entra | Identificar al usuario mediante OpenID Connect. |
| Seguridad funcional Gaia | Determinar módulos, aplicaciones y operaciones permitidas. |
| Power Platform / Dataverse | Autorizar técnicamente las lecturas/escrituras y metadatos ejecutados con identidad delegada. |

Que una persona inicie sesión no significa que tenga permisos Gaia ni permisos Dataverse. Asignar Administrador del sistema para solucionar un 403 no es una solución aceptable para usuarios generales.

## Flujo de autenticación y token

1. `startLogin()` en `apps/web/src/lib/api-client.ts` guarda una marca de transición, no un token, y navega a `/api/auth/login?returnUrl=...`.
2. `IdentityEndpoints` valida el origen de retorno y hace challenge OIDC.
3. Microsoft.Identity.Web procesa autorización `code`, establece cookie y permite adquirir token delegado para el scope Dataverse.
4. `SecurityProvider` consulta `/api/security/me` con `credentials: "include"`; recibe usuario funcional, roles, permisos y módulos.
5. `RouteAccessGate` valida la ruta para UX. Las políticas ASP.NET vuelven a validar cada operación en servidor.
6. `DataverseDelegatedClientFactory` llama `GetAccessTokenForUserAsync`, añade Bearer al HttpClient nombrado Dataverse y ejecuta la consulta con permisos del usuario.
7. Logout cierra cookie y sesión OIDC. Reiniciar API puede perder la caché de tokens en memoria; una cookie restante no garantiza que el token delegado pueda recuperarse sin reautenticación.

No existe en este flujo una conexión Dataverse directa desde React ni un esquema de token Bearer guardado en localStorage. No introducirlo.

## Configuración actual

- Cookie `__Host-Gaia.Session`: HttpOnly, Secure Always; SameSite None en desarrollo y Strict fuera de desarrollo.
- Duración configurada: 8 horas con SlidingExpiration. Inactividad: 40 minutos gestionados desde `security-context.tsx`, sincronizados entre pestañas por marca de actividad en localStorage. No es todavía una política de inactividad impuesta por servidor.
- Transición final del login: 300 ms en `route-access-gate.tsx`.
- Tokens: `AddInMemoryTokenCaches()`, `SaveTokens=false`. Para múltiples instancias habrá que diseñar caché distribuida/data protection y despliegue, no asumirlo disponible.
- CORS admite origen frontend configurado y credenciales; localhost adicional solo en desarrollo. No usar AllowAnyOrigin con cookies.
- No registrar secretos, cookies o tokens en logs. No subir configuración real a Git.

## Seguridad funcional

Fuente principal: `SecurityContracts.cs`, `SecurityModule.cs`, `SecurityModuleRules.cs`, `SecurityAssignmentRules.cs`, `SecurityEndpoints.cs` y `DataverseSecurityStore.cs`.

Tablas técnicas usadas por esta implementación: `gaia_usuarioaplicacion`, `gaia_usuariorol`, `gaia_rol`, `gaia_permiso`, `gaia_rolpermiso`, `gaia_modulo`; también terceros/correo para relación institucional.

- `AdminCorePermissions.All` registra políticas conocidas. Cada endpoint debe usar la política adecuada; agregar un código solo al frontend no lo protege.
- `PermissionScope.RequiresAdminCore` añade acceso AdminCore cuando corresponde. La entrada es `INT.APP.ADMINCORE.VER` además del permiso de la operación.
- Rutas frontend en `lib/route-access.ts`; validación de permisos en `lib/security-permissions.ts`. Requisitos del array se combinan con AND; expresiones `A|B` representan alternativas según el helper existente.
- Navegación y asignación de permisos son conceptos relacionados pero distintos. Visibilidad de módulo no equivale por sí misma a conceder acceso.
- Aplicaciones externas deben autorizarse individualmente; `INT.APLICACIONES.VER` no debe conceder automáticamente todas las `INT.APP.*`.
- Árbol de módulos: respetar actividad efectiva de padres, orden y prevención de ciclos.
- Usuarios pueden tener varios roles. Retirar una asignación debe operar sobre su ID y usuario correctos, nunca sobre todos los usuarios de un rol.
- Guardar un rol y sus permisos no debe cambiar asignaciones de usuarios. Preservar fechas/historial y verificar resultado con una lectura posterior.
- `SecurityBootstrap` depende de configuración de administradores iniciales; es una operación de preparación sensible, no un workaround rutinario.
- Hay caché de seguridad en servidor; invalidarla mediante mecanismos existentes cuando cambien roles/permisos. No convertir una respuesta antigua en autorización permanente.

## Errores y diagnóstico

- 401: sesión ausente o renovación delegada necesaria. Middleware puede devolver `code: reauth_required`; cliente despacha `gaia:reauth-required`. Verificar consumidor del evento antes de asumir que reintenta automáticamente.
- 403: revisar política Gaia y luego operación rechazada por Dataverse. No confundir acceso a registros con acceso a metadata.
- 400: revisar tipo de campo, filtro OData, lookup y parámetros requeridos. `$skip` es un fallo ya observado.
- 503 `dataverse_unavailable`: problema de comunicación; no presentarlo como credenciales incorrectas.
- Validaciones de módulos usan respuestas estructuradas (ValidationProblem/Problem) con diferencias entre módulos. Seguir y probar el contrato de la feature.

Para un rol técnico mínimo de Dataverse, inventariar operaciones reales por tabla y alcance. No copiar ciegamente permisos Organización ni permisos de creación/escritura. Los nombres de privilegios de metadatos y su disponibilidad deben validarse en el entorno. No hay una matriz universal verificada aquí.

## Reglas para nuevas implementaciones

- Validar autorización y propiedad/alcance en servidor para cada ID recibido.
- No confiar en campos ocultos, botones deshabilitados ni IDs enviados por el cliente.
- No exponer contactos personales o atributos sensibles fuera de los DTO autorizados.
- Toda caché de datos privados debe contemplar identidad, expiración, logout, revocación y errores 401/403. No copiar la caché visual del directorio como patrón general de seguridad.
- Subidas: validar tamaño, tipo y destino. No ampliar excepciones antiforgery ni asumir que un endpoint existente demuestra protección CSRF global; revisar explícitamente cookie/CORS/antiforgery en nuevas mutaciones.
- Pruebas negativas obligatorias: sin sesión, sin permiso de módulo, sin permiso de acción, aplicación no asignada y retirada de rol.
