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
