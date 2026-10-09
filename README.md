# Gaia Enterprise Platform

Gaia es una plataforma empresarial modular con Intranet y AdminCore. El frontend se construye con Next.js/React y la API con ASP.NET Core; Microsoft Entra ID provee identidad, Dataverse almacena información empresarial y SharePoint conserva archivos mediante Microsoft Graph.

## Documentación

La fuente oficial y navegable es [docs/README.md](docs/README.md). Allí se encuentran arquitectura, seguridad, módulos, API y datos, frontend, instalación, desarrollo, ADR y la clasificación de documentación histórica.

Antes de modificar código, leer también [AGENTS.md](AGENTS.md).

## Estructura

- `apps/web`: frontend de Intranet y AdminCore.
- `src/Gaia.Api`: API, composición y adaptadores.
- `src/Modules`: contratos y reglas de dominio.
- `src/BuildingBlocks`: capacidades transversales.
- `tests`: pruebas automatizadas.
- `docs`: documentación oficial única, incluidos módulos y ADR.

## Inicio rápido

Requisitos: Git, .NET SDK definido en `global.json`, Node.js compatible y pnpm.

```powershell
dotnet tool restore
dotnet restore Gaia.Platform.slnx
Set-Location apps\web
pnpm install --frozen-lockfile
Set-Location ..\..
```

Configure los archivos locales a partir de los `.example`; no versione secretos. Consulte [Instalación, configuración y operación](docs/06-instalacion-operacion.md) para Entra, Dataverse, SharePoint y ejecución.

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
dotnet build Gaia.Platform.slnx -c Debug --no-restore -warnaserror
dotnet test Gaia.Platform.slnx -c Debug --no-build --no-restore
Set-Location apps\web
pnpm exec tsc --noEmit
pnpm test
pnpm exec eslint . --max-warnings 0
pnpm build
```

## Seguridad

No confirme secretos, tokens, configuraciones reales, datos personales, exportaciones institucionales, logs, `bin`, `obj`, `.next` ni `node_modules`.
