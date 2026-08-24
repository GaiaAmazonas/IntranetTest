# Informe técnico para montaje del servidor — Plataforma Gaia

**Fecha:** 11 de agosto de 2026  
**Propósito:** entregar al responsable de infraestructura la información necesaria para publicar la aplicación empresarial Gaia.

## 1. Resumen de la solución

La plataforma se compone de dos procesos web independientes:

1. **Frontend web:** interfaz que utiliza el usuario desde el navegador.
2. **API backend:** servicio que autentica al usuario, aplica permisos y reglas, y se comunica con Microsoft Dataverse.

La arquitectura objetivo utiliza **Microsoft Dataverse como plataforma de datos empresarial**. El navegador no debe conectarse directamente a Dataverse: todas las operaciones pasan por la API de Gaia.

## 2. Tecnologías implementadas

### Frontend

- **Next.js 16.2.12**.
- **React 19.2.4**.
- **TypeScript 5** y TSX.
- **HTML5 y CSS3**, generados y administrados por React/Next.js.
- **Tailwind CSS 4** para utilidades visuales.
- **Lucide React** para iconografía funcional.
- Recursos gráficos institucionales propios de Gaia.

### Backend y API

- **.NET 10**, SDK fijado actualmente en `10.0.301`.
- **ASP.NET Core 10**.
- Lenguaje **C#** con tipos nulos habilitados.
- API HTTP/JSON con endpoints REST.
- Cliente HTTP OData para la **Dataverse Web API v9.2**.
- **Microsoft.Identity.Web 4.14.0** para autenticación y adquisición segura de tokens.

### Datos

- Plataforma objetivo: **Microsoft Dataverse**.
- Entorno Dataverse: `https://<organizacion>.crm<region>.dynamics.com`.
- API: `https://<organizacion>.api.crm<region>.dynamics.com/api/data/v9.2`.
- Permiso delegado: `https://<organizacion>.crm<region>.dynamics.com/user_impersonation`.

Organización, Tipos de Unidad, Sedes y el CRUD principal de Terceros ya utilizan adaptadores de Dataverse. El acceso se realiza mediante la API de Gaia y el token delegado del usuario autenticado.

## 3. Autenticación

La aplicación utiliza **Microsoft Entra ID**, no usuarios ni contraseñas propios.

El flujo es:

1. El usuario pulsa **Continuar con Microsoft**.
2. La API inicia el protocolo **OpenID Connect**.
3. Microsoft Entra ID autentica la cuenta corporativa.
4. La API recibe el código de autorización y establece una cookie de sesión segura.
5. La API obtiene un token delegado para llamar Dataverse en nombre del usuario.
6. Dataverse aplica también los permisos asignados al usuario dentro del entorno.

La sesión de Gaia utiliza una cookie `__Host-Gaia.Session`:

- `HttpOnly` para impedir lectura desde JavaScript.
- `Secure`, únicamente sobre HTTPS.
- duración de 8 horas;
- renovación deslizante mientras exista actividad;
- cierre de sesión coordinado con Entra ID.

La aplicación registrada actualmente en Entra se denomina **Gaia Enterprise Web - Desarrollo**. Para producción puede usarse un registro independiente, lo cual es lo recomendado.

## 4. Información que debe suministrar infraestructura

Antes del montaje deben definirse estos datos:

1. Dominio público del frontend, por ejemplo `https://plataforma.gaiaamazonas.org`.
2. Dominio público de la API, por ejemplo `https://api-plataforma.gaiaamazonas.org`.
3. Servidor y sistema operativo: Windows Server o Linux.
4. Método de publicación: IIS en Windows o Nginx/Apache como proxy inverso en Linux.
5. Certificado TLS válido para ambos dominios.
6. DNS que apunte los dominios al servidor.
7. Dirección IP pública o mecanismo institucional de publicación.
8. Responsable de administrar secretos y renovarlos.
9. Si se instalará inicialmente una sola instancia de la API o varias instancias balanceadas.
10. Política institucional de copias, monitoreo, logs, actualizaciones y recuperación.

## 5. Configuración de Microsoft Entra ID

El administrador de Entra debe configurar el registro de producción con:

- Tipo de cuenta: solo cuentas del inquilino de Fundación Gaia Amazonas.
- URI de redirección: `https://DOMINIO_API/signin-oidc`.
- URI posterior al cierre: `https://DOMINIO_API/signout-callback-oidc`.
- Permiso delegado de Dynamics CRM/Dataverse: `user_impersonation`.
- Consentimiento administrativo concedido.
- Un secreto de cliente o, preferiblemente cuando la infraestructura lo permita, un certificado de aplicación.

La API necesita estos valores como secretos o variables seguras:

- `MicrosoftEntra__TenantId`
- `MicrosoftEntra__ClientId`
- `MicrosoftEntra__ClientSecret`
- `MicrosoftEntra__Instance=https://login.microsoftonline.com/`
- `MicrosoftEntra__CallbackPath=/signin-oidc`
- `MicrosoftEntra__SignedOutCallbackPath=/signout-callback-oidc`

El secreto nunca debe guardarse en Git, archivos públicos, código fuente ni variables del frontend.

## 6. Configuración de Dataverse

La API requiere:

- `Dataverse__EnvironmentUrl=https://<organizacion>.crm<region>.dynamics.com`
- `Dataverse__WebApiEndpoint=https://<organizacion>.api.crm<region>.dynamics.com/api/data/v9.2`
- `Dataverse__Scope=https://<organizacion>.crm<region>.dynamics.com/user_impersonation`

Cada usuario que use la plataforma debe:

- existir como usuario habilitado en el entorno de Dataverse;
- contar con licencia/derecho de uso aplicable;
- tener un rol de seguridad con permisos sobre las tablas y operaciones correspondientes.

El servidor necesita salida HTTPS por el puerto 443 hacia, como mínimo:

- `login.microsoftonline.com`
- `*.dynamics.com`
- los puntos de conexión de Microsoft requeridos por Entra y Dataverse.

No se debe exponer Dataverse directamente a través del servidor ni almacenar tokens en el frontend.

## 7. Configuración del frontend

El frontend requiere durante su compilación:

- `NEXT_PUBLIC_GAIA_API_URL=https://DOMINIO_API`

Este valor es público por diseño y solo contiene la URL de la API; no debe contener secretos.

Para ejecutar el frontend se necesita una versión de Node.js compatible con Next.js 16 y el administrador de paquetes PNPM. La instalación debe ser reproducible utilizando `pnpm-lock.yaml`.

Comandos de publicación de referencia:

```powershell
pnpm install --frozen-lockfile
pnpm --dir apps/web run build
pnpm --dir apps/web run start
```

El proceso debe ejecutarse como servicio y reiniciarse automáticamente si el servidor se reinicia.

## 8. Configuración y publicación de la API

El servidor de compilación necesita el SDK .NET 10. El servidor de ejecución necesita el runtime ASP.NET Core 10; si se publica de forma autocontenida, el runtime puede incluirse en el paquete.

Comando de referencia:

```powershell
dotnet publish src/Gaia.Api/Gaia.Api.csproj -c Release -o ./publish/api
```

Variables adicionales:

- `ASPNETCORE_ENVIRONMENT=Production`
- `WebApplication__BaseUrl=https://DOMINIO_FRONTEND`
- `Authorization__BootstrapAdministrators__0=correo-administrador@gaiaamazonas.org`

La API incluye el endpoint `/health`, que debe utilizarse para monitoreo.

## 9. HTTPS, proxy y comunicaciones

La publicación recomendada es:

```text
Internet
   |
HTTPS 443
   |
IIS o Nginx (certificado TLS)
   |-- dominio frontend --> Next.js
   |-- dominio API ------> ASP.NET Core
                               |
                               | HTTPS 443
                               +--> Microsoft Entra ID
                               +--> Microsoft Dataverse
```

No deben exponerse directamente los puertos internos de Node.js ni Kestrel. El proxy debe agregar cabeceras reenviadas correctamente, limitar tamaños de solicitud y registrar errores sin exponer tokens o cookies.

Si frontend y API se publican en dominios diferentes, la API debe incluir en producción una política CORS explícita para el dominio exacto del frontend, con credenciales. Actualmente CORS está configurado solamente para `localhost` y únicamente en modo Development. Esto debe ajustarse antes de la publicación productiva o resolverse publicando ambos servicios bajo un mismo origen mediante proxy inverso.

## 10. Recursos iniciales sugeridos

Para un piloto de 30 a 50 usuarios administrativos concurrentes, sin almacenar archivos ni base de datos local en el servidor, puede iniciarse con:

- 2 vCPU.
- 4 GB de RAM.
- 20 a 40 GB de disco para sistema, aplicación y logs.
- conexión estable a Internet.
- certificado HTTPS válido.

Estos valores son un punto inicial y deben ajustarse con monitoreo real de CPU, memoria, tiempos de respuesta y volumen de logs.

## 11. Seguridad y operación

Se requiere:

- HTTPS obligatorio y HTTP redirigido a HTTPS.
- Secretos almacenados en un almacén seguro o variables protegidas del servicio.
- Renovación planificada del secreto/certificado de Entra.
- Acceso administrativo al servidor limitado.
- Logs centralizados y sin datos sensibles.
- Monitoreo del endpoint `/health`.
- Alertas por caída del frontend, API o errores de Dataverse.
- Actualización periódica de .NET, Node.js y dependencias.
- Copia de seguridad de configuración y paquetes de despliegue.
- Entornos separados para desarrollo, pruebas y producción.
- Roles de Dataverse con mínimo privilegio.

Para una sola instancia, la caché de tokens en memoria es suficiente como inicio. Si se despliegan varias instancias de la API, se necesitará caché de tokens distribuida y compartir las claves de protección de datos para que las sesiones funcionen en todos los nodos.

## 12. Punto crítico antes de producción

Aunque el modelo objetivo solicitado es **solo Dataverse**, el código actual todavía registra contextos de Entity Framework y ejecuta migraciones de PostgreSQL al iniciar Organización, Terceros e Inventario. También Cargos e Inventario conservan operaciones sobre PostgreSQL.

En consecuencia, hoy existen dos alternativas:

1. **Montaje transitorio:** instalar/configurar PostgreSQL para que la versión actual pueda iniciar y conservar las funcionalidades aún no migradas.
2. **Cierre recomendado antes del montaje definitivo:** terminar la migración de Cargos e Inventario y retirar del arranque los contextos/migraciones de PostgreSQL. Después de este ajuste, el servidor podrá operar únicamente con Dataverse y no necesitará servidor de base de datos propio.

No se recomienda ocultar esta dependencia al responsable de infraestructura. Si el objetivo de producción es solo Dataverse, el cierre de esta deuda técnica debe ser una tarea previa formal.

## 13. Checklist de aceptación del montaje

- [ ] Dominios y DNS resuelven correctamente.
- [ ] Certificados HTTPS válidos.
- [ ] Frontend responde por HTTPS.
- [ ] `/health` de la API responde correctamente.
- [ ] Inicio de sesión con cuenta Gaia funciona.
- [ ] Cierre de sesión funciona.
- [ ] Callback de Entra no produce errores de redirección.
- [ ] La API obtiene token delegado de Dataverse.
- [ ] Un usuario autorizado puede consultar y crear datos.
- [ ] Un usuario sin permisos recibe 403.
- [ ] CORS/origen único funciona en producción.
- [ ] No hay secretos en archivos públicos ni repositorio.
- [ ] Reiniciar el servidor levanta automáticamente ambos servicios.
- [ ] Logs y alertas están habilitados.
- [ ] Se ha decidido formalmente cómo cerrar la dependencia transitoria de PostgreSQL.
