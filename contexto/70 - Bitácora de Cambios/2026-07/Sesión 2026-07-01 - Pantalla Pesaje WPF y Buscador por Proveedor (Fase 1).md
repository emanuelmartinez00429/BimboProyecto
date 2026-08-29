---
title: Sesión 2026-07-01 — Pantalla Pesaje (WPF) y Buscador de Productos por Proveedor (Fase 1)
type: sesion
status: Fase 1 completada
tags:
  - bitácora
  - pesaje
  - buscador
  - wpf
  - xaml
  - rendimiento
date: 2026-07-01
updated: 2026-07-01
summary: "Se convirtió un diseño de alta fidelidad (export HTML/React \"BimboPesaje Standalone\") a XAML nativo dentro del shell WPF actual: la pantalla Recepción de Materia…"
scope:
  - CapaAplicacion4/Pesaje/Interfaces
  - CapaDatos/Repositories/Pesaje
  - CapaUI/Formularios/Principal/Pantallas/Pesaje
symbols:
  - BuscarPorProveedorAsync
  - BuscarTodosAsync
  - CamionModal
  - CancellationTokenSource
  - Completed
  - ConstructionVM
  - DataGrid
  - DataTemplate
  - Effect
  - FabricanteConsulta
branch: feat/fase6-IntegracionWpf/MenuPrincipal
---

# Sesión 2026-07-01 — Pantalla Pesaje (WPF) y Buscador por Proveedor (Fase 1)

## Contexto

Se convirtió un diseño de alta fidelidad (export HTML/React "BimboPesaje Standalone") a XAML nativo dentro del shell WPF actual: la pantalla **Recepción de Materia Prima** (3 paneles en cascada: Camiones → Movimiento de Materia Prima → Entradas de pesaje) con 5 modales y toasts. Además se añadió un buscador de productos **acotado al proveedor de la placa** con opción de buscar en todo el catálogo.

Entra por el sidebar ya existente **Pesajes → Movimientos y Entradas** (`Routes.Pesajes = "pesajes-sub"`), que antes caía en `ConstructionVM`.

Plan completo: `.claude/plans/soft-swimming-abelson.md`.

---

## Decisiones clave

- **Producto↔Proveedor por puente (sin cambio de esquema):** `productos.id_fabricante → fabricante.id_proveedor → proveedores`. `productos` NO tiene `id_proveedor`. Verificado en BD: 505 productos (todos con fabricante), 2 fabricantes (ambos con proveedor), 11 proveedores. El camión/placa vive en `movimientos.id_proveedor`.
- **Reuso del buscador sin modificarlo:** se agregaron clases nuevas siguiendo el patrón de extensión de [[Buscador Universal Bimbo]] ("registrar en DI, cero cambios en código existente"). **No** se tocó `ISearchStrategy`, `IRepository<T>`, `UniversalSearchHandler`, `ProductoSearchRepository`, `ProductoCrudRepository` ni `ProductoFiltros`.
- **Alcance Fase 1:** paridad visual + buscador cableado a la BD real. La pantalla opera con **estado en memoria** (semilla); solo el picker consulta la BD.

---

## Backend del buscador (extensión aditiva)

**Nuevos:**
- `CapaAplicacion4/Pesaje/Interfaces/IPickerProductoRepository.cs` — `TopPorProveedorAsync`, `BuscarPorProveedorAsync`, `BuscarTodosAsync`. Reutiliza `ProductoDto` y `Result` (sin DTO nuevo).
- `CapaDatos/Repositories/Pesaje/PickerProductoRepository.cs` — hereda `RepositorioBase`; puente en **2 pasos**: 1) ids de fabricante del proveedor (reusa `FabricanteConsulta`, filtro por columna `id_proveedor` aunque el modelo no la mapee), 2) `productos` filtrado por `id_fabricante IN (...)` + `.Or([nombre ILike, codigo ILike]).Limit(10)`. Evita filtros sobre recursos embebidos de PostgREST (ver [[Bug - Filter OR con Op.Equals en postgrest-csharp]]).

**Modificado:** `CapaDatos/DependencyInjection.cs` — 1 línea: `services.AddTransient<IPickerProductoRepository, PickerProductoRepository>();`

> [!note] Deuda técnica menor
> El método `Map(Productos → ProductoDto)` se **copió** de `ProductoCrudRepository` a propósito, para no tocar el archivo existente. Si se agrega un tercer consumidor, extraer a un mapper compartido.

> [!tip] Optimización futura (opcional)
> El puente en 2 pasos se puede reemplazar por join embebido `.Select("*, fabricante!inner(*)").Filter("fabricante.id_proveedor", Eq, id)`. Se dejó el de 2 pasos por seguridad con el cliente actual (Supabase 1.1.1 / postgrest 3.x).

---

## UI (XAML) — Recepción de Materia Prima

Carpeta `CapaUI/Formularios/Principal/Pantallas/Pesaje/`:
- `PesajeView.xaml(.cs)`, `PesajeViewModel.cs`, `Modelos/PesajeModels.cs` (CamionPesaje / ProductoCamion / EntradaPesaje + `PesajeCalc` + `ProveedorItem`).
- `Modales/`: `CamionModal`, `ProductoCamionModal`, `SeleccionarProductoModal` (el buscador), `PesajeModal`, `ReporteModal`, y `PesajeModalStyles.xaml` (ModalShell gradiente + inputs/botones compartidos, idéntico al de `ProductoModal.xaml`).

**Navegación cableada:** `MainViewModel` (`Routes.Pesajes` → `PesajesVM`), marcador `PesajesVM`, `DataTemplate` en `MainWindow.xaml`, `AddTransient<PesajeViewModel>()` en `App.xaml.cs`.

**El buscador (SeleccionarProductoModal):** al abrir muestra top-10 del proveedor de la placa; caja con **debounce 300 ms** (`CancellationTokenSource`) busca acotado; botón **"Buscar en todos los proveedores"** alterna a global. Marca duplicados (ya agregados) como deshabilitados. `CamionModal` puebla el combo de proveedor con datos reales vía `IProveedorRepository` (reuso, solo lectura), así la placa lleva `id_proveedor` real.

---

## Rendimiento aplicado

Según [[WPF - Rendimiento de Efectos y Niveles de Renderizado]] y la sesión [[Sesión 2026-06-25 - Optimización de Rendimiento en Modales (DropShadowEffect)]]:
- Raíz de cada control: `UseLayoutRounding` + `TextFormattingMode=Display` + `TextRenderingMode=Grayscale`.
- Sombras estáticas `BlurRadius ≤ 24`, sin sombras por input, sin animar `Effect`.
- `DataGrid` virtualizados (`IsVirtualizing` + `Recycling`).
- Animaciones **transform-based**: fade backdrop (Opacity 140 ms), pop modal (`ScaleTransform` 0.94→1, 160 ms, CubicEase), toast (`TranslateTransform`), barra de progreso (`ScaleTransform.ScaleX`, sin layout).
- Fix de secuencia: guard `_modalGen` para que el `Completed` del fade de cierre no oculte un modal recién abierto (caso "Cerrar camión" → Reporte).

---

## Verificación

- `dotnet build CapaUI.csproj` → **0 errores** (41 warnings nullable preexistentes).
- Arranque de la app sin crash (contenedor DI con registros nuevos OK).
- **Caja blanca del buscador contra BD real:** proveedor CISA → 505 productos, top-10 por nombre; proveedor sin fabricantes → vacío sin excepción; global sobre las 505. Validado con SQL.
- **Pendiente:** verificación visual con login (navegar a Pesajes → Movimientos y Entradas y recorrer picker/modales).

---

## Pendiente (Fase 2)

- Persistir en `movimientos` / `movimiento_productos` / `entradas_producto` (CRUD + pesaje + cierre/reporte) reutilizando el patrón de [[Paginación y Búsqueda - Arquitectura Detallada]] y realtime.
- `ProductoDto` no expone tara → la tara individual de productos elegidos por el picker es 0 en Fase 1 (se usa tara extra). Evaluar exponer `peso_tara`/`id_tara` si se requiere.

---

## Relaciones

- [[Buscador Universal Bimbo]] — patrón de extensión reutilizado
- [[Paginación y Búsqueda - Arquitectura Detallada]] — patrón de debounce/repos para Fase 2
- [[WPF - Rendimiento de Efectos y Niveles de Renderizado]] — reglas de optimización aplicadas
- [[Módulo Productos]] — patrón MVVM/modal-overlay replicado
