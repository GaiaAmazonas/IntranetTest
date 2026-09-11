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
