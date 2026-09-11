# Arquitectura y convenciones verificadas

Revisado: 2026-09-02. Rutas relativas a la raíz del repositorio.

## Mapa de responsabilidades

```text
apps/web/src/
  app/                         rutas Next App Router, layouts y globals.css
  components/                  shells, seguridad, feedback y componentes compartidos
  features/intranet/           Inicio, Personas, Calendario, aplicaciones, Helpdesk, footer
  features/communications/     administración de comunicaciones
  features/organization/      interfaces organizacionales
  features/talent/            interfaces de talento humano
  lib/                         api-client, permisos, reglas de rutas y exportaciones
apps/web/public/               imágenes y SVG locales públicos
src/Gaia.Api/
  Program.cs                  composition root, CORS, pipeline, registros DI y rutas
  Infrastructure/Dataverse/   clientes, metadata, serialización y adaptadores por módulo
src/Modules/
  Identity/                   OIDC, cookies, login/logout
  Security/                   contratos, reglas, políticas y endpoints
  Organization/               unidades, sedes, cargos y contratos de operaciones
  ThirdParties/               terceros, contactos, directorio e importaciones
  Communications/             eventos y banners
  Inventory/                  estructura pendiente de adaptador Dataverse
src/BuildingBlocks/           abstracciones mínimas compartidas, IModule
tests/Gaia.ArchitectureTests/  xUnit: dependencias, reglas, adaptadores y autorización
Md/                           especificaciones históricas y continuidad técnica
```

La separación de dominio/aplicación/infraestructura es conceptual y por proyectos; **no todos los módulos tienen carpetas Domain/Application**. No imponer CQRS, MediatR, repositorios genéricos ni un ORM nuevo por uniformidad.

## Tecnologías y construcción

- `Directory.Build.props`: .NET 10, nullable habilitado, implicit usings y warnings como errores.
- `global.json`: SDK fijado; consultarlo antes de cambiar versiones.
- `Gaia.Platform.slnx`: solución .NET. `src/Gaia.Api/Gaia.Api.csproj` referencia los módulos activos.
- `apps/web/package.json`: Next 16.2.12 / React 19.2.4 / TypeScript 5 / Tailwind 4 / Lucide / ExcelJS; Vitest y ESLint.
- `apps/web/next.config.ts`: export estático, trailing slash, imágenes no optimizadas; `GAIA_BASE_PATH` opcional. No añadir Server Actions ni rutas API Next sin revisar esta restricción.

## Flujo de una operación

```text
Página App Router → componente de feature → apiRequest<T>
  → API ASP.NET: cookie + política funcional
  → interfaz del módulo → adaptador Dataverse
  → cliente delegado + metadata → consulta/escritura Dataverse
  → DTO JSON → estado local / feedback / actualización de la pantalla
```

`app/layout.tsx` envuelve las rutas con FeedbackProvider, SecurityProvider y RouteAccessGate. Intranet usa su shell/footer; AdminCore usa sus componentes propios. Consultar los layouts concretos antes de añadir una ruta.

## Convenciones de código

### C#

- Namespace `Gaia.Modules.<Modulo>` para contratos/reglas, `Gaia.Api.Infrastructure.Dataverse.<Modulo>` para adaptadores.
- Tipos, métodos y propiedades en PascalCase; variables/parámetros camelCase. Interfaces con `I`; métodos asíncronos con `Async`.
- Preferir `record` para DTO inmutables y contratos de request/response. `Guid` para identificadores, `DateOnly` para fecha civil y `DateTimeOffset` para instantes.
- `string?` y tipos anulables expresan ausencia; no usar nulo como sinónimo de activo.
- Inyectar interfaces por constructor; registrar la implementación scoped en `Program.cs` según patrón existente. Clientes HTTP por fábrica; no crear uno nuevo por fila.
- I/O con `async/await`, `CancellationToken`, `using`/`await using`. Paralelizar consultas independientes con límites razonables; evitar `.Result`, `.Wait()` y N+1.
- Las reglas puras validan periodos, jerarquía, estados y conflictos. El endpoint traduce errores a resultados HTTP. El adaptador traduce Dataverse al contrato, no define cómo se ve una tarjeta.
- Endpoints agrupados mediante extensiones `Map...Endpoints`; DI mediante `Add...Module` cuando corresponda. Los módulos existentes tienen variantes: tomar el más cercano como ejemplo.

### TypeScript / React

- Componentes PascalCase; funciones/variables camelCase; constantes descriptivas. Tipar DTO y props; evitar `any`.
- `"use client"` cuando hay hooks/eventos/browser API. No leer `window` incondicionalmente durante render exportado.
- Reutilizar `apiRequest<T>` de `lib/api-client.ts`, `useSecurity`, feedback y componentes existentes. El cliente maneja JSON, FormData, 204 y errores; no duplicar tokens ni lógica de sesión.
- Estado local para filtros, paginación, selección y panel. Debounce de búsquedas; cancelar o ignorar respuestas obsoletas al desmontar/cambiar filtros.
- Después de una mutación actualizar solo el dato afectado y reconciliar con backend. Rollback/error visible si se usó actualización optimista.
- Evitar efectos con dependencias inestables y bucles de refresco. Claves estables basadas en ID, no en índice cuando la lista cambia.
- CSS de Intranet bajo `.intranet-*`; AdminCore conserva su estilo `.gaia-*`. No cambiar globales para solucionar un componente aislado.

## Dataverse: patrón de conexión

Archivos base: `DataverseConfiguration.cs`, `DataverseDelegatedClientFactory.cs`, `DataverseMetadataResolver.cs`, `DataverseJson.cs` y adaptadores de `Infrastructure/Dataverse`.

1. Resolver configuración y obtener HttpClient delegado del usuario.
2. Resolver la tabla por nombre lógico mediante `TableAsync`.
3. Usar `EntitySetName`, `PrimaryIdAttribute` y `Attribute(schemaName)`; no adivinar plurales ni convertir schema names a minúsculas arbitrariamente.
4. Para relaciones usar `Relationship(...)` y su NavigationProperty; para lecturas de lookup respetar `_<atributo>_value`; para escrituras seguir el `@odata.bind` del adaptador de referencia.
5. Resolver choices/tipos de campo con helpers. Existen campos históricos codificados como texto: no asumir que todos aceptan enteros JSON/OData.
6. Limitar `$select`; escapar literales de texto de filtros y validar parámetros. Seguir `@odata.nextLink` para paginar. No usar `$skip`.
7. No enviar campos nulos/desconocidos o cambiar estado por convenciones inventadas. Consultar el esquema real antes de crear payloads.

`ReadAllAsync` recorre todas las páginas; usarlo solamente cuando se justifique. `ReadPageAsync` entrega Items, Total y NextLink. Revisar cómo se comporta `$top` junto con el tamaño de página en el entorno antes de copiar un patrón de paginación.

## Datos y fechas

- Entidades Dataverse usan prefijo `gaia_`. No aplicar automáticamente la sugerencia histórica de snake_case a los schema names existentes.
- Unidades `gaia_organizacion`, cargos `gaia_cargo`, sedes `gaia_sede`, asignaciones `gaia_asignacionorganizacional`; personas `gaia_terceros`; contactos `gaia_correocolaborador` y `gaia_telefonocolaborador`.
- Relaciones y atributos exactos: obtenerlos del adaptador/metadata, no de esta lista abreviada.
- Vigencia: fecha inicial/final; conservar historial y validar solapamientos. Una publicación debe evaluarse por su intervalo, no por coincidir su fecha inicial con hoy.
- Inicio calcula el día institucional con zona de Bogotá en la API. Algunas vistas usan `Date` del navegador; comprobar bordes de día, medianoche, mes y año antes de introducir filtros temporales.

## Documentación histórica útil

`Md/03_MODELO_DATOS_GENERAL.md`, `04_MODULO_ORGANIZACIONAL.md`, `05_MODULO_TERCEROS.md`, `06_MODULO_INVENTARIOS.md`, `08_REGLAS_DE_NEGOCIO.md`, `09_MIGRACION_Y_CALIDAD_DATOS.md`, `14_ADMINISTRACION_CONTENIDOS_INTRANET.md`. Contienen requisitos y objetivos; comprobar cuáles están implementados antes de depender de ellos.
