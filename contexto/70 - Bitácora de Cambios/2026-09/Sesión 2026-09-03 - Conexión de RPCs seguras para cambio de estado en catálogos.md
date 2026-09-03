---
title: "Sesión 2026-09-03 — Conexión de RPCs seguras para cambio de estado en catálogos"
tags:
  - sesion
  - catalogos
  - rpc
  - supabase
  - bugfix
  - rbac
date: 2026-09-03
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente)
---

# Sesión 2026-09-03 — Conexión de RPCs seguras para cambio de estado en catálogos

> [!success] Resultado
> Diagnóstico completo y resolución de la falla de activación/desactivación en los catálogos administrativos (**Categorías**, **Productos**, **Proveedores** y **Fabricantes**). Se descartó cualquier relación con el sistema de caché (FusionCache) y se conectaron las RPCs seguras `cambiar_estado_*_seguro` en los repositorios de C# y modales de edición. Compilación limpia (0 errores, 0 advertencias) y 243/243 pruebas unitarias superadas.

---

## Diagnóstico y Causa Raíz

### 1. Descarte de la Hipótesis de Caché
- Se verificó que las grillas de los catálogos cargan directo desde Supabase mediante `GetPagedAsync()` sin intermediación de caché.
- `FusionCache` únicamente aplica en selectores de modales (`CachedCatalogoRepository`) y en sugerencias de autocompletado (`BuscarSugerenciasAsync`).
- El módulo **Presentaciones** cuenta con la misma infraestructura de caché y funcionaba correctamente, comprobando que la caché no era la causa.

### 2. Causa Raíz Real: Desacople en Commit `bb506f0`
- En el commit `bb506f0` (*noti*, 2026-09-02) se migraron las mutaciones a RPCs de seguridad para RBAC y auditoría.
- Las funciones PostgreSQL `actualizar_*_seguro` intencionalmente omitieron las columnas de estado (`estado_categoria`, `id_estado`), delegando esa responsabilidad a funciones especializadas `cambiar_estado_*_seguro`.
- En la capa C#, los repositorios llamaban a `actualizar_*_seguro` omitiendo el estado, y los modales solo llamaban a `UpdateAsync`.
- En consecuencia, al cambiar de "Activo" a "Inactivo" en los modales, la operación retornaba éxito pero el estado nunca se modificaba en la base de datos.
- **Presentaciones** no se había visto afectada porque nunca fue migrada a RPCs en aquel commit y continuaba usando `UPDATE` directo vía Postgrest.

---

## Cambios Aplicados

### 1. Interfaces (`CapaAplicacion4`)
Se agregó la declaración del método de cambio de estado en:
- `ICategoriaRepository`: `Task<Result> CambiarEstadoAsync(int id, bool nuevoEstado, Guid idSolicitud, CancellationToken ct = default);`
- `IProductoRepository`: `Task<Result> CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default);`
- `IProveedorRepository`: `Task<Result> CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default);`
- `IFabricanteRepository`: `Task<Result> CambiarEstadoAsync(int id, int nuevoEstado, Guid idSolicitud, CancellationToken ct = default);`

### 2. Repositorios (`CapaDatos/Repositories/`)
Se implementó `CambiarEstadoAsync` invocando exclusivamente las funciones RPC de PostgreSQL con sus respectivos parámetros y `idSolicitud`:
- `CategoriaCrudRepository` -> `client.Rpc("cambiar_estado_categoria_seguro", ...)`
- `ProductoCrudRepository` -> `client.Rpc("cambiar_estado_producto_seguro", ...)`
- `ProveedorCrudRepository` -> `client.Rpc("cambiar_estado_proveedor_seguro", ...)`
- `FabricanteCrudRepository` -> `client.Rpc("cambiar_estado_fabricante_seguro", ...)`

Asimismo, `DeleteAsync` en cada repositorio fue refactorizado para reutilizar `CambiarEstadoAsync` con estado inactivo.

### 3. Modales de Edición (`CapaUI/.../Pantallas/`)
En los modales `CategoriaModal`, `ProductoModal`, `ProveedorModal` y `FabricanteModal`:
- Al presionar **Guardar** en modo edición (`!_esNuevo`), se verifica si el estado seleccionado difiere del estado original (`dto.Estado != _original.Estado`).
- De haber cambiado, se invoca `CambiarEstadoAsync`, disparando la auditoría y las notificaciones RBAC correspondientes.
- Si solo se modificaron campos informativos (nombre, descripción, etc.), no se llama a la RPC de estado, protegiendo contra peticiones innecesarias y requerimientos indebidos de permisos de cambio de estado.

---

## Estandarización de Fallbacks Contextuales en Tablas

Se eliminaron los guiones genéricos (`—`) en todas las columnas de datos del sistema para reemplazarlos por etiquetas contextuales que indican con precisión la falta de información, en cursiva y color atenuado (`#9CA3AF`), coincidiendo con el encabezado correspondiente:

- **Fabricantes**:
  - `PROVEEDOR` ➔ *Sin proveedor*
  - `PAÍS` ➔ *Sin país*
  - `DESCRIPCIÓN` ➔ *Sin descripción*
- **Proveedores**:
  - `RTN` ➔ *Sin RTN*
  - `TELÉFONO` ➔ *Sin teléfono*
  - `CORREO` ➔ *Sin correo*
  - `DIRECCIÓN` ➔ *Sin dirección*
- **Productos**:
  - `TARA` ➔ *Sin tara*
  - `FABRICANTE` ➔ *Sin fabricante*
  - `PROVEEDOR` ➔ *Sin proveedor*
  - `PAÍS` ➔ *Sin país*
  - `CONTENIDO` ➔ *Sin contenido*
  - `PRESENTACIÓN` ➔ *Sin presentación*
- **Empleados**:
  - `IDENTIDAD / DNI` ➔ *Sin identidad*
  - `TELÉFONO` ➔ *Sin teléfono*
  - `CORREO` ➔ *Sin correo*
- **Contactos**:
  - `TELÉFONO` ➔ *Sin teléfono*
  - `CORREO` ➔ *Sin correo*
- **Usuarios**:
  - `EMAIL` ➔ *Sin correo*
  - `ÚLTIMO ACCESO` ➔ *Sin registros*
- **Bitácora**:
  - `CAMPO AFECTADO` ➔ *Sin campo*
  - `ESTADO ACTUAL` ➔ *Sin detalle*
- **Estilos (`Styles.xaml`)**:
  - El estilo `TextoCeldaConFallback` se actualizó con disparadores para cada una de estas etiquetas, garantizando un renderizado visual idéntico (cursiva gris `#9CA3AF`).

### Alineación y Auto-dimensionamiento en Fabricantes y Productos

- **Alineación a la izquierda**: Se alineó el encabezado y las celdas de la columna `PROVEEDOR` a la izquierda con padding consistente (`10,0`).
- **Auto-dimensionamiento por contenido y cabecera**: Se cambió el ancho fijo de `PROVEEDOR` (`Width="150"`) a `Width="Auto"` con `MinWidth="180"` y `PAÍS` a `Width="Auto"` con `MinWidth="150"` (tanto en Fabricantes como en Productos), asegurando que nombres como "INDUSTRIAS GRAFICAS...", "DISTRIBUIDORA CARIBE..." o "República Dominicana" se desplieguen completos sin cortarse con elipsis (`...`). Se activó además `ScrollViewer.HorizontalScrollBarVisibility="Auto"`.

### Estandarización Arquitectónica de Tablas y Alineación de Columnas (/goal)

- **Centralización en `Styles.xaml`**:
  - `ProductRowStyle` promovido a recurso global en `CapaUI/Resources/Styles.xaml`, eliminando más de 250 líneas de código duplicado en los `.xaml` de vistas individuales (`ProductosView`, `CategoriasView`, `PresentacionesView`, `FabricantesView`, `ProveedoresView`, `ContactosFabricantesView`, `ContactosProveedoresView`).
  - `DataGridColumnHeader` global definido con alineación a la izquierda y padding uniforme `10,0`.
  - `HeaderCentrado` y `HeaderDerecho` centralizados para uso transversal en todo el sistema.
  - Saneamiento de colores estáticos (`#1E3A8A`) en `PresentacionesView.xaml` reemplazados por `{DynamicResource EmpresaPrimaryBrush}`.
- **Alineación consistente a la derecha en el Módulo de Productos**:
  - `PESO TEÓRICO` y `PRECIO / KG` en `ProductosView.xaml` pasaron a alinearse a la derecha (`HeaderDerecho` + `CeldaDerecha` con formato `{0:N2}`).
  - `CREADO` y `ACTUALIZADO` unificados con `HeaderDerecho` + `CeldaDerecha` en todas las tablas del módulo (`Productos`, `Categorías`, `Presentaciones`, `Fabricantes`, `Proveedores`).
  - Columnas de estado unificadas con `HeaderCentrado` + `CeldaCentrada`.

### 3. Estandarización de Responsividad en Buscadores (`SuggestionSearchBox` y `Limpiar Filtros`)

- **Problema detectado**: El convertidor responsivo `AnchoMinimoAVisibilidad` (umbral 760 px en `TarjetaToolbar`) solo estaba implementado en `ProductosView.xaml` y `PresentacionesView.xaml`. En el resto de submódulos, el botón `Limpiar Filtros` tenía un ancho fijo que comprimía la caja del buscador `SuggestionSearchBox` al achicar la ventana.
- **Implementación**:
  - Homologado el contenedor `Border` con `x:Name="TarjetaToolbar"` y `ClipToBounds="True"`.
  - Conectado `AnchoMinimoAVisibilidad` con `ConverterParameter=760` al texto `"Limpiar Filtros"` y fijado `ToolTip="Limpiar filtros"`.
  - Saneado el botón en `PresentacionesView.xaml` reemplazando `#4A6FA8` por `{DynamicResource EmpresaPrimaryDarkBrush}`.
  - Vistas cubiertas: `CategoriasView.xaml`, `FabricantesView.xaml`, `ProveedoresView.xaml`, `EmpleadosView.xaml`, `UsuariosView.xaml`, `BitacoraView.xaml`.

---

## Verificación

1. **Compilación de la Solución:**
   ```bash
   dotnet build BimboProyecto.sln
   ```
   *Resultado:* Compilación correcta. 0 Advertencias, 0 Errores.
2. **Pruebas Unitarias:**
   ```bash
   dotnet test BimboProyecto.sln
   ```
   *Resultado:* 243 de 243 pruebas superadas (100%).

---

## Relaciones

- [[Buscador y Barra de Herramientas Responsive - Proteccion de Ancho con AnchoMinimoAVisibilidad]]
- [[Panel de Filtros Fluido - Barra responsive con prioridad y equilibrado]]
- [[Módulos de Catálogos Administrativos]]
- [[Módulo Productos]]
- [[Sesión 2026-09-02 - Alineación de columnas y fallback universal de campos vacíos]]
- [[Plan de Migración de Mutaciones Directas a RPC]]
- [[Plan de Migración de Presentaciones a RPC segura]]
- [[Deuda Técnica - Pendientes]]
