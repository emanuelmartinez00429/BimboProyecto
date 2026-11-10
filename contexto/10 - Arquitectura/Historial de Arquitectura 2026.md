---
title: "Historial de Arquitectura 2026 — Bimbo"
type: arquitectura
status: vigente
date: 2026-05-21
updated: 2026-11-09
summary: "Recopilación cronológica de actualizaciones y decisiones arquitectónicas de BimboProyecto entre mayo y noviembre de 2026"
autor: fernando
tags: [arquitectura, historial, bimbo, decisiones]
---

# Historial de Arquitectura 2026 — Bimbo

> [!info] Documento de archivo arquitectónico
> Este documento contiene la acumulación cronológica de actualizaciones y decisiones arquitectónicas registradas en el proyecto desde mayo hasta noviembre de 2026.
> Para consultar el estado vivo y vigente del sistema, ver [[Arquitectura Actual]].

---

## Actualizaciones de Noviembre 2026

> [!success] Actualizado 2026-11-08 — Fix de identificador en Reporte de productos
> El preview del `DataGrid` y el reporte exportado de productos muestran ahora `Código` como primera columna y omiten `IdProducto`; el ID interno sigue disponible para identificar los registros. El commit `9f5bd21` se subió a `Fix/Reporte-productos` y se integró en `main`. El usuario confirmó que la aplicación encontró la actualización tras una demora; no se verificaron desde este entorno el run/release exactos ni las migraciones incluidas previamente en la rama. Ver [[Sesión 2026-11-08 - Fix reporte de productos y actualización remota]].

> [!success] Actualizado 2026-11-08 — Ciclo de actualización remota verificado en cliente
> El canal `main` → Actions → Release → cliente funcionó de punta a punta: el run `42550903322` publicó `v1.0.3` y una instalación **per-user** (`%LOCALAPPDATA%\BimboPesaje`, HKCU) detectó, descargó el paquete completo y se actualizó sola, sin UAC, quedando `sq.version` en `1.0.3`. Después se separó validación de publicación: `ci.yml` compila y prueba sin publicar, y `publish-velopack-release.yml` solo dispara con cambios de código o `workflow_dispatch`, así que documentación y configuración de agentes ya no generan versiones. Queda pendiente el alcance per-machine (elevación vía IT), la firma de código, la prueba en una máquina limpia y un canal de pruebas independiente. Ver [[Sesión 2026-11-08 - Resolución P-067 (Pkcs 10.0.14)]].

> [!success] Actualizado 2026-11-08 — P-067 resuelto: `System.Security.Cryptography.Pkcs` 10.0.14
> El transitivo vulnerable que arrastraban `PDFsharp` y `PDFsharp-MigraDoc` 6.2.4 (8.0.1, `NU1903` alta y `NU1902` moderada) quedó fijado en **10.0.14** con un `PackageReference` directo en `CapaDatos`, porque actualizar el padre no sirve: `PDFsharp` 6.5.0 —última estable— sigue declarando 8.0.1. Sin cambios de comportamiento: build 0/0, `dotnet list --vulnerable` limpio en `CapaDatos`, `BimboProyecto.Tests` y `CapaUI`, y 734/734 pruebas (incluida la generación real de PDF). Ver [[Sesión 2026-11-08 - Resolución P-067 (Pkcs 10.0.14)]].

> [!success] Actualizado 2026-11-07 — Releases automáticos y MSI de Velopack
> `main` integra runtime Velopack, el workflow `.github/workflows/publish-velopack-release.yml` (publicación al hacer push a `main`) y el empaquetado de Setup.exe/MSI mediante `scripts/publish-release.ps1`. Actions asigna `1.0.<run_number>` y usa `GITHUB_TOKEN`. Se generó localmente `Releases/BimboPesaje-win.msi` versión 1.0.2 con alcance `Either`; instalación y ejecución de Actions aún no verificadas en un equipo cliente. Ver [[Sesión 2026-11-07 - Merge y MSI de actualizaciones Velopack]].

> [!success] Actualizado 2026-11-06 — CapaUI adopta el punto de entrada de Velopack
> `CapaUI` pasó a tener `Main` propio (`StartupObject=CapaUI.App`) y `App.xaml` compilado como `Page`, para que `VelopackApp.Build().Run()` corra antes de que exista la `Application` y antes de mergear `Resources/Styles.xaml`; un hook de instalación ya no paga el costo del árbol de recursos completo. Se incorporó `Velopack` 1.2.161 y `<Version>` como fuente única de la versión de producto. Identidad de distribución fijada: `packId=BimboPesaje`, `packTitle=Bimbo Pesaje Honduras`, `mainExe=CapaUI.exe`, canal único `win`; firma de código diferida. El arranque funcional (DI, Serilog, login, cierre de sesión) no cambió y no se tocó `main`. Todavía no existe consulta ni aplicación de actualizaciones en runtime. Ver [[Sesión 2026-11-06 - Integración inicial de Velopack en CapaUI]] y [[Plan de CI-CD y Actualizaciones Remotas]].

---

## Actualizaciones de Septiembre 2026

> [!success] Actualizado 2026-09-21 — Notificaciones homologadas y relaciones por nombre
> La bandeja completa usa los filtros compartidos `ComboFiltroBox` dentro de `PanelFiltrosFluido`. `NotificacionDetalleWindow` fue reemplazado por `NotificacionDetalleModal`, alojado en el overlay de `MainWindow`, con la anatomía de Bitácora y pinceles `EmpresaPrimary*` dinámicos. El RPC `listar_mis_notificaciones` enriquece la metadata de salida con el nombre completo del usuario o el nombre del rol, sin reescribir el historial; el ID queda reservado para navegación y nunca se presenta como «Registro relacionado». La migración `20260921141136_mostrar_nombre_registro_en_notificaciones.sql` está aplicada y verificada en Supabase. Ver [[Módulo Notificaciones]] y [[Sesión 2026-09-21 - Homologación visual y nombres relacionados en Notificaciones]].

> [!success] Actualizado 2026-09-21 — Detalle de Bitácora por doble clic y reporte individual
> Un doble clic izquierdo sobre una fila real de Bitácora abre `BitacoraDetalleModal`, de solo lectura, usando el `BitacoraDto` enlazado y sin consultar nuevamente Supabase. La proyección `BitacoraDetalle` concentra etiquetas amigables, formato `dd/MM/yyyy HH:mm` y fallback `Sin información`; la misma proyección alimenta un PDF/Excel individual desde el selector de formato existente. Encabezados, espacios vacíos y controles interactivos no abren el detalle; se evita duplicar el modal y al cerrar se restaura el foco en la fila. La exportación conserva `Generar Reporte`, la auditoría previa y el archivo temporal del flujo existente. Ver [[Sesión 2026-09-21 - Detalle de Bitácora por doble clic y reporte individual]].

> [!success] Actualizado 2026-09-21 — Configuración valida el código RBAC estable antes de guardar
> `EmpresaRepository` dejó de comparar la etiqueta visible `Modificar Configuración` contra la sesión, que almacena códigos de acción, y ahora exige `CONFIGURACION_MODIFICAR`. La autorización sigue siendo doble: validación local con `IUsuarioSesionService` y RLS de Supabase para `public.empresa` y `storage.objects`. Una prueba de regresión fija este contrato. Ver [[Sesión 2026-09-21 - Corrección RBAC al guardar Configuración de empresa]].

> [!success] Actualizado 2026-09-20 — «Mi Usuario» deja de ser un placeholder y trae el control de escala
> `Routes.MiUsuario` pasó de `ConstructionVM` a `MiUsuarioViewModel`, con `MiUsuarioView` en `Formularios/Principal/Pantallas/MiUsuario/`. Tres secciones: **Perfil** (solo lectura), **Apariencia** (escala de la app) y **Seguridad** (placeholder que dice dónde se cambia la contraseña hoy). Los botones `−`/`+` mueven la escala en pasos de 5 % entre 70 % y 130 %, **aplican al instante** y persisten con un `Debouncer` de 600 ms. Ver [[Sesión 2026-09-20 - Preferencias por usuario (Fase 1 del escalado)]].

> [!success] Actualizado 2026-09-20 — Popups y ToolTips siguen el escalado propio de la app
> `Popup`, `ToolTip` y los desplegables de `ComboBox` viven en ventanas Win32 propias y no heredan el `LayoutTransform` del escalado. Ahora lo toman por `{DynamicResource EscalaApp}` sobre un `ScaleTransform` en su `LayoutTransform`. Ver [[Sesión 2026-09-20 - Preferencias por usuario (Fase 1 del escalado)]] y [[WPF - Escalar Popups y ToolTips que no heredan el LayoutTransform]].

> [!success] Actualizado 2026-09-20 — `RolModal` deja de ser ventana y pasa a modal sobre overlay
> Ahora es un `UserControl` sobre el overlay de `RolesView` con `ModalLayout.LimitarAlOverlay`, como los otros modales. Ver [[Sesión 2026-09-20 - Preferencias por usuario (Fase 1 del escalado)]].

> [!success] Actualizado 2026-09-20 — Escalado propio de la app: base, repositorio y servicio (Fases 1 a 3)
> Nueva tabla `public.usuario_preferencias`: clave/valor `jsonb` con PK `(id_usuario, clave, ambito)`. Ver [[Sesión 2026-09-20 - Preferencias por usuario (Fase 1 del escalado)]].

> [!success] Actualizado 2026-09-19 — Tooltips ya no quedan tapados por el cursor
> Todos los ToolTip de la app salen 17 px más abajo de lo que WPF pone por defecto. Ver [[Sesión 2026-09-19 - Tooltips tapados por el cursor]].

> [!success] Actualizado 2026-09-19 — Fila TOTAL de Entradas sin recorte al scrollear y layout de Movimiento
> Fila TOTAL en `ScrollViewer` sincronizado (`TotalScroll`). Ver [[Sesión 2026-09-19 - Layout de Movimiento de Materia Prima y sincronización de fila TOTAL en Pesajes]].

> [!success] Actualizado 2026-09-18 — «Recordar usuario» cifrado con DPAPI
> Cifrado con DPAPI en `%LOCALAPPDATA%\BimboPesaje\Preferencias\ultimo_usuario.bin`. Ver [[Sesión 2026-09-18 - Recordar usuario cifrado con DPAPI]].

> [!success] Actualizado 2026-09-18 — Camiones de Pesaje en tabla plana y reglas de recepción en BD
> Recepciones de pesaje en tabla plana con reglas garantizadas por trigger de base de datos. Ver [[ADR-029 - Recepciones de pesaje planas con reglas en trigger de tabla]].

---

## Actualizaciones de Mayo a Agosto 2026

> [!success] Hitos Históricos
> - **CapaServicios disuelto:** Sus clases pasaron a `CapaDominio` y se eliminó la referencia circular.
> - **Módulos Proveedores, Fabricantes y Categorías:** Replicados con el patrón MVVM y Clean Architecture.
> - **`SuggestionSearchBox` compartido:** UserControl en `CapaUI/Core/Controls/` centraliza sugerencias y debounce.
> - **Módulos Contactos Fabricantes y Contactos Proveedores:** Implementados con patrón drill-down.
> - **Soporte DPI Per-Monitor V2 y multi-resolución:** `app.manifest` y optimizaciones de renderizado.

---

## Relaciones

- [[Arquitectura Actual]]
- [[AGENTS]]
- [[INDEX]]
