---
title: "Sesión 2026-09-10 — Auditoría y refactorización técnica del módulo de Productos"
date: 2026-09-10
tags:
  - bitacora
  - sesion
  - wpf
  - mvvm
  - refactor
  - qa
aliases:
  - Auditoría Módulo Productos
---

# Sesión 2026-09-10 — Auditoría y refactorización técnica del módulo de Productos

## Resumen

Se ejecutó una auditoría exhaustiva de caja blanca sobre la pantalla de Productos (`ProductosView`, `ProductosViewModel` y `ProductoModal`), identificando y resolviendo de forma progresiva seis antipatrones de arquitectura y rendimiento (AP-01 al AP-06).

## Intervenciones Realizadas

1. **Frente 1: Desacoplamiento de DI y Mutación Atómica (AP-01 y AP-03):**
   - Se eliminó el uso de Service Locator global (`App.Services.GetRequiredService`) dentro del constructor parametrizado de `ProductoModal.xaml.cs`.
   - Se preservó el constructor sin parámetros para soporte del diseñador de Visual Studio conforme a `ADR-028`.
   - Se independizó la lógica de guardado en `BtnGuardar_Click`, invocando selectivamente las RPCs de Supabase (`actualizar_producto_seguro` vs `cambiar_estado_producto_seguro`) y aislando errores sin falsos positivos en `TxtCodigo`.

2. **Frente 2: Converters MVVM y Ciclo Asíncrono Seguro (AP-02 y AP-05):**
   - Se implementó `EnumToBooleanConverter` reutilizable con enlace bidireccional y retorno de `Binding.DoNothing` en deselección para evitar colisiones en grupos de `RadioButton`.
   - Se enlazaron declarativamente en XAML el `ItemsSource` y `SelectedItem` del DataGrid y los filtros de estado y ordenamiento, suprimiendo banderas imperativas (`_suppressFilterChange`), eventos `Checked=` y más de 30 líneas de code-behind.
   - Se saneó el ciclo de vida de `CancellationTokenSource` en `ProductosViewModel.cs` mediante `using var cts` y cancelación segura protegida de `ObjectDisposedException`.

3. **Frente 3: Componentización de LoadingOverlay y Sombras Desacopladas (AP-04 y AP-06):**
   - Se creó el control reutilizable `CapaUI.Core.Controls.LoadingOverlay` con spinner vectorial, animación encapsulada y propiedad `HeaderOffset`.
   - Se eliminó la duplicación del `Storyboard` de rotación en `ProductosView.xaml.cs`.
   - Se desacopló el `DropShadowEffect` en `ProductoModal.xaml` mediante un `Border` hermano posterior sin `ClipToBounds`, garantizando desenfoque completo y previniendo la invalidación continua de texturas en la GPU.

## Verificación de Calidad

- **Compilación:** `dotnet build BimboProyecto.sln --no-incremental` -> **0 Advertencias, 0 Errores** en todos los proyectos de la solución (.NET 10).
- **Pruebas Unitarias:** `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` -> **357/357 (100%) superadas**.
