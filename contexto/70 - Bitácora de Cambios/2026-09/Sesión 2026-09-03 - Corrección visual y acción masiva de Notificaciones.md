---
title: "Sesión 2026-09-03 - Corrección visual y acción masiva de Notificaciones"
tags: [sesion, bimbo, notificaciones, wpf, supabase, rpc]
date: 2026-09-03
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex
revisor: Fernando
---

# Sesión 2026-09-03 - Corrección visual y acción masiva de Notificaciones

> [!success] Resultado
> Se rediseñaron la campana, la bandeja completa y el diálogo de detalle, y se convirtió la acción masiva en una lectura y archivo atómicos de toda la Bandeja. Fernando aprobó la corrección visual el 2026-09-03; la solución quedó con compilación limpia, 240/240 pruebas y validación transaccional remota.

---

## Problema / motivo

La presentación de notificaciones no diferenciaba con suficiente claridad la severidad del estado de lectura, exponía valores internos en los filtros y tenía acciones secundarias ambiguas. El diálogo programático conservaba espacio inferior innecesario y métricas distintas al resto de la pantalla. Además, la acción masiva solo marcaba como leídas las notificaciones, cuando el flujo esperado era vaciar `Bandeja` y conservarlas en `Archivadas` sin cambiar automáticamente de apartado.

## Cambios aplicados

### Experiencia WPF compartida

- Se creó `CapaUI/Formularios/Principal/Pantallas/Notificaciones/NotificacionesResources.xaml` como diccionario común para la bandeja, el desplegable y el diálogo. Define botones de texto de 36 px, iconos de 32 × 32, estados de interacción y los indicadores de severidad azul, ámbar y rojo.
- `CapaUI/Formularios/Principal/Pantallas/Notificaciones/NotificacionesView.xaml` y `CapaUI/Formularios/Principal/MainWindow.xaml` ahora muestran tarjetas completas presionables, separan severidad de lectura, destacan las no leídas y usan los textos `Abrir registro`, `Marcar leída`, `Archivar` y `Restaurar`.
- Los filtros visibles quedaron como `Bandeja`, `No leídas`, `Leídas` y `Archivadas`; no se exponen valores internos.
- El desplegable de la campana usa una colección independiente limitada a las cinco notificaciones más recientes de `Bandeja`.
- `CapaUI/Formularios/Principal/Pantallas/Notificaciones/NotificacionDetalleWindow.xaml` reemplaza la construcción programática del diálogo. Usa `SizeToContent="Height"`, no establece `MinHeight`, limita `MaxHeight` a 720 px, coloca el pie inmediatamente después del contenido y conserva el renderizado seguro de metadata conocida, vacía o desconocida.

### Estado y contrato de aplicación

- `NotificacionesViewModel` ejecuta `MarcarTodasLeidasYArchivarAsync`, mantiene seleccionado `Bandeja` y recarga lista, cinco recientes y contador mediante RPC. No elimina filas ni altera el contador de forma optimista.
- `INotificacionRepository` y `NotificacionRepository` renombraron el contrato C# a `MarcarTodasLeidasYArchivarAsync`; el nombre público de la función PostgreSQL se conservó por compatibilidad.
- Se añadieron pruebas de contrato en `BimboProyecto.Tests/Notificaciones/ContratoNotificacionesTests.cs` para fijar el nombre del método y las métricas/recursos XAML relevantes.

### Persistencia Supabase

- La migración forward-only `supabase/migrations/20260903075117_archivar_notificaciones_al_marcar_todas.sql` reemplaza solamente el cuerpo de `public.marcar_todas_mis_notificaciones_leidas()`.
- La función ejecuta un único `UPDATE` sobre todas las filas no archivadas del usuario resuelto mediante `auth.uid()`: `fecha_leida = coalesce(fecha_leida, now())` y `fecha_archivada = coalesce(fecha_archivada, now())`.
- Se incluyen filas no leídas y previamente leídas; las ya archivadas quedan intactas. Se conservaron nombre, firma, propietario, `SECURITY DEFINER`, `search_path`, RLS y privilegios existentes, sin añadir `GRANT`.
- La migración quedó aplicada al proyecto remoto; el historial remoto la registra con la versión `20260903020638` y el nombre `archivar_notificaciones_al_marcar_todas`.

## Verificación

- Aprobación visual de Fernando el 2026-09-03: la presentación y el comportamiento quedaron como fueron solicitados.
- `dotnet build BimboProyecto.sln` finalizó con 0 errores y 0 advertencias.
- La suite automatizada superó 240/240 pruebas.
- La prueba transaccional remota con bandeja mixta confirmó el total afectado sobre el estado real previo, ambas fechas, preservación de fechas existentes, aislamiento entre usuarios, Bandeja vacía e idempotencia en la segunda ejecución.
- Casos negativos remotos confirmados: usuario sin `NOTIFICACIONES_CONSULTAR` rechazado y usuario inactivo rechazado.
- Todas las pruebas de base se revirtieron; se comprobó que no quedaron usuarios, permisos ni notificaciones residuales.
- Se verificaron en remoto propietario, `SECURITY DEFINER`, `search_path` y privilegios de la función. El aviso de asesor sobre funciones `SECURITY DEFINER` accesibles por `authenticated` se conserva porque forma parte intencional del contrato protegido por permiso y `auth.uid()`.
- `git diff --check` no detectó errores; solo emitió avisos informativos de normalización LF/CRLF.

## Lo que NO cambió

- No se añadieron permisos, políticas RLS, caché local, cola offline ni mutaciones optimistas.
- No se modificaron migraciones previamente aplicadas ni el nombre/firma pública de la RPC.
- `PRODUCTO_EXISTENCIA_BAJA`, resolución global y retención automática siguen fuera de alcance.
- La aprobación recibida cubre la corrección visual y funcional solicitada; todavía queda pendiente una prueba manual multisesión de navegación y reconexión con dos sesiones WPF autenticadas.
- No se creó un ADR nuevo: [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]] continúa cubriendo la decisión arquitectónica vigente.
- No se creó commit ni se hizo push.

---

## Relaciones

- [[Módulo Notificaciones]]
- [[Arquitectura Actual]]
- [[ADR-025 - Notificaciones internas con Supabase como fuente de verdad]]
- [[Sesión 2026-09-02 - Infraestructura de notificaciones internas]]
