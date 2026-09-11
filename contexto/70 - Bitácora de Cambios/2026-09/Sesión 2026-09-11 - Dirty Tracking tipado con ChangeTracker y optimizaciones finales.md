---
title: "Sesión 2026-09-11 — Dirty Tracking tipado con ChangeTracker<T> y optimizaciones de catálogo"
date: 2026-09-11
tags:
  - bitacora
  - sesion
  - wpf
  - refactor
  - dirty-tracking
  - change-tracker
  - qa
aliases:
  - Rollout ChangeTracker
  - Dirty Tracking Tipado Modales
---

# Sesión 2026-09-11 — Dirty Tracking tipado con ChangeTracker<T> y optimizaciones de catálogo

## Resumen

Se completó la erradicación definitiva del antipatrón **AP-03** (dirty tracking manual con cadenas de `!string.Equals` o `!=` en code-behind) mediante el diseño y despliegue del componente genérico y desacoplado `ChangeTracker<T>` con `records` posicionales privados en los 5 modales de catálogo (`ProveedorModal`, `ProductoModal`, `FabricanteModal`, `CategoriaModal`, `PresentacionModal`). Asimismo, se resolvieron las observaciones de la auditoría externa de QA: sellado y comparación bit a bit en `EnumToBooleanConverter` (AP-02), estandarización de alineación a la izquierda en columnas `CREADO` / `ACTUALIZADO` y corrección de opacidad en `LoadingOverlay`.

## Contexto y Motivación

Durante la auditoría arquitectónica de los formularios de edición, se detectó una vulnerabilidad crítica de mantenimiento en el dirty tracking:
1. **Riesgo de Fallo Silencioso (*Silent Failure*):** En `ProveedorModal` y `ProductoModal`, la comprobación de cambios dependía de una larga cadena de condiciones `OR` manuales (`!string.Equals(...)`). Si un desarrollador agregaba un nuevo campo editable al formulario y olvidaba sumarlo a esa cadena, el modal asumía falsamente que "no hubo cambios", descartando silenciosamente la persistencia.
2. **Actualizaciones Redundantes de Red:** En `FabricanteModal`, `CategoriaModal` y `PresentacionModal`, no existía dirty tracking para los datos generales, emitiendo llamadas `UpdateAsync` a Supabase aún cuando el operador no modificaba ningún campo.
3. **Desviación de AP-02:** `EnumToBooleanConverter` permanecía como clase abierta (`public class`) y evaluaba enumeraciones con `[Flags]` mediante `Enum.HasFlag` en lugar de la operación bit a bit sobre enteros sin signo prescrita en la investigación técnica.

## Intervenciones Realizadas

### 1. Creación de `ChangeTracker<T>` (`CapaUI.Core.Validacion`)
- Se implementó la clase genérica sellada `ChangeTracker<T>` y su fábrica estática `ChangeTracker.Create<T>`.
- Opera puramente mediante igualdad por valor estructural (`EqualityComparer<T>.Default.Equals`) sin arrastrar dependencias del subsistema visual de WPF ni de hilos STA.
- Si el snapshot inicial es nulo (registro nuevo), `IsDirty(actual)` retorna siempre `true`. Si existe snapshot, evalúa la discrepancia de valor.

### 2. Rollout a los 5 Modales de Catálogo
Cada modal define un `record` privado y posicional que modela estrictamente sus campos editables:
- **`ProveedorModal`**: `ProveedorSnapshot(Nombre, Rtn, Telefono, Correo, Direccion)`. Reemplaza la cadena de 5 `!string.Equals` por `_tracker.IsDirty(snapshotActual)`.
- **`ProductoModal`**: `ProductoSnapshot(CodigoInterno, Nombre, Contenido, IdPresentacion, IdFabricante, IdCategoria, IdPais, PesoTeorico, IdTara, IdUnidad, PrecioPorKg)`. Reemplaza 11 condiciones `||` por `_tracker.IsDirty(snapshotActual)`.
- **`FabricanteModal`**: `FabricanteSnapshot(Nombre, Descripcion, IdProveedor, IdPais)`. Evita `UpdateAsync` innecesario y desacopla la mutación de estado `CambiarEstadoAsync`.
- **`CategoriaModal`**: `CategoriaSnapshot(Nombre, Descripcion)`.
- **`PresentacionModal`**: `PresentacionSnapshot(Nombre, Descripcion)`.

**Garantía en Tiempo de Compilación:** Al modificar los campos del formulario, la firma del constructor posicional del `record` obliga al desarrollador a actualizar tanto el snapshot en `OnLoaded` como la recolección en `BtnGuardar_Click`, haciendo imposible omitir campos por olvido.

### 3. Ajuste de `EnumToBooleanConverter` (AP-02)
- Se declaró la clase como `public sealed class EnumToBooleanConverter`.
- Se añadió constructor explícito sin parámetros `public EnumToBooleanConverter()` para diseñadores XAML.
- Se implementó la ruta rápida para `[Flags]` convirtiendo a `ulong` y ejecutando `(numericValue & numericParameter) == numericParameter`, eliminando la sobrecarga de `Enum.HasFlag`.
- Se expuso la propiedad canónica `public static EnumToBooleanConverter Instance => Instancia;`.

### 4. Pulido de Interfaz y Grillas de Catálogo
- **Alineación de Fechas:** Se corrigió la alineación de las columnas `CREADO` y `ACTUALIZADO` en las 5 vistas (`ProveedoresView`, `ProductosView`, `FabricantesView`, `CategoriasView`, `PresentacionesView`), utilizando `CellStyle="{StaticResource CeldaIzquierda}"`, `HorizontalAlignment="Left"` y cabeceras uniformes con padding izquierdo `10,0`.
- **`LoadingOverlay` Opaco:** Se corrigió el bug de transparencia en `LoadingOverlay.xaml` estableciendo `Background="White"` e `IsHitTestVisible="True"`, impidiendo que el spinner se solape visualmente con las filas del `DataGrid` durante el filtrado.

### 5. Regla 14 en `AGENTS.md` y Regla de Workspace
- Se formalizó la directiva en `.agents/rules/dirty_tracking_change_tracker.md`.
- Se incorporó la **Regla 14** a las Reglas de Oro en `AGENTS.md`.

## Verificación de Calidad y Tests

- **Pruebas Unitarias de `ChangeTracker`:** Creado `BimboProyecto.Tests/Validacion/ChangeTrackerTests.cs` (5 hechos que prueban snapshots nulos, igualdad de valor, campos numéricos/nulos y simulación de 11 campos de producto).
- **Pruebas de Caja Blanca Anti-Regresión:** Se añadieron aserciones arquitectónicas en `ProveedoresWhiteBoxTests`, `ProductosWhiteBoxTests`, `FabricantesWhiteBoxTests`, `CategoriasWhiteBoxTests` y `PresentacionesWhiteBoxTests` validando que cada modal contenga su `ChangeTracker` y no reintroduzca cadenas manuales de `string.Equals`.
- **Resultado de Tests:** **419/419 superadas (100%)** en `BimboProyecto.Tests.dll`.
- **Compilación de la Solución:** **0 Advertencias, 0 Errores** (.NET 10).

## Relaciones

- [[Auditoría Externa — Optimizaciones WPF de Antigravity vs. Investigaciones QA]]
- [[Dirty Tracking y Orquestación RPC]]
- [[Enlace Enum RadioButton en WPF]]
- [[Optimización De Renderizado En WPF]]
- [[Arquitectura Actual]]
- [[Deuda Técnica - Pendientes]]
