---
title: "Sesión 2026-08-14 — Scroll horizontal con Shift+rueda/trackpad en DataGrid"
tags:
  - sesion
  - ui
  - wpf
  - datagrid
date: 2026-08-14
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Claude Sonnet 5 (Claude Code)
---

# Sesión 2026-08-14 — Scroll horizontal con Shift+rueda/trackpad en DataGrid

> [!success] Resultado
> `Shift` + rueda del mouse (o `Shift` + gesto de dos dedos del trackpad) ahora mueve `DgProductos` en horizontal. Se armó como helper compartido (`ScrollHorizontalConShift.Habilitar(grid)`) para engancharlo en una línea a cualquier otro `DataGrid`. Build: `0 errores CS`.

---

## Pedido y corrección de rumbo

El usuario preguntó si se podía usar el trackpad como "mouse" para mover las tablas en horizontal, y qué propiedad de WPF lo hacía. Primera respuesta (**incorrecta**): afirmé que `ScrollViewer.OnMouseWheel` ya interpreta `Shift + rueda` como scroll horizontal de forma nativa, y que como `DgProductos` ya tenía `ScrollViewer.HorizontalScrollBarVisibility="Auto"` puesto, no hacía falta tocar nada.

El usuario probó y **no funcionaba**. Al repreguntar, la afirmación no se sostuvo: WPF no tiene ese comportamiento incorporado. `ScrollViewer.OnMouseWheel` scrollea vertical siempre, sin mirar `Keyboard.Modifiers` — el "Shift+rueda = horizontal" que dan por sentado Excel o los navegadores es una convención de cada aplicación, no algo que trae el framework. Hubo que implementarlo a mano.

**Lección:** no volver a afirmar un comportamiento de framework como "ya viene así" sin haberlo verificado (código fuente de WPF, o al menos una prueba concreta) — más aún cuando la alternativa barata (decirlo como hipótesis a confirmar) no cuesta nada y evita una ronda de ida y vuelta con una afirmación falsa.

---

## Diagnóstico previo (correcto, se mantiene)

Sigue siendo cierto que **el scroll vertical de dos dedos ya funcionaba sin tocar nada**: el driver de Windows Precision Touchpad traduce ese gesto en mensajes `WM_MOUSEWHEEL` estándar, que el `ScrollViewer` interno del `DataGrid` ya escucha nativamente.

También sigue siendo cierto que `PanningMode` **no era la propiedad correcta acá** — esa habilita panning por manipulación táctil (dedo en pantalla táctil, con `IsManipulationEnabled="True"`), no gestos de trackpad. Un trackpad no genera eventos de `Manipulation`.

Lo que faltaba corregir era la afirmación sobre el atajo de teclado — ese es el error de esta sesión.

---

## Implementación

**`CapaUI/Core/Controls/ScrollHorizontalConShift.cs`** (nuevo) — helper estático, mismo patrón liviano que `ComboFiltro`/`SuggestionDebouncer`:

```csharp
public static class ScrollHorizontalConShift
{
    public static void Habilitar(DataGrid grid)
    {
        ScrollViewer? scroll = null;

        grid.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Shift) return;

            scroll ??= BuscarScrollViewer(grid);
            if (scroll is null) return;

            scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        };
    }

    private static ScrollViewer? BuscarScrollViewer(DependencyObject raiz) { /* BFS por VisualTreeHelper */ }
}
```

`PreviewMouseWheel` en vez de `MouseWheel` porque el `DataGrid` (y su `ScrollViewer` interno) consumen el evento antes de que llegue a un handler normal — sin el `Preview`, `Shift+rueda` nunca se ve.

El `ScrollViewer` se busca **una sola vez** con `VisualTreeHelper` y se cachea en un closure — no se camina el árbol en cada evento de rueda. Es un caso distinto del que fichó **P-005** (resuelto 2026-05-28): ahí el problema era acoplarse a la estructura interna de un `ItemsControl` de sugerencias armado a mano, frágil ante cualquier cambio de XAML propio. Acá se busca el `ScrollViewer` dentro del template **por defecto** de `DataGrid` — parte fija del control, no algo que este proyecto vaya a cambiar — y la búsqueda es genérica (cualquier `ScrollViewer`, no acoplada a nombres), así que sobrevive a cambios de estilo.

**Enganche** — una línea en el constructor de la vista, antes de cualquier otra inicialización:

```csharp
public ProductosView()
{
    InitializeComponent();
    ScrollHorizontalConShift.Habilitar(DgProductos);
    ...
```

---

## Verificación

- `dotnet build CapaDatos/CapaDatos.csproj` → 0 errores.
- `dotnet build CapaUI/CapaUI.csproj` → 0 errores `CS*` (falló solo la copia de DLL por `CapaUI.exe` corriendo — mismo caso recurrente de toda la sesión).
- **Pendiente en runtime**: cerrar la app, recompilar, correr, y confirmar `Shift+rueda`/`Shift`+trackpad sobre la grilla de Productos.

## Alcance — solo Productos por ahora

El helper quedó armado para reusarse en una línea, pero **solo se enganchó en `ProductosView`**. Los otros 5 módulos con `DataGrid` y scroll horizontal (Categorías, Fabricantes, Proveedores, ContactosFabricantes, ContactosProveedores) no lo tienen todavía — se dejó así a propósito, para que el usuario confirme que funciona en Productos antes de replicarlo. Si se pide después, es:

```csharp
ScrollHorizontalConShift.Habilitar(DgCategorias);
```

por vista, sin más cambios.

## Estado en git

Commiteado en esta sesión. Se dejó **fuera del commit** un cambio no relacionado en `CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs` que apareció modificado en el árbol de trabajo sin que esta conversación lo haya tocado — parece trabajo concurrente de otra sesión sobre el mismo repo (ver [[Sesión 2026-08-14 - Realtime en columnas de join de Productos]] para contexto de que hay más de un agente trabajando el mismo día). No se debe commitear a nombre de este cambio sin que su autor lo confirme.

---

## Relaciones

- [[Módulo Productos]]
- [[Deuda Técnica - Pendientes]] — P-005 (por qué esta búsqueda con `VisualTreeHelper` es un caso distinto, no una reincidencia)
