# Línea base y remediación de seguridad

> Revisión: 14 de octubre de 2026
> Fuente: informes SAST, SCA, OWASP Top 10, API, configuración, infraestructura y DevSecOps recibidos en `auditoria-seguridad`.

## Controles implementados

- autenticación obligatoria por defecto; únicamente login y health son anónimos de forma explícita;
- CSRF por token, cookie segura y validación de `Origin`/`Referer` para mutaciones autenticadas bajo `/api`;
- logout por `POST`, cookie de sesión absoluta de una hora y sin renovación deslizante;
- HSTS en ambientes no Development, CSP, `Permissions-Policy`, `nosniff`, anti-frame y política estricta de referencia;
- rate limiting global particionado por usuario o dirección remota;
- identificador de correlación en respuesta y registro de toda finalización, incluso ante excepciones;
- health público mínimo, sin versión ni identificadores internos;
- utilidad destructiva de Solicitudes registrada exclusivamente en Development;
- bootstrap de seguridad deshabilitado por defecto y ruta inexistente mientras permanezca deshabilitado;
- permisos funcionales específicos en diagnósticos Dataverse y publicación directa de capacitaciones;
- URLs públicas limitadas a HTTPS sin credenciales embebidas;
- paginación Dataverse limitada al origen configurado y errores externos sanitizados;
- límites de entradas, expansión, hojas, filas, columnas, XML/DTD y celdas para importaciones XLSX;
- verificación de firma binaria para los tipos de archivo admitidos y escaneo obligatorio por defecto fuera de Development;
- protección persistente y cifrada de llaves Data Protection obligatoria fuera de Development;
- actualización de dependencias vulnerables y auditorías SCA bloqueantes;
- codificación contextual de toda información editable o restaurada desde `localStorage` en el demo portable, con ZIP y checksum regenerados;
- CI con compilación, pruebas, cobertura recolectada, lint, build, SCA, CodeQL, detección de secretos y reglas de higiene del repositorio;
- Dependabot, CODEOWNERS y proceso de reporte/respuesta en `SECURITY.md`.

## Trazabilidad de hallazgos

| Hallazgo | Estado | Tratamiento |
|---|---|---|
| SAST-01 | Corregido | Reset completo solo se registra en Development. |
| SAST-02 | Corregido | Token antiforgery y validación de origen centralizados; cliente web incorpora el token. |
| SAST-03 | Corregido | Eventos y destacados solo aceptan HTTPS seguro. |
| SAST-04 | Corregido | Se retiró el identificador personal codificado; la exclusión ahora depende de validaciones genéricas. |
| SAST-05 | Mitigado | Lecturas compartidas y diagnóstico ya no entregan cuerpos crudos; se mantiene revisión gradual de adaptadores especializados. |
| SAST-06 | Corregido | Límites de ZIP/XML/libro y DTD prohibido. |
| SAST-07 | Corregido | Extensión, MIME, firma binaria y escaneo fail-closed en producción. |
| SAST-08 | Corregido | `nextLink` debe conservar esquema, host y puerto de Dataverse. |
| SCA-01..06 | Corregido/monitorizado | Versiones corregidas, overrides transitivos acotados y auditorías sin vulnerabilidades conocidas; Dependabot y CI previenen regresiones. |
| OWASP-01..07 | Corregido/mitigado | Fallback autenticado, headers, rate limit, correlación, CI, sesión absoluta y health mínimo. |
| API-01..02 | Corregido | Publicación exige `CAP.PUBLICAR`; diagnóstico/metadata y catálogos exigen permisos funcionales. |
| CFG-01 | Corregido | `App_Data` ignorado y archivos operativos retirados del índice. |
| CFG-02..04 | Corregido | HTTPS productivo, llaves persistentes cifradas y bootstrap apagado por defecto. |
| INFRA-02 | Corregido | El flujo device-code no pasa secretos en argumentos de proceso. |
| INFRA-03 | Mitigado | Liveness público mínimo; readiness autenticado queda condicionado a la topología productiva. |
| INFRA-01, INFRA-04 | Pendiente ambiental | Definir IaC/topología y ejecutar prueba documentada de restauración con RTO/RPO. |
| INFRA-05 | Corregido | Datos de formularios y `localStorage` se codifican antes de llegar a `innerHTML`; ZIP y checksum fueron regenerados. |
| DEVSECOPS-01..04 | Implementado en repositorio | CI, CodeQL, Dependabot, CODEOWNERS, cobertura recolectada y política de seguridad. La protección de rama y funciones avanzadas se activan en GitHub. |

## Configuración obligatoria de producción

- `WebApplication:BaseUrl`: URL HTTPS exacta del frontend.
- `AllowedHosts`: hosts públicos explícitos; nunca `*`.
- `DataProtection:KeysPath`: volumen persistente, privado y respaldado.
- `DataProtection:CertificateThumbprint`: certificado válido en `CurrentUser/My`, con acceso exclusivo de la identidad de la API.
- `FileStorage:SharePoint:RequireContentScan=true` y al menos un `IFileContentScanner` operativo.
- `Authorization:BootstrapEnabled=false` después de cualquier inicialización controlada.

## Acciones administrativas necesarias

1. Proteger `main`: un aprobador, CODEOWNERS, conversaciones resueltas y checks `backend`, `frontend`, `codeql` y `secrets`.
2. Habilitar secret scanning y push protection en GitHub; si el plan no lo permite, ejecutar un escáner equivalente en CI.
3. Configurar el proxy para sobrescribir encabezados externos y registrar su red como confiable en la plataforma.
4. Centralizar logs y alertar por 401/403/429, fallas de autenticación, cambios de roles, bootstrap y operaciones destructivas.
5. Documentar y probar restauración de Dataverse, SharePoint y llaves Data Protection; registrar RPO y RTO aprobados.

No se considera cerrado un control ambiental hasta adjuntar evidencia de la configuración o de una prueba real.
