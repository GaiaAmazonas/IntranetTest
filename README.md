# Gaia Enterprise Platform

Plataforma empresarial modular para la gestión institucional de Gaia y la
proyección autorizada de información en su intranet.

## Alcance inicial

- Identidad, usuarios y permisos.
- Estructura organizacional.
- Terceros y vinculaciones.
- Inventario, asignaciones y movimientos.
- Auditoría, documentos e importaciones.

## Principios

- Monolito modular con límites explícitos por dominio.
- PostgreSQL como fuente transaccional.
- Backend en ASP.NET Core y frontend en Next.js.
- Software libre y despliegue portable.
- Auditoría, permisos y pruebas desde la primera entrega.

Los archivos originales de datos y diseño se mantienen fuera del control de
versiones porque pueden contener información institucional o personal.

## Desarrollo local

Requisitos instalados:

- .NET SDK 10
- Node.js 24 y pnpm
- PostgreSQL 18
- herramienta local `dotnet-ef`

Comandos principales:

```powershell
dotnet tool restore
dotnet build Gaia.Platform.slnx
dotnet test Gaia.Platform.slnx
dotnet run --project src\Gaia.Api\Gaia.Api.csproj
```

En otra terminal:

```powershell
Set-Location apps\web
pnpm dev
```

La API utiliza secretos de usuario de .NET para la conexión a PostgreSQL y la
contraseña del administrador inicial. Ninguna credencial debe agregarse a
`appsettings.json` ni al repositorio.

## Identidad

El primer incremento utiliza ASP.NET Core Identity con PostgreSQL y sesión
mediante cookie segura `HttpOnly`. Incluye:

- bloqueo temporal tras intentos fallidos;
- auditoría de inicio de sesión;
- roles y permisos propios de Gaia;
- administrador inicial configurable;
- endpoints protegidos para consultar y crear usuarios;
- estructura preparada para agregar Microsoft Entra ID como proveedor externo.

## Organización

El módulo organizacional mantiene su propio esquema `organization` y expone
gestión administrativa para:

- tipos de unidad con token de color;
- sedes;
- unidades organizacionales de jerarquía ilimitada;
- cargos institucionales;
- vigencias, estado y orden visual;
- auditoría de cada creación y modificación con valores antes/después.

Las unidades se inactivan en lugar de eliminarse. El backend calcula sus niveles,
impide la autorreferencia y rechaza cualquier cambio que produzca ciclos.
