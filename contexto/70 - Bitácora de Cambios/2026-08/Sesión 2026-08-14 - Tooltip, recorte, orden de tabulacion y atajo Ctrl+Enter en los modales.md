---
title: "Sesión 2026-08-14 — Tooltip + recorte de texto, orden de tabulación explícito y atajo Ctrl+Enter en los modales de edición"
tags:
  - sesion
  - modales
  - ux
  - accesibilidad
  - productos
date: 2026-08-14
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Opus 5 / Claude Sonnet 5 (Claude Code)
---

# Sesión 2026-08-14 — Tooltip, recorte de texto, tabulación y atajo Ctrl+Enter en los modales

> [!success] Resultado
> Tres comportamientos nuevos, todos reusables vía estilo/comportamiento compartido — sin tocar campo por campo: (1) tooltip + "…" cuando un valor no entra en su campo, en modales que se achican; (2) foco realmente visible en `TextBox`/`ComboBox` de modal, con el mismo verde de "fila seleccionada" que ya usan las tablas; (3) `Ctrl+Enter` guarda desde cualquier campo. Más orden de tabulación explícito en `ProductoModal` (único modal de 3 columnas) y foco automático en el primer campo al abrir, en los 8 modales. Build: `0 errores`.

---

## 1. Tooltip + recorte "…" cuando el texto no entra

**Motivo:** captura del usuario mostrando "Nombre producto" con el valor `AZUCAR NOR` cortado a mitad de palabra, sin ningún indicio visual de que había más texto.

**Lo que ya existía sin usar:** [TextoResponsivo.cs](../../CapaUI/Core/Controls/TextoResponsivo.cs) — comportamiento adjunto completo, apareció en el árbol de trabajo sin commitear y sin cablear a ningún estilo. Cubre los campos de **solo lectura** (los que se llenan por lupa: Fabricante, Proveedor, Categoría…): mide el texto con `FormattedText`, si no entra lo recorta con una búsqueda binaria del prefijo más largo que sí entra + "…", y dejaba el texto completo en el `ToolTip`. Como el valor real persistido vive en un `int? _idXxx` aparte, es seguro mutar `TextBox.Text` ahí.

**Lo que faltaba:** los campos **editables** (Nombre, Código…). Ahí `TextoResponsivo` a propósito no toca `Text` — mutarlo le comería al usuario lo que está tipeando — así que solo agregaba el `ToolTip`, sin recorte visual.

**Solución:** en el `ControlTemplate` de `ModalInput` (`CapaUI/Resources/Styles.xaml`) se agregó un `TextBlock` superpuesto al `ScrollViewer` del campo, con `TextTrimming="CharacterEllipsis"` **nativo** de WPF (no hace falta medir nada a mano, a diferencia del camino que toma `TextoResponsivo` para los de solo lectura). Solo se muestra cuando el campo **no tiene foco** (`Trigger IsFocused=False`): mientras se escribe, se ve el `TextBox` real con su cursor y su scroll normal; al salir del campo, el overlay tapa el `ScrollViewer` con la versión recortada.

Como el `Setter Property="controls:TextoResponsivo.Activo" Value="True"` y el `Template` viven en el `Style` compartido `ModalInput`, cubre los 8 modales de edición de una — nadie tuvo que tocar campo por campo.

> [!note] Encontrado en el árbol, no hecho en esta sesión
> `PesajeModalStyles.xaml` también quedó con `TextoResponsivo.Activo="True"` cableado en su propio `MInput` local — cambio de otra sesión en paralelo trabajando sobre el mismo `TextoResponsivo.cs` (compartido porque estaba sin commitear en el árbol). Se incluye en el mismo commit por quedar en el mismo lote de trabajo, no fue escrito acá.

## 2. Foco casi invisible en los campos de modal

**Motivo:** *"casi no se nota lo que está seleccionado"*, y después *"ponla en verde claro el que usamos en las tablas y algo más gruesa"*.

**Causa:** el único cambio al enfocar un `TextBox` era `BorderBrush="White"` — contra un borde en reposo `#7FFFFFFF` (blanco translúcido) y un fondo de campo blanco sólido, la diferencia es casi imperceptible. El `ComboBox` de modal (`ModalCombo`) no tenía **ninguna** reacción al foco.

**Primera iteración (descartada):** borde azul `#60A5FA` — el mismo del listón decorativo de arriba de todos los modales. El usuario pidió cambiarlo por el verde que ya usan las tablas para marcar la fila seleccionada.

**Solución final**, en `CapaUI/Resources/Styles.xaml`:
- `ModalInput`: al enfocar, el campo pasa a fondo `#C2F2E0` (el mismo relleno de fila seleccionada en las tablas) + borde `#34D399` de **3px** (antes 2px, "algo más gruesa") + resplandor del mismo verde.
- `ModalCombo`: no tiene `ControlTemplate` propio (usa el del tema), así que alcanzó con un `Style.Trigger` sobre `IsKeyboardFocusWithin` (no `IsFocused`: el foco real de un `ComboBox` cae en una parte interna — el `ToggleButton`/editor —, no en el control en sí), con los mismos tres colores.
- Recurso compartido `FocoCampoModal` (`DropShadowEffect`, color `#34D399`) para no duplicar el efecto entre los dos estilos.

> [!bug] `InvalidOperationException` al mantener Tab presionado
> Reportado por el usuario con el stack trace completo del motor de propiedades de WPF (`DependencyObject.GetEffectiveValue`), apuntando a `InvalidPropertyValue`. Causa: `FocoCampoModal` es un `Effect` — un `Freezable`, que solo puede tener **un dueño** a la vez. Sin congelarlo, `ModalInput` y `ModalCombo` (y cada instancia de cada uno, en los 8 modales) compartían el mismo objeto; tabular rápido hacía que varios campos se lo disputaran casi al mismo tiempo y el motor de propiedades tiraba la excepción. Se corrigió agregando `x:Shared="False"` a la declaración del recurso: cada `Setter` que lo pide recibe una instancia nueva, no hay nada que disputarse.

## 3. Orden de tabulación explícito en `ProductoModal`

Es el único modal de 3 columnas (los otros 7 son de una sola columna, así que su orden natural ya es arriba→abajo sin ambigüedad). El orden de declaración en el XAML ya coincidía con izquierda→derecha, arriba→abajo — pero dependía de que nadie reordenara un campo sin darse cuenta de que eso también reordena el tabulado.

Se agregó `TabIndex` explícito (10, 20, 30…) a cada campo y su lupa, fila por fila. De paso, `TxtCreatedAt`/`TxtUpdatedAt` (solo lectura, sin lupa, puramente informativos) pasaron a `IsTabStop="False"` — tabular hasta ahí no le sirve a nadie.

**Foco en el primer campo al abrir**, en los 8 modales: una línea al final de cada `OnLoaded` (después de poblar los valores del registro, así corre en alta y en edición por igual) — `TxtCodigo.Focus()` en Productos, `TxtNombre.Focus()` en Categorías/Fabricantes/Proveedores/Empleados/Contactos×2, `CmbEmpleado.Focus()` en Usuarios (ahí el primer campo real es un combo, no un textbox). Mismo patrón que ya usa `SelectorCatalogoModal.EnfocarCaja() => TxtBusqueda.Focus()` en este proyecto — `.Focus()` llamado directo dentro de `Loaded`, sin `Dispatcher`.

## 4. `Ctrl+Enter` guarda desde cualquier campo

**Pregunta del usuario, respondida antes de implementar:** ¿choca con algo? No — se revisaron los tres lugares que capturan `Key.Enter` en la app (`ComboFiltro`, `SuggestionSearchBox`, `SelectorCatalogoModal`) y ninguno usa el modificador `Ctrl`; `Ctrl+Enter` tampoco tiene comportamiento nativo en un `TextBox` de una sola línea. A pedido explícito del usuario, se dejó **fuera** de los modales de tabla/selección (`SelectorCatalogoModal`), donde Enter simple ya confirma la fila — agregar `Ctrl+Enter` ahí no aportaba nada.

**Implementación:** [AtajoGuardar.cs](../../CapaUI/Core/Controls/AtajoGuardar.cs), comportamiento adjunto nuevo. En vez de reimplementar el guardado, dispara el `Click` real del botón vía `((IInvokeProvider)new ButtonAutomationPeer(boton)).Invoke()` — es lo mismo que un clic físico, así que toda la lógica que ya existe en cada `BtnGuardar_Click` (validación, "Guardando…", deshabilitado mientras guarda — ver [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]]) se respeta sin tocarla. `PreviewKeyDown` tunela desde la raíz del modal hasta el control con foco, así que funciona sin importar en qué campo esté parado el usuario.

Cableado en los 8 modales de edición vía un solo atributo en la raíz de cada uno:
```xml
controls:AtajoGuardar.Boton="{Binding ElementName=BtnGuardar}"
```

**Bug encontrado y corregido en la propia implementación:** el primer intento comprobaba solo `e.Key == Key.Enter` y el usuario reportó que no disparaba el guardado. Se corrigió a `e.Key is Key.Enter or Key.Return` — el mismo patrón defensivo que ya usan `ComboFiltro` y `SuggestionSearchBox` en este proyecto para la misma tecla.

---

## Verificación

- `dotnet build CapaUI/CapaUI.csproj` → 0 errores (con la app cerrada), en cada una de las iteraciones (incluida la del fix del `InvalidOperationException`).
- **Pendiente en runtime:** achicar un modal responsive y confirmar tooltip+"…" en un campo editable con texto largo; tabular por `ProductoModal` y confirmar el orden fila por fila **sin que tire la excepción** (motivo del fix de `x:Shared`); confirmar que `Ctrl+Enter` guarda desde un campo de texto, un combo y un radio button; confirmar que **no** guarda mientras `BtnGuardar` está deshabilitado (ej. durante "Guardando…"); confirmar visualmente que el foco se ve verde claro con borde de 3px.

## Lo que NO cambié

- No toqué los modales de Pesaje (`PesajeModal`, `ProcesoDescargaModal`, `TaraExtraTotalModal`, `ReporteModal`, `SelectorProductosModal`): no tienen el mismo patrón de guardado ni de tabulación multi-columna. El cambio en `PesajeModalStyles.xaml` que quedó en el mismo commit es de otra sesión, según se documenta arriba.
- No armé un `Style.Trigger` de foco para `RadioButton` (`ModalSegBtn`) ni para `LupaBtn`: el pedido puntual eran los textbox/combo; esos dos estilos además están duplicados localmente en 6 modales cada uno (deuda preexistente, no de esta sesión), así que tocarlos hoy hubiera significado repetir el mismo cambio 6 veces.

---

## Relaciones

- [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]] — la lógica de `BtnGuardar_Click` que `AtajoGuardar` reutiliza sin duplicar
- [[Módulo Productos]]
- [[Arquitectura Actual]]
