# Gaia — calidad y preparación para producción

Fecha de revisión: 18 de agosto de 2026.

## Dictamen

La solución compila correctamente en configuración Release y cuenta con una base sólida de autenticación, autorización, separación Intranet/AdminCore y acceso a Dataverse. Todavía no debe declararse lista para producción definitiva porque persisten dependencias funcionales y operativas que requieren cierre explícito.

## Controles técnicos verificados

- Autenticación corporativa mediante Microsoft Entra ID y OpenID Connect.
- Cookie de sesión `HttpOnly`, `Secure`, con expiración y renovación controladas.
- Token delegado para Dataverse conservado en backend.
- Autorización de endpoints mediante permisos explícitos.
- Protección de rutas del frontend, incluida navegación directa.
- Separación visual y de navegación entre Intranet y AdminCore.
- Redirecciones de login y logout restringidas al origen configurado.
- OpenAPI habilitado únicamente en desarrollo.
- Endpoint `/health` disponible para monitoreo.
- Logs estructurados de método, ruta, estado y duración de peticiones.
- Respuesta global controlada en producción para excepciones no previstas.
- Tratamiento específico de renovación de autenticación Dataverse.
- CORS restringido al origen de `WebApplication:BaseUrl` en producción.
- Cabeceras `X-Content-Type-Options`, `X-Frame-Options` y `Referrer-Policy` en la API.
- Estados de carga, error, vacío y acceso denegado en las nuevas experiencias.
- Catálogo de aplicaciones filtrado por permisos.
- Directorio sin documento, correo personal, teléfono personal ni año de nacimiento.

## Resultado de validaciones

- Backend Release: compilación correcta.
- Pruebas backend: 82 aprobadas, 0 fallidas.
- ESLint frontend: correcto.
- Pruebas frontend: 18 aprobadas, 1 omitida.
- TypeScript: correcto durante el build.
- Next.js production build: correcto.
- Rutas generadas: 16 rutas funcionales más las rutas técnicas de Next.js.

La prueba omitida debe revisarse antes del cierre formal para confirmar si corresponde a una condición deliberada o a cobertura pendiente.

## Cambios de endurecimiento aplicados

La política CORS dejó de estar limitada al entorno de desarrollo. En producción ahora utiliza exclusivamente el origen derivado de `WebApplication:BaseUrl`, admite credenciales y no acepta comodines.

La API incorporó manejo global de errores mediante Problem Details en producción, evitando devolver detalles internos o páginas de excepción.

Se agregaron cabeceras defensivas contra interpretación incorrecta de contenido, inclusión en marcos y filtración del referente.

## Bloqueos para producción definitiva

### 1. Dependencia de PostgreSQL

El arranque todavía registra contextos de Entity Framework y ejecuta migraciones de Organización, Terceros e Inventario. Inventario conserva operaciones sobre PostgreSQL. El servidor actual necesita una conexión PostgreSQL válida hasta retirar formalmente esta dependencia.

No se debe eliminar PostgreSQL hasta migrar las funcionalidades restantes, verificar datos y retirar inicializadores y paquetes de manera controlada.

### 2. Inventario incompleto en Dataverse

Debe cerrarse la migración funcional del módulo de Inventario antes de afirmar que la solución opera solamente con Dataverse.

### 3. Asignación Organizacional

Personas todavía no puede mostrar de manera confiable cargo, unidad y sede porque falta implementar o conectar la relación real de Asignación Organizacional.

### 4. Helpdesk

Permanece pendiente del levantamiento funcional. No existen aún trámites, flujos, tablas ni endpoints aprobados.

### 5. Contenidos institucionales

Noticias, comunicaciones y eventos continúan como referencia visual. Deben definirse responsables, fuente, modelo editorial, audiencia y almacenamiento de imágenes antes de conectarlos.

### 6. Aplicaciones externas

Help Desk y otras aplicaciones necesitan URL, propietario, disponibilidad y permiso explícito antes de publicarse.

## Controles de infraestructura obligatorios

- Dominio y DNS definitivos para frontend y API.
- Certificado TLS válido y renovación automatizada.
- Registro Entra separado para producción.
- Secreto o certificado almacenado fuera del repositorio.
- `WebApplication__BaseUrl` con el dominio real del frontend.
- `NEXT_PUBLIC_GAIA_API_URL` definido durante el build del frontend.
- `AllowedHosts` restringido mediante configuración de producción.
- Proxy inverso con cabeceras reenviadas configuradas y validadas.
- Puertos internos de Next.js y Kestrel no expuestos públicamente.
- Usuario de servicio sin privilegios administrativos.
- Logs centralizados, retención y alertas.
- Monitoreo del frontend, `/health`, Entra y Dataverse.
- Copias de configuración, procedimientos de recuperación y rollback.
- Actualización de dependencias con revisión de vulnerabilidades.
- Prueba de carga con concurrencia representativa.
- Ambientes separados de desarrollo, pruebas y producción.

## Pruebas pendientes en ambiente desplegado

Estas pruebas requieren dominios, proxy, certificado y usuarios reales; no pueden validarse únicamente con compilación local:

1. login y logout a través del dominio final;
2. cookies y callbacks detrás del proxy;
3. CORS entre los dominios definitivos;
4. acceso autorizado y denegado con usuarios de prueba;
5. intento de URL directa para todas las áreas protegidas;
6. renovación de sesión y `reauth_required`;
7. pérdida temporal de Dataverse;
8. tiempos de respuesta con 30 a 50 usuarios concurrentes;
9. comportamiento responsive en equipos institucionales reales;
10. navegación por teclado, lector de pantalla, contraste y zoom;
11. carga y descarga de archivos cuando esos módulos existan;
12. reinicio automático de frontend y API;
13. restauración y rollback de una versión.

## Criterios mínimos de salida

El piloto puede prepararse cuando exista infraestructura de pruebas, configuración segura y decisión formal sobre PostgreSQL. La producción definitiva exige además cerrar Inventario, ejecutar pruebas integrales en el dominio real y aprobar los pendientes de seguridad y operación.

Helpdesk y contenidos pueden entregarse después como módulos independientes siempre que las pantallas de referencia permanezcan claramente identificadas y no presenten datos ficticios como información real.
