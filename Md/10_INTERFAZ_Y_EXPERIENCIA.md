# Interfaz y experiencia de usuario

## 1. Identidad visual

Codex recibirá un toolkit organizacional con colores, tipografías, imágenes y componentes. Debe reutilizar esos tokens y componentes. La imagen del organigrama sirve como referencia visual, no como especificación exacta de CSS.

Reglas:

- No codificar colores de Gaia repetidamente en componentes.
- Definir tokens semánticos: directivos, subdirección, asesoría, coordinación directa, coordinación transversal, operativa, activo, advertencia y error.
- Mantener contraste y accesibilidad.
- Diseñar para escritorio y resoluciones menores sin perder navegación.

## 2. Navegación sugerida

- Inicio o tablero.
- Organización.
  - Organigrama.
  - Unidades.
  - Tipos de unidad.
  - Cargos y roles.
- Terceros.
  - Directorio.
  - Vinculaciones.
  - Validaciones documentales.
- Inventarios.
  - Resumen.
  - Elementos.
  - Productos.
  - Asignaciones.
  - Movimientos.
  - Actas.
  - Ubicaciones.
  - Bajas.
- Administración.
  - Catálogos.
  - Importaciones.
  - Auditoría.

La navegación final debe adaptarse a la estructura existente del proyecto.

## 3. Pantallas organizacionales

### Organigrama

- Zoom y desplazamiento.
- Filtro por estado o rama.
- Búsqueda por código o nombre.
- Tarjetas con color por tipo de unidad.
- Indicador de estado.
- Ficha de detalle al seleccionar una unidad.

### Administración de unidades

- Tabla con filtros.
- Formulario con selector de padre.
- Vista previa de la ubicación en el árbol.
- Advertencia antes de cambiar el padre.

## 4. Pantallas de terceros

### Directorio

Columnas no sensibles: nombre, estado de vinculación, unidad principal, cargo, correo corporativo y acciones permitidas.

### Ficha

Pestañas:

- Datos personales.
- Vinculaciones.
- Organización.
- Estudios.
- Idiomas.
- Formación.
- Experiencia.
- Documentos.
- Inventario asignado.
- Historial.

Los campos sensibles deben ocultarse según permisos.

## 5. Pantallas de inventario

### Tablero

- Total de elementos por estado.
- Asignados, disponibles, mantenimiento y baja.
- Distribución por clase.
- Alertas de inconsistencias.
- Elementos sin acta o serial.

### Elementos

- Filtros por producto, marca, estado, ubicación, tercero, unidad y nivel de control.
- Búsqueda por serial, placa, modelo o nombre.
- Acciones contextuales según estado.

### Ficha del elemento

- Identificación.
- Situación actual.
- Responsable y ubicación.
- Historial cronológico.
- Actas y soportes.
- Datos financieros básicos.

### Flujo de asignación

1. Seleccionar receptor o unidad.
2. Validar vinculación activa.
3. Seleccionar elementos disponibles.
4. Confirmar condición física.
5. Definir lugar y fecha.
6. Generar movimientos.
7. Generar acta.
8. Registrar aceptación o firma según mecanismo definido.

## 6. Mensajes y validaciones

- Explicar el error y cómo corregirlo.
- No mostrar errores técnicos crudos.
- Distinguir bloqueo, advertencia e información.
- Confirmar acciones irreversibles o sensibles.
- Mostrar identificadores de seguimiento para errores de servidor.

## 7. Estándar obligatorio de botones y color

Toda pantalla nueva o modificada debe reutilizar las clases y tokens globales; no debe reconstruir botones con colores o bordes locales.

- Acción principal: `gaia-button gaia-button-primary`. Se usa una sola acción dominante por contexto: crear, guardar, publicar o confirmar.
- Acción secundaria: `gaia-button gaia-button-secondary`. Se usa para editar, gestionar, volver, expandir o acciones no destructivas.
- Acción destructiva confirmada: `gaia-button gaia-button-danger`. No usar rojos escritos directamente en JSX.
- Acción de icono: debe conservar un área táctil mínima de 36 × 36 px, `aria-label`, borde `--gaia-line-strong`, fondo de superficie y foco visible.
- Los botones deben usar `--gaia-green-800`, `--gaia-green-900`, `--gaia-line`, `--gaia-line-strong`, `--gaia-accent-pale` y demás tokens semánticos. Está prohibido copiar hexadecimales entre componentes cuando exista un token equivalente.
- Hover: cambio discreto de fondo/borde y, como máximo, desplazamiento vertical de 1 px. Nunca debe ser la única señal de una acción.
- Foco: debe seguir visible para teclado. Disabled debe conservar el texto legible y bloquear clics repetidos.
- Móvil: las acciones deben envolver o pasar a una columna; ningún selector o botón puede ampliar el viewport. Los botones con texto deben ocupar todo el ancho cuando no caben cómodamente.
- Una tarjeta interactiva debe tener hover suave, pero su acción de expandir/contraer también debe estar representada por un botón y `aria-expanded`.
- Las acciones `Gestionar`, `Agregar contenido`, edición, eliminación y publicación deben seguir estas variantes compartidas en todos los módulos.
- Las barras de acciones dentro de una tarjeta deben agrupar controles relacionados sobre una superficie secundaria; los botones de icono tienen el mismo tamaño y radio. Una acción destructiva de icono usa fondo de peligro suave, no un bloque rojo dominante.
- Todos los botones deben definir `hover`, `focus-visible`, `disabled` y `active`. El color principal siempre procede de `--brand-primary`, de modo que responda al tema seleccionado; no debe fijarse al verde institucional cuando el usuario haya elegido otra paleta.
- En una fila con varias acciones, solo la acción que hace avanzar el flujo puede ser primaria. Editar, expandir, volver, duplicar y gestionar son secundarias; eliminar es peligro discreto y solo se vuelve sólido dentro de una confirmación final.

Antes de cerrar un cambio visual se debe comprobar al menos escritorio, 768 px, 390 px, textos largos, zoom y colores de tema configurables.

## 8. Tablas de gestión

- Los encabezados se centran mediante la regla transversal de `.gaia-app-page table thead th`; no repetir alineaciones locales en cada módulo.
- Una tabla operativa debe usar separación por `--gaia-line`, encabezado con superficie de acento, hover discreto, celdas verticalmente centradas y acciones claramente diferenciadas.
- Los estados se expresan con etiquetas semánticas y texto; el color nunca es la única señal.
- En móvil debe habilitarse desplazamiento horizontal controlado o una transformación a tarjetas cuando las acciones no resulten legibles.

## 9. Participación en capacitaciones

La pantalla de Intranet usa el mismo `Button` y `Badge` del toolkit, no una paleta paralela de botones. La acción primaria avanza el flujo (confirmar material, iniciar o enviar evaluación); volver, actualizar, expandir y contraer son secundarias. Mantener hover, foco visible, estado deshabilitado y altura táctil mínima de 42 px.

Las preguntas se contienen completamente en su superficie, con título dentro del bloque y etiqueta accesible para el control. Una lista desplegable es selección única; nunca convertirla en checkboxes. No incluir respuestas correctas en los datos iniciales del participante. Los controles deben envolver en 390 px sin ampliar el viewport. La hoja `training-participant.css` tiene alcance por clases del módulo y respeta `--brand-primary`.

El estado de carga y el error de una consulta no equivalen a cero asignaciones. Conservar esa distinción en Inicio, catálogo y resultados. Véase `31_CAPACITACIONES_FLUJO_Y_PRUEBAS.md` para reglas y pruebas funcionales.
## 10. Portada y navegación de Intranet

- Inicio, Personas y Calendario permanecen en primer nivel. Mi espacio agrupa Mis aplicaciones, Mis solicitudes de ayuda y Mis capacitaciones; cada acceso conserva su permiso y ruta. No mostrar Helpdesk como etiqueta principal al colaborador.
- Accesos rápidos: una sola fila, navegación anterior/siguiente, sin scrollbar. En móvil se muestra un acceso por página; escritorio muestra cuatro. El bloque omite Explorar aplicaciones; ese acceso permanece en Mi espacio. La fila de accesos tiene 102 px. Orden: Buscador de personas, Mis solicitudes, Mis capacitaciones, Consultar agenda. No ocultar accesos sin ofrecer navegación.
- Celebraciones y Agenda comparten altura en cada breakpoint. Agenda muestra máximo dos eventos por página.
- La fila inferior conserva orden Cumpleaños, Herramientas, Capacitaciones (derecha), con tarjetas compactas de 270 px y navegación paginada. Cumpleaños muestra cuatro personas por página, en una sola fila. No reintroducir una cuarta tarjeta de soporte ni barras de scroll.
- Mis capacitaciones permite alternar Listado/Bloques sin perder búsqueda ni filtro. Comenzar, Continuar y Ver capacitación dependen del estado, no solo del porcentaje de contenido. Una aprobada se consulta sin iniciar nuevos intentos.
- Calificación manual de preguntas abiertas y revisión editorial para publicar son controles diferentes. Seguimiento debe indicar dónde consultar Resultados y calificar, sujeto a permisos.
### Panel Mi espacio

El desplegable reutiliza superficies y acentos del tema: encabezado breve, icono, nombre, descripción y flecha por acceso. Tiene ancho acotado al viewport y muestra solo enlaces autorizados. No inventar contadores, personalización o Ver todo sin una funcionalidad real. Se abre con botón, conserva aria-expanded/aria-controls, se cierra al salir del panel o pulsar Escape y devuelve el foco al botón con Escape. En móvil permanece dentro del menú lateral y permite desplazamiento vertical si la altura no alcanza. Perfil y Mi espacio tienen disparadores de tamaño coherente.

### Línea visual compartida de Intranet
Inicio, Personas, Mesa de ayuda y Capacitaciones deben usar los tokens comunes: `--brand-primary` para acciones, `--brand-primary-hover` para interacción, `--gaia-accent-soft` y `--gaia-accent-pale` para fondos, `--surface-card` para tarjetas, `--intranet-ink` / `--intranet-muted` para textos y `--gaia-line` para bordes. No introducir verdes o turquesas particulares por página. Amarillo, rojo y verde semánticos se reservan para advertencias, errores y resultados, no para navegación. Las pestañas activas usan un fondo suave sin sombra o desplazamiento.
Cumpleaños próximos muestra solo ocurrencias futuras dentro de 60 días, ordenadas por fecha, con cuatro personas por página. Los cumpleaños de hoy tienen su celebración independiente.
Mi espacio usa un panel compacto y un icono de panel personal distinto al de aplicaciones. En la conversación de solicitudes los mensajes propios/del solicitante aparecen a la izquierda y los del equipo a la derecha. Atender observación queda habilitado al abrir y exige texto y documento mediante validación del formulario antes de confirmar.
### Ficha personal e identidad de la aplicación
Mi perfil consulta `/api/profile/`: la API obtiene el tercero de la identidad autenticada; no acepta un identificador arbitrario ni requiere acceso administrativo a Talento Humano. Muestra foto institucional, nombre, documento de la cuenta, correo, teléfono corporativo, cargo, unidad/código y sede; los datos faltantes se identifican sin inventarlos.
El saludo del banner no duplica la foto del menú. El icono vectorial está en `public/brand/gaia-app-icon.svg`, con PNG de 32, 180, 192 y 512 px y favicon ICO. `scripts/generate-app-icons.mjs` regenera las versiones; el manifest habilita la identidad visual del atajo móvil, sin prometer uso sin conexión.
La transición de atender observación también se incluye cuando el solicitante tiene permisos administrativos; se preservan las otras transiciones del equipo.
