---
title: "Plan de CI-CD y Actualizaciones Remotas"
tags: [bimbo, plan, despliegue, cicd, wpf, velopack]
date: 2026-09-08
estado: planificado
implementacion: no_iniciada
---

# Plan de CI-CD y Actualizaciones Remotas

> [!important] Alcance autorizado: documentación solamente
> Este plan recoge la conversación y adapta `guia.md` y `Plan_CICD_Actualizaciones_WPF_Supabase.docx` al proyecto inspeccionado. No autoriza implementar, instalar dependencias, modificar workflows, crear repositorios, publicar paquetes ni cambiar Supabase. Las fases siguientes son trabajo futuro, sujeto a autorización expresa. Ninguna está implementada por esta sesión.

## 1. Resultado buscado y acuerdos

Entregar una aplicación WPF mediante un instalador inicial por máquina y permitir que el equipo publique versiones remotamente. Las estaciones consultarán y descargarán las actualizaciones por Internet desde la propia aplicación, sin depender de una carpeta compartida ni de estar en la misma red.

| Tema | Acuerdo o restricción |
|---|---|
| Estaciones | Dos inicialmente; previsión de diez en seis meses. |
| Plataforma | Windows 11 x64, según la premisa indicada para los equipos Dell empresariales; confirmar físicamente en el piloto. |
| Instalación existente | Ninguna: ambas computadoras recibirán su primera instalación. |
| Ámbito | Por máquina. Los privilegios efectivos y la intervención de IT se deben validar. |
| Red | Descarga por Internet desde distintas ubicaciones; no se presupone LAN común ni dominio Windows. |
| Presupuesto | Cero; cuenta GitHub normal sin plan de pago declarado. No se han verificado sus ajustes remotos. |
| Código y distribución | Código privado y repositorio público separado, exclusivamente para distribución de binarios mediante Releases. No hacer público el repositorio de desarrollo. |
| Experiencia | Instalador inicial; aviso «Actualización disponible» dentro del programa; descarga iniciada por el usuario y aplicación en un momento seguro. No abrir GitHub ni exigir cuenta GitHub al operador. |
| Arquitectura inicial | Mantener el funcionamiento online actual. Offline-first, SQLCipher y outbox no son prerrequisitos ni parte de esta implementación. |
| Línea base futura | `master`, con el último commit existente al autorizar el inicio. Registrar entonces su SHA completo y mantenerlo fijo durante la construcción y promoción. |
| Piloto | Dos jornadas operativas de observación; incluir una actualización real, además de la instalación inicial. |
| Retención | Conservar al menos las tres últimas versiones estables y todos los artefactos de los últimos 90 días; ampliar si los exige la recuperación. |
| Supabase staging | No habrá proyecto separado inicialmente; dejar prevista su incorporación. Esto no permite saltar validación ni aprobación de cambios productivos. |

La restricción inicial contra binarios públicos fue reconsiderada en la conversación: se acepta su distribución pública sin publicar fuentes. La descarga pública no concede acceso a los datos, pero tampoco demuestra que las políticas de autorización estén correctamente implementadas. Ver [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]].

### Decisiones todavía pendientes

- Identificar responsable de asignar canales, aprobador de promoción, operador de despliegue y persona que atiende incidentes. No asignar estos roles automáticamente al operador de planta.
- Confirmar organización/cuenta propietaria de cada repositorio, capacidades efectivas de GitHub Free y mecanismo verificable de aprobación sin presupuesto.
- Definir entidad/persona firmante y alternativa viable de firma; no hay entidad legal ni certificado identificado. Evaluar confianza interna con IT, sin darla por aceptada.
- Confirmar usuarios Windows, sesiones simultáneas, acceso administrador y mecanismo admitido por IT para instalar/actualizar por máquina.
- Aprobar campos, permisos y plazo de retención del diagnóstico central mínimo. El responsable de soporte sigue sin designarse.
- Acordar ventana concreta para instalar/reiniciar; las dos jornadas son observación del piloto, no dos jornadas de parada.
- Definir calendario de salida de .NET 8, cuyo soporte termina el 2026-11-10 según la referencia oficial consultada.

## 2. Punto de partida verificado localmente

Inspección de la sesión: rama `feat/fase8-MaquetadodeRoles`, SHA `162818d4237e96ecbd1a7c8a0a52d1479a12b2e2`. Esta fotografía no es el SHA de implementación autorizado. No se hizo `fetch`, auditoría remota de GitHub ni verificación de la base viva.

| Área | Evidencia local y consecuencia para el plan |
|---|---|
| Solución | `BimboProyecto.sln`: CapaUI, CapaAplicacion, CapaDatos, CapaDominio, ServicioConexión y BimboProyecto.Tests. Publicar exclusivamente `CapaUI/CapaUI.csproj`. |
| UI y capas | CapaUI es WPF `net8.0-windows`; CapaAplicacion reside en `CapaAplicacion4/`. Mantener contratos y DI, sin invertir la dependencia Aplicación/Datos ni tocar BimboPesaje. |
| Inicio | `CapaUI/App.xaml` usa el punto de entrada generado; `App.xaml.cs` inicializa recursos, DI y login. No existe integración Velopack. |
| Cierre | `MainWindow.OnClosing` advierte, limpia sesión/recursos y cierra. No constituye una barrera global que coordine todas las operaciones y procesos para actualizar. |
| Estado operativo | Guardados y estado editable están distribuidos en vistas/viewmodels. Las recepciones activas consultadas por Pesaje no equivalen a trabajo local de una estación. |
| Caché | El código ya usa FusionCache L1 en RAM y limpieza global al salir. Algunas notas anteriores aún lo llaman propuesta; no introducir una segunda caché ni tratar RAM como persistencia offline. |
| Archivos del usuario | `%APPDATA%\BimboPesaje\LogoEmpresa`, `IconoSidebar`, `TemaEmpresa\color.txt` y `Logs\app-.log`; las exportaciones se guardan en la ubicación elegida. Preservar cada perfil Windows. |
| Offline | No se encontró SQLCipher, outbox ni migración de base local implementados. [[Plan Offline-First de Pesaje]] sigue separado. |
| CI y versiones | `.github/workflows/` vacío; sin pipeline, perfiles de publicación, instalador, `global.json` ni lockfiles encontrados. No hay fuente de versión de producto consolidada. |
| SDK | SDK seleccionado localmente 10.0.400; también instalado 8.0.100. El TFM de la app sigue siendo .NET 8. Fijar y probar un SDK antes de implantar CI. |
| Configuración | `CapaUI/App.config` y `SearchTest/App.config` siguen versionados pese a reglas de ignore. Hay valores no-placeholder; no se copiaron ni se clasificó el privilegio de la clave. La configuración admite variables `SUPABASE_URL`/`SUPABASE_KEY`. |
| Conexión | La implementación compilada está en `CapaDatos/Conexion.cs`; ServicioConexión duplica el concepto y está huérfano respecto a la app. P-056 ya registra esa deuda. |
| Tests | Proyecto xUnit; hay pruebas que retornan sin verificar BD si falta `BIMBO_POSTGRES_CONNECTION_STRING`. Un resultado verde no garantiza integración ejercitada. No se ejecutaron pruebas en esta planificación. |
| Migraciones | 23 archivos SQL locales; sin `supabase/config.toml` encontrado y sin baseline autocontenido demostrado para recrear una BD vacía. No renombrar ni reescribir migraciones ya aplicadas. |
| Hardware | No se identificó integración activa SerialPort/USB de báscula en el recorrido revisado; probar los equipos reales y no presuponer drivers o lectores. |

`SearchTest` y `ServicioConexión/CapaConexión.csproj` no forman parte de la solución inspeccionada. No ampliar automáticamente el producto publicable a esos proyectos. El estado previo de [[Plan de Auditoría SBOM]] conserva sus restricciones: no se genera una SBOM ni se ejecuta la auditoría por guardar este plan.

## 3. Diseño de entrega y experiencia del usuario

### Responsabilidades

| Componente | Responsabilidad futura |
|---|---|
| Repositorio privado | Fuentes, pruebas, revisión y definición de construcción. |
| GitHub Actions | Validar el SHA autorizado, construir una vez, firmar/empacar y publicar con identidad de mínimo privilegio. |
| Velopack NuGet | Integrar consulta, descarga y aplicación de actualizaciones con la aplicación C#. |
| Velopack CLI (`vpk`) | Construir instalador y paquetes compatibles con la versión de la biblioteca integrada. |
| Repositorio público de distribución | Releases, instalador, paquetes e índices públicos. No almacenar los binarios como historial Git. |
| Aplicación WPF | Avisos, progreso, consentimiento, coordinación de trabajo local y reinicio seguro. |
| Política de estación | Identidad de equipo y canal autorizado; no debe ser editable por cualquier usuario. |
| Supabase | Backend online existente; eventualmente política/diagnóstico autorizado. No sirve como almacén binario en la opción elegida. |
| IT y responsables de liberación | Instalación inicial, privilegios, confianza de firma, autorizaciones y recuperación. |

### Flujo previsto

1. IT recibe una vez el instalador verificado y lo instala por máquina. Puede entregarlo directamente; el operador no necesita entrar a GitHub.
2. Al abrir e iniciar sesión, la app obtiene su política de estación y consulta actualizaciones sin bloquear innecesariamente el uso normal.
3. Si existe una versión elegible, muestra «Actualización disponible», versión/notas y «Descargar» / «Más tarde».
4. «Descargar» obtiene el paquete por HTTPS, muestra progreso y verifica integridad. El usuario puede seguir trabajando mientras descarga.
5. Tras descargar, aparece «Instalar y reiniciar». Descargar no autoriza instalar automáticamente ni al siguiente arranque.
6. Al solicitar la instalación, se revalida la elegibilidad de esa versión y se adquiere la barrera de actualización. Si hay trabajo pendiente, se explica qué debe terminarse y se pospone.
7. Solo cuando el equipo está seguro se cierran recursos, se aplica la actualización y se reinicia. La autenticación posterior sigue la política normal de sesiones.
8. El nuevo arranque registra versión y resultado técnico. Si algo falla, se conserva diagnóstico local y se usa el procedimiento de recuperación.

No imponer cierre inesperado por una actualización opcional. Una futura versión mínima obligatoria requerirá diseñar y aprobar su política de compatibilidad y mensajes; no se incorpora implícitamente aquí.

### Qué significa «operación abierta»

Para instalar se revisa el trabajo local del equipo afectado, no todas las recepciones de la planta. Una recepción ya guardada y abierta en Supabase no impide actualizar; otra computadora puede seguir operando.

Sí bloquean: captura/edición sin resolver, guardado o RPC en curso, exportación en curso, respuesta de escritura perdida cuyo resultado remoto aún sea incierto y otra instancia/sesión Windows usando la misma instalación. El usuario puede guardar o descartar conscientemente una edición; el actualizador no debe descartarla por él.

La barrera debe ser atómica: impedir nuevas operaciones, esperar a que terminen las admitidas, reconciliar resultados inciertos, comprobar todas las instancias y recién entonces liberar sesión/archivos. Si falla una comprobación, no aplicar; si se cancela el intento, reabrir la admisión de trabajo. Un booleano consultado antes del cierre no elimina la carrera entre comprobar y empezar un guardado.

## 4. Restricciones técnicas de la implementación futura

### Arranque, instalación y datos

- Prototipar Velopack y fijar versiones iguales/compatibles de NuGet y CLI antes de adoptarlas en producción; revisar mantenimiento, licencia MIT, dependencias e incidencias relevantes.
- Integrar el bootstrap temprano mediante un punto de entrada WPF explícito `[STAThread]`, antes de crear WPF, DI o conexiones. Ajustar `App.xaml`/proyecto conforme a la versión elegida; no duplicar el `Main` generado.
- Desactivar la aplicación automática al inicio y aplicar únicamente después de la barrera. Los hooks del instalador no ejecutarán negocio ni migraciones remotas.
- No usar el tiempo de espera de `WaitExitThenApplyUpdates` como protección del guardado: la referencia consultada describe terminación tras el timeout. La app debe estar preparada para cerrar antes de invocarlo.
- Evaluar MSI por máquina en el prototipo y acordar la elevación con IT. No prometer actualización silenciosa de `Program Files` por un usuario estándar ni debilitar ACL para conseguirla.
- Mantener configuraciones modificables, logs, exportaciones e identidad/política de estación fuera de directorios reemplazados por el instalador. `%APPDATA%` es por usuario, aunque el ejecutable sea por máquina.
- Proponer una ubicación protegida en `%ProgramData%` para política compartida, con ACL definidas por IT; el nombre definitivo y su administración quedan pendientes.
- Publicar inicialmente `win-x64`, autocontenido, sin trimming, Native AOT ni single-file. Verificar tamaño real; incluir runtime obliga a liberar parches cuando se actualice .NET.
- No introducir base local, outbox o migraciones offline. Si se aprueba offline-first posteriormente, revisar este plan antes de permitir downgrade o reemplazo de archivos.

### Versiones, canales y promoción

- Canales propuestos: `beta` para el piloto y `stable` para el resto. Asignación administrada, no un selector libre para el operador.
- Elegir una sola fuente de versión y generar desde ella metadatos, paquete y manifiesto. Cada versión identifica un SHA y artefactos inmutables.
- Usar, por ejemplo, `1.4.2` como candidato en beta y promover exactamente `1.4.2` a stable. No convertir `1.4.2-beta.1` a `1.4.2` reconstruyendo y afirmar que son los mismos bytes.
- El canal embebido del paquete y el canal de despliegue no son intercambiables. Validar un diseño con canal de empaquetado fijo e índices de despliegue separados, más `ExplicitChannel` desde política externa protegida. No copiar un índice beta y asumir que la estación queda correctamente en stable.
- Probar descubrimiento, persistencia de canal y actualización siguiente antes de aceptar este diseño; si la versión de Velopack requiere otra estrategia, registrarla y aprobarla sin perder la promoción del mismo artefacto.
- Promover instalador, paquete completo y deltas aplicables con el mismo SHA-256; índices y acta de aprobación pueden diferir. No reconstruir, reempacar ni volver a firmar en promoción.
- Publicar primero objetos completos; anunciar después el índice/release activo mediante el mecanismo de consistencia validado. No exponer índices que apunten a archivos todavía ausentes.
- Validar deltas desde las versiones realmente instaladas en stable, no solo desde beta. Si no existe delta aplicable, descargar el paquete completo.
- Los índices y binarios públicos no son una frontera de autorización: la asignación de canal gobierna el cliente administrado, pero no impide a terceros descargar una beta pública.

### Seguridad de la entrega

- Revisar y clasificar toda configuración antes de publicar. Una clave pública de cliente no equivale a una credencial privilegiada; ninguna `service_role`, secret key, contraseña de BD, PAT o clave de firma puede viajar en el instalador.
- El cliente no incorpora token GitHub para descargar paquetes públicos. La automatización que publica en otro repositorio usará una identidad específica de mínimo privilegio; evaluar GitHub App con token temporal.
- Revisar fuentes, PDB/SourceLink, configuraciones, logs y manifiestos para impedir publicación accidental de material interno. Los binarios siguen siendo inspeccionables/descompilables.
- HTTPS y hashes detectan corrupción, pero un hash servido por un origen comprometido no prueba por sí solo autenticidad. Definir firma/confianza y credenciales de publicación antes de producción.
- Firma interna con IT es una alternativa por evaluar para flota administrada; no implica confianza pública ni ausencia automática de SmartScreen. No aprobar producción sin firma de manera tácita por carecer de presupuesto.
- No afirmar que login y RLS hacen segura toda la base: comprobar permisos efectivos, RPC, Storage y Realtime pertinentes, con cuentas sin privilegio, antes de la primera distribución pública.
- GitHub Free no permite asumir protecciones de ramas privadas o revisores de entornos privados disponibles. Comprobar capacidades; separar aprobación operativa verificable de bloqueo técnico efectivo. Si no se puede proteger la publicación con el mecanismo elegido, no declarar el gate satisfecho.

### Supabase y compatibilidad

- Validar migraciones en entorno local desechable reproducible. Completar una baseline autorizada o fixtures suficientes antes de afirmar que CI puede recrear el esquema; no modificar el historial ya aplicado.
- Las pruebas de integración deben distinguir ejecutada, omitida y fallida. En el job que las exige, ausencia de conexión no puede aprobar el gate mediante `return` silencioso.
- Desplegar cambios aditivos compatibles con clientes viejos y nuevos; retirar columnas/RPC solo después de verificar que ninguna versión soportada depende de ellas.
- Migraciones productivas separadas de la actualización de escritorio, con aprobación individual, respaldo/recuperación y concurrencia exclusiva. No cancelar una migración en ejecución porque llegó un commit nuevo.
- Verificar aplicación mediante historial remoto, catálogo físico, advisors de seguridad y recorrido UI → VM → repositorio → RPC cuando corresponda. Un `.sql` local o build verde no acredita despliegue.
- Mantener staging separado en el backlog; no disponer de él es una limitación explícita, no una razón para probar indiscriminadamente en producción.

## 5. Automatizaciones previstas, no creadas

| Workflow propuesto | Activación y controles | Entregable |
|---|---|---|
| `ci.yml` | PR y cambios de `master`; permisos de lectura, sin secretos productivos; SDK fijado y restore bloqueado después de generar/revisar lockfiles. Windows para WPF y entorno separado si conviene para BD local. | Build Release, pruebas unitarias y resultado explícito de integración; evidencia ligada al SHA. |
| `release-beta.yml` | Ejecución autorizada o tag protegido, SHA fijo de master y validaciones repetibles; publicación exclusiva y credenciales limitadas. | Un publish autocontenido, firma acordada, instalador/paquetes, hashes, notas y manifiesto de procedencia. SBOM solo tras habilitar su plan. |
| `promote-stable.yml` | Aprobación identificada tras el piloto; verificar SHA, hashes y pruebas. Sin build, empaquetado ni firma. | Los mismos artefactos habilitados para stable y acta de promoción. |
| `supabase-migrations.yml` | Validación local en PR; despliegue remoto en paso/manual separado, autorizado, serializado y sin cancelación a mitad de aplicación. | Evidencias de validación, aplicación y compatibilidad, independientes del release de escritorio. |

Los nombres y ubicaciones finales se decidirán al implementar. Estudiar si la aprobación puede alojarse en el repositorio público de distribución con permisos disponibles sin exponer fuentes o secretos. Configurar límites de uso y retención de Actions: «sin presupuesto» requiere no habilitar gasto inesperado, no asumir capacidad ilimitada.

## 6. Fases de implementación y aceptación

Todas pendientes. La autorización actual no activa ninguna. Cada fase requiere evidencia propia y no queda aprobada por documentarla.

### Fase 1 — Congelar la línea base

- **Objetivo/alcance:** verificar master terminado/autorizado, registrar SHA, estado del árbol, inventario y versiones. Abarca solución, proyectos, configuración y deuda relevante.
- **Dependencias/decisiones:** responsable técnico, versión inicial, SDK y calendario de .NET; revisar P-056 sin resolverlo incidentalmente.
- **Prueba/aceptación:** inventario reproducible; valores sensibles clasificados sin exponerlos; lista de gates aceptada. **Riesgo:** trabajar sobre master cambiante. **Retirada:** no iniciar publicación y conservar el manifiesto de diagnóstico.

### Fase 2 — Establecer CI reproducible

- **Objetivo/alcance:** futuro `global.json`, lockfiles, `ci.yml` y ajustes de tests estrictamente necesarios.
- **Dependencias/decisiones:** fase 1; permisos/cuotas GitHub y entorno de integración reproducible.
- **Prueba/aceptación:** restore bloqueado, build sin errores/advertencias y pruebas ejecutadas; integración ausente marcada como tal y no aprobada. **Riesgo:** falsos verdes o secretos en PR. **Retirada:** deshabilitar el workflow nuevo, no tocar datos ni historial aplicado.

### Fase 3 — Proteger operaciones y archivos locales

- **Objetivo/alcance:** diseñar/implementar coordinador de trabajo, barrera de instalación y coordinación entre procesos/sesiones; integrar guardados/exportaciones y cierre, preservando perfiles.
- **Dependencias/decisiones:** inventario completo de operaciones locales, política de IT y límites de instancia.
- **Prueba/aceptación:** no existe carrera entre check y nuevo guardado; incertidumbre remota impide aplicar; cancelar restaura el uso normal. **Riesgo:** pérdida de captura o muerte de otra instancia. **Retirada:** mantener deshabilitada la instalación desde UI; usar cierre normal sin forzar procesos.

### Fase 4 — Preparar compatibilidad de Supabase

- **Objetivo/alcance:** baseline/fixtures locales, pruebas de contratos y procedimiento de migración compatible; no incluir la migración offline.
- **Dependencias/decisiones:** acceso autorizado a definiciones necesarias y procedimiento productivo con responsable.
- **Prueba/aceptación:** clientes anterior/nuevo funcionan con esquema expandido; pruebas realmente ejecutadas y autorización separada antes de producción. **Riesgo:** ruptura de otras estaciones. **Retirada:** detener rollout de clientes y aplicar corrección hacia adelante; nunca un down destructivo automático.

### Fase 5 — Integrar Velopack en WPF

- **Objetivo/alcance:** futuro punto de entrada, proyecto CapaUI, DI, servicio de actualización y VM/UI de aviso/progreso; contratos donde corresponda.
- **Dependencias/decisiones:** fases 2–3, versión NuGet/CLI fijada, política de estación y fuente de releases.
- **Prueba/aceptación:** detección/descarga sin navegador, «Más tarde», reintentos y aplicación solo tras barrera; autoaplicación deshabilitada. **Riesgo:** inicio doble o actualización inesperada. **Retirada:** desactivar descubrimiento/aplicación, preservando la versión instalada y el login normal.

### Fase 6 — Prototipo de instalación en laboratorio

- **Objetivo/alcance:** empaquetado de dos versiones de prueba, MSI por máquina, Windows 11 x64 limpio y usuarios estándar/administrador.
- **Dependencias/decisiones:** fase 5, IT y estrategia provisional de confianza identificada como laboratorio.
- **Prueba/aceptación:** instalación limpia, actualización, reinicio, reparación/desinstalación controlada y archivos preservados en varios perfiles; probar UAC y procesos concurrentes. **Riesgo:** permisos incompatibles con la experiencia prevista. **Retirada:** volver a imagen/instalador conocido sin borrar datos del usuario; replantear mecanismo con IT.

### Fase 7 — Preparar canal beta y política de estación

- **Objetivo/alcance:** futura configuración de canales, identidad de estación e índices en repositorio binario; esquema autorizado si la política reside en Supabase.
- **Dependencias/decisiones:** asignador de canal, ACL/roles, mecanismo `ExplicitChannel` y seguridad de edición de política.
- **Prueba/aceptación:** cada estación consulta su canal después del reinicio/actualización; no queda fijada accidentalmente a beta al promover. **Riesgo:** recepción de versión equivocada. **Retirada:** suspender anuncios y mantener clientes en versión conocida.

### Fase 8 — Firmar y publicar el candidato

- **Objetivo/alcance:** release-beta, identidad de publicación, firma elegida, manifiesto/hash y retención.
- **Dependencias/decisiones:** fase 6–7, responsable identificado, alternativa de firma viable dentro de restricciones y autorización de publicar binarios.
- **Prueba/aceptación:** fuentes/credenciales ausentes, firma verificada con confianza prevista, publicación completa y recuperación posible. **Riesgo:** paquete manipulado o exposición de secretos. **Retirada:** retirar anuncio, revocar credenciales comprometidas y emitir candidato nuevo; no reemplazar bytes bajo la misma versión.

### Fase 9 — Diagnóstico y recuperación

- **Objetivo/alcance:** logs locales, reporte central mínimo propuesto, inventario de versiones y runbook de retirada/reinstalación.
- **Dependencias/decisiones:** datos permitidos, RBAC/RLS pertinentes, soporte responsable y compatibilidad para downgrade.
- **Prueba/aceptación:** fallo sin conexión deja evidencia local, fallo de telemetría no rompe operación y una versión fallida se recupera conservando configuración. **Riesgo:** confundir heartbeat con salud funcional. **Retirada:** apagar telemetría si falla y usar soporte manual; no asumir rollback automático de Velopack.

### Fase 10 — Primera instalación, no migración legacy

- **Objetivo/alcance:** instalar en una estación piloto la primera versión ya capaz de actualizarse; preparar entrega/control del instalador a IT.
- **Dependencias/decisiones:** candidato firmado/validado, cuenta operativa autorizada y privilegios verificados.
- **Prueba/aceptación:** arranque, login, permisos, configuración, pesaje y exportación en equipo real; registrar versión y estación. **Riesgo:** asumir runtime/driver previo. **Retirada:** reparación o reinstalación controlada; no hay versión anterior del sistema instalada que migrar.

### Fase 11 — Piloto durante dos jornadas operativas

- **Objetivo/alcance:** publicar un segundo candidato y actualizar realmente la estación piloto desde el primero; observar dos jornadas representativas, incluida operación posterior al cambio.
- **Dependencias/decisiones:** fases anteriores, ventana de instalación acordada, responsable disponible y checklist funcional.
- **Prueba/aceptación:** cero pérdida de trabajo atribuible al cambio, pruebas críticas aprobadas, canal/diagnóstico correctos y recuperación ensayada. Un fallo crítico pausa y exige nuevo candidato/observación. **Riesgo:** aprobar solo instalación inicial. **Retirada:** retirar anuncio, conservar evidencias y ejecutar recuperación autorizada.

### Fase 12 — Promover el mismo artefacto a stable

- **Objetivo/alcance:** workflow de promoción, hashes y registro de quién aprobó qué versión/SHA.
- **Dependencias/decisiones:** piloto aprobado, compatibilidad de BD y gates de firma/permisos satisfechos.
- **Prueba/aceptación:** identidad byte a byte de instalador y paquetes, sin compilación nueva; estación stable recibe el candidato y sigue en stable. **Riesgo:** diferencias no probadas o índice incorrecto. **Retirada:** revertir anuncio de elegibilidad; tratar descargas ya realizadas como indica el runbook.

### Fase 13 — Despliegue gradual y mantenimiento

- **Objetivo/alcance:** incorporar segunda estación y después las nuevas hasta diez, inventariadas individualmente; mantener versiones, retención y soporte.
- **Dependencias/decisiones:** stable aprobado, responsables y calendario de parches/.NET definido.
- **Prueba/aceptación:** confirmación técnica y funcional por estación, sin asumir que descargar significa instalar; al menos tres estables y 90 días recuperables. **Riesgo:** equipos rezagados y runtime sin soporte. **Retirada:** detener nuevas instalaciones, mantener compatibilidad y recuperar solo equipos afectados.

## 7. Matriz mínima de pruebas futuras

| Caso | Resultado exigido |
|---|---|
| PC limpia Windows 11 x64, sin runtime previo | Instalación y primer arranque autocontenidos. |
| Usuario estándar, instalación por máquina | Elevación administrada o rechazo claro; sin permisos inseguros. |
| Dos perfiles/sesiones Windows y varias instancias | Actualización coordinada de la instalación compartida; datos por perfil intactos. |
| Abrir app sin actualización | Inicio/login y uso normal sin interrupción. |
| Versión nueva / «Más tarde» | Aviso dentro de app, sin navegador ni cierre. |
| Descarga mientras se opera | UI utilizable y datos no afectados. |
| Corte de red, proxy o rate limit | Error recuperable; permanece la versión instalada. |
| Paquete corrupto o firma no confiable | No instalar; diagnóstico utilizable. |
| Disco insuficiente | Fallo controlado y versión anterior utilizable. |
| Captura sin guardar | No aplicar hasta guardar o descartar conscientemente. |
| Guardado/exportación en curso | Drenaje sin abortar ni duplicar efectos. |
| RPC con respuesta perdida | Reconciliar resultado; no asumir éxito ni fracaso para cerrar. |
| Nuevo guardado simultáneo a solicitud de instalar | La barrera elimina la carrera. |
| Recepción guardada abierta y otra estación operando | No bloquear toda la planta; conservar compatibilidad. |
| Reiniciar después de descargar sin instalar | No autoaplicar silenciosamente. |
| Delta disponible/no aplicable | Actualización correcta o fallback a completo. |
| Beta → stable y siguiente actualización | Mismos bytes promovidos y canal correcto persistente. |
| Versión retirada ya descargada | Revalidación antes de aplicar; si no puede comprobar elegibilidad, posponer. |
| Logo, tema, logs y exportaciones existentes | Permanecen tras actualizar/reparar según política acordada. |
| Versiones vieja y nueva contra Supabase | Contratos y permisos compatibles; sin migración destructiva implícita. |
| Fallo después de instalar | Runbook ensayado; corrección hacia adelante o downgrade expresamente compatible. |
| Piloto e incorporación de segunda estación | Actualización real, dos jornadas observadas y evidencia funcional por equipo. |

Build y xUnit no sustituyen prueba manual WPF ni evidencia física en los dos equipos. Registrar por separado automatización, validación remota y prueba funcional/visual.

## 8. Diagnóstico, retención y recuperación

Diagnóstico central propuesto: ID técnico de estación, versión instalada, canal asignado, fecha/resultado del último intento y código de error acotado. No capturas de pantalla, datos de pesaje, nombres personales, tokens ni logs completos por defecto. Los logs detallados permanecen locales, revisados para no filtrar credenciales. Diseñar permisos por estación/rol y evitar que un cliente altere el estado de otros equipos.

La retención acordada de paquetes (tres estables y 90 días) no establece automáticamente retención de telemetría. Definirla aparte. Mantener instaladores completos, índices/manifiestos y dependencias de deltas necesarias; no borrar artefactos requeridos por estaciones aún soportadas.

Retirar una versión del índice no cancela una descarga existente: el cliente debe revalidar autorización/compatibilidad antes de aplicar. Si esa comprobación es imposible, posponer la instalación. Una app que no arranca o carece de red puede no reportar al servidor; se requieren logs locales y soporte de IT.

No se presupone rollback de salud automático. Preferir nueva versión correctiva; permitir volver a una anterior únicamente si el esquema remoto, archivos y política de versión lo admiten. Nunca deshacer la BD de toda la planta porque falla una estación. Ante incidente: detener anuncio/promoción, identificar afectados, conservar evidencias, decidir recuperación, verificar operación y documentar nueva autorización.

## 9. Superficies que podrían cambiar al autorizar implementación

- `CapaUI/CapaUI.csproj`, `App.xaml`, `App.xaml.cs` y un nuevo punto de entrada; servicios/viewmodels/vistas de actualización en CapaUI, mediante DI.
- Contratos de coordinación en CapaAplicacion si corresponde; adaptadores en la capa responsable, sin `CapaAplicacion → CapaDatos` ni Service Locator.
- Cierre principal y rutas de captura/guardado/exportación que participen de la barrera; ampliar tests de forma proporcional.
- Configuración de construcción: SDK, lockfiles, versión, publicación y workflows nuevos. Los nombres de archivos futuros de la sección 5 no existen por este documento.
- Migraciones nuevas solo si se aprueba política/telemetría central o compatibilidad adicional; nunca editar migraciones aplicadas.
- Configuración externa: repositorio binario, identidad GitHub, políticas de publicación, firma/confianza, ACL e instalación IT. Requieren autorización independiente; no se han creado.

## 10. Backlog y puertas de autorización

- [ ] Confirmar responsables de canal, firma, promoción, instalación y soporte.
- [ ] Elegir firma/confianza y demostrar instalación por máquina con usuario estándar.
- [ ] Definir calendario de .NET y versión/SDK inicial soportados.
- [ ] Autorizar inicio desde un SHA fijo de master; volver a contrastar código, configuración y pruebas.
- [ ] Clasificar configuración y validar seguridad antes de cualquier publicación pública.
- [ ] Verificar prestaciones/cuotas GitHub y gates efectivos sin gasto.
- [ ] Demostrar baseline de BD y pruebas de integración que no aprueben sin ejecutar.
- [ ] Aprobar diagnóstico central, permisos y retención propios.
- [ ] Validar promoción byte a byte y asignación de canal en laboratorio.
- [ ] Acordar ventana de reinicio y ejecutar piloto con actualización real de primera a segunda versión.
- [ ] Incorporar Supabase staging cuando exista capacidad; mantener registrado que inicialmente no existe.

La adaptación principal del documento de origen es reemplazar una transición de instalaciones existentes por una primera instalación con actualizador; separar alojamiento de paquetes y experiencia del usuario; mantener online-first; y condicionar automatización, firma y despliegue a restricciones reales de presupuesto, seguridad y operación. P-056 y los demás pendientes existentes no quedan resueltos por esta planificación.

## Referencias

- Documentos de entrada aportados: `guia.md` y `Plan_CICD_Actualizaciones_WPF_Supabase.docx`, leídos durante la sesión; las decisiones posteriores de la conversación prevalecen sobre sus supuestos.
- [[Velopack y GitHub Actions - Referencias para actualizaciones WPF]] — documentación oficial consultada, límites y verificaciones pendientes de versión.
- [[Plan de Auditoría SBOM]] — habilitación y alcance propios; no ejecutado aquí.

## Relaciones

- [[Conocimiento Principal]]
- [[Arquitectura Actual]]
- [[ADR-027 - Codigo privado y distribucion publica de actualizaciones]]
- [[Velopack y GitHub Actions - Referencias para actualizaciones WPF]]
- [[Deuda Técnica - Pendientes]]
- [[Módulo Pesaje]]
- [[Módulo Reportería]]
- [[Plan Offline-First de Pesaje]]
- [[Plan de Migración de Mutaciones Directas a RPC]]
- [[Sesión 2026-09-08 - Plan de CI-CD y actualizaciones remotas]]
