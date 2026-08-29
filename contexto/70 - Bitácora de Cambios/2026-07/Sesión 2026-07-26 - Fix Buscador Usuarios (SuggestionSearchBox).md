---
title: Sesión 2026-07-26 — Fix Buscador Usuarios (SuggestionSearchBox)
type: sesion
status: vigente
tags:
  - sesion
  - usuarios
  - ui
  - wpf
  - disonancia-visual
date: 2026-07-26
updated: 2026-07-26
summary: "Fernando reportó que el buscador de la vista Usuarios \"no se ve igual ni funciona como los de los otros formularios\". Comparación contra los 6 formularios que ya…"
scope:
  - CapaUI/Formularios/Principal/Pantallas/Usuarios
symbols:
  - Codigo
  - HighlightIndex
  - ObtenerConteosAsync
  - ObtenerPaginaAsync
  - OnFiltrosLimpiados
  - OnVmPropertyChanged
  - RefrescarSugerenciasAsync
  - SeleccionarSugerencia
  - ShowSuggestions
  - SuggestionItemData
branch: feat/fase7-GestióndeUsuarios
autor_cambios: "Claude (Sonnet 5), dirigido por Fernando"
---

# Sesión 2026-07-26 — Fix Buscador Usuarios (SuggestionSearchBox)

## Disonancia encontrada

Fernando reportó que el buscador de la vista Usuarios "no se ve igual ni funciona como los de los otros formularios". Comparación contra los 6 formularios que ya usan `controls:SuggestionSearchBox` (Productos, Proveedores, Fabricantes, Categorías, Contactos Fabricantes, Contactos Proveedores):

| | Otros 6 módulos | Usuarios (antes) |
|---|---|---|
| Control | `<controls:SuggestionSearchBox>` | `<TextBox x:Name="TxtSearch">` plano |
| Popup de sugerencias | Sí (lista desplegable con highlight) | No — no existía ningún popup |
| Navegación por teclado (↑↓ Enter Esc) | Sí | No |
| Selección con mouse | Sí | No |

Lo curioso: **`UsuariosViewModel` ya tenía toda la lógica lista** (`Suggestions`, `ShowSuggestions`, `HighlightIndex`, debounce de 300ms en `RefrescarSugerenciasAsync`, `SeleccionarSugerencia`) — el gap era puramente de wiring en la View/code-behind. El módulo se construyó "con el patrón de Productos" (commit `f105047`) pero el paso de cablear el control compartido se saltó.

Auditoría del resto del formulario (header, chips de stats, toolbar, filtros por ComboBox, tabla, paginación, botones de acción, modal overlay) contra Categorías/Productos: **sin otras disonancias** — estilos, templates y estructura de Grid son consistentes.

## Fix aplicado

**`CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosView.xaml`**
- Agregado `xmlns:controls="clr-namespace:CapaUI.Core.Controls"`.
- `TextBox TxtSearch` reemplazado por `<controls:SuggestionSearchBox Query="{Binding Query, Mode=TwoWay, ...}" HighlightIndex="{Binding HighlightIndex, Mode=TwoWay}" Placeholder="Buscar usuario por alias o nombre de empleado..." ItemSelected="SearchBox_ItemSelected"/>` — mismo patrón exacto que `CategoriasView.xaml`.

**`CapaUI/Formularios/Principal/Pantallas/Usuarios/UsuariosView.xaml.cs`**
- `using CapaUI.Core.Controls;` agregado.
- Caso `ShowSuggestions` agregado al switch de `OnVmPropertyChanged` → dispara `ActualizarSuggestions()`.
- `ActualizarSuggestions()`: mapea `UsuarioVistaDto` → `SuggestionItemData` (`Nombre = NombreEmpleado`, `Meta = "{CorreoUsuario} · {NombreRol}"`, `Activo = IdEstado==1`, sin `Codigo` — mismo criterio que Proveedores, que tampoco tiene código corto natural).
- `SearchBox_ItemSelected`: llama `_vm.SeleccionarSugerencia(...)` + `SeleccionarEnTabla()`, igual que los demás módulos.
- `TxtSearch_TextChanged` eliminado (código muerto, el control ya no existe).
- `OnFiltrosLimpiados`: quitada la línea `TxtSearch.Text = ""` — ningún otro módulo limpia el buscador al presionar "Limpiar Filtros" (tampoco lo hace `LimpiarFiltros()` en el ViewModel), así que se dejó de hacerlo aquí también por consistencia.

No se tocó el backend: `ObtenerPaginaAsync`/`ObtenerConteosAsync` siguen usando la vista `vista_usuarios_busqueda` de [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] — es la única diferencia real (y esperada) frente a los otros módulos, que buscan sobre una sola tabla.

## Verificación

- `dotnet build BimboProyecto.sln` → 0 errores, 0 advertencias.
- **No se pudo probar en runtime**: la app requiere login contra Supabase y no hay forma de automatizar esa autenticación de forma seguera desde el agente. Pendiente que Fernando confirme visualmente: escribir en el buscador de Usuarios debe abrir el popup con sugerencias, resaltar con ↑/↓, seleccionar con Enter/click, y el look debe ser idéntico al de Categorías/Proveedores.

## Documentación actualizada

- [[Módulo Productos]] — nueva nota `[!warning]` en "Notas críticas": el buscador siempre debe ser `SuggestionSearchBox`, nunca `TextBox` plano; éste es el ejemplo real de cuando no se hizo así.
- [[Arquitectura Actual]] — conteo de formularios con `SuggestionSearchBox` corregido de "4" a "7" (ya estaba desactualizado antes de esta sesión — Contactos Fabricantes/Proveedores tampoco se habían contado).
- [[Conocimiento Principal]] — mismo conteo actualizado en la tabla de estado del dashboard.

## Relaciones

- [[Módulo Productos]] — patrón de referencia, ahora con la advertencia
- [[Paginación y Búsqueda - Arquitectura Detallada]] — sección 4 (búsqueda con sugerencias)
- [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]] — por qué Usuarios busca distinto a nivel de datos
- [[Sesión 2026-07-23 - Revisión QA Módulo Usuarios y Refactor de Sesión (Emanuel)]] — origen del módulo
