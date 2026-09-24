# Módulo de ambientación visual

Revisado: 2026-09-23.

## Objetivo

Administrar campañas visuales temporales para Intranet, AdminCore o ambas superficies. Los datos y la vigencia se almacenan en Dataverse; las imágenes se almacenan en SharePoint mediante la infraestructura común de archivos.

## Arquitectura

- Contratos y endpoints: `src/Modules/Communications/Gaia.Modules.Communications`.
- Persistencia: `DataverseVisualAmbienceStore`, usando `IDataverseDelegatedClientFactory` y metadata.
- Imágenes: `IFileStorage` e `IFileStorageMaintenance`; Dataverse guarda únicamente referencias seguras.
- Administración: `/configuracion/ambientacion`.
- Presentación: `VisualAmbienceLayer`, montada dentro de `SecurityProvider` y aplicada solo a usuarios autenticados.
- Permisos: reutiliza las políticas editoriales vigentes de destacados de Comunicaciones. No se crearon ni asignaron permisos en Dataverse.

## Tabla Dataverse

Nombre lógico validado en ejecución: `gaia_ambientacionvisual`. Las columnas se resuelven por schema name mediante metadata: `gaia_Nombre`, `gaia_Codigo`, `gaia_Descripcion`, `gaia_Ambito`, `gaia_Tema`, `gaia_Efecto`, `gaia_EstadoPublicacion`, `gaia_FechaInicio`, `gaia_FechaFinalizacion`, `gaia_Intensidad`, `gaia_ColorPrincipal`, `gaia_ColorSecundario`, `gaia_ColorAcento`, `gaia_PermitirAnimacion`, `gaia_MostrarDecoracionSuperior`, `gaia_MostrarFondoDecorativo`, `gaia_TextoPromocional`, `gaia_UrlDestino`, `gaia_TextoAlternativo`, `gaia_ImagenEscritorio` y `gaia_ImagenMovil`.

No existen columnas funcionales duplicadas para fecha de publicación o publicado por; se usan los metadatos estándar de Dataverse.

## Choices

- Ámbito: `1 Intranet`, `2 AdminCore`, `3 Ambos`.
- Efecto: `1 Ninguno`, `2 Partículas suaves`, `3 Nieve`, `4 Confeti`, `5 Elementos flotantes`, `6 Luces ambientales`.
- Estado: `1 Borrador`, `2 Publicada`, `3 Retirada`.
- Intensidad: `1 Sutil`, `2 Media`, `3 Destacada`.

`Tema` es texto abierto y no controla la lógica de renderizado.

## Reglas

- La fecha final debe ser posterior a la inicial.
- Una publicación nueva retira publicaciones existentes cuyo ámbito se superponga.
- La consulta activa exige estado Publicada, registro activo, ámbito compatible y fecha actual dentro de la vigencia.
- Los colores son opcionales y usan `#RRGGBB`.
- La URL promocional, cuando existe, debe ser HTTP(S).
- Imágenes: JPG, PNG o WebP, máximo 8 MB. Recomendaciones: escritorio `1920×320`, móvil `1080×320`.
- `prefers-reduced-motion` detiene las animaciones sin retirar la ambientación estática.
- La capa decorativa usa `pointer-events: none`; únicamente el mensaje enlazable admite interacción.

## Endpoints

- `GET/POST /api/communications/visual-ambiences`
- `PUT /api/communications/visual-ambiences/{id}`
- `POST /api/communications/visual-ambiences/{id}/publish|retire`
- `PUT/GET/DELETE /api/communications/visual-ambiences/{id}/images/{desktop|mobile}`
- `GET /api/communications/active-visual-ambience?surface=intranet|admincore`

## Verificación

- `dotnet build`: correcto, cero advertencias y errores.
- `dotnet test`: 312 pruebas superadas.
- `pnpm exec tsc --noEmit`: correcto.
- `pnpm build`: correcto; ruta `/configuracion/ambientacion` exportada.

## Pendientes operativos

- Confirmar con una sesión delegada que todos los schema names publicados en Dataverse coinciden exactamente; el adaptador falla de forma explícita si alguno no existe.
- Crear datos de campaña únicamente desde la interfaz; no ejecutar seeds ni modificar Dataverse automáticamente.
- Revisar visualmente cada efecto en escritorio y móvil con una campaña de prueba antes de publicar una campaña institucional.
