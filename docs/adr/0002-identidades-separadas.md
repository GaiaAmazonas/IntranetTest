# ADR-0002: Identidades separadas para usuario, Dataverse y archivos

- Estado: aceptado
- Fecha de verificación: 2026-10-09
- Decisión anterior: consolidada desde la implementación y continuidad de seguridad

## Contexto

Gaia necesita autenticar personas, respetar privilegios individuales en Dataverse y realizar operaciones técnicas sobre SharePoint. Reutilizar una sola credencial ampliaría privilegios y dificultaría la auditoría.

## Decisión

- Microsoft Entra ID autentica al usuario mediante OpenID Connect.
- Gaia.Api mantiene una cookie segura de sesión.
- Dataverse se consume con token delegado del usuario.
- La autorización funcional se evalúa además con roles y permisos Gaia persistidos en Dataverse.
- SharePoint/Graph se consume con identidad técnica del backend, de mínimo privilegio y preferiblemente `Sites.Selected`.
- La configuración pública previa al login usa información publicada no sensible; no depende de identidad delegada.

## Consecuencias

- El navegador no almacena tokens de Dataverse o Graph.
- Una operación Dataverse debe superar autorización Gaia y privilegios de la identidad delegada.
- Las operaciones de archivo pueden ejecutarse sin otorgar permisos Graph al usuario, pero deben validar el permiso funcional antes de usar la identidad técnica.
- Escalar a varias instancias exige revisar la cache de tokens y la protección compartida de datos.
- Ningún identificador técnico o secreto se convierte en contrato público.

## Evidencia

`IdentityModule.cs`, `SecurityModule.cs`, adaptadores Dataverse, `GraphApplicationTokenProvider.cs`, configuración de archivos y documentación de continuidad 02, 04 y 07.
