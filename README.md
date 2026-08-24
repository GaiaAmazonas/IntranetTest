# Gaia Enterprise Platform

Plataforma empresarial modular con AdminCore e intranet. El repositorio conserva el código fuente, historial, migraciones, pruebas, recursos visuales y archivos de configuración de ejemplo necesarios para reconstruir el entorno en otro computador.

Los Excel, datos personales, credenciales y configuraciones reales de una organización permanecen fuera de Git.

## Arquitectura actual

- Frontend: Next.js 16, React 19, TypeScript y Tailwind CSS.
- API: ASP.NET Core sobre .NET 10.
- Autenticación: Microsoft Entra ID mediante OpenID Connect.
- Datos empresariales activos: Microsoft Dataverse Web API v9.2.
- Persistencia empresarial: Microsoft Dataverse mediante Web API OData.
- Arquitectura: monolito modular con puertos y adaptadores.
- Pruebas: xUnit, Vitest, TypeScript y ESLint.

## Contenido protegido por Git

- `apps/web`: frontend AdminCore e intranet.
- `src/Gaia.Api`: API y adaptadores de Dataverse.
- `src/Modules`: módulos de dominio y persistencia local.
- `tests`: pruebas automatizadas.
- `Md`: documentación funcional y arquitectónica.
- `apps/web/public`: recursos visuales utilizados por la aplicación.
- migraciones EF Core, lockfiles y versiones de herramientas.

## Requisitos para un computador nuevo

- Git.
- .NET SDK `10.0.301` o una revisión compatible indicada en `global.json`.
- Node.js 24 y pnpm.
- Visual Studio Code es opcional.

## Recuperación desde GitHub

```powershell
git clone https://github.com/hackmunar/GestionProyecto.git
Set-Location GestionProyecto
dotnet tool restore
dotnet restore Gaia.Platform.slnx
Set-Location apps\web
pnpm install --frozen-lockfile
Set-Location ..\..
```

## Configuración local segura

Copie `src/Gaia.Api/appsettings.Development.example.json` como `src/Gaia.Api/appsettings.Development.json`. Este último está ignorado por Git.

Registre el secreto de Entra ID sin escribirlo en archivos versionados:

```powershell
dotnet user-secrets set "MicrosoftEntra:ClientSecret" "VALOR_REAL" --project src\Gaia.Api\Gaia.Api.csproj
```

Complete localmente:

- Tenant ID, Client ID y secreto de Entra ID.
- URL, API y scope del entorno Dataverse.
- correo del administrador inicial.
- URL pública del frontend.

Para el frontend copie `apps/web/.env.example` como `apps/web/.env.local` si necesita cambiar la URL de la API.

## Ejecución

Terminal 1:

```powershell
dotnet run --project src\Gaia.Api\Gaia.Api.csproj --launch-profile https
```

Terminal 2:

```powershell
Set-Location apps\web
pnpm dev
```

- Aplicación: `http://localhost:3000`
- API: `https://localhost:7168`
- Salud: `https://localhost:7168/health`

## Verificación

```powershell
dotnet test Gaia.Platform.slnx
Set-Location apps\web
pnpm test
pnpm lint
pnpm build
```

## Edición independiente futura

El repositorio no depende de PostgreSQL. Los módulos operativos utilizan Dataverse; las funcionalidades todavía pendientes deben implementarse mediante sus adaptadores Dataverse conservando los contratos públicos.

## Seguridad

Nunca confirme en Git:

- secretos de cliente, contraseñas o tokens;
- `appsettings.Development.json`, `.env.local` o archivos equivalentes;
- Excel o exportaciones con información institucional/personal;
- logs, carpetas `bin`, `obj`, `.next` o `node_modules`.
