---
title: "Sesión 2026-09-06 — Cierre Cuatro Entregables: P-034, P-037, P-039, P-042 y P-047"
tags:
  - bitacora
  - paginacion
  - busqueda
  - estilos
  - cache
  - realtime
date: 2026-09-06
---

# Sesión 2026-09-06 — Cierre Cuatro Entregables: P-034, P-037, P-039, P-042 y P-047

## 1. Contexto y Objetivos
Ejecución en profundidad de los 4 entregables viables de deuda técnica seleccionados:
1. **P-037:** Reemplazo del code-behind duplicado de paginación por un `UserControl` compartido desacoplado (`PaginadorControl.xaml`) con clamping de rango.
2. **P-042 / P-047:** Estandarización de estilos de entrada en `Styles.xaml`, promoviendo `CeldaInput` a nivel global y agregando trigger de error a `InputBox`.
3. **P-039:** Extensión de búsqueda insensible a mayúsculas y tildes a las 7 tablas de catálogo restantes mediante columnas generadas `STORED` e índices GIN trigramas.
4. **P-034:** Cierre documental y verificación de la invalidación reactiva de caché con suscriptor de vida larga a nivel de aplicación (`InvalidadorCacheRealtime.cs`).

---

## 2. Implementaciones Realizadas

### 2.1. P-037: Control de Paginación Compartido (`PaginadorControl`)
- Se creó `CapaUI/Core/Controls/PaginadorControl.xaml` y `.xaml.cs`:
  - Expone DependencyProperties: `Page` (con coerce callback para fijar `1 <= Page <= Math.Max(1, TotalPages)`), `TotalPages`, `PageInfo`, e `IsLoading`.
  - Reutiliza la lógica de elipsis y cálculo de páginas de `CapaUI/Core/Controls/Paginacion.cs`.
  - Bloquea clics durante cargas activas.
- Migración de las 10 vistas de catálogo:
  1. `ProductosView`
  2. `CategoriasView`
  3. `FabricantesView`
  4. `ProveedoresView`
  5. `UsuariosView`
  6. `EmpleadosView`
  7. `BitacoraView`
  8. `PresentacionesView`
  9. `ContactosFabricantesView`
  10. `ContactosProveedoresView`
- En `UsuariosView`, `EmpleadosView` y `BitacoraView`, se desacopló `ItemsSource = _vm.PageRows` de la paginación, solucionando la fragilidad histórica #719.
- Se incorporó `BimboProyecto.Tests/Paginacion/PaginacionTests.cs` con 5 pruebas xUnit.

### 2.2. P-042 / P-047: Estandarización de Estilos de Entrada
- En `CapaUI/Resources/Styles.xaml`:
  - `InputBox`: agregado trigger para `validacion:Validacion.TieneError` (borde `#EF4444`, grosor 2).
  - Promovido el estilo `CeldaInput` (alto 34px, padding `10,0`, borde foco y error) como estilo global de la aplicación.
  - Promovidos `PageBtn` y `ActivePageBtn` a nivel de aplicación en `Styles.xaml`.
- En `RegistroCamionesModal.xaml`: eliminada la definición local redundante de `CeldaInput`.

### 2.3. P-039: Búsqueda insensible a tildes (7 tablas en Supabase + Repositorios C#)
- Migración SQL `supabase/migrations/20260906060000_busqueda_insensible_tildes_todas_tablas.sql`:
  - Función `public.sin_tildes(text)` con `unaccent`.
  - Columnas generadas `STORED` e índices GIN de trigramas (`gin_trgm_ops`):
    * `fabricante.busqueda_fabricante`
    * `proveedores.busqueda_proveedor`
    * `categoria.busqueda_categoria`
    * `presentacion_producto.busqueda_presentacion`
    * `empleados.busqueda_empleado`
    * `usuarios.busqueda_usuario`
    * `bitacora.busqueda_bitacora`
  - Recreación de `vista_usuarios_busqueda` con columna `busqueda_usuario`.
- Repositorios C# actualizados para usar `busqueda_<tabla>` con `TextoBusqueda.Normalizar(query)`:
  * `FabricanteCrudRepository.cs`
  * `ProveedorCrudRepository.cs`
  * `CategoriaCrudRepository.cs`
  * `PresentacionCrudRepository.cs`
  * `EmpleadoCrudRepository.cs`
  * `EmpleadoRepository.cs` (Search)
  * `UsuarioRepository.cs` (y modelo `usuarioVista.cs`)
  * `BitacoraCrudRepository.cs`
- Pruebas unitarias ampliadas en `BimboProyecto.Tests/Busqueda/TextoBusquedaTests.cs`.

### 2.4. P-034: Invalidación proactiva de caché local ante eventos Realtime
- Verificación de `InvalidadorCacheRealtime.cs` (Singleton), conectado a `MainWindow.xaml.cs`:
  - `Suscribir()` en `OnLoaded` (10 tablas de catálogo).
  - Purga de memoria síncrona en eventos (`_cache.InvalidarEtiqueta`).
  - Purga masiva de catálogos en reconexión de red (`_conexion.Reconectado`).
  - `Desuscribir()` en `MainWindow.LimpiarRecursosAsync`.
- Creación de suite unitaria: `BimboProyecto.Tests/Cache/InvalidadorCacheRealtimeTests.cs` (4 pruebas unitarias).

---

## 3. Verificación
- **Compilación de la solución:** `dotnet build BimboProyecto.sln --no-incremental` → **0 advertencias, 0 errores**.
- **Suite de pruebas:** `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj` → **286 superadas, 0 con error, 0 omitidas (100%)**.
- **Base de Datos Remota (Supabase):** Verificada presencia de 8 columnas `busqueda_*` y 8 índices GIN activos.
