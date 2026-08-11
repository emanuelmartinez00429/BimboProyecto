---
title: Deuda Técnica — Pendientes
tags:
  - pendiente
  - deuda-tecnica
  - auditoria
date: 2026-05-28
---

# Deuda Técnica — Pendientes

> [!info] Origen
> Encontrados en auditoría del 2026-05-28 antes de replicar el módulo Productos como plantilla.
> Ver [[Sesión 2026-05-28 - Fix Búsqueda Multi-Campo Productos]].

> [!todo] Pendiente de diseño (no es deuda de código)
> [[Pendiente - Servicio Genérico de Validaciones y Pruebas Caja Negra]] — servicio aún no implementado, registrado 2026-06-10.

---

## 🔴 Críticos — resolver ANTES de replicar el módulo

Estos errores se propagarán en cascada a cada módulo nuevo si no se corrigen primero.

---

### ~~P-013 · Regresión de auditoría: `IdUsuario ?? 0` en Pesaje pierde el "fail-loud"~~ ✅ Resuelto 2026-07-26

**Archivo:** `CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeViewModel.cs`
**Introducido en:** commit `f105047` (Emanuel, 2026-07-23), refactor de sesión. Ver [[Sesión 2026-07-23 - Revisión QA Módulo Usuarios y Refactor de Sesión (Emanuel)]].

```csharp
// ANTES (fallaba explícito si no había sesión):
private static int UsuarioActual => CapaDominio.SesionActual.IdUsuario; // throw si null
// AHORA (silencioso):
private int UsuarioActual => _sesionService.SesionActual?.IdUsuario ?? 0;
```

El `SesionActual.IdUsuario` borrado lanzaba `InvalidOperationException` a propósito si no había sesión — su comentario lo decía: *"Falla explícitamente… para evitar auditoría falsa (antes tenía default = 1)"*. El reemplazo retorna `0` en silencio, así que un pesaje podría **persistirse con `id_usuario = 0`** (usuario inexistente) en lugar de fallar visiblemente.

**Riesgo:** Registros de auditoría/movimientos atribuidos a un usuario fantasma. Silencioso — no da error.

**Solución aplicada:** `UsuarioActual` lanza `InvalidOperationException` si no hay sesión (garantía de última defensa) + guarda `HaySesionActiva()` en los 2 puntos de uso que loguea (Serilog) y muestra Toast "Sesión expirada" — mensaje genérico al usuario, detalle al log. Ver [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]].

**Estado:** `[x] Resuelto` — `dotnet build` 0 errores.

---

### ~~P-001 · Filtros duplicados 3 veces en `ProductoCrudRepository`~~ ✅ Resuelto 2026-05-28

**Archivo:** `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs`
**Líneas afectadas:** `GetPagedInternal`, `BuscarSugerenciasInternal`, `GetPaginaDeProductoInternal`

El bloque de aplicación de filtros es idéntico en los tres métodos:
```csharp
if (filtros.IdEstado.HasValue)
    query = query.Filter("id_estado", Op.Equals, filtros.IdEstado.Value.ToString());
if (filtros.IdFabricante.HasValue)
    query = query.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
if (filtros.IdPais.HasValue)
    query = query.Filter("id_pais", Op.Equals, filtros.IdPais.Value.ToString());
```

**Riesgo:** Agregar un filtro nuevo obliga a cambiarlo en 3 lugares. Fácil olvidar uno → resultados inconsistentes según qué operación se use.

**Solución:**
```csharp
private static ISupabaseTable<Productos, RealtimeChannel> AplicarFiltros(
    ISupabaseTable<Productos, RealtimeChannel> query, ProductoFiltros filtros)
{
    if (filtros.IdEstado.HasValue)
        query = query.Filter("id_estado", Op.Equals, filtros.IdEstado.Value.ToString());
    if (filtros.IdFabricante.HasValue)
        query = query.Filter("id_fabricante", Op.Equals, filtros.IdFabricante.Value.ToString());
    if (filtros.IdPais.HasValue)
        query = query.Filter("id_pais", Op.Equals, filtros.IdPais.Value.ToString());
    return query;
}
```

**Estado:** `[ ] Pendiente`

---

### ~~P-002 · Magic numbers para estados sin constante ni enum~~ ✅ Resuelto 2026-05-28

**Archivos:** `ProductoCrudRepository.cs`, `ProductosViewModel.cs`

Los valores `1` (activo) y `2` (inactivo) para `id_estado` aparecen dispersos sin ninguna constante central:
```csharp
// En ViewModel
EstadoFilter.Habilitados    => 1,
EstadoFilter.Deshabilitados => 2,

// En Repositorio (soft-delete)
.Set(p => p.idEstado, 2)

// En GetConteosAsync
.Filter("id_estado", Op.Equals, "1")
```

**Riesgo:** Si otro módulo usa una convención diferente, o si el esquema cambia, los bugs no dan error de compilación — fallan en runtime silenciosamente.

**Solución:** Crear constantes en `CapaAplicacion`:
```csharp
// CapaAplicacion4/Common/EstadoRegistro.cs
public static class EstadoRegistro
{
    public const int Activo   = 1;
    public const int Inactivo = 2;
}
```

**Estado:** `[ ] Pendiente`

---

### ~~P-003 · `_filteredCount` calculado en dos lugares del ViewModel~~ ✅ Resuelto 2026-05-28

**Archivo:** `CapaUI/.../Productos/ProductosViewModel.cs`
**Métodos:** `CargarPaginaAsync` y `CargarPaginaSilenciosamenteAsync`

El mismo switch está duplicado:
```csharp
_filteredCount = filtros.IdEstado switch {
    1 => pagina.Activos,
    2 => pagina.Inactivos,
    _ => pagina.Total
};
```

**Riesgo:** Si se cambia la lógica en uno y no en el otro, los conteos de la UI mostrarán números distintos según si la actualización vino del usuario o de un evento Realtime.

**Solución:** Extraer a método privado:
```csharp
private int ResolverFilteredCount(PagedResult<ProductoDto> pagina, ProductoFiltros filtros) =>
    filtros.IdEstado switch
    {
        EstadoRegistro.Activo   => pagina.Activos,
        EstadoRegistro.Inactivo => pagina.Inactivos,
        _                       => pagina.Total
    };
```

**Estado:** `[ ] Pendiente`

---

### ~~P-009 · `BrandBlock` dejó de sincronizar su ancho con `Sidebar` (regresión)~~ ✅ Resuelto 2026-07-23

**Archivo:** `CapaUI/Formularios/Principal/MainWindow.xaml.cs` — `CollapseSidebar()` / `ExpandSidebar()`
**Introducido en:** commit `2f5489d` (2026-07-18, branch `feat/fase6-IntegracionWpf/MenuPrincipal`), sin documentar. Ver [[Sesión 2026-07-23 - Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock]].

`BrandBlock` (`MainWindow.xaml:110`, `Width="226"` fijo) trae el comentario explícito *"mismo ancho que sidebar, anima junto"*. Antes del commit `2f5489d` ambas funciones llamaban `AnimateWidth(BrandBlock, …, 160)` junto con `AnimateWidth(Sidebar, …, 160)`. Esa llamada fue eliminada al introducir el manejo de `ContentAreaBorder` y no quedó ningún binding/trigger que la reemplace.

**Riesgo:** Al colapsar el sidebar (72px), el bloque de marca en la top bar se queda fijo en 226px — desalineación visual entre la barra superior y el sidebar. Es una regresión de funcionalidad, no solo un tema de estilo de código.

**Solución aplicada:** nuevo helper de instancia `AnimateSidebarWidth(double to)` que anima `Sidebar` y `BrandBlock` juntos desde un único call site (`CollapseSidebar()`/`ExpandSidebar()` ya no llaman `AnimateWidth` por separado para cada uno) — imposible que vuelvan a desincronizarse por accidente.

**Estado:** `[x] Resuelto` — `dotnet build` 0 errores.

---

## 🟡 Importantes — no bloquean pero generan deuda en cascada

---

### ~~P-004 · `PropertyChanged` handler con 30+ condiciones en el code-behind~~ ✅ Resuelto 2026-05-28

**Archivo:** `CapaUI/.../Productos/ProductosView.xaml.cs`
**Líneas:** ~62–96

```csharp
_vm.PropertyChanged += (s, ev) => {
    if (ev.PropertyName == nameof(ProductosViewModel.PageRows))    RefrescarPaginacion();
    if (ev.PropertyName == nameof(ProductosViewModel.IsLoading))   ActualizarSpinner();
    // ... 9+ condiciones más
};
```

**Riesgo:** Se copiará completo a cada módulo nuevo. Es propenso a typos silenciosos (el PropertyName como string no compila si cambia el nombre de la propiedad). Cada cambio de cualquier propiedad evalúa todas las condiciones.

**Solución ideal:** Mover a bindings declarativos en XAML con Converters o Behaviors donde sea posible.

**Estado:** `[ ] Pendiente`

---

### ~~P-005 · `VisualTreeHelper` para highlight de sugerencias~~ ✅ Resuelto 2026-05-28

**Archivo:** `CapaUI/.../Productos/ProductosView.xaml.cs`
**Líneas:** ~314–326

Navega el árbol visual manualmente para pintar el ítem seleccionado en el popup de sugerencias. Si el template XAML cambia aunque sea un `Border` de más, falla en runtime sin excepción clara.

**Riesgo al replicar:** Cada módulo con buscador copiará esta lógica frágil y acoplada al XAML específico de Productos.

**Solución:** Encapsular en un `Behavior` o usar `ListBox` con `SelectedItem` binding en lugar de `ItemsControl` manual.

**Estado:** `[ ] Pendiente`

---

### P-006 · Lógica Realtime acoplada a paginación en el ViewModel

**Archivo:** `CapaUI/.../Productos/ProductosViewModel.cs`
**Método:** `OnCambioProducto` (~120 líneas)

Contiene lógica muy específica de "¿estoy en la última página?", "¿el INSERT puede caer en esta página?". Es correcta para Productos, pero si se copia a un módulo sin paginación o con distinta lógica de páginas, producirá bugs sutiles.

**Estado:** `[ ] Aceptar como deuda — documentar en cada módulo nuevo los puntos que deben adaptarse`

---

### ~~P-010 · Elementos del sidebar repetidos manualmente en 4 bloques paralelos~~ ✅ Resuelto 2026-07-23

**Archivo:** `CapaUI/Formularios/Principal/MainWindow.xaml.cs` — `CollapseSidebar()` / `ExpandSidebar()`

Los ~12 elementos visuales del sidebar (`LblModuloUsuarios`, `ChevUsuarios`, `LblModuloProductos`, … `LogoContainer`, `UserCardButton`) se listan a mano en al menos 4 bloques distintos: fade-out al colapsar, `Visibility.Collapsed` + restore opacity al colapsar, hacerlos `Visible` con opacidad 0 al expandir, fade-in al expandir. `BrandBlock` vivía en una quinta lista (`AnimateWidth`) que se perdió sin que nada avisara — causa raíz de **P-009**.

**Riesgo:** Agregar/quitar un elemento del sidebar (o simplemente refactorizar) obliga a tocar 4–5 listas idénticas. Es fácil que uno quede huérfano, como ya ocurrió.

**Solución aplicada:** campo `private readonly UIElement[] _sidebarChromeElements`, poblado una vez en el constructor tras `InitializeComponent()`. Los 4 bloques de 12 líneas cada uno se reemplazaron por `foreach (var el in _sidebarChromeElements) ...` en `CollapseSidebar()`/`ExpandSidebar()`. `BrandBlock` sigue fuera de este array a propósito (se anima `Width`, no `Opacity`/`Visibility` — ver P-009).

**Estado:** `[x] Resuelto` — `dotnet build` 0 errores.

---

### ~~P-011 · Guard `_animating` no cubre `BtnModulo_Click`~~ ✅ Resuelto 2026-07-23

**Archivo:** `CapaUI/Formularios/Principal/MainWindow.xaml.cs`

`BtnHamburger_Click` verifica `if (_animating) return;` antes de animar. `BtnModulo_Click` también dispara `await ExpandSidebar()` cuando el sidebar está colapsado, pero no verifica `_animating` primero — un click durante la cola de `CollapseSidebar` puede solapar dos animaciones sobre las mismas propiedades (`Sidebar.Width`, `ContentAreaBorder.Opacity`).

**Riesgo:** Animaciones encimadas → estado visual inconsistente, similar al bug que motivó la sesión 2026-05-26 originalmente.

**Solución aplicada:** agregado `if (_animating) return;` como primera línea de `BtnModulo_Click`, mismo patrón que `BtnHamburger_Click`.

**Estado:** `[x] Resuelto` — `dotnet build` 0 errores.

---

### ~~P-012 · Duraciones de animación como magic numbers sin constantes nombradas~~ ✅ Resuelto 2026-07-23

**Archivo:** `CapaUI/Formularios/Principal/MainWindow.xaml.cs`

Los valores `50, 55, 60, 65, 70, 75, 80, 100, 160, 180, 220` ms aparecen como literales dispersos en `CollapseSidebar`/`ExpandSidebar`, sin relación explícita con la tabla de referencia en [[Animaciones WPF - Referencia de Easings]]. (El `180` que rota el chevrón en `AnimateChevron(entry.Chevron, 180)` es un ángulo en grados, no una duración — no forma parte de este ítem aunque coincida numéricamente con `ChevronRotateMs`.)

**Riesgo:** Difícil mantener consistencia entre lo documentado y lo implementado (ya pasó: el timeline documentado en 2026-05-26 quedó desactualizado sin que nada lo señalara). Ajustar un timing implica buscar el número mágico correcto entre varios iguales.

**Solución aplicada:** 15 constantes nuevas (`ContentFadeOutMs`, `ChromeFadeOutMs`, `SidebarWidthAnimMs`, `SubMenuOpenMs`, `ChevronRotateMs`, etc.) agrupadas junto a `SidebarExpanded`/`SidebarCollapsed`/`SubItemHeight`. Todos los `Task.Delay`/`AnimateOpacity`/`AnimateWidth`/`AnimateSubMenu`/`AnimateChevron` del archivo ahora referencian una constante en vez de un literal.

**Estado:** `[x] Resuelto` — `dotnet build` 0 errores.

---

### ~~P-014 · `Debug.WriteLine` logueando prefijos de access token (Usuarios)~~ ✅ Resuelto 2026-07-26

**Archivo:** `CapaDatos/Repositories/Usuarios/UsuarioRepository.cs` — `CrearAsync`
**Introducido en:** commit `f105047` (Emanuel). Ver [[Sesión 2026-07-23 - Revisión QA Módulo Usuarios y Refactor de Sesión (Emanuel)]].

Tres `Debug.WriteLine` loguean prefijos del access token (`token={session?.AccessToken?[..20]}...`) y emails de sesión antes/después del SignUp y la RPC. Aunque sea solo en Debug y truncado, loguear cualquier parte de un token es un smell de seguridad.

**Riesgo:** Fuga parcial de tokens en logs/Output. Bajo (solo Debug), pero debe limpiarse antes de producción.

**Solución aplicada:** eliminados los 3 `Debug.WriteLine` con token; queda un solo `Serilog.Log.Debug` booleano ("sesión restaurada: sí/no"). Regla permanente agregada al [[Plan de Seguridad - Roadmap 10-10]] §2.1b: nunca loguear secretos, ni truncados.

**Estado:** `[x] Resuelto`

---

### ~~P-015 · Debug scaffolding "[MapToDto] Diagnóstico FK" corre por cada fila~~ ✅ Resuelto 2026-07-26

**Archivo:** `CapaDatos/Repositories/Usuarios/UsuarioRepository.cs` — `MapToDto`

`MapToDto` ejecuta un `Debug.WriteLine` de diagnóstico de FKs (`empleados`/`roles` null u OK) **por cada usuario mapeado en cada carga de página**. Es andamiaje de depuración dejado en el código.

**Riesgo:** Ruido en Output y trabajo inútil en cada render. Menor pero se replicará si se usa este repo como plantilla.

**Solución aplicada:** eliminado el bloque de diagnóstico completo; las variables locales del mapeo se conservan.

**Estado:** `[x] Resuelto`

---

### ~~P-016 · `Normalizar()` del modal itera bytes UTF-8 como `char` (stripping de acentos incorrecto)~~ ✅ Resuelto 2026-07-26

**Archivo:** `CapaUI/.../Usuarios/UsuarioModal.xaml.cs` — `Normalizar`

Para quitar tildes al auto-generar el email, el código hace `Encoding.UTF8.GetBytes(s.Normalize(FormD))` y luego filtra **bytes** casteados a `char` según su categoría Unicode (`NonSpacingMark`). Tratar bytes UTF-8 individuales como caracteres es incorrecto para multi-byte: las marcas combinantes de FormD son secuencias de 2 bytes, así que el filtrado no es confiable (puede dejar bytes sueltos o corromper caracteres).

**Riesgo:** Emails auto-generados con caracteres raros o acentos sin quitar para nombres como "Muñoz", "Peña", "Hernández".

**Solución aplicada:** iteración sobre `char` de la cadena FormD + filtrado `NonSpacingMark` + recomposición FormC. Verificado: `Muñoz→munoz`, `María→maria`, `Peña→pena`. Patrón documentado en [[NET - Normalizacion Unicode para Quitar Acentos]].

**Estado:** `[x] Resuelto`

---

### ~~P-017 · Dependencia muerta: `IUsuarioSesionService` inyectada y no usada en `UsuarioModal`~~ ✅ Resuelto 2026-07-26

**Archivo:** `CapaUI/.../Usuarios/UsuarioModal.xaml.cs`

El constructor recibe y guarda `IUsuarioSesionService _sesionService` pero nunca lo usa.

**Riesgo:** Ninguno funcional; ensucia el contrato del modal y confunde sobre por qué depende de la sesión.

**Solución aplicada:** eliminados campo, parámetro y asignación; actualizados los 2 llamadores en `UsuariosView.xaml.cs`.

**Estado:** `[x] Resuelto`

---

### ~~P-018 · Acoplamiento frágil: permisos por nombre de enum vs. string de BD~~ ✅ Resuelto 2026-07-26

**Archivo:** `CapaUI/Core/Permisos/SesionPermisos.cs` — `Tiene`

`SesionPermisos.Tiene(permiso)` hace `sesion.TieneAccion(permiso.ToString())`. El sistema depende de que el nombre del miembro del enum `Permiso` (p. ej. `Pesajes_Ver`) coincida **exactamente** con `acciones.nombre_accion` en la BD. Si difieren (typo, renombre en BD, mayúsculas), el permiso devuelve `false` en silencio → el módulo se oculta sin ningún error.

**Riesgo:** Permisos "desaparecen" sin diagnóstico. Difícil de depurar porque no hay excepción.

**Solución aplicada:** contrato documentado en el doc-comment de `Permiso.cs` (no renombrar sin migración) + `SesionPermisos.ValidarContraBD()` invocado tras login: loguea con Serilog los valores del enum que la sesión no reconoce. No bloquea la app — es diagnóstico.

**Estado:** `[x] Resuelto (documentación + validación diagnóstica)`

---

## 🟢 Menores — aceptables por ahora

---

### ~~P-019 · Nombre confuso: propiedad `correoUsuario` mapea a columna `alias_usuario`~~ ✅ Resuelto 2026-07-26

**Archivo:** `CapaDatos/Modelados/Usuarios/Usuarios.cs`

La propiedad C# `correoUsuario` tenía `[Column("alias_usuario")]`. El nombre de la propiedad y el de la columna sugerían conceptos distintos ("correo" vs "alias").

**Solución aplicada:** renombrada la propiedad a `aliasUsuario` en los 2 modelos + 3 servicios que la referencian. El atributo `[Column("alias_usuario")]` no cambió — el esquema de BD quedó intacto.

**Estado:** `[x] Resuelto`

---

### ~~P-020 · Artefactos de tooling de IA commiteados al repo~~ ✅ Resuelto 2026-07-23

**Archivos:** `.atl/.skill-registry.cache.json`, `.atl/skill-registry.md`, `.codegraph/.gitignore` (agregados en `f105047`).

Artefactos generados por herramientas de agentes de IA, no forman parte del código del proyecto.

**Riesgo:** Ruido en el repo, posibles conflictos de merge, tamaño innecesario.

**Solución aplicada:** `.atl/` y `.codegraph/` agregados al `.gitignore` y removidos del índice con `git rm -r --cached` (siguen en disco). Hecho junto con la reestructuración multi-agente. Ver [[Sesión 2026-07-23 - Plan Preparar Bóveda Multi-Agente (AGENTS.md)]].

**Estado:** `[x] Resuelto` (pendiente de commit por el usuario).

---

### ~~P-021 · Búsqueda de Usuarios solo por `alias_usuario`, no por nombre de empleado~~ ✅ Resuelto 2026-07-26

**Archivo:** `CapaDatos/Repositories/Usuarios/UsuarioRepository.cs` — `ObtenerPaginaAsync` / `ObtenerConteosAsync`

La búsqueda filtraba solo `alias_usuario` (correo). El nombre del empleado vive en la tabla joineada `empleados`, y PostgREST no permite un OR que cruce padre + JOIN.

**Solución aplicada:** vista SQL `vista_usuarios_busqueda` (`security_invoker = true`, respeta RLS) que aplana `nombre_completo`; `usuarioVista` apunta a la vista y el OR server-side cubre `alias_usuario` + `nombre_completo` en página y conteos. Primera búsqueda cross-tabla del sistema — ver [[ADR-005 - Vista SQL para Búsquedas Cross-Tabla]].

**Estado:** `[x] Resuelto`

---

### P-007 · `GetConteosAsync` carga IDs completos en lugar de usar `COUNT`

**Archivo:** `CapaDatos/Repositories/Productos/ProductoCrudRepository.cs`

Workaround documentado con comentario en el código: `Count() no aplica filtros correctamente en esta versión del cliente`. Descarga todos los IDs para contar en memoria.

**Aceptable** para tablas < 10,000 filas. Revisar cuando se actualice el SDK de Supabase.

**Estado:** `[ ] Revisar al actualizar Supabase NuGet de v1.1.1`

---

### P-022 · `UsuarioModal` tiene 3 constructores, el original con ComboBox está muerto

**Archivo:** `CapaUI/.../Usuarios/UsuarioModal.xaml.cs`

Tras el refactor del flujo de creación (Sesión 2026-07-26), el `UsuarioModal` tiene 3 constructores:
1. `(repo, rolRepo, usuario)` — edición (activo)
2. `(repo, rolRepo, null)` — creación original con ComboBox de empleados (**muerto**, nadie lo llama)
3. `(repo, rolRepo, idEmpleado, nombre, correo)` — creación desde Empleados (activo)

El constructor #2 y todo el código del `OnLoaded` que carga `ObtenerEmpleadosSinUsuarioAsync()` es código muerto.

**Riesgo:** Confunde a quien lea el código. El ComboBox de empleados ya no se muestra desde ningún lado.

**Estado:** `[ ] Limpiar constructor muerto y código de CmbEmpleado`

---

### P-008 · Mapeo tabla→PK en `RealtimeService` es manual

**Archivo:** `CapaDatos/Realtime/RealtimeService.cs`

Diccionario estático que mapea nombre de tabla a columna PK. Si se agrega una tabla nueva al sistema de Realtime y se olvida actualizar este diccionario, el servicio no podrá extraer el ID del registro cambiado — falla silenciosamente.

**Estado:** `[ ] Agregar validación o comentario de advertencia`

---

### P-023 · 🔴 Catálogo de taras con datos de prueba — afecta el peso que se le paga al proveedor

**Tablas:** `tara`, `productos.peso_teorico`
**Detectado en:** [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]]

La tabla `tara` tiene **una sola fila**: 20 kg, descripción *"tara de 20 kg"*, usada por **503 de 505 productos**. Pero **500 de esos productos tienen `peso_teorico` menor a 10 kg** — el empaque pesaría el doble o más que el producto que contiene. Los 5 restantes (1,000 a 10,000 kg) tienen nombres tipo *"Producto Number 1"*, *"Producto bien five"*.

Además existe una tabla `tarima` aparte con su propio `peso_tarima`, y `entradas_producto` ya tiene `id_tarima` — o sea las tarimas se manejan por separado, lo que refuerza que ese 20 kg no es un empaque real.

**Riesgo:** la tara se resta del peso bruto para calcular el neto, y **el neto es lo que se le paga al proveedor**. Con datos de relleno, todo cálculo de recepción es incorrecto. Además el indicador de bultos teóricos (Fase 8) va a dar números sin sentido hasta que se cargue el catálogo real.

**Solución:** poblar `tara` con los empaques reales y asignar el `id_tara` correcto a cada producto. Verificar también los `peso_teorico` de los 5 productos de prueba.

**Estado:** `[ ] Pendiente — requiere datos de planta`

---

### P-024 · Tara de empaque: plana en el trigger, por bulto en el cálculo de bultos teóricos

**Archivos:** función de BD `calcular_pesos_entrada`, `CapaUI/.../Pesaje/Modelos/PesajeModels.cs` (`PesajeCalc.BultosTeoricos`)
**Detectado en:** [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]]

El trigger de BD resta la tara de empaque **una sola vez por pesada** (plana), sin importar cuántos bultos se pesen. La fórmula nueva de bultos teóricos la trata **por bulto** (cada bulto trae su empaque), que es lo físicamente correcto.

Ambas conviven **a propósito**: cambiar el trigger alteraría el `peso_neto` de los pesajes futuros y dejaría inconsistentes los ya guardados — y ese neto es lo que se le paga al proveedor. El indicador nuevo es solo informativo y no toca el neto.

**Riesgo:** si la tara realmente es por bulto, el sistema viene **sobrestimando el neto** desde siempre (se paga empaque como si fuera producto). Si es plana, la fórmula de bultos teóricos está inflando el peso por bulto y subestimando la cantidad.

**Solución:** resolver junto con P-023 — con las taras reales cargadas se puede determinar cuál interpretación corresponde y alinear ambos lados.

**Estado:** `[ ] Pendiente — bloqueado por P-023`

---

### P-025 · Repositorios de movimientos duplicados y sin uso

**Archivos:** `CapaDatos/Repositorios/productos_movimientos/RepositorioMovimiento.cs`, `RepositorioMovimientoProducto.cs`

Implementación **vieja** del acceso a `movimientos`/`movimiento_productos`: métodos estáticos, sin Result Pattern, sin `RepositorioBase`. Hace lo mismo que `CapaDatos/Repositories/Pesaje/PesajeRepository.cs`, que es el que realmente se usa.

Verificado: **`CapaUI` no los referencia en ningún lado**.

**Riesgo:** confunde a quien busque el repositorio de pesaje (hay dos carpetas parecidas: `Repositorios/` y `Repositories/`). Un agente nuevo podría modificar el archivo equivocado.

**Estado:** `[ ] Eliminar tras confirmar que nada más los usa`

---

### P-026 · Puente ViewModel → SuggestionSearchBox duplicado 9 veces

**Archivos:** los 9 pares `*View.xaml.cs` / `*ViewModel.cs` de `CapaUI/Formularios/Principal/Pantallas/` que usan el control (Productos, Proveedores, Fabricantes, Categorías, ContactosProveedores, ContactosFabricantes, Usuarios, Empleados, Bitácora).
**Detectado en:** [[Sesión 2026-07-28 - Fix Refresco del Popup de Sugerencias (9 módulos)]]

El control compartido resolvió la duplicación del **XAML** y del comportamiento de teclado, pero el puente entre el ViewModel y el control sigue siendo imperativo y copiado íntegro en cada pantalla: los `case` de `OnVmPropertyChanged`, el método `ActualizarSuggestions()` (idéntico salvo el mapeo `DTO → SuggestionItemData`), el `SearchBox_ItemSelected` y el bloque `RefrescarSugerenciasAsync()` / `SeleccionarSugerencia()` del ViewModel.

**Riesgo — ya se materializó dos veces.** Un defecto en el patrón se replica en las 9 y hay que arreglarlo 9 veces:
- 2026-07-26 · Usuarios se construyó con un `TextBox` plano en vez del control.
- 2026-07-28 · las 9 pantallas escuchaban solo `ShowSuggestions`, un `bool` que no notifica cuando no cambia, y el popup quedaba congelado al seguir escribiendo.

Es la misma familia que **P-006** (lógica acoplada al ViewModel que se copia entre módulos).

**Solución aplicada (2026-07-28), en dos pasos:**

1. **Binding directo.** `Suggestions` + `ShowSuggestions` (que nunca se bindeaban en XAML — eran plomería del acoplamiento) se reemplazaron por una sola propiedad `SuggestItems` ya mapeada, bindeada desde el XAML. Desaparecieron los 9 `ActualizarSuggestions()` y los `case` del switch. El mapeo `DTO → SuggestionItemData` se mudó al ViewModel como método `Map` estático.
2. **`SuggestionDebouncer` compartido.** `CapaUI/Core/Controls/SuggestionDebouncer.cs` encapsula el `CancellationTokenSource`, el `Task.Delay(300)`, los guards y el `catch (OperationCanceledException)`. Se usa por **composición** — 5 de los 9 VMs heredan de `RealtimeAwareViewModel` y 4 de `ObservableObject`, así que una clase base común no era opción.

Por módulo queda solo lo que genuinamente varía: qué repositorio llamar y cómo se ve una sugerencia.

**Estado:** `[x] Resuelto 2026-07-28` — ver [[Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)]]

---

### P-027 · Verificación funcional y RLS del RBAC con rol Consulta

**Archivos:** `UsuarioSesionService.cs`, `PermisoCatalogo`, `PermisoBehavior`, tablas `acciones_roles` / `acciones` / `modulos`
**Detectado en:** [[Sesión 2026-08-09 - Implementación RBAC visual y gestión de roles]]

El contrato del cliente ya coincide con las 28 acciones reales de Supabase y la auditoría estática no encuentra nombres inválidos. Antes de la corrección, el log mostraba sesiones con cero permisos porque el cliente comparaba contra la nomenclatura provisional con guiones bajos.

**Verificación realizada (2026-08-09):** el usuario compiló y probó la aplicación con el rol Consulta; el menú y las acciones se cargaron correctamente según sus permisos.

**Estado:** `[x] Resuelto 2026-08-09 — prueba funcional confirmada por el usuario`

---

### P-028 · Verificar en runtime el rediseño de Roles y medir el shimmer

**Archivos:** `CapaUI/Formularios/Principal/Pantallas/Roles/*`, `CapaUI/Core/Controls/SpanningGridPanel.cs`
**Detectado en:** [[Sesión 2026-08-11 - Rediseño de Gestión de Roles y esqueleto con shimmer]]

La pantalla se reescribió por completo pero **no se vio corriendo**: Visual Studio y la app estaban abiertos bloqueando los DLL de salida. Solo se comprobó que compila (0 errores) y que los BAML se generan.

Quedan dos verificaciones:

1. **Maquetado** — que el layout caiga como el mockup, en particular el `SpanningGridPanel` (grilla de 4 columnas en vista compacta / 2 en detalle, con el módulo ancho ocupando 2).
2. **Costo del shimmer** — medir en la PC con iGPU **UHD 630** (la del caso de [[WPF - Rendimiento de Efectos y Niveles de Renderizado]]) con Administrador de tareas → GPU/CPU. El pico debe durar lo que dura el esqueleto y **volver a línea base al aparecer las tarjetas**; si queda elevado, el shimmer no se frenó al ocultarse.

Hasta medirlo, [[WPF - Esqueleto con Shimmer (Skeleton Loading)]] queda en `lifecycle: draft`.

**Estado:** `[ ] Pendiente`

---

## Historial de resolución

| ID | Descripción | Estado | Sesión |
|---|---|---|---|
| P-001 | Filtros duplicados 3x en repositorio | ✅ Resuelto | [[Sesión 2026-05-28 - Refactor Críticos P001-P003]] |
| P-002 | Magic numbers de estados | ✅ Resuelto | [[Sesión 2026-05-28 - Refactor Críticos P001-P003]] |
| P-003 | `_filteredCount` duplicado en VM | ✅ Resuelto | [[Sesión 2026-05-28 - Refactor Críticos P001-P003]] |
| P-004 | PropertyChanged handler masivo | ✅ Resuelto | [[Sesión 2026-05-28 - Refactor P004-P005]] |
| P-005 | VisualTreeHelper frágil | ✅ Resuelto | [[Sesión 2026-05-28 - Refactor P004-P005]] |
| P-006 | Realtime acoplado a paginación | ✅ Documentado | [[Checklist - Replicar Módulo con Realtime]] |
| P-007 | GetConteosAsync workaround SDK | ✅ TODO en código | `ProductoCrudRepository.cs:231` |
| P-008 | Mapeo tabla→PK manual | ✅ Warning en log | `RealtimeService.cs — ExtraerCambio` |
| P-009 | `BrandBlock` no sincroniza ancho con `Sidebar` (regresión) | ✅ Resuelto | [[Sesión 2026-07-23 - Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock]] |
| P-010 | Elementos del sidebar repetidos en 4 bloques paralelos | ✅ Resuelto | [[Sesión 2026-07-23 - Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock]] |
| P-011 | Guard `_animating` no cubre `BtnModulo_Click` | ✅ Resuelto | [[Sesión 2026-07-23 - Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock]] |
| P-012 | Magic numbers de duración de animación | ✅ Resuelto | [[Sesión 2026-07-23 - Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock]] |
| P-013 | Regresión auditoría: `IdUsuario ?? 0` en Pesaje | ✅ Resuelto | [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]] |
| P-014 | Debug logueando prefijos de access token | ✅ Resuelto | [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]] |
| P-015 | Debug scaffolding en `MapToDto` por fila | ✅ Resuelto | [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]] |
| P-016 | `Normalizar()` itera bytes UTF-8 como char | ✅ Resuelto | [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]] |
| P-017 | Dependencia muerta en `UsuarioModal` | ✅ Resuelto | [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]] |
| P-018 | Permisos por nombre de enum vs string de BD | ✅ Resuelto | [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]] |
| P-019 | `correoUsuario` mapea a `alias_usuario` | ✅ Resuelto | [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]] |
| P-020 | Artefactos `.atl`/`.codegraph` commiteados | ✅ Resuelto | [[Sesión 2026-07-23 - Plan Preparar Bóveda Multi-Agente (AGENTS.md)]] |
| P-021 | Búsqueda de Usuarios incompleta (solo alias) | ✅ Resuelto | [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]] |
| P-023 | Catálogo de taras con datos de prueba | `[ ]` Pendiente 🔴 | [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]] |
| P-024 | Tara plana (trigger) vs por bulto (bultos teóricos) | `[ ]` Pendiente | [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]] |
| P-025 | Repositorios de movimientos duplicados sin uso | `[ ]` Pendiente | [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]] |
| P-026 | Puente VM → SuggestionSearchBox duplicado 9× | ✅ Resuelto | [[Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)]] |
| P-027 | Verificación funcional del RBAC con rol Consulta | ✅ Resuelto | [[Sesión 2026-08-09 - Implementación RBAC visual y gestión de roles]] |
| P-028 | Verificar en runtime el rediseño de Roles y medir el shimmer | `[ ]` Pendiente | [[Sesión 2026-08-11 - Rediseño de Gestión de Roles y esqueleto con shimmer]] |

---

## Relaciones

- [[Módulo Productos]] — módulo auditado
- [[Sesión 2026-05-28 - Fix Búsqueda Multi-Campo Productos]] — sesión donde se realizó la auditoría
- [[Paginación y Búsqueda - Arquitectura Detallada]] — arquitectura de referencia
- [[Sesión 2026-07-23 - Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock]] — origen de P-009 a P-012
- [[Sesión 2026-07-23 - Revisión QA Módulo Usuarios y Refactor de Sesión (Emanuel)]] — origen de P-013 a P-021
- [[Sesión 2026-07-28 - Fix Refresco del Popup de Sugerencias (9 módulos)]] — origen de P-026
- [[Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)]] — resolución de P-026
- [[Sesión 2026-08-09 - Implementación RBAC visual y gestión de roles]] — origen de P-027
