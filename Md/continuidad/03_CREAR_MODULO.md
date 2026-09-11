# Receta para crear un módulo Gaia

Revisado: 2026-09-02. Usar junto con los otros documentos de continuidad.

## 1. Delimitar sin reescribir

1. Leer el contexto principal, revisar `git status --short` y las instrucciones locales aplicables.
2. Definir si el módulo vive en AdminCore, Intranet o ambos; listar operaciones y quién las puede ejecutar.
3. Revisar el módulo existente más cercano: Organization para catálogos/jerarquía, ThirdParties para contactos/directorio, Communications para publicaciones/estados.
4. Distinguir nuevo módulo técnico, nuevo elemento del árbol de navegación y nueva aplicación externa. No todos requieren crear un proyecto .NET.
5. Identificar tablas, columnas, choices, relaciones, permisos técnicos y datos ya disponibles. Si faltan, entregar al usuario la especificación exacta antes de depender de campos inventados.

## 2. Entrega vertical recomendada

```text
Reglas/DTO/puerto → adaptador Dataverse → DI y endpoints protegidos
    → rutas y permisos frontend → componentes → pruebas → documentación
```

| Archivo/lugar | Cambio habitual |
|---|---|
| `src/Modules/<Nombre>/Gaia.Modules.<Nombre>/` | Proyecto solo si corresponde; contratos, reglas puras, endpoints y extensión del módulo. |
| `src/Gaia.Api/Infrastructure/Dataverse/<Nombre>/` | Adaptador que implementa el puerto del módulo y reutiliza cliente/metadata. |
| `Gaia.Platform.slnx` y `src/Gaia.Api/Gaia.Api.csproj` | Referencias de proyecto cuando se crea uno nuevo. |
| `src/Gaia.Api/Program.cs` | DI scoped y mapeo del módulo. |
| `src/Modules/Security/.../SecurityContracts.cs` | Códigos/políticas nuevas cuando son necesarias; revisar scope AdminCore. |
| `apps/web/src/app/<ruta>/page.tsx` | Entrada de página; delegar UI a feature. |
| `apps/web/src/features/<feature>/` | Componentes, tipos/reglas frontend y CSS específico. |
| `apps/web/src/lib/route-access.ts` | Regla de acceso de la nueva ruta, con pruebas. |
| Navegación y catálogo de módulos | Incorporación según patrón actual; configuración Dataverse puede requerir preparación separada. |
| `tests/Gaia.ArchitectureTests/` y tests frontend | Reglas, permisos, payloads, errores y regresiones. |

No copiar códigos de permisos de otro módulo para que “funcione”. Documentar recurso/acción, nivel de entrada y operaciones habilitadas. Crear metadatos o seeds en código no autoriza ejecutar bootstrap ni asignar roles en el entorno.

## 3. Diseño / toolkit / tokens

La expresión de conversaciones anteriores “turking” no identifica una tecnología verificable del repositorio. Esta guía cubre tanto **toolkit/tokens visuales** como **tokens de autenticación** (documento de seguridad). Confirmar si el usuario se refiere a otra cosa.

- Reutilizar `components/ui.tsx`, feedback, shells y patrones existentes antes de añadir componentes/dependencias.
- Intranet: `features/intranet/intranet-shell.tsx`, `intranet-footer.tsx`, `intranet.css`. Tokens existentes incluyen `--intranet-ink`, `--intranet-green`, `--intranet-teal`, `--intranet-muted`, `--intranet-border`, `--intranet-radius`, `--intranet-shadow`; comprobar declaración vigente.
- Tipografía existente del layout/globales; no descargar fuentes nuevas para una sola pantalla.
- Mantener ancho de contenido coherente, jerarquía título/subtítulo/acción, bordes suaves y hover discretos. No añadir alturas fijas grandes para “alinear” paneles.
- Distinguir carga, vacío, error y contenido. No mostrar “no hay registros” mientras la consulta sigue pendiente.
- Reservar espacio para contenido progresivo sin bloquear banner por cumpleaños o recursos secundarios cuando se diseñe una carga independiente.
- Asegurar correos/nombres largos, 0/1/muchos elementos, árbol profundo, móvil y zoom. No ocultar acciones en móvil sin alternativa.
- Foco visible, labels, navegación por teclado y reduced motion. No crear botones anidados dentro de botones ni usar hover como única forma de obtener información.
- No modificar footer, login ni AdminCore por herencia accidental de CSS de una feature.

## 4. Rendimiento y persistencia

- Medir API y Dataverse con logs existentes; separar tiempo de autenticación, consulta, descarga de imagen y render.
- Consultas independientes en paralelo; filtros/select/paginación en origen cuando sea posible. Evitar traer todos los registros por cada pulsación.
- Preservar búsqueda, selección, scroll y panel al mutar. Bloquear únicamente la acción que está guardando, con finalización en `finally`.
- No mostrar éxito antes de respuesta válida; verificar que una recarga confirme la escritura. No tocar usuarios ajenos al ID seleccionado.
- No agregar reintentos infinitos ni refrescos globales para solucionar errores de estado.
- Modelar concurrencia e idempotencia donde una operación pueda duplicarse; no afirmar que el sistema completo ya las implementa.

## 5. Ejecución y verificación

Desde la raíz, utilizando SDK/Node disponibles en la máquina (no hardcodear la ruta personal de otro desarrollador):

```powershell
dotnet restore Gaia.Platform.slnx
dotnet build Gaia.Platform.slnx
dotnet test Gaia.Platform.slnx
Set-Location apps/web
pnpm install --frozen-lockfile
pnpm exec tsc --noEmit
pnpm test
pnpm lint
pnpm build
```

Instalar/restaurar solo cuando hace falta; no actualizar lockfiles por rutina. Para ejecutar: API `dotnet run --project src/Gaia.Api/Gaia.Api.csproj --launch-profile https`; frontend `pnpm dev` desde `apps/web`.

Configurar mediante archivos de ejemplo y user-secrets: `MicrosoftEntra`, `Dataverse`, `WebApplication:BaseUrl`, `NEXT_PUBLIC_GAIA_API_URL`. Consultar README y ejemplos; nunca copiar secretos al Markdown. URL habitual local: frontend `http://localhost:3000`, API `https://localhost:7168`.

Además de tests:

- Crear/editar/consultar con permiso; denegar sin permiso.
- Probar 0/1/muchos registros, cancelación/error y fechas límite.
- Revisar árbol/navegación y autorización por aplicación individual.
- Validar persistencia tras recarga y que otros registros permanezcan iguales.
- Revisar escritorio/móvil, nombres largos, foco y hover. Una compilación exitosa no demuestra calidad visual: no declarar verificación visual sin haberla realizado.
- Reportar fallos preexistentes de lint/test separados de los introducidos; no decir que todo pasó si solo corrió TypeScript.

## 6. Publicación

- Un commit/push no actualiza automáticamente el servidor. Publicar solo si lo pide el usuario.
- Next exporta `apps/web/out`; la API necesita su propio build/publish. No desplegar solo archivos sueltos ni mezclar HTML, JS y payloads RSC de builds diferentes.
- Hubo 404 de `__next...txt` tras publicar: comprobar contenido completo del export, reglas del servidor, base path y caché. No asumir que borrar caché resuelve archivos ausentes.
- No usar force push ni borrar trabajo ajeno. Confirmar remoto/rama: existe flujo de Git personal y espejo de empresa, cuyos detalles deben verificarse antes de operar.

## 7. Checklist de entrega y memoria

- [ ] Alcance implementado y módulos ajenos preservados.
- [ ] Contratos, DI, endpoint, política y UI coherentes.
- [ ] Datos reales; mocks/recursos temporales identificados y aislados.
- [ ] Permisos técnicos Dataverse pendientes entregados al usuario, sin sobreasignación.
- [ ] Validación backend, errores frontend y estado de guardado completos.
- [ ] Tests/compilación/visual reportados con exactitud.
- [ ] Documentos de continuidad actualizados con nuevas tablas, rutas, reglas y pendientes.
- [ ] Sin secretos, exportaciones institucionales ni logs en el commit.

Para un módulo nuevo añadir `Md/modulos/<nombre>.md` con: objetivo, estado real, responsables por archivo, contratos y rutas, permisos funcionales, matriz técnica por operación Dataverse, reglas/fechas, diagrama de flujo, pruebas, despliegue y pendientes. Enlazarlo desde `CONTEXTO_PARA_NUEVO_CHAT.md`.
