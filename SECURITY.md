# Política de seguridad

## Reporte responsable

No publique vulnerabilidades, credenciales ni datos personales en issues. Repórtelos de forma privada al propietario del repositorio, indicando componente, impacto, pasos mínimos de reproducción y una evidencia sanitizada. No incluya secretos reales ni copias de datos productivos.

## Atención

| Severidad | Acuse de recibo | Objetivo de contención | Objetivo de corrección |
| --- | ---: | ---: | ---: |
| Crítica | 4 horas hábiles | 24 horas | 72 horas |
| Alta | 1 día hábil | 3 días hábiles | 10 días hábiles |
| Media | 3 días hábiles | 10 días hábiles | 30 días |
| Baja | 5 días hábiles | Según planificación | 90 días |

Toda excepción debe tener propietario, justificación, controles compensatorios y fecha de vencimiento. Las credenciales expuestas se revocan y rotan; eliminarlas solamente del último commit no se considera remediación.

## Cambios sensibles

Los cambios en autenticación, autorización, carga de archivos, integraciones, infraestructura y flujos de despliegue requieren revisión de CODEOWNERS y aprobación de CI. `main` debe protegerse en GitHub exigiendo revisión, conversaciones resueltas y los checks `backend`, `frontend` y `codeql`.
