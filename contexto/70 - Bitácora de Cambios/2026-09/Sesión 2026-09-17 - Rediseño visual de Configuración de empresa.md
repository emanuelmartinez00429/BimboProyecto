---
title: "Sesión 2026-09-17 — Rediseño visual de Configuración de empresa"
tags:
  - sesion
  - configuracion
  - ui
  - xaml
date: 2026-09-17
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Code (agente)
---

# Sesión 2026-09-17 — Rediseño visual de Configuración de empresa

> [!success] Resultado
> `ConfiguracionEmpresaView` pasó de una tarjeta única con dos columnas a un diseño de **dos tarjetas hermanas + barra de acciones**, siguiendo la captura de referencia de Fernando. Solo cambió el XAML y se sumó un converter; el ViewModel quedó intacto. Build limpio, 63/63 tests de Configuración, arné de instanciación OK y render comparado contra la referencia.

---

## Problema / motivo

Fernando pidió que la pantalla de configuración se viera "más profesional", y pasó una captura de referencia: encabezados de sección con ícono, rótulos en mayúsculas, vistas previas grandes y muestras de color anchas.

## Cambios aplicados

### `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaView.xaml`

- **Encabezado:** el ícono de 44×44 va a la izquierda. A su lado, el breadcrumb con `Run`s ("Ajustes" gris / "Configuración de empresa" oscuro semibold) y el título de 22pt.
- **Dos tarjetas hermanas** (`TarjetaSeccion`: blanco, borde `#E2E8F0`, radio 12, sin sombra por la regla Zero-Shader). Comparten fila en un `Grid` de 3 columnas (`* / 20 / *`), así que se estiran a la **misma altura** sin fijar tamaños.
- **Encabezado de sección:** ícono de línea (`IconEdificio` / `IconPaleta`, `PathGeometry` con `po:Freeze`), título en mayúsculas con `EmpresaPrimaryBrush`, subtítulo gris a la derecha ("Datos fiscales y de contacto", "Logo y color") y una línea de 2px en color de empresa debajo.
- **Rótulos:** estilo `LabelStyle` en mayúsculas, 11.5pt, Bold, `#8A96A8`.
- **Inputs (`InputStyle`):** 44px de alto, radio 6 y marcador `controls:Placeholder.Texto` dibujado en la propia plantilla. Placeholders: `0000-0000-000000`, `Colonia, calle, ciudad y departamento`, `+504 0000-0000`, `contacto@empresa.com`, `@empresa.com`, `#000000`. Dirección pasó a ser de una sola línea (antes era multilínea de 70px).
- **Identidad visual:** es un `Grid` interno con fila `*` (`MinHeight="180"`) para las vistas previas, que ocupan todo el alto sobrante de la tarjeta. El marco es un `Rectangle` con `StrokeDashArray="4 3"`, porque `Border` no soporta borde punteado. El botón muestra "Reemplazar" si hay imagen y "Seleccionar" si no (estilos `TextoBotonLogo` / `TextoBotonIcono` con `DataTrigger` sobre `Tiene*VistaPrevia`).
- **Color principal:** 5 muestras anchas en `UniformGrid Columns="5"` (38px de alto). La muestra activa lleva aro `#0F172A` de 2px con separación blanca de 2px. Debajo van el hex y un cuadro de vista previa (útil cuando el hex no coincide con ninguna muestra).
- **Barra de acciones:** es una tarjeta aparte, con "Los cambios se aplican a todo el sistema al guardar." a la izquierda ("Cargando configuración..." mientras `Cargando`) y **Guardar cambios** a la derecha.
- **`BotonGuardarInstitucional`:** pasó de degradado a verde plano `#11885F` (hover `#0E7552`), radio 6 y 44px.

### `CapaUI/Converters/TextosIgualesConverter.cs` (nuevo)

`IMultiValueConverter` que devuelve `true` si todos los valores son el mismo texto (sin distinguir mayúsculas y con `Trim`). Expone `Instancia` para `{x:Static}` (ADR-028). Se usa para marcar la muestra de color activa comparando `ColorEmpresa` contra el `CommandParameter` de cada botón.

### Botón Cancelar — NO se agregó

La captura de referencia lo tenía, pero la [[Sesión 2026-09-17 - Migración de Configuración a Vista Completa y Caché Inmediata 0ms]] lo había eliminado a propósito, y el test `VistaXaml_EstructuraBotones_SoloGuardarInstitucional` lo exige. Se respetó esa decisión: el `CancelarCommand` que se llegó a escribir en el ViewModel se revirtió. Si Fernando lo quiere de vuelta, hay que actualizar el test y dejar constancia de la reversión de la decisión.

## Gotchas encontrados

> [!warning] `Padding` duplicado en la plantilla de `TextBox`
> La primera versión de `InputStyle` ponía `Margin="{TemplateBinding Padding}"` en `PART_ContentHost` (el mismo patrón que usa `InputBox` en `Styles.xaml`). En el render, el texto tipeado quedaba ~16px más adentro que el placeholder: `TextBoxBase` ya aplica el `Padding` dentro del host, así que se sumaba dos veces. **Fix:** el host va sin `Margin` y el marcador usa `Margin="16,0,14,0"` (Padding 14 + los ~2px internos del `TextBoxView`). Queda como [[Deuda Técnica - Pendientes#P-064|P-064]] revisar los estilos compartidos que usan el mismo patrón.

> [!warning] `RelativeSource TemplatedParent` dentro de `ControlTemplate.Triggers` no resuelve
> En un `MultiBinding` de un `DataTrigger` que vive en `ControlTemplate.Triggers`, `RelativeSource TemplatedParent` no devolvía el `CommandParameter` y el aro nunca aparecía. Dentro de los triggers de la plantilla, **`RelativeSource Self` ya es el control plantillado**. Con `Self` funcionó.

## Verificación

- `dotnet build BimboProyecto.sln` → **0 errores, 0 advertencias**.
- `dotnet test --filter Configuracion` → **63/63**. Incluye los invariantes de vista: sin `DropShadowEffect`, `po:Freeze`, sin Cancelar, banners de Realtime y dominio, y merge de `Styles.xaml`.
- **Arné de instanciación** (`new Application()` vacío, ctor sin parámetros, `Measure`/`Arrange` a 1400×900) → `OK`. El proyecto del scratchpad se borró.
- **Render a PNG** con `RenderTargetBitmap` (1220×800, `EmpresaPrimaryBrush` = `#1E3A8A` y un VM de prueba), comparado contra la captura de referencia. Con el render se detectaron los dos gotchas de arriba.
- Después del cambio, [[Sesión 2026-09-18 - Panel de control y gráfico de pesadas en PesajeModal]] corrigió encima de este diseño los títulos cruzados logo/ícono y dejó el aviso del dominio visible solo con foco. Fernando lo revisó visualmente y lo aprobó.

## Lo que NO cambió

- `ConfiguracionEmpresaViewModel.cs` y el code-behind (sin cambios netos).
- Bindings, comandos y el flujo de guardado/Realtime/ChangeTracker.
- Los 5 colores preestablecidos (`#1E3A8A`, `#7C3AED`, `#0F766E`, `#9A3412`, `#991B1B`).
- `Styles.xaml`: todos los estilos nuevos son locales a la vista.

---

## Relaciones

- [[Módulo Configuración de Empresa]]
- [[Arquitectura Actual]]
- [[Convenciones de UI (WPF) — leer antes de tocar XAML]]
- [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]]
- [[Sesión 2026-09-17 - Migración de Configuración a Vista Completa y Caché Inmediata 0ms]]
- [[Sesión 2026-09-18 - Panel de control y gráfico de pesadas en PesajeModal]]
- [[Deuda Técnica - Pendientes]] — P-064
