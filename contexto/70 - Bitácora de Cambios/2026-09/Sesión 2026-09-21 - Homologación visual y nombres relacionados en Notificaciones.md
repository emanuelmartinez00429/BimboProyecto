---
title: "Sesión 2026-09-21 — Homologación visual y nombres relacionados en Notificaciones"
tags:
  - sesion
  - notificaciones
  - wpf
  - supabase
  - rpc
date: 2026-09-21
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-09-21 — Homologación visual y nombres relacionados en Notificaciones

> [!success] Resultado
> La bandeja completa usa los mismos filtros que el resto del sistema y el detalle dejó de ser una ventana independiente: ahora es un modal sobre el overlay de `MainWindow`, con la anatomía visual de Bitácora y colores dinámicos de la empresa. «Registro relacionado» muestra el nombre del usuario o rol, nunca su identificador técnico. Emanuel solicitó guardar el resultado en contexto después de la implementación.

---

## Problema / motivo

Los dos `ComboBox` de la bandeja tenían un fondo gris, métricas locales y alineación distinta a los filtros compartidos. El detalle de notificación seguía siendo `NotificacionDetalleWindow`, por lo que no respetaba el patrón vigente de modales sobre overlay ni el tema dinámico. Además, el renderizador presentaba `id_registro_origen` bajo la etiqueta «Registro relacionado», aunque el usuario esperaba el nombre legible de la entidad.

## Cambios aplicados

### Filtros compartidos

- `CapaUI/Resources/Styles.xaml` concentra `CeldaFiltro`, `EtiquetaFiltro` y `ComboFiltroBox` como recursos reutilizables.
- `NotificacionesView.xaml` usa `PanelFiltrosFluido`, dos `ComboFiltroBox` de fondo blanco y `DisplayMemberPath="Etiqueta"`; desaparecen los anchos rígidos que desalineaban los filtros.
- Los estilos locales equivalentes se retiraron de Usuarios y Productos; Bitácora consume también el estilo compartido. Se conservaron sus opciones, búsqueda y comportamiento previo.

### Detalle de notificación

- `NotificacionDetalleWindow.xaml/.cs` se retiró y fue reemplazado por `NotificacionDetalleModal.xaml/.cs`, un `UserControl` de `720 × 660`.
- `MainWindow.xaml/.cs` aloja el modal en `NotificacionModalOverlay`, evita aperturas duplicadas, limita el contenido con `ModalLayout.LimitarAlOverlay` y restaura el foco al cerrar.
- El encabezado, resumen, tarjetas y pie siguen la anatomía de `BitacoraDetalleModal`; los pinceles `EmpresaPrimary*` se resuelven mediante recursos dinámicos.
- `NotificacionMetadataRenderer.RenderizarCuerpo` evita repetir fecha y severidad entre el resumen y el cuerpo.

### Nombre del registro relacionado

- `NotificacionMetadataRenderer` consume `nombre_registro_origen` y oculta `id_registro_origen` tanto para códigos registrados como para metadata genérica segura.
- La migración forward-only `supabase/migrations/20260921141136_mostrar_nombre_registro_en_notificaciones.sql` reemplaza `public.listar_mis_notificaciones(...)` sin cambiar su firma.
- El RPC enriquece únicamente la metadata de salida: para `usuarios` devuelve nombre y apellido del empleado con `alias_usuario` como respaldo; para `roles`, `nombre_rol`. El ID canónico permanece disponible en las columnas de origen para navegación.
- Las filas históricas no se reescribieron. La comprobación remota resolvió nombre para los 9 registros relacionados existentes y confirmó `historico_modificado = 0`.
- La migración quedó aplicada en Supabase con versión remota `20260921141136` y nombre `mostrar_nombre_registro_en_notificaciones`.
- El RPC conserva `SECURITY DEFINER`, `search_path=pg_catalog, pg_temp`, deniega ejecución a `anon` y la concede a `authenticated`. La ejecución autenticada de la bandeja confirmó que cada registro relacionado retornado incluía el nombre.

## Verificación

- `dotnet build BimboProyecto.sln --no-restore --nologo` → **0 errores, 0 advertencias**.
- `ContratoNotificacionesTests` → **11/11 aprobadas**.
- Arné WPF de instanciación → `NotificacionesView`, `NotificacionDetalleModal`, Productos, Usuarios y Bitácora instanciaron y midieron correctamente con los estilos compartidos.
- Base viva → migración listada, definición y ACL inspeccionados, 9/9 orígenes históricos resolubles y ejecución autenticada del RPC validada.
- Advisors de seguridad y rendimiento ejecutados. No apareció un hallazgo nuevo asociado a `listar_mis_notificaciones`; permanecen avisos generales preexistentes fuera de este alcance.
- Suite completa → **718 aprobadas, 4 fallidas, 722 totales**. Las cuatro fallas son las ya conocidas de `PreferenciasInicioSesionServiceTests` por DPAPI/archivo temporal (`Guardar_y_leer_devuelve_el_mismo_correo`, `El_archivo_no_contiene_el_correo_en_claro`, `Migra_el_texto_plano_viejo_y_lo_borra` y `Olvidar_borra_el_temporal_de_una_escritura_cortada`); no pertenecen a Notificaciones.
- `git diff --check` sin errores de espacios; solo avisos informativos de conversión LF/CRLF.

## Decisiones

- El nombre se resuelve al leer, no se copia al historial: así las notificaciones existentes reciben el nombre vigente y `notificaciones.metadata` conserva su contrato mínimo.
- Si el registro relacionado ya no puede resolverse, la UI omite el campo antes que volver a exponer el ID técnico.
- No se creó ADR: la fuente de verdad, seguridad y lectura por RPC continúan bajo [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]], y el modal reutiliza el patrón WPF vigente.

## Lo que NO cambió

- No cambió la firma ni el contrato tabular de `listar_mis_notificaciones`/`obtener_mi_notificacion`.
- No se reescribieron notificaciones históricas ni se alteró la navegación por `tabla_origen` e `id_registro_origen`.
- No cambiaron lectura, archivo, restauración, paginación, filtros funcionales, contador o Realtime.
- No se creó commit ni se hizo push.

## Pendientes

- Continúa pendiente la prueba manual multisesión de navegación y reconexión con dos sesiones WPF autenticadas, ya registrada en el módulo.
- Los cuatro tests DPAPI permanecen fuera del alcance de esta sesión.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Notificaciones]]
- [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]]
- [[Convenciones de UI (WPF) — leer antes de tocar XAML]]
- [[Sesión 2026-09-03 - Corrección visual y acción masiva de Notificaciones]]

