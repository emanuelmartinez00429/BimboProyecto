---
title: "Sesión 2026-09-02 — Componente reutilizable EmptyStateOverlay para tablas"
tags:
  - sesion
  - ui
  - ux
  - wpf
  - datagrid
  - controls
  - productos
date: 2026-09-02
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (sesión gestionada por Fernando)
---

# Sesión 2026-09-02 — Componente reutilizable EmptyStateOverlay para tablas

> [!success] Resultado
> Se creó el componente desacoplado `EmptyStateOverlay` en `CapaUI.Core.Controls` para mostrar mensajes y estados vacíos contextuales en medio de las tablas manteniendo visibles los encabezados de columna. Se integró y validó exitosamente en el Catálogo de Productos.

---

## Problema / Motivo

1. **Tabla en blanco sin indicación en filtros vacíos:**
   Al seleccionar el filtro de estado "Inactivos" cuando la base de datos no contenía ningún registro inactivo (conteo = 0), el área de la tabla se mostraba completamente en blanco y el usuario solo veía un texto pequeño "Sin resultados" en la barra de paginación inferior.
2. **Encubrimiento de elementos en WPF:**
   El mensaje previo `EmptyState` en `ProductosView.xaml` era un `StackPanel` ubicado antes del `DataGrid`. Debido al orden de dibujado de WPF y al `Background="White"` del `DataGrid`, este último se dibujaba encima tapándolo por completo.
3. **Condición restrictiva en `NoResults`:**
   En los ViewModels, la propiedad `NoResults` estaba condicionada a `TotalCount > 0`, lo que impedía que se mostrara el estado vacío si una tabla estuviera totalmente desprovista de registros.

---

## Decisiones de Diseño y Arquitectura

1. **Retención de encabezados de columna:**
   Los encabezados de las columnas proporcionan la estructura visual de la pantalla y no deben colapsarse cuando la consulta esté vacía.
2. **Overlay no intrusivo (`EmptyStateOverlay`):**
   - Se ubica en la misma celda de `Grid` que el `DataGrid`, con `Panel.ZIndex="5"` e `IsHitTestVisible="False"`.
   - Propiedad `HeaderOffset="40"` que compensa la altura de los encabezados de columna (40px) y centra el mensaje exactamente en el cuerpo disponible de filas.
3. **Lógica de mensajes contextuales inteligentes:**
   - `"No hay registros inactivos"`: cuando se filtra por *Inactivos*.
   - `"No hay registros activos"`: cuando se filtra por *Activos* y no hay activos pero sí existen otros registros.
   - `"No se encontraron resultados con los filtros actuales"`: cuando hay texto de búsqueda o filtros de combos aplicados.
   - `"No hay registros"`: tabla vacía general.
4. **Reactividad MVVM:**
   `_isLoading` incluye `[NotifyPropertyChangedFor(nameof(NoResults), nameof(MensajeSinResultados))]`, asegurando que al terminar la carga se notifiquen inmediatamente los estados calculados.

---

## Cambios Aplicados

### 1. Componente Reutilizable
- `CapaUI/Core/Controls/EmptyStateOverlay.xaml`: `UserControl` con marco circular suave, icono vectorial, título semi-bold y subtexto opcional.
- `CapaUI/Core/Controls/EmptyStateOverlay.xaml.cs`: Code-behind con DependencyProperties (`Mensaje`, `Submensaje`, `EstaVacio`, `HeaderOffset`).

### 2. Catálogo de Productos
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosViewModel.cs`:
  - `NoResults` simplificado a `!IsLoading && _filteredCount == 0`.
  - Nueva propiedad `MensajeSinResultados` con lógica contextual.
  - Notificaciones en `CargarPaginaAsync`, `CargarPaginaSilenciosamenteAsync` y `RefrescarConteosAsync`.
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml`:
  - Eliminado el antiguo `StackPanel` oculto.
  - Agregado `<controls:EmptyStateOverlay>` enlazado a `MensajeSinResultados` y `NoResults`.
- `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml.cs`:
  - Actualización de visibilidad en `ActualizarCarga()` para sincronización inmediata tras el spinner.

---

## Verificación

- Compilación de `CapaUI` limpia: 0 errores, 0 advertencias.
- Prueba manual del usuario: validado y aprobado en el Catálogo de Productos.

---

## Relaciones

- [[Empty State en DataGrid - Overlay centrado con encabezados visibles]]
- [[Módulo Productos]]
- [[Conocimiento Principal]]
