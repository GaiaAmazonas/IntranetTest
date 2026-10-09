# ADR-0001: Monolito modular con Dataverse

- Estado: aceptado
- Fecha de verificación: 2026-10-09
- Decisión anterior: consolidada desde documentación y código existentes

## Contexto

Gaia reúne varias capacidades empresariales que comparten identidad, estructura organizacional, permisos, datos y experiencia. El repositorio ya separa contratos por módulo y compone una única API. La información empresarial reside en Microsoft Dataverse.

## Decisión

Mantener Gaia como monolito modular:

- frontend estático Next.js y API ASP.NET Core como procesos desplegables separados;
- módulos de dominio como proyectos dentro de la solución;
- adaptadores concretos en Gaia.Api;
- Dataverse Web API v9.2 como persistencia empresarial;
- integración de archivos mediante un puerto compartido y SharePoint/Graph;
- comunicación del navegador exclusivamente con Gaia.Api.

## Consecuencias

Positivas:

- transacciones y políticas se coordinan en un límite operativo claro;
- los módulos conservan lenguaje y pruebas propias;
- el despliegue y la observabilidad son más simples que en microservicios;
- Dataverse continúa gobernando metadatos, seguridad y datos empresariales.

Restricciones:

- los módulos no acceden directamente a adaptadores de otros módulos;
- no se introduce una base paralela para acelerar una función puntual;
- una eventual separación en servicios requiere un ADR nuevo, propiedad de datos, contratos, observabilidad y plan operativo.

## Evidencia

`Gaia.Platform.slnx`, `src/Modules`, `src/Gaia.Api/Program.cs`, infraestructura Dataverse y `apps/web/next.config.ts`.
