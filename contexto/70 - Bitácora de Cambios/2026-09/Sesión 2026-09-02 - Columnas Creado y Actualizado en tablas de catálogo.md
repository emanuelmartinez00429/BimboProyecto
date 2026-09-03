---
title: "Sesión 2026-09-02 — Columnas Creado y Actualizado en tablas de catálogo"
tags:
  - sesion
  - ui
  - ux
  - wpf
  - datagrid
  - catalogos
  - auditoria
date: 2026-09-02
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (sesión gestionada por Fernando)
---

# Sesión 2026-09-02 — Columnas Creado y Actualizado en tablas de catálogo

> [!success] Resultado
> Se incorporaron de manera estándar las columnas de auditoría **CREADO** y **ACTUALIZADO** en las tablas de los 5 submódulos de catálogo (**Productos**, **Proveedores**, **Fabricantes**, **Categorías** y **Presentaciones**), ubicadas inmediatamente antes de la columna **ESTADO**, con encabezados acordes a la jerarquía de cada tabla, celdas de fecha alineadas a la derecha (`CeldaDerecha`) en formato `dd/MM/yyyy HH:mm` y dimensionamiento dinámico (`Width="Auto"` con `MinWidth="130"`). Asimismo, se institucionalizó la **Regla 8** de traducción idiomática conceptual a WPF.

---

## Problema / Requerimiento

1. **Falta de visibilidad de trazabilidad temporal:**
   Las tablas de catálogo principales carecían de columnas visibles que permitieran a los operadores conocer la fecha y hora exacta de creación o última modificación de los registros sin tener que inspeccionar la bitácora o la base de datos.
2. **Estandarización de presentación visual:**
   Se requería que ambas columnas precedieran inmediatamente a la columna `ESTADO`, con auto-dimensionamiento al contenido (`Width="Auto"` con `MinWidth="130"`), formateo de fecha con hora local de Honduras (`ToLocalTime()`) y alineación a la derecha para los valores de fecha, preservando la alineación uniforme de los encabezados de columna.
3. **Lección aprendida sobre traducción conceptual de requerimientos:**
   Al solicitar el usuario *"ponelo displayed cells"*, la transcripción literal `SizeToDisplayedCells` en XAML causó una excepción de parser en tiempo de ejecución (`XamlParseException`), ya que dicho término corresponde a Windows Forms (`DataGridViewAutoSizeColumnMode.DisplayedCells`) y no a WPF. Esto motivó la creación de la **Regla 8** en las reglas de oro de `AGENTS.md`.

---

## Cambios Implementados

### 1. Capa de Presentación (`CapaUI`)

- **Estilos globales ([`Styles.xaml`](file:///d:/Proyectos/Proyecto%20de%20BIMBO/BimboProyecto/CapaUI/Resources/Styles.xaml)):**
  - Creados los estilos reutilizables `HeaderDerecho` y `CeldaDerecha` (`DataGridCell` con `HorizontalAlignment="Right"`).
- **Vistas XAML:**
  - Se añadieron las columnas `CREADO` y `ACTUALIZADO` justo antes de `ESTADO` con `Width="Auto"`, `MinWidth="130"`, `CellStyle="{StaticResource CeldaDerecha}"`, `StringFormat={}{0:dd/MM/yyyy HH:mm}` y fallback `TargetNullValue='—'` en:
    - `CapaUI/Formularios/Principal/Pantallas/Productos/ProductosView.xaml`
    - `CapaUI/Formularios/Principal/Pantallas/Proveedores/ProveedoresView.xaml`
    - `CapaUI/Formularios/Principal/Pantallas/Fabricantes/FabricantesView.xaml`
    - `CapaUI/Formularios/Principal/Pantallas/Categorias/CategoriasView.xaml`
    - `CapaUI/Formularios/Principal/Pantallas/Presentaciones/PresentacionesView.xaml` (reemplazando la antigua columna solitaria y fija de `ACTUALIZADO`).

### 2. Capa de Aplicación (`CapaAplicacion4`)

- Incorporadas las propiedades `DateTime? CreatedAt` y `DateTime? UpdatedAt` en:
  - `ProveedorDto.cs`
  - `FabricanteDto.cs`
  - `CategoriaDto.cs`
  *(Nota: `ProductoDto` y `PresentacionDto` ya las contenían).*

### 3. Capa de Datos (`CapaDatos`)

- **Modelos PostgREST:**
  - Mapeadas las columnas `[Column("created_at", ignoreOnInsert: true, ignoreOnUpdate: true)]` y `[Column("updated_at", ignoreOnInsert: true, ignoreOnUpdate: true)]` en:
    - `Modelados/Pesajes/Proveedores.cs`
    - `Modelados/Fabricantes/FabricanteCrud.cs`
    - `Modelados/Productos/Categoria.cs`
- **Repositorios CRUD:**
  - En los métodos `Map(...)` de los 5 repositorios (`ProveedorCrudRepository`, `FabricanteCrudRepository`, `CategoriaCrudRepository`, `ProductoCrudRepository`, `PresentacionCrudRepository`) se asignaron `CreatedAt` y `UpdatedAt` aplicando `.ToLocalTime()` para proyectar desde UTC a la hora oficial de Honduras.

### 4. Gobernanza y Reglas de Agente (`AGENTS.md`)

- **Regla 8 agregada a las Reglas de Oro:**
  > *"Traducción conceptual a la tecnología activa (WPF / .NET 8 / XAML): Si el usuario describe un requisito usando conceptos genéricos o de otros entornos (ej. DisplayedCells de WinForms, flex/div de CSS), nunca trasladar el término de forma literal. Traducir siempre al equivalente nativo e idiomático de WPF."*
- **Nueva regla de personalización:**
  - Creado `.agents/rules/wpf_idiomatic_translation.md` con lineamientos de mapeo técnico y validación contra esquemas XAML.

---

## Verificación

- **Compilación de la solución:** `dotnet build BimboProyecto.sln` finalizó con **0 errores y 0 advertencias**.
- **Pruebas unitarias:** Ejecutadas con éxito las **223 pruebas unitarias** de `BimboProyecto.Tests`.
- **Validación visual y funcional:** Aprobada por Fernando en la interfaz en ejecución.

---

## Relaciones

- [[Conocimiento Principal]]
- [[Módulo Productos]]
- [[Módulos de Catálogos Administrativos]]
- [[Columnas de Auditoria Temporal en DataGrid - Estandarizacion Creado y Actualizado]]
- [[Empty State en DataGrid - Overlay centrado con encabezados visibles]]
