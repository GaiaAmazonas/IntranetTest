# Gobierno documental y arquitectura canónica de Gaia

Fecha de revisión: **22 de septiembre de 2026**.

## Propósito

Este documento es la puerta de entrada obligatoria para cualquier persona o agente que modifique Gaia. Define qué documentación gobierna, qué documentos son históricos y cuáles son las restricciones arquitectónicas que no se pueden cambiar por inferencia.

No reemplaza la inspección del código afectado. Evita que una especificación antigua, un informe fechado o un README generado por una herramienta se interpreten como el estado vigente.

## Regla de precedencia

Cuando dos fuentes difieran, se utiliza este orden:

1. Solicitud actual y explícita del propietario del proyecto.
2. Este documento y las decisiones canónicas que enlaza.
3. Código y pruebas vigentes del repositorio para describir el comportamiento ya implementado.
4. Documentación de continuidad y documentación vigente del módulo afectado.
5. Modelo o manifiesto aprobado de Dataverse.
6. Especificaciones iniciales, levantamientos, planes e informes fechados, únicamente como antecedentes.

Una contradicción no autoriza a escoger silenciosamente la alternativa más cómoda. Se debe informar, conservar compatibilidad y actualizar la documentación al cerrar el cambio.

## Lectura mínima obligatoria

Antes de modificar código:

1. Este documento.
2. [Contexto para un nuevo chat](CONTEXTO_PARA_NUEVO_CHAT.md).
3. [Arquitectura verificada](continuidad/01_ARQUITECTURA.md).
4. [Seguridad, sesión y permisos](continuidad/02_SEGURIDAD.md).
5. [Receta para crear o ampliar módulos](continuidad/03_CREAR_MODULO.md).
6. La documentación vigente del módulo afectado.
7. Para archivos o identidades técnicas, [infraestructura SharePoint](continuidad/04_ARCHIVOS_SHAREPOINT.md) y [autenticación Dataverse/SharePoint](continuidad/07_AUTENTICACION_DATAVERSE_SHAREPOINT.md).

## Arquitectura canónica

### Forma de la solución

- Gaia es un **monolito modular**, no un conjunto de microservicios.
- Existen dos procesos desplegables: frontend Next.js y API ASP.NET Core.
- Intranet y AdminCore son experiencias distintas sobre servicios compartidos; no deben fusionarse visualmente ni en permisos.
- El navegador consume la API de Gaia. No consulta directamente Dataverse ni Microsoft Graph.

### Frontend

- Ubicación: `apps/web`.
- Stack vigente: Next.js 16, React 19, TypeScript 5, Tailwind CSS 4 y CSS modular/compartido existente.
- La interfaz no decide reglas críticas, permisos, resultados, SLA, aprobación ni transiciones.
- Se deben reutilizar shell, cliente API, componentes, tokens, feedback y patrones responsive existentes.
- No introducir otro framework de interfaz, estado global o cliente HTTP sin aprobación explícita.

### Backend

- Composición: `src/Gaia.Api`.
- Contratos y reglas: `src/Modules/<Modulo>`.
- Capacidades transversales: `src/BuildingBlocks`.
- Adaptadores Dataverse y Graph: `src/Gaia.Api/Infrastructure`.
- Stack vigente: ASP.NET Core sobre .NET 10.
- Los módulos no deben depender de implementaciones concretas de infraestructura.
- Los endpoints deben validar sesión y permisos en el servidor y devolver errores controlados.

### Persistencia

- La persistencia empresarial vigente es **Microsoft Dataverse Web API v9.2 mediante OData**.
- El repositorio no contiene una dependencia operativa vigente de Entity Framework o PostgreSQL. Las referencias documentales que afirman lo contrario son históricas.
- No crear bases de datos paralelas, tablas locales ni persistencia temporal como sustituto silencioso de Dataverse.
- No inventar nombres lógicos. Consultar metadatos y conservar prefijo, tipos, choices, relaciones y claves aprobados.
- Las solicitudes de creación de metadatos deben asociar los componentes a la solución Dataverse correcta.
- Los aprovisionamientos deben ser idempotentes y tener validación previa; ningún script destructivo se ejecuta sin autorización explícita.

### Identidad, seguridad y archivos

- Inicio de sesión: Microsoft Entra ID con OpenID Connect y cookie segura del backend.
- Dataverse autenticado: token delegado del usuario; Dataverse vuelve a evaluar sus privilegios.
- Autorización funcional Gaia: usuarios, roles, permisos y módulos propios en Dataverse.
- SharePoint/Graph para archivos: identidad técnica del backend con mínimo privilegio, preferiblemente `Sites.Selected`.
- La configuración necesaria antes del login no puede depender de un token delegado; seguir `continuidad/07_AUTENTICACION_DATAVERSE_SHAREPOINT.md`.
- No almacenar tokens en el navegador ni secretos en Git, Markdown, logs o configuración pública.
- No exponer `siteId`, `driveId`, `driveItemId` ni credenciales en URLs públicas.

### Convenciones de cambio

- Respetar límites del módulo y evitar cambios colaterales.
- Preservar historial; no reescribir registros históricos por cambios posteriores de maestros.
- Preferir inactivación o eliminación controlada cuando existan dependencias o auditoría.
- Una eliminación solicitada debe comprobar referencias y explicar el bloqueo funcionalmente.
- Toda nueva capacidad necesita pruebas proporcionales: autorización negativa, reglas de dominio, adaptador y experiencia responsive cuando aplique.
- No declarar producción lista por compilación local. Usar criterios del módulo, pruebas y validación del ambiente.
- Actualizar la documentación del módulo y este índice cuando cambie una decisión arquitectónica.

## Módulos registrados

Los módulos backend vigentes son:

- Communications
- Helpdesk
- Identity
- Inventory
- Organization
- Security
- ThirdParties
- Training

La API registra sus contratos y compone los adaptadores. Crear un módulo nuevo requiere seguir `continuidad/03_CREAR_MODULO.md`; no se agrega funcionalidad transversal directamente a `Program.cs` salvo composición o middleware.

## Estado y autoridad de los Markdown auditados

### Canónicos o de continuidad vigente

- `README.md`: instalación y resumen actual del repositorio.
- `Md/00_GOBIERNO_DOCUMENTAL_Y_ARQUITECTURA.md`: precedencia y límites arquitectónicos.
- `Md/CONTEXTO_PARA_NUEVO_CHAT.md`: estado operativo y punto de continuación.
- `Md/continuidad/01_ARQUITECTURA.md`: arquitectura y convenciones verificadas.
- `Md/continuidad/02_SEGURIDAD.md`: sesión, tokens y permisos.
- `Md/continuidad/03_CREAR_MODULO.md`: proceso de implementación.
- `Md/continuidad/04_ARCHIVOS_SHAREPOINT.md`: infraestructura de archivos.
- `Md/continuidad/05_PREPARACION_ADJUNTOS_HELPDESK.md`: antecedentes de integración de adjuntos.
- `Md/continuidad/06_FOTOS_PERFIL_ENTRA.md`: fotografías delegadas de perfil.
- `Md/continuidad/07_AUTENTICACION_DATAVERSE_SHAREPOINT.md`: separación de identidades y caso pre-login.
- `Md/modulos/helpdesk.md`: comportamiento vigente de Helpdesk.
- `Md/modulos/CAPACITACIONES.md`: comportamiento vigente de Capacitaciones.
- `Md/31_CAPACITACIONES_FLUJO_Y_PRUEBAS.md`: flujo y pruebas funcionales de Capacitaciones.
- `docs/helpdesk-workflow-acceptance.md`: aceptación técnica fechada del motor de flujos; sus cifras de pruebas son una fotografía, no un valor permanente.

### Especificaciones base útiles, subordinadas al estado vigente

- `Md/01_CONTEXTO_Y_ALCANCE.md`
- `Md/02_ARQUITECTURA_Y_CONVENCIONES.md`
- `Md/03_MODELO_DATOS_GENERAL.md`
- `Md/04_MODULO_ORGANIZACIONAL.md`
- `Md/05_MODULO_TERCEROS.md`
- `Md/06_MODULO_INVENTARIOS.md`
- `Md/07_AUDITORIA_METADATOS_Y_DOCUMENTOS.md`
- `Md/08_REGLAS_DE_NEGOCIO.md`
- `Md/09_MIGRACION_Y_CALIDAD_DATOS.md`
- `Md/10_INTERFAZ_Y_EXPERIENCIA.md`
- `Md/11_PLAN_IMPLEMENTACION_Y_PRUEBAS.md`
- `Md/14_ADMINISTRACION_CONTENIDOS_INTRANET.md`

Conservan reglas funcionales y de diseño útiles. Cuando describan tecnología "sugerida", fases iniciales o modelos aún por confirmar, prevalecen el código y la documentación canónica.

### Históricos; no gobiernan implementaciones nuevas

- `Md/00_README_INICIO.md`: paquete inicial anterior a la implementación actual.
- `Md/12_PROMPT_MAESTRO_CODEX.md`: prompt de diagnóstico inicial.
- `Md/13_HELPDESK_LEVANTAMIENTO_FUNCIONAL.md`: levantamiento anterior al Helpdesk ya implementado.
- `Md/15_CALIDAD_Y_PREPARACION_PRODUCCION.md`: dictamen fechado que todavía describía PostgreSQL y módulos inexistentes.
- `entregables/Informe_tecnico_montaje_servidor_Gaia.md`: informe de infraestructura fechado; debe regenerarse antes de un montaje productivo.
- `apps/web/README.md`: README genérico de Next.js; no describe la arquitectura Gaia.

Los históricos no deben eliminarse porque explican decisiones y evolución, pero deben citarse como antecedentes, nunca como fuente de verdad actual.

## Hallazgos de la auditoría del 22 de septiembre de 2026

1. Los 31 Markdown existentes fueron inventariados; sus enlaces relativos no presentan destinos rotos.
2. No se encontraron secretos ni GUID de credenciales incrustados en los Markdown auditados.
3. `Md/13_HELPDESK_LEVANTAMIENTO_FUNCIONAL.md` afirma que Helpdesk no existe; el módulo, adaptadores y documentación funcional actuales demuestran que esa afirmación quedó histórica.
4. `Md/15_CALIDAD_Y_PREPARACION_PRODUCCION.md` y el informe de servidor describen una dependencia de PostgreSQL. Los proyectos actuales no referencian Entity Framework ni Npgsql; Dataverse es la persistencia vigente.
5. `Md/00_README_INICIO.md` y `Md/12_PROMPT_MAESTRO_CODEX.md` son instrucciones de arranque, no órdenes permanentes para reiniciar el proyecto.
6. La documentación de continuidad es más reciente que varias especificaciones generales, pero las cifras de pruebas y fechas siguen siendo fotografías y deben volver a verificarse.
7. Las tablas `gaia_configuracionlogin` y `gaia__redsociallogin` mencionadas por otro ambiente no están confirmadas en este repositorio; se deben validar con metadatos antes de usarlas.

## Contrato para otra cuenta o agente Codex

Entregar el repositorio junto con esta instrucción:

> Antes de proponer o ejecutar cambios, lee `Md/00_GOBIERNO_DOCUMENTAL_Y_ARQUITECTURA.md`, `Md/CONTEXTO_PARA_NUEVO_CHAT.md` y los documentos de continuidad 01, 02 y 03. Lee además la documentación del módulo afectado. Conserva el monolito modular, Next.js + ASP.NET Core, Dataverse como persistencia, la separación Intranet/AdminCore, la autenticación delegada y los adaptadores existentes. No cambies arquitectura, autenticación, almacenamiento, tablas, nombres lógicos, permisos o Design System por iniciativa propia. Si encuentras contradicciones, presenta evidencia y solicita una decisión; no adoptes documentos históricos como estado vigente. No accedas, imprimas ni almacenes secretos.

## Cómo mantener este gobierno

Al finalizar una entrega que cambie arquitectura, seguridad, persistencia, rutas principales o límites de módulo:

1. Actualizar la documentación del módulo.
2. Actualizar `Md/CONTEXTO_PARA_NUEVO_CHAT.md`.
3. Actualizar este documento si cambió una decisión canónica o la clasificación documental.
4. Registrar fecha, evidencia y pruebas ejecutadas.
5. No modificar afirmaciones históricas para simular que siempre estuvieron actualizadas; añadir una advertencia y crear la definición vigente correspondiente.
