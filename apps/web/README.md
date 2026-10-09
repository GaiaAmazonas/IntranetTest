# Frontend Gaia

Frontend compartido de Intranet y AdminCore, construido con Next.js 16, React 19, TypeScript y Tailwind CSS 4.

La arquitectura completa está en [`../../docs/README.md`](../../docs/README.md) y el sistema de diseño en [`../../docs/05-frontend-diseno.md`](../../docs/05-frontend-diseno.md).

## Configuración

Crear `.env.local` a partir del ejemplo cuando sea necesario:

```dotenv
NEXT_PUBLIC_GAIA_API_URL=https://localhost:7168
```

Las variables `NEXT_PUBLIC_*` llegan al navegador y no pueden contener secretos.

## Desarrollo

```powershell
pnpm install --frozen-lockfile
pnpm dev
```

La aplicación escucha en `http://localhost:3000`. La API debe estar disponible en la URL configurada y aceptar ese origen.

## Verificación

```powershell
pnpm exec tsc --noEmit
pnpm test
pnpm exec eslint . --max-warnings 0
pnpm build
```

El proyecto usa exportación estática (`output: "export"`) y genera `out/`. No se deben implementar reglas empresariales en rutas API de Next o Server Actions; el backend es Gaia.Api.

## Convenciones

- Reutilizar `src/components`, `src/lib/api-client.ts` y tokens de `src/app/globals.css`.
- Actualizar `src/lib/route-access.ts` cuando se añada una ruta protegida.
- Tratar el control de ruta del cliente como experiencia, no como autorización definitiva.
- Mantener estados de carga, vacío, error, `401` y `403`.
- No mostrar GUID, nombres lógicos ni códigos técnicos como instrucciones de usuario.
