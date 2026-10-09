# Comunicaciones y experiencia institucional

> Estado: **implementado con validaciones ambientales pendientes** · Verificado: **2026-10-09**
> Código principal: `Gaia.Modules.Communications`, adaptadores Dataverse y componentes de Intranet/Configuración.

## Capacidades

- tipos de evento y eventos;
- destacados del carrusel de Inicio;
- configuración pública del login;
- redes asociadas al login;
- ambientaciones visuales para Intranet, AdminCore o ambas;
- lectura autorizada de eventos y banners en Intranet.

La Intranet consume contenido; la edición vive en AdminCore. Cumpleaños y personas no son contenido editorial: proceden de ThirdParties. La navegación procede de Security y no debe duplicarse en un catálogo editorial.

## Destacados

Dataverse conserva contenido, vigencia, orden, estado y referencias; SharePoint conserva imágenes de escritorio y móvil. El navegador obtiene los binarios por streaming de la API, sin IDs ni URL técnicas.

Estados:

1. Creada: puede enviarse a diseño o rechazarse.
2. En diseño: puede publicarse o rechazarse.
3. Publicada: puede cerrarse.
4. Cerrada.
5. Rechazada.

Publicar exige imagen de escritorio, registro activo y estado En diseño. Inicio muestra solo registros activos, publicados y vigentes. La variante móvil usa escritorio como respaldo. Reemplazar una imagen actualiza primero la referencia; eliminar retira la referencia y solicita limpieza controlada. Una limpieza remota fallida no debe fingir que la fila eliminada reapareció.

Permisos: `COM.DESTACADOS.VER`, `CREAR`, `ACTUALIZAR`, `ADMINISTRAR` y `ELIMINAR`. Eliminar es independiente y no se hereda de inactivar.

## Ambientación visual

Una campaña define ámbito, tema, efecto, vigencia, intensidad, colores, texto, URL e imágenes. Reglas:

- fin posterior al inicio;
- publicar retira campañas superpuestas del mismo ámbito;
- activa significa publicada, vigente y compatible con la superficie;
- colores opcionales `#RRGGBB` y URL solo HTTP(S);
- imágenes JPG/PNG/WebP de hasta 8 MB;
- `prefers-reduced-motion` detiene animaciones;
- la capa decorativa no intercepta interacción salvo su enlace explícito.

Choices actuales: ámbito Intranet/AdminCore/Ambos; efectos Ninguno, Partículas suaves, Nieve, Confeti, Elementos flotantes y Luces ambientales; estado Borrador/Publicada/Retirada; intensidad Sutil/Media/Destacada.

## Login institucional

La pantalla previa al login consume una instantánea pública, no Dataverse delegado por visita. AdminCore debe ejecutar “Actualizar publicación” después de cambiar la configuración.

- Escritorio admite imagen JPG/PNG/WebP de hasta 8 MB o video MP4/WebM de hasta 100 MB.
- Tableta y móvil admiten imagen.
- El video se reproduce silenciado, en bucle y `playsInline`; en pantallas pequeñas se usa imagen.
- Una imagen institucional actúa como fallback.
- La API soporta rangos para medios y no expone el proveedor físico.

Los límites se coordinan con API, proxy, Graph y SharePoint antes de ampliarlos.

## Eventos

Los endpoints administrativos gestionan tipos, eventos y cambios de estado. Intranet consulta un rango temporal autorizado. Categorías, audiencias, inscripción o recurrencia solo deben ampliarse si existe un requisito y modelo confirmado.

## Navegación

Intranet y AdminCore conservan shells independientes. AdminCore configura Comunicaciones; Intranet muestra únicamente contenido vigente y autorizado. Una persona administradora no recibe controles editoriales dentro de la experiencia de consumo.

## Verificación ambiental

- confirmar nombres de columnas mediante metadata;
- probar carga, reemplazo, streaming y limpieza de variantes;
- validar vigencias y cambios de estado;
- revisar efectos con movimiento normal y reducido en escritorio/móvil;
- ejecutar el bootstrap de permisos solo por un administrador autorizado;
- no crear campañas, seeds ni permisos por iniciativa del código.
