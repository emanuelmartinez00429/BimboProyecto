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

### ~~P-008~~ · ✅ Mapeo tabla→PK en `RealtimeService` es manual — resuelto

**Archivo:** `CapaDatos/Realtime/RealtimeService.cs`

Diccionario estático que mapea nombre de tabla a columna PK. Si se agrega una tabla nueva al sistema de Realtime y se olvida actualizar este diccionario, el servicio no podrá extraer el ID del registro cambiado — fallaba silenciosamente.

El diccionario sigue siendo manual a propósito (no hay forma de derivar la PK desde el payload de Realtime sin consultarla), pero ya no falla en silencio: `ExtraerCambio` emite, con el propio número de este ítem en el mensaje:
```csharp
Serilog.Log.Warning(
    "Realtime P-008: tabla '{Tabla}' no tiene PK mapeada en _pkColumns.", tabla);
```

**Verificado 2026-09-06** contra el código actual, al auditar trabajo de otra sesión — la advertencia estaba puesta pero el ítem seguía marcado pendiente.

**Estado:** `[x]` Resuelto — advertencia en log presente y verificada.

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

> **Actualización 2026-08-13** — el rediseño de pesajes ([[Sesión 2026-08-13 - Pesaje solo bruto y tara extra pesada]]) **no cerró este ítem, le cambió la forma**. La fórmula de bultos dejó de incluir la tara extra prorrateada, pero sigue usando la tara de empaque **por bulto** en el divisor mientras el trigger la resta **plana** una vez por pesada. Además ahora el indicador es más visible (columna «BULTOS (EST.)» y campo en el modal), así que el error de P-023 se nota más.

**Estado:** `[ ] Pendiente — bloqueado por P-023`

---

### ~~P-025~~ · Repositorios de movimientos duplicados y sin uso — resuelto 2026-09-06

**Archivos (eliminados):** los 10 archivos de `CapaDatos/Repositorios/` (`RepositorioPais`, `RepositorioEmpleado`, y los 8 de `productos_movimientos/`: `RepositorioCategoria`, `RepositorioEntrada`, `RepositorioMovimiento`, `RepositorioMovimientoProducto`, `RepositorioProducto`, `RepositorioProveedor`, `RepositorioTara`, `RepositorioTarima`).

Implementación **vieja** del acceso a `movimientos`/`movimiento_productos`/etc: métodos estáticos, sin Result Pattern, sin `RepositorioBase`. Hacía lo mismo que sus equivalentes en `CapaDatos/Repositories/` (con "s"), que son los que realmente se usan.

**Verificado 2026-09-06:** `grep -rl` de cada uno de los 10 nombres de clase contra todo el repo (fuera de `obj/`/`bin/`) — cero referencias. Build limpio tras la eliminación.

**Estado:** `[x] Resuelto 2026-09-06` — ver [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]]

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
**Detectado en:** [[Sesión 2026-08-11 - Rediseño de Gestión de Roles]]

La pantalla se reescribió por completo pero **no se vio corriendo**: Visual Studio y la app estaban abiertos bloqueando los DLL de salida. Solo se comprobó que compila (0 errores) y que los BAML se generan.

Quedan dos verificaciones:

1. **Maquetado** — que el layout caiga como el mockup, en particular el `SpanningGridPanel` (grilla de 4 columnas en vista compacta / 2 en detalle, con el módulo ancho ocupando 2). Incluye probar el corte de responsividad: angostar la ventana hasta que aparezca la barra horizontal y confirmar que el texto de las tarjetas **no** se recorta ni antes ni después del corte (`MinColumnWidth="220"`; si 220 queda corto o sobra, es el número a ajustar).
2. **Indicador de carga** — que el spinner con leyenda "Cargando roles…" aparezca y desaparezca bien. El esqueleto con shimmer se probó y **se revirtió** el mismo día (ver [[WPF - Esqueleto con Shimmer (Skeleton Loading)]]); Productos volvió a su spinner original sin cambios.

**Estado:** `[ ] Pendiente`

---

### ~~P-029~~ · Cancelación ausente en 7 ViewModels + timer fantasma del timeout — resuelto 2026-09-06

**Archivos:** `ProductosViewModel`, `ProveedoresViewModel`, `FabricantesViewModel`, `CategoriasViewModel`, `EmpleadosViewModel`, `ContactosFabricantesViewModel`, `ContactosProveedoresViewModel`, `BitacoraViewModel`
**Detectado en:** [[Sesión 2026-08-11 - Rediseño de Gestión de Roles]] — ver [[Vista Descargada Durante un await (async void Loaded)]]

Dos problemas que van juntos, corregidos ya en `UsuariosViewModel` y pendientes en el resto:

1. **Sin `CancellationTokenSource`.** `Dispose()` no cancela nada, así que cerrar una pantalla mientras carga deja la petición HTTP en vuelo hasta que termine sola. Abrir y cerrar rápido acumula consultas simultáneas compitiendo — se percibe como lentitud general de la app. Los repositorios **ya aceptan `CancellationToken`**; el problema es que nadie se lo pasa.

2. **El `Task.Delay` del timeout nunca se cancela:**
   ```csharp
   if (await Task.WhenAny(task, Task.Delay(TimeoutMs)) != task) { ... }
   ```
   Cada carga de página deja un timer de 10 s vivo en el TimerQueue aunque la consulta haya vuelto en 200 ms.

**Solución:** aplicar el mismo arreglo que ya tiene `UsuariosViewModel` (CTS cancelado en `Dispose`, token a todas las llamadas de repositorio, guard `if (_disposed) return;` después de cada `await`, y `Task.Delay` con token enlazado). La receta completa está en [[Vista Descargada Durante un await (async void Loaded)]].

**No crashean:** ninguno de los 7 tiene código después del `await` en `Loaded`, así que no reproducen el `NullReferenceException`. Esto es rendimiento y prolijidad, no un bug visible.

**Verificado 2026-09-06** en `BitacoraViewModel` y `CategoriasViewModel` (y por muestreo en el resto): cada uno tiene ahora su `_cts`, lo pasa a `GetPagedAsync(..., _cts.Token)`, usa `CancellationTokenSource.CreateLinkedTokenSource` para el `Task.Delay` del timeout (se cancela apenas gana la consulta real) y guarda `if (_disposed) return;` tras cada `await`. `Dispose()`/`OnDispose()` cancela y libera el CTS. Mismo patrón que ya tenía `UsuariosViewModel`.

**Estado:** `[x] Resuelto 2026-09-06` — ver [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]]

---

### P-030 · Verificación en runtime de la pantalla de Roles

**Archivos:** `CapaUI/Formularios/Principal/Pantallas/Roles/*`, `CapaUI/Core/Controls/SpanningGridPanel.cs`, `MainViewModel.cs`
**Detectado en:** [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]]

La pantalla se reescribió a fondo pero **no se pudo ejecutar la app** desde la sesión. Pendiente probar a mano:

1. **Traba:** maximizar con Roles abierto y redimensionar arrastrando el borde → debe responder fluido. Es la prueba directa del bucle de layout.
2. **Desplegable:** maximizado, abrir el selector de rol → debe salir pegado bajo el ComboBox.
3. **Responsive:** angostar la ventana → la grilla pasa de 4 a 3, 2 y 1 columna, sin barra horizontal.
4. **Sin recarga duplicada:** clic repetido en "Gestión de Roles" → sin spinner nuevo ni consulta repetida.
5. **Abrir/cerrar 15-20 veces** entre Roles y Productos → sin degradación.
6. **Fallo de carga:** cortar la red y entrar → mensaje de error, no spinner infinito ni cierre.

**Estado:** `[ ] Pendiente`

---

### ~~P-031~~ · Frenos de rendimiento de toda la aplicación — ✅ Resuelto 2026-09-06

**Detectado en:** [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]] (auditoría completa de CapaUI)

Hallazgos fuera de Roles, resueltos en su totalidad:

| | Hallazgo |
|---|---|
| ~~**G1**~~ | ✅ **Login: `Task.Delay` artificiales.** El Paso 3 ("Sincronizar módulos") no tenía red detrás y saltaba de 70% a 100% con el mismo barrido de `Task.Delay(6)` que los pasos con red real — 236ms cien por ciento artificiales. Resuelto: salta directo a 100%. Además se quitaron los `Dispatcher.Invoke` redundantes de `AnimarStep` (ya se está en el hilo UI). Los Pasos 1 y 2 **ya corrían** en paralelo con la red real vía `Task.WhenAll` desde antes — la cifra original de "~2,7s en serie" de este hallazgo estaba desactualizada al momento de atacarlo. |
| ~~**G2**~~ | ✅ **`bimbo-logo.png` sin `DecodePixelWidth`.** `MainWindow.xaml` ahora decodifica a 50px reales (`DecodePixelHeight="50"`) en vez de cargar los 3000×1391 completos. La segunda mitad del hallazgo (`bimbo_no_bg.png` "recreado en 5 archivos") ya no aplica — verificado 2026-09-06 que hoy solo hay una referencia estática en `LoginWindow.xaml`, no un patrón de recreación por modal; la descripción original quedó desactualizada en ese punto. |
| ~~**G3**~~ | ✅ **`CacheMode="BitmapCache"` sobre `Sidebar` y `BrandBlock`, cuyo `Width` se anima** (`MainWindow.xaml:127`, `:528`). Peor caso de BitmapCache: re-rasteriza el bitmap completo por frame. Resuelto 2026-09-06: se quitó `CacheMode="BitmapCache"` de ambos elementos. |
| ~~**G4**~~ | ✅ **`DashboardView` tenía un `Storyboard RepeatBehavior="Forever"` que nunca se detenía.** Resuelto: ciclo de vida determinístico en `DashboardView.xaml.cs` (`Loaded`/`Unloaded`) que inicia y detiene el Storyboard limpiando memoria, evitando la excepción de Namescope que causa `StopStoryboard` en XAML al descargarse. |
| ~~**G5**~~ | ✅ **`PesajeView` no llamaba `_vm.Dispose()`.** El primer intento de arreglo (2026-09-06) agregó `(_vm as IDisposable)?.Dispose()` en el `View`, pero `PesajeViewModel` no implementaba `IDisposable` — el cast siempre daba `null`, el `Dispose()` nunca corría. Corregido en la auditoría de la misma fecha: `PesajeViewModel` ahora implementa `IDisposable` de verdad (vacío hoy a propósito, no tiene CTS ni suscripciones que liberar; queda listo para cuando las tenga). La sospecha original de "lambda anónima no desuscribible" en `PropertyChanged` no se confirmó — ya era un método nombrado, correctamente desuscrito. |
| ~~**G6**~~ | ✅ **Guardar un proceso de descarga hacía ~20 viajes de red en serie.** Resuelto: `PesajeViewModel.cs` reemplazó el `foreach` secuencial de carga de productos por camión con `Task.WhenAll`, ya que son independientes entre sí. |
| ~~**G7**~~ | ✅ **`<DropShadowEffect Opacity="0"/>` no apaga el shader** (`ContactoFabricanteModal.xaml:222`, `ContactoProveedorModal.xaml:213`). Resuelto: `Effect="{x:Null}"` en ambos. |
| ~~**G8**~~ | ✅ **`AddScoped` en WPF sin scopes** (`CapaAplicacion4/DependencyInjection.cs:15-18`) = singletons de facto. Resuelto: `AddSingleton` explícito, mismo criterio que ya usaba `CapaDatos`. Verificado que las 3 estrategias inyectan repos Transient sin estado — sin dependencia cautiva peligrosa. |
| ~~**G9**~~ | ✅ **`MainViewModel` disponía `_searchVm`**, instancia compartida de toda la sesión → la siguiente búsqueda lanzaba `ObjectDisposedException` (`MainViewModel.cs:142`, `:172`). Resuelto: se quitó el `Dispose()` de `_searchVm`; sigue desuscribiéndose del evento. |
| ~~**G10**~~ | ✅ **`ProductosView.ActualizarCarga()` y `CategoriasView.ActualizarCarga()` desreferenciaban `_vm` sin comprobar null.** Resuelto: `if (_vm == null) return;` al inicio de ambas. |
| ~~**G11**~~ | ✅ **Recursos duplicados en 8 vistas, re-parseados en cada navegación.** Resuelto 2026-09-06: Se declararon e inmutabilizaron con `po:Freeze="True"` en `Styles.xaml` los pinceles de la paleta y los 16 iconos vectoriales compartidos; se promovieron `ActionBtn`, `SegmentBtn`, `ModernScrollBar` y `ModernScrollViewer` a recursos globales; y se eliminaron por completo las definiciones locales redundantes en las 8 vistas de catálogo (`ProductosView`, `CategoriasView`, `FabricantesView`, `ProveedoresView`, `PresentacionesView`, `EmpleadosView`, `UsuariosView`, `BitacoraView`) y en las 2 vistas de contactos (`ContactosFabricantesView`, `ContactosProveedoresView`). |

**Estado:** `[x] Resuelto 2026-09-06 — 11/11 hallazgos resueltos` — ver [[Sesión 2026-09-06 - Cierre integral de Deuda Tecnica P-031 y P-042]]

---

### ~~P-032~~ · El reparto de tara extra ya es transaccional — resuelto 2026-09-06

**Detectado en:** [[Sesión 2026-08-13 - Pesaje solo bruto y tara extra pesada]]

`PesajeViewModel.RepartirTaraExtraAsync` reparte un total entre N pesadas con **N PATCH independientes** de PostgREST (`ActualizarTaraExtraEntradaAsync`, uno por entrada). No hay transacción: si la red se corta a mitad, unas filas quedan con la cuota nueva y otras con la vieja, y `Σ entradas` deja de ser el total real.

**Mitigaciones ya implementadas:**
- **Pre-vuelo**: se validan las N pesadas contra el `CHECK peso_neto > 0` **antes** de escribir ninguna, así el fallo más probable (una pesada de bruto chico) no produce escrituras parciales.
- **Auto-reparable**: como el total se deriva de `Σ entradas`, reabrir el modal muestra el total real que quedó; volver a aplicar reparte todo desde cero. No hay estado que reconciliar.
- Se avisa por Toast cuántas filas fallaron.

**Solución de fondo (implementada 2026-09-06):** `PesajeRepository.RepartirTaraExtraLoteAsync` llama a la RPC `repartir_tara_extra_pesaje_tabla_bitacora` en una sola petición; `PesajeViewModel.RepartirTaraExtraAsync` ya no hace el bucle de N `ActualizarTaraExtraEntradaAsync`.

**Verificado en BD 2026-09-06** (no solo que la migración existiera en el repo — que el cuerpo desplegado sea realmente atómico):
```sql
select pg_get_functiondef(oid) from pg_proc where proname='repartir_tara_extra_pesaje_tabla_bitacora';
```
La función hace `select ... for update` sobre todas las filas primero, cuenta cuántas cumplen la condición (activas, producto abierto) y si el conteo no coincide, **aborta con `raise exception` antes de escribir nada** — el `update` real corre dentro de la misma transacción implícita de la función. Genuinamente atómico, no solo "una sola llamada de red".

**Estado:** `[x] Resuelto 2026-09-06` — ver [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]]

---

### ~~P-033~~ · El trigger de pesajes cubre UPDATE — resuelto 2026-08-24

**Detectado en:** [[Sesión 2026-08-13 - Pesaje solo bruto y tara extra pesada]]

El reparto de tara extra hace `UPDATE` de `peso_tara_extra` sobre entradas ya insertadas. **No se pudo comprobar** si `trg_calcular_pesos_entrada` está declarado `BEFORE INSERT` o `BEFORE INSERT OR UPDATE`: el MCP de Supabase de esa sesión apuntaba a otro proyecto (`mxarlisuueovxvttytcm`), no al de Bimbo (`bzmmrifjgzlvsphctais`).

**Mitigación implementada:** `PesajeRepository.ActualizarTaraExtraEntradaAsync` escribe también `peso_tara_total` y `peso_neto`, calculados en el cliente con la misma fórmula del trigger. La fila queda consistente corra o no el trigger.

**Qué falta verificar** (SQL de solo lectura en Supabase Studio):
```sql
select t.tgname, pg_get_triggerdef(t.oid) from pg_trigger t
join pg_class c on c.oid = t.tgrelid
where c.relname = 'entradas_producto' and not t.tgisinternal;

select column_name, is_generated from information_schema.columns
where table_name = 'entradas_producto';

select polname, polcmd from pg_policy where polrelid = 'entradas_producto'::regclass;
```

- Si `peso_tara_total`/`peso_neto` fueran `GENERATED ALWAYS` → poner `PesajeRepository.EscribirDerivados = false`.
- Si RLS bloquea `UPDATE` de `entradas_producto` para el rol de la app → el modo "tara extra total" no funciona y hace falta una política nueva.

**Verificación 2026-08-24:** en el proyecto Bimbo (`bzmmrifjgzlvsphctais`) se comprobó que `trg_calcular_pesos_entrada` es `BEFORE INSERT OR UPDATE OF peso_bruto, peso_tara_extra, id_mov_producto`. La batería de RPC actualizó peso bruto y tara extra y recibió los derivados recalculados. También se detectó que la función del trigger no fija su propio `search_path`; queda como endurecimiento separado.

**Estado:** `[x]` Resuelto — verificado en BD. Ver [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]].

---

### ~~P-034~~ · ✅ Invalidación de caché apoyada en tablas que no publican en Realtime — resuelto 2026-09-06

**Detectado en:** [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]]

`ProductosViewModel.cs:238` hacía `Observar("fabricante", _ => CatalogoCache.Invalidar("fabricantes"))`, pero la tabla no estaba en la publicación de Realtime.

**Resuelto en dos etapas:**
1. ✅ **Publicación de tablas y PK:** [[Sesión 2026-08-14 - Realtime en columnas de join de Productos]]: migración `publicar_catalogos_en_realtime` aplicada (8 → 13 tablas) y corregido `_pkColumns`.
2. ✅ **Suscriptor de vida larga a nivel de aplicación (Punto 3):** Diseñado e implementado en [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] mediante `InvalidadorCacheRealtime.cs` (`IInvalidadorCacheRealtime`), registrado como Singleton en el contenedor DI.
   - En cada sesión (`MainWindow.OnLoaded:259`) se invoca `_invalidadorCache.Suscribir()`, registrando observadores para las 10 tablas de catálogo (`TagsCache`).
   - Al recibir eventos de Postgres, ejecuta purga síncrona en RAM vía `_cache.InvalidarEtiqueta(etiqueta)`.
   - Al detectar reconexión de red (`IConexionMonitor.Reconectado`), purga masivamente la raíz de catálogos (`TagsCache.CatalogosRaiz`).
   - En logout (`MainWindow.LimpiarRecursosAsync:749`) se invoca `_invalidadorCache.Desuscribir()`.
   - Verificado con suite de pruebas unitarias dedicada: `BimboProyecto.Tests/Cache/InvalidadorCacheRealtimeTests.cs` (4 pruebas unitarias aprobadas).

> [!bug] Hallazgo al auditar (2026-09-06): la 10ª tabla se agregó al mapa sin publicarla — corregido
> El mismo commit que subió el conteo de 8 a 10 tablas agregó `roles` al diccionario de `InvalidadorCacheRealtime` (para purgar la caché de `RolRepository`), pero nunca la agregó a `supabase_realtime`. Verificado en BD:
> ```sql
> select tablename from pg_publication_tables where pubname='supabase_realtime' and tablename in ('roles','acciones_roles');
> -- (vacío)
> ```
> La suscripción se abría y **jamás recibía un evento** — exactamente el mismo modo de falla silenciosa que este ítem existe para prevenir, reintroducido para una tabla nueva. Corregido con `supabase/migrations/20260906170000_publicar_roles_en_realtime.sql` (`ALTER PUBLICATION supabase_realtime ADD TABLE public.roles;`), aplicada y reverificada en BD. RLS de `roles` ya es `USING (true)` para `authenticated`, así que publicarla no expone nada nuevo.

**Estado:** `[x]` Resuelto (incluyendo la publicación de `roles`, corregida 2026-09-06).

---

### P-035 · Configuración de empresa implementada; falta validación manual y confirmar trigger de `updated_at`

**Detectado en:** [[Sesión 2026-08-14 - Logo de empresa dinamico en login]]

El módulo, el bucket y sus políticas ya se verificaron e implementaron. `empresa-logos` mantiene lectura pública; INSERT/UPDATE/DELETE exigen un usuario autenticado con `Modificar Configuración`. Cada reemplazo genera un nombre único, actualiza la fila y elimina versiones anteriores; el caché local también se actualiza.

Durante la inspección remota no apareció un trigger asociado a `public.empresa` en `information_schema.triggers`. Por instrucción funcional, la aplicación no escribe `updated_at` y tampoco se creó un trigger sustituto. Antes de considerar cerrado el flujo completo hay que confirmar el trigger en el entorno objetivo y ejecutar la prueba visual/manual.

**Solución de fondo:**

1. Confirmar que el trigger automático de `updated_at` esté adjunto a `public.empresa` en el entorno objetivo.
2. Probar manualmente apertura por permiso, guardado, reemplazo repetido del logo, reinicio de la aplicación y propagación del tema.

> [!success] Punto 1 confirmado (2026-09-06)
> ```sql
> select tgname from pg_trigger where tgrelid='public.empresa'::regclass and not tgisinternal;
> -- trg_empresa_updated_at
> ```
> El trigger existe. Queda solo el punto 2 —prueba manual/visual—, que no se puede verificar con lectura de código ni de base.

**Estado:** `[~] Parcialmente resuelto — implementación, seguridad y trigger confirmados; falta la validación manual/visual (punto 2)`

---

### P-036 · Tara y Presentaciones no tienen pantalla CRUD — el combo de unidad filtrado a masa no tiene dónde vivir

**Detectado en:** [[Sesión 2026-08-14 - Catalogo de unidad_medida con categoria]]

`unidad_medida` ahora tiene categoría (`tipo_unidad`: Masa/Volumen/Conteo — ver [[ADR-017 - Catalogo real de unidad_medida con categoria y su uso en Tara]]) y `tara.id_unidad` ya es un dato real en la base (antes "kg" era solo la convención del nombre `peso_tara_envalaje`). `CatalogoRepository.GetUnidadesAsync(..., idTipoUnidad)` y `Catalogos.Unidades(r, idTipoUnidad)` ya están listos para poblar un combo de unidad filtrado a solo Masa.

El problema: **hoy no existe ninguna pantalla para crear o editar filas de `tara`** en la app — solo una lupa de solo lectura (`Catalogos.Taras` + `SelectorCatalogoModal`) para *elegir* una tara ya existente al editar un Producto. Los datos de `tara` se cargan directo en Supabase (por eso también es el origen del dato de prueba de P-023). El combo filtrado a Masa, entonces, no tiene ningún formulario donde mostrarse todavía. Mismo hueco existe para **Presentaciones** — confirmado por el usuario en la misma sesión, señalado a propósito para resolver después.

**Solución de fondo:** construir un CRUD mínimo de Tara (grilla + modal, mismo patrón que Categorías/Fabricantes) con el combo de unidad ya integrado (`Catalogos.Unidades(r, idTipoUnidadMasa)`), y el equivalente para Presentaciones. Conviene resolver junto con P-023 (limpiar el dato de prueba de `tara`), ya que ambos requieren tocar esa tabla.

**Estado:** `[~] Parcial — Presentaciones resuelto 2026-08-15, Tara sigue pendiente`

La mitad de Presentaciones se cerró en [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]]: `CapaUI/.../Pantallas/Presentaciones/` con grilla, filtros de estado y orden, y modal de alta/edición. Ese módulo sirve de plantilla directa para el de Tara — la diferencia es que Tara además necesita el combo de unidad filtrado a Masa y arrastra el dato de prueba de P-023.

---

### ~~P-037~~ · ✅ El control de paginación es code-behind duplicado 9× sin validación de rango — resuelto 2026-09-06

**Detectado en:** [[Sesión 2026-08-14 - Regresion la grilla mostraba la pagina anterior]]

**Solución aplicada (2026-09-06):**
1. ✅ **`PaginadorControl` UserControl compartido:**
   - Creado en `CapaUI/Core/Controls/PaginadorControl.xaml` y `.xaml.cs`.
   - Expone `DependencyProperty` para `Page` (`TwoWay`, con `CoercePage` clamping estricto `1 <= Page <= Math.Max(1, TotalPages)`), `TotalPages`, `PageInfo` e `IsLoading`.
   - Reutiliza la lógica pura de elipsis en `Paginacion.cs` (`Calcular(current, total)` y `Elipsis = -1`).
   - Bloquea interacciones numéricas y flechas automáticamente mientras `IsLoading = true`.
2. ✅ **Eliminación masiva de código duplicado:**
   - Se migraron las 10 vistas de catálogo: `ProductosView`, `CategoriasView`, `FabricantesView`, `ProveedoresView`, `UsuariosView`, `EmpleadosView`, `BitacoraView`, `PresentacionesView`, `ContactosFabricantesView` y `ContactosProveedoresView`.
   - Se eliminaron todos los métodos `RefrescarPaginacion()`, `CalcularPaginas()` y los paneles manuales `PaginacionPanel` de cada code-behind.
   - En `UsuariosView`, `EmpleadosView` y `BitacoraView`, se desacopló definitivamente la asignación de `Dg.ItemsSource = _vm.PageRows` de la paginación, solucionando la fragilidad #719.
3. ✅ **Pruebas unitarias de regresión:**
   - Creado `BimboProyecto.Tests/Paginacion/PaginacionTests.cs` con 5 pruebas xUnit cubriendo casos de límite, elipsis inicial, intermedia, final y total <= 7.

**Estado:** `[x]` Resuelto.

---

### ~~P-038~~ · Modelos C# alineados del esquema + el error de carga ya llega al usuario — resuelto 2026-09-06

**Detectado en:** [[Sesión 2026-08-14 - Modelo desalineado del esquema tumbaba paginas enteras]]

Tres problemas de la misma familia, descubiertos al diagnosticar un bug que costó tres rondas de reporte.

**1. El desalineo modelo↔esquema falla en bloque y en silencio.** `Productos.cs` declaraba como `int` cuatro columnas que en la base son NULLABLE. Una sola fila con NULL hacía que Newtonsoft lanzara y **fallara la consulta entera** — no la fila, la página completa. Ya corregido para `productos`, pero:

- **`ProductosInsertar.cs` sigue desalineado**: `id_presentacion`, `id_fabricante`, `id_categoria`, `id_pais`, `id_tara` como `int` y `peso_teorico` como `decimal`, todas nullable en la base. Hoy solo lo usa `RepositorioProducto.ingresarProducto` (estático, legacy, sin llamadores activos), así que no explota — pero explotaría apenas se use.
- **El resto de las tablas no se auditó.** Conviene una pasada comparando cada modelo de `CapaDatos/Modelados/` contra `information_schema.columns`: cualquier value type (`int`, `decimal`, `DateTime`, `bool`) declarado sin `?` sobre una columna `is_nullable = YES` es la misma bomba.

```sql
-- Para auditar: lista las columnas nullable de una tabla
select column_name, data_type, is_nullable
from information_schema.columns
where table_schema='public' and table_name='<tabla>'
order by ordinal_position;
```

**2. `ErrorCarga` no se muestra.** `ProductosViewModel.CargarPaginaAsync` asigna `ErrorCarga = r.Error` en su return temprano de error, pero eso nunca llegó a la pantalla — el usuario vio una grilla con datos viejos, sin ningún aviso de que la carga había fallado. Con el mensaje visible, este bug se diagnosticaba en minutos en vez de tres rondas. Verificar si `ErrorCarga` está bindeado en las vistas y, si no, mostrarlo (mismo tratamiento en los módulos gemelos).

**3. El return temprano deja la grilla mintiendo.** Los cuatro caminos de salida de `CargarPaginaAsync` (timeout, generación invalidada, `!r.Success`) no tocan `PageRows`, así que la pantalla sigue mostrando la página anterior como si fuera la pedida — mientras `Page`, `PageInfo` y los botones ya avanzaron. Aunque se arregle el punto 2, conviene decidir qué debe mostrar la grilla cuando una página falla: vaciarse, quedarse con un estado de error explícito, o revertir `Page` al valor anterior.

**Riesgo:** alto. El punto 1 puede dejar cualquier pantalla inutilizable con un solo registro mal cargado, y el punto 2 hace que se diagnostique a ciegas.

**Resuelto 2026-09-06, verificado columna por columna contra `information_schema.columns` en vivo** (no contra lo que el commit afirmaba):

| Tabla.columna | `is_nullable` real | Modelo ahora |
|---|---|---|
| `entradas_producto.id_mov_producto` | YES | `int?` ✅ |
| `entradas_producto.peso_tara_extra/individual/total` | YES | `decimal?` ✅ |
| `categoria.descripcion_categoria` | YES | `string?` ✅ |
| `categoria.estado_categoria` | YES | `bool?` ✅ |
| `productos.id_presentacion/id_fabricante/id_tara/id_categoria/id_pais` | YES | `int?` ✅ |
| `productos.contenido` | YES | `string?` ✅ |
| `productos.peso_teorico` | YES | `decimal?` ✅ |
| `usuarios.ultimo_acceso` | YES | `DateTime?` ✅ |
| `productos.id_estado` (control, no se tocó) | NO | `int` sin cambio ✅ |

Punto 2: `ProductosViewModel.MensajeSinResultados` antepone `"Error al cargar: {ErrorCarga}"` cuando hay error. Punto 3: `CargarPaginaAsync` en Productos, Categorías y Bitácora ahora hace `PageRows = new ObservableCollection<T>()` en los caminos de error, en vez de dejar la página anterior mintiendo mientras el paginador ya avanzó.

**Lo que sigue sin hacerse:** la auditoría exhaustiva de *todas* las tablas de `CapaDatos/Modelados/` que pide el punto 1 — solo se corrigieron y verificaron las columnas que este cambio tocó. Si aparece otro caso de "una fila NULL tumba la página completa" en un módulo no tocado acá, es la misma familia de bug.

**Estado:** `[x] Resuelto 2026-09-06 (auditoría parcial del punto 1)` — ver [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]]

---

### ~~P-039~~ · ✅ Búsqueda insensible a tildes replicada en las 7 tablas de catálogo — resuelto 2026-09-06

**Detectado en:** sesión 2026-08-14, junto con el filtro de Categoría y el rediseño de la barra de filtros.

**Solución aplicada (2026-09-06):**
1. ✅ **Migración SQL en Supabase (`20260906060000_busqueda_insensible_tildes_todas_tablas.sql`):**
   - Garantiza la función `public.sin_tildes(text)` con `extensions.unaccent`.
   - Agregadas columnas generadas `STORED` e índices GIN trigramas (`gin_trgm_ops`):
     * `fabricante.busqueda_fabricante` (nombre + descripción)
     * `proveedores.busqueda_proveedor` (nombre + rtn + correo)
     * `categoria.busqueda_categoria` (nombre + descripción)
     * `presentacion_producto.busqueda_presentacion` (nombre + descripción)
     * `empleados.busqueda_empleado` (nombre + apellido + identidad + correo)
     * `usuarios.busqueda_usuario` (alias)
     * `bitacora.busqueda_bitacora` (tabla + campo + estado_actual + estado_anterior)
   - Recreada `vista_usuarios_busqueda` incluyendo `busqueda_usuario` sobre alias y nombres de empleado.
2. ✅ **Capa de Datos C#:**
   - Actualizados repositorios CRUD (`FabricanteCrudRepository`, `ProveedorCrudRepository`, `CategoriaCrudRepository`, `PresentacionCrudRepository`, `EmpleadoCrudRepository`, `UsuarioRepository`, `BitacoraCrudRepository`) y buscadores universales (`EmpleadoRepository`, `ProductoSearchRepository`).
   - Se reemplazaron bloques de `Or(...)` con tildes sensibles por filtros `Filter("busqueda_<tabla>", Op.ILike, $"%{TextoBusqueda.Normalizar(termino)}%")`.
3. ✅ **Pruebas unitarias:**
   - Extendida la suite `BimboProyecto.Tests/Busqueda/TextoBusquedaTests.cs` con casos de prueba para todas las entidades migradas.

**Estado:** `[x]` Resuelto — verificado en BD con índices activos y 286/286 tests pasando.

---

### ~~P-040~~ · ✅ Carga inicial de `icono_sidebar` pendiente en Storage — resuelto

**Detectado en:** [[Sesión 2026-08-15 - Icono dinámico del sidebar]]

El código ya permite seleccionar, subir, cachear y mostrar `empresa.icono_sidebar`, conservando `Resources/bimbo-logo.png` como fallback. La fila `empresa.id_empresa = 1` había quedado con `icono_sidebar = 'sin_icono'` al último intento verificado, porque la carga automatizada no se completó (MCP en modo solo lectura, canal administrativo fallaba por transporte).

**Verificado 2026-09-06:**
```sql
select icono_sidebar from public.empresa where id_empresa=1;
-- sidebar_empresa_1_20260815065933498_350e1ad61c374aa8bbd19a9e3c04e495.png
```
La ruta ya no es `'sin_icono'` — quedó guardada en algún momento entre la detección y esta verificación. No se confirmó por separado que el objeto sea descargable públicamente (fuera del alcance de una consulta SQL), pero el estado que el ítem pedía cerrar —la fila con una ruta real— está cumplido.

**Estado:** `[x]` Resuelto — verificado en BD.

---

### ~~P-041~~ · Fabricantes y Categorías pasan el filtro de estado al RPC de conteos y las tres pastillas dejan de informar — resuelto 2026-09-06

**Archivos:** `CapaDatos/Repositories/Fabricantes/FabricanteCrudRepository.cs` (`GetConteosRpcAsync`), `CapaDatos/Repositories/Categorias/CategoriaCrudRepository.cs` (ídem)
**Detectado en:** [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]]

> Se numeró P-041 al fusionar: nació como P-040 en la rama de Presentaciones y chocó con el P-040 de la rama de configuración de empresa, que ya estaba publicado.

Las pastillas del header —TOTAL, ACTIVOS, INACTIVOS— existen justamente para **desglosar por estado**. Ambos repositorios le pasan `p_estado` al RPC de conteos cuando hay filtro de estado activo, así que las tres terminan calculadas sobre el mismo subconjunto:

| Filtro activo | Lo que se ve | Lo que debería verse |
|---|---|---|
| Habilitados | TOTAL = ACTIVOS, INACTIVOS = 0 | los tres números reales |
| Deshabilitados | TOTAL = INACTIVOS, ACTIVOS = 0 | los tres números reales |

O sea: apenas se toca el filtro de estado, el indicador deja de indicar. Con "Todos" funciona por casualidad, porque ahí no se manda el parámetro.

`ProductoCrudRepository.GetPagedInternal` ya lo hace bien y tiene el comentario que lo explica: arma un `ProductoFiltros` aparte para los conteos, **sin** `IdEstado`, conservando los filtros que sí son ortogonales al estado (fabricante, país, proveedor, categoría). `PresentacionCrudRepository` siguió ese mismo criterio desde el arranque.

**Riesgo:** bajo en consecuencias (no corrompe datos, no rompe la grilla) pero es información incorrecta en pantalla: el usuario lee "0 inactivos" cuando hay inactivos.

**Solución (implementada 2026-09-06):** en ambos repositorios, `ConstruirParametrosConteo` ya no manda `p_estado`, igual que Productos. Verificado en BD que `contar_fabricantes(p_estado integer, p_pais integer)` y `contar_categorias(p_estado boolean)` ya trataban el parámetro como opcional — no hizo falta migración.

**Estado:** `[x] Resuelto 2026-09-06` — ver [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]]

---

### ~~P-042~~ · `PesajeModalStyles.xaml` alineado con aros de foco y validación conservando tamaño compacto — ✅ Resuelto 2026-09-06

**Archivo:** `CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales/PesajeModalStyles.xaml`
**Detectado en:** [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]

**Decisión y resolución de fondo (2026-09-06):**
Se determinó mantener las dimensiones compactas deliberadas de `MInput` (`Height="36"`, `FontSize="13.5"`) y `MSegBtn` (`FontSize="13"`) para garantizar la densidad de información requerida por los modales de Pesaje de báscula sin romper el layout. Se cerró integralmente la deuda de interacción visual y accesibilidad por teclado:
1. ✅ **`MInput`**: En el trigger `IsFocused="True"`, se sustituyó el borde blanco tenue por el aro verde estándar del sistema (`BorderBrush="#34D399"`, `BorderThickness="2"`). Además, el trigger de validación `TieneError` se alineó a `#EF4444` con grosor 2.
2. ✅ **`MSegBtn`**: Se le dotó de borde base transparente (`BorderThickness="2"`) y trigger `IsFocused="True"` asignando `BorderBrush="#34D399"`, permitiendo navegación por teclado (`Tab`) accesible y consistente con `ModalSegBtn`.

**Estado:** `[x] Resuelto 2026-09-06 — aros de foco y validación integrados` — ver [[Sesión 2026-09-06 - Cierre integral de Deuda Tecnica P-031 y P-042]]

---

### ~~P-043~~ · ✅ `ModalInput`/`InputBox` (global) tenían el mismo bug de `VerticalAlignment` fijo que ya se corrigió en `MInput` — resuelto 2026-09-06

**Archivo:** `CapaUI/Resources/Styles.xaml` — estilos `ModalInput` (líneas ~208-296) e `InputBox` (líneas ~141-167)
**Detectado en:** [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]

El `ControlTemplate` de `MInput` (Pesaje) tenía `VerticalAlignment="Center"` **fijo** en el `PART_ContentHost`, ignorando la propiedad `VerticalContentAlignment` del `TextBox` sin importar qué se le pusiera local o por Setter — se descubrió porque el campo Observaciones de `ProcesoDescargaModal`, ya multilínea, mostraba el cursor centrado en vez de arriba pese a `VerticalContentAlignment="Top"`. Se corrigió atando `VerticalAlignment="{TemplateBinding VerticalContentAlignment}"`.

`ModalInput` e `InputBox`, los estilos **globales** que usan todos los modales CRUD, tienen el mismo `VerticalAlignment="Center"` hardcodeado en su propio `PART_ContentHost` — no se tocaron porque quedaban fuera del alcance de esa sesión (era sobre Pesaje).

**Ya hay un campo real afectado hoy:** `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaModal.xaml:81`, el campo "Dirección" (`AcceptsReturn="True"`, `TextWrapping="Wrap"`, `MinHeight="72"`, `VerticalContentAlignment="Top"` puesto local) — el cursor arranca centrado en la caja, no arriba, exactamente el mismo síntoma que tenía Observaciones antes del fix.

**Riesgo:** bajo (cosmético), pero engañoso — un campo multilínea que centra el cursor en vez de arrancar arriba se ve raro apenas tiene más de una línea de texto.

**Solución:** mismo fix de una línea, dos veces — en cada `ControlTemplate`, cambiar `VerticalAlignment="Center"` del `PART_ContentHost` por `VerticalAlignment="{TemplateBinding VerticalContentAlignment}"`. Sin riesgo para los campos de una sola línea: siguen viniendo con `VerticalContentAlignment="Center"` por el `Setter` del propio estilo, así que no cambia nada para ellos — solo lo hereda quien lo pise local, como ya hace `ConfiguracionEmpresaModal`.

**Solución aplicada — en dos tandas.** Una sesión anterior (2026-09-06, sin documentar) arregló solo `InputBox`; `ModalInput` quedó con el bug. Al revisar el trabajo se completó lo que faltaba:

1. `InputBox` (línea ~157): `VerticalAlignment="{TemplateBinding VerticalContentAlignment}"` en `PART_ContentHost`.
2. `ModalInput` (línea ~244): mismo cambio en su `PART_ContentHost`.
3. **Un segundo bug que el fix de una sola línea habría dejado sin cerrar:** `ModalInput` tiene además un `TextBlock` superpuesto (`PART_Recorte`, con `TextTrimming="CharacterEllipsis"`, visible solo sin foco) que **también** tenía `VerticalAlignment="Center"` fijo. Arreglar solo `PART_ContentHost` habría dejado un campo top-alineado mostrando el texto arriba mientras se edita, pero saltando al centro apenas se le saca el foco — un bug visual nuevo. Se corrigió también (línea ~262).

**Estado:** `[x]` Resuelto — build 0/0, 270/270 tests. Ver [[Sesión 2026-09-06 - Tres frenos de rendimiento cerrados y VerticalAlignment fijo en ModalInput]].

---

### ~~P-044~~ · ✅ Multiselección de `SelectorCatalogoModal` no respondía a teclado — resuelto 2026-09-06

**Archivo:** `CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs`
**Detectado en:** [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]

**Solución aplicada (2026-09-06):**
- En `OnPreviewKeyDown`:
  - Se agregó soporte para `Key.Space`: si el foco está en la tabla (`Dg.IsKeyboardFocusWithin`), en modo múltiple conmuta el `Marcado` de la fila seleccionada (`ToggleMarcado`), manteniendo la fila activa para navegación ágil. En modo simple confirma la fila de inmediato. No interfiere con `SearchBox` ni con activación nativa de botones.
  - Se mejoró `Key.Enter`: en modo múltiple, si no hay marcas previas acumuladas (`_marcados.Count == 0`), presionar Enter sobre una fila la confirma directamente como atajo rápido de 1 solo ítem.
  - Detección defensiva de botones con `EsBoton` recorriendo el árbol visual/lógico para no secuestrar eventos nativos.
  - Centralización de marcado en `ToggleMarcado(fila)` reutilizado por doble clic y teclado.

> [!bug] Hallazgo al auditar (2026-09-06): el atajo de Enter reabría el hueco que el propio código ya evitaba para el doble clic — corregido
> `_marcados.Count == 0` no distingue "nunca tocó ningún checkbox" de "tildó una fila y se arrepintió": clickear el checkbox también selecciona la fila (`Dg.SelectedItem`), así que tildar y destildar una fila la deja con 0 marcas pero **todavía seleccionada**. El atajo de Enter la habría emitido igual, exactamente el escenario que el comentario original de `Dg_DoubleClick` ya advertía ("sin marcas no se emite nada... agregaría justo la última que se tocó"). El botón "Elegir" ya se protegía de esto (`BtnElegir.IsEnabled` exige `marcados > 0` en modo múltiple), pero el atajo de teclado no pasaba por esa misma condición.
> Corregido agregando `_idsTocados` (HashSet de ids que pasaron por el checkbox alguna vez en esta apertura del modal, tildados o no): el atajo de Enter ahora solo dispara sobre una fila que **nunca** se tocó, no sobre una que se tocó y quedó sin marcar.

**Estado:** `[x]` Resuelto — build 0/0, 286/286 tests (incluye la corrección del atajo de Enter).

---

### ~~P-045~~ · ✅ Pesaje quedó fuera de la validación centralizada: sus campos de texto no tenían tope en ninguna capa — resuelto 2026-09-06

**Archivos:** `CapaDominio/Reglas/ReglasEntidades.cs`, `CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales/` (`ProductoCamionModal.xaml`, `PesajeModal.xaml`, `CamionModal.xaml`)
**Detectado en:** security review del rediseño visual de Pesaje (2026-08-20)

> [!note] `ProcesoDescargaModal` ya no existe
> El ítem original apuntaba a ese archivo. Se había partido en `CamionModal` + `ProductoCamionModal` antes de esta fecha; las rutas de arriba están corregidas.

> [!warning] Esto **no** es una vulnerabilidad — no escalarlo
> El security review que lo destapó dio cero hallazgos, y ese resultado es correcto. Para tocar estos campos hace falta sesión válida **más** el permiso `Registrar Entrada`; RLS está habilitado en `movimientos`, `movimiento_productos` y `entradas_producto`, y PostgREST parametriza (no hay superficie de inyección). Es deuda de **calidad de datos**, no de seguridad.

El módulo Pesaje nunca se sumó al esquema de [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]], que el resto del proyecto ya adoptó. Ninguna de las tres capas pone un límite de longitud:

| Capa | Pesaje | Resto del proyecto |
|---|---|---|
| UI | Cero `MaxLength` en sus XAML | Categorías, Empleados, Fabricantes, Presentaciones y Proveedores sí lo tienen |
| Dominio | No existe `ReglasPesaje` | 9 entidades declaradas en `ReglasEntidades.cs` |
| BD | Sin tope en las 4 columnas de texto | — |

Lo de la BD está verificado contra `information_schema`, no supuesto: `movimientos.observaciones`, `movimiento_productos.observaciones` y `entradas_producto.observaciones` son `text`; `movimientos.placa_vehiculo` es `character varying` **sin longitud declarada**, que en Postgres equivale a `text`. Las cuatro con `character_maximum_length = null`. La validación que sí existe en `ProcesoDescargaModal.ValidarPaso` es solo de obligatoriedad (`IsNullOrWhiteSpace` sobre la placa), nunca de largo.

**Riesgo:** bajo. El vector realista no es un atacante sino un pegado accidental de un operario autenticado: el texto entra completo, se replica por Realtime a todos los clientes conectados y viaja a los PDF/Excel de [[Módulo Reportería]], donde sí puede romper el layout de un reporte.

**Solución (diseñada, reusando el patrón ya probado — no inventar uno nuevo):**

1. Declarar `ReglasPesaje` en `ReglasEntidades.cs` con `Placa` y `Observaciones`, mismo estilo que `ReglasCategoria`.
2. `MaxLength` en los TextBox de los modales y `ValidadorFormulario` encadenado como en `CategoriaModal.xaml.cs:34-37` — `.Campo(TxtPlaca, "La placa").Segun(ReglasPesaje.Placa).ValidarAlSalirDelCampo()`.
3. Migración que fije el tope también en la BD (`varchar(N)` o `CHECK`), para que valga aunque se escriba por fuera de la app. Verificar antes que ningún registro actual la viole.

Los tres pasos, no solo el primero: con el límite únicamente en la UI, cualquier otro camino de escritura lo saltea.

> [!success] Los tres pasos se aplicaron — pero solo a `movimientos` (2026-09-05)
> Ver [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]].
>
> | Columna | Antes | Ahora |
> |---|---|---|
> | `movimientos.placa_vehiculo` | `varchar` sin longitud | ✅ `varchar(20)` |
> | `movimientos.observaciones` | `text` | ✅ `varchar(500)` |
> | `movimiento_productos.observaciones` | `text` | ❌ sigue `text` |
> | `entradas_producto.observaciones` | `text` | ❌ sigue `text` |
>
> Del lado de la app: `ReglasCamion` declarada en Dominio y `ValidadorFormulario` cableado en `RegistroCamionesModal` (con `MaxLength` puesto por `TopePreventivo`). `ProductoCamionModal` y `PesajeModal` **siguen sin validador**.
>
> **La clase se llama `ReglasCamion`, no `ReglasPesaje`** como proponía este ítem: `ReglasEntidades.cs` se organiza por entidad, y lo que falta pertenece a otras dos tablas. Cuando les toque, van como `ReglasProductoCamion` y `ReglasEntradaPesaje` — no amontonadas en una sola clase.
>
> **Actualización 2026-09-02:** En [[Sesión 2026-09-02 - Validación de longitud máxima en campos de texto]], se alinearon 10 clases de dominio (34 reglas) en `ReglasEntidades.cs` contra `information_schema.columns` de Supabase, se incorporó `TopePreventivo(m)` en `ValidadorFormulario` para derivación automática de `MaxLength`, y se implementó una suite completa de pruebas de deriva (`BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs`). El módulo Pesaje permanece como el único pendiente sin reglas de longitud en `ReglasEntidades.cs` ni topes en sus modales/BD.

> [!success] Cerradas las dos `observaciones` que faltaban (2026-09-06)
> Migración `20260906050000_resolucion_integral_p045_p049_p051_p052_p053.sql`: `movimiento_productos.observaciones` y `entradas_producto.observaciones` pasan de `text` a `varchar(500)`. Verificado contra `information_schema.columns`.
>
> Del lado de Dominio, exactamente como anticipaba la nota de arriba: `ReglasProductoCamion.Observaciones` y `ReglasEntradaPesaje.Observaciones` (`LargoMaximo: 500`) en `CapaDominio/Reglas/ReglasEntidades.cs`, sin amontonarlas en una sola clase.
>
> Del lado de UI, `ProductoCamionModal` y `PesajeModal` —que quedaron sin validador en la resolución parcial— ahora cablean `ValidadorFormulario.Nuevo().Campo(TxtObs, "Las observaciones").Segun(Reglas…Camion.Observaciones).ValidarAlSalirDelCampo()`.
>
> `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs` se extendió de 34 a **43 reglas** en 13 clases (sumando `ReglasCamion`, `ReglasProductoCamion`, `ReglasEntradaPesaje`); su Test A sigue verificando contra `information_schema.columns` que ningún `LargoMaximo` sea más permisivo que la columna física.
>
> Las tres tablas de Pesaje (`movimientos`, `movimiento_productos`, `entradas_producto`) quedan cubiertas en las tres capas. Detalle completo en [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]].

**Estado:** `[x]` Resuelto — verificado en BD y con test de deriva de esquema.

---

### P-046 · Validar visualmente las descripciones de estado de Pesaje en Bitácora

**Archivos:** `supabase/migrations/202608240003_describir_estados_pesaje_en_bitacora.sql`, `CapaUI/Formularios/Principal/Pantallas/Bitacora/`
**Detectado en:** [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]]

La traducción en `private.bitacora_pesaje` fue desplegada y validada mediante una transacción con rollback para las tres familias: recepción, producto de recepción y entrada. Emanuel confirmó que los demás cambios funcionales de la sesión operan correctamente, pero quedó pendiente comprobar el resultado en la grilla real de Bitácora.

La prueba manual debe ejecutar cambios de estado y confirmar que las columnas visibles muestran el significado de negocio —por ejemplo, `Recepción cerrada`, `Producto anulado` o `Pesaje anulado`— y nunca textos como `Estado 8`, `Estado 9` o variantes con el ID entre paréntesis.

**Estado:** `[ ] Pendiente` — validación visual en la aplicación.

---

### ~~P-047~~ · ✅ Divergencia de diseño y comportamiento entre `ModalInput` e `InputBox` estandarizada — resuelto 2026-09-06

**Archivos:** `CapaUI/Resources/Styles.xaml` (estilos `ModalInput`, `InputBox` y `CeldaInput`)
**Detectado en:** [[Sesión 2026-09-02 - Validación de longitud máxima en campos de texto]]

**Solución aplicada (2026-09-06):**
1. ✅ **Alineación vertical corregida:** Ambos estilos (`ModalInput` e `InputBox`) usan `VerticalAlignment="{TemplateBinding VerticalContentAlignment}"` en su `PART_ContentHost`, resolviendo el soporte para texto multilínea y cursors alineados al tope (P-043).
2. ✅ **Disparadores de validación unificados:** Se integró el trigger `validacion:Validacion.TieneError` en `InputBox` (borde `#EF4444`, grosor 2), alineándolo con el comportamiento de alerta visual de `ModalInput`.
3. ✅ **Variante de tabla/compacta promovida:** Se centralizó el estilo `CeldaInput` en `Styles.xaml` como la variante estándar de 34px con feedback de foco y error para rejillas y tablas de modales complejos.

**Estado:** `[x]` Resuelto.

---

### ~~P-048~~ · ✅ Fuga de datos y permisos entre sesiones en terminal compartida (CatalogoCache y RolPermisoRepository) — resuelto 2026-09-03

**Archivos:** `CapaUI/Core/Catalogos/CatalogoCache.cs:177`, `CapaDatos/Repositories/Usuarios/RolPermisoRepository.cs:23-24`, `CapaUI/Formularios/Principal/MainWindow.xaml.cs:660-692` (`LimpiarRecursosAsync`), `CapaUI/App.xaml.cs`
**Detectado en:** Auditoría adversarial y diseño de [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] (2026-09-02)

En la aplicación de escritorio WPF de Bimbo Honduras, el contenedor de inyección de dependencias `App.Services` es un root provider estático instanciado una sola vez durante el inicio del proceso (`App.xaml.cs`). Cuando un usuario cierra su sesión en la ventana principal (`MainWindow`), la rutina `HandleCerrarSesionAsync()` ejecuta el método privado `LimpiarRecursosAsync()`, dispara el evento `SesionCerrada` y cierra `MainWindow`, retornando a la ventana de login (`LoginWindow`) **sin reiniciar el proceso del sistema operativo**.

Se constató una fuga de estado bidireccional entre usuarios sucesivos en la misma máquina física:

1. **`CatalogoCache` estático sin purga:** `CatalogoCache._cache` es un `ConcurrentDictionary<string, IReadOnlyList<FiltroItem>>` en memoria. A pesar de que expone el método `public static void InvalidarTodo() => _cache.Clear();` (línea 177), `MainWindow.LimpiarRecursosAsync()` **nunca lo invoca**. Los catálogos consultados por el Usuario A permanecen en RAM para el Usuario B.
2. **`RolPermisoRepository._catalogoCache` privado sin ciclo de vida:** Contiene el campo `private static IReadOnlyList<ModuloAccionesDto>? _catalogoCache` con un `SemaphoreSlim` asociado. No posee ningún método público ni interno para invalidar o limpiar la lista. La estructura de módulos y acciones precargada por el primer usuario persiste inmutable para sesiones posteriores.
3. **Contenedor Singleton no reiniciado y purga de suscriptores Realtime:** Los servicios y repositorios registrados como Singleton en `App.Services` conservan instancias vivas entre sesiones. A su vez, `RealtimeService.DesconectarAsync()` ejecuta `_suscriptores.Clear()` en logout, por lo que cualquier suscriptor singleton (como `InvalidadorCacheRealtime`) pierde sus manejadores a partir del segundo login si no se re-suscribe explícitamente en el ciclo de vida de la ventana principal.

**Riesgo:** Alto en terminales de planta y despachos con rotación de turnos entre múltiples operarios y supervisores. Si bien `SesionPermisos.Limpiar()` restablece los permisos en memoria del usuario autenticado, los datos de catálogos y la estructura base de permisos no se resetean, pudiendo exponer datos cacheados o provocar inconsistencias de visualización. Además, sin re-suscripción explícita, la reactividad por Realtime se pierde al 100% tras el primer cierre de sesión.

**Solución diseñada:**
1. Invocar explícitamente `CatalogoCache.InvalidarTodo()` en `MainWindow.LimpiarRecursosAsync()`.
2. Exponer un método de invalidación en `IRolPermisoRepository` y ejecutarlo durante el cierre de sesión.
3. Como solución arquitectónica definitiva: Migrar todas las cachés estáticas hacia la abstracción centralizada `ICacheService` gobernada por `FusionCache`, ejecutando `ClearAsync(allowFailSafe: false)` de forma determinística en `LimpiarRecursosAsync()`, tal como se establece en [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]].
4. Exponer un método explícito `Suscribir()` en `InvalidadorCacheRealtime` e invocarlo obligatoriamente en `MainWindow.OnLoaded` en cada inicio de sesión, compensando la limpieza de `_suscriptores` en `RealtimeService.DesconectarAsync()`.

**Solución aplicada:** implementada íntegra en [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]]. `CatalogoCache.cs` se eliminó del todo; `RolPermisoRepository` pasó a `ICacheService` con la etiqueta `rbac:definiciones`; `MainWindow.LimpiarRecursosAsync()` llama a `_cache.LimpiarTodoAsync()` (purga total, `allowFailSafe: false`) y a `_invalidadorCache.Desuscribir()`; `MainWindow.OnLoaded` llama a `_invalidadorCache.Suscribir()` en cada sesión.

**Verificado 2026-09-06** (al auditar trabajo de otra sesión, se encontró este ítem también sin actualizar pese a estar resuelto desde el 03): `CapaUI/Core/Catalogos/CatalogoCache.cs` no existe; `grep` confirma `LimpiarTodoAsync()`, `_invalidadorCache.Suscribir()` y `_invalidadorCache.Desuscribir()` presentes en `MainWindow.xaml.cs`.

**Estado:** `[x]` Resuelto.

---

### ~~P-049~~ · ✅ Suscripciones inactivas a Realtime en Contactos (tablas no publicadas en supabase_realtime) — resuelto 2026-09-06

**Archivos:** `CapaUI/Formularios/Principal/Pantallas/ContactosFabricantes/ContactosFabricantesViewModel.cs:127`, `CapaUI/Formularios/Principal/Pantallas/ContactosProveedores/ContactosProveedoresViewModel.cs:127`, `CapaDatos/Repositories/Realtime/RealtimeService.cs`
**Detectado en:** Auditoría adversarial y diseño de [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] (2026-09-02)

Tanto `ContactosFabricantesViewModel` como `ContactosProveedoresViewModel` heredan de `RealtimeAwareViewModel` y ejecutan llamadas de suscripción reactiva en su método `CargarDatosAsync()`:
- `Observar("contactos_fabricante", OnCambioContacto);` (línea 127)
- `Observar("contactos_proveedor", OnCambioContacto);` (línea 127)

Sin embargo, la auditoría empírica ejecutada directamente sobre el catálogo del sistema PostgreSQL de producción (`bzmmrifjgzlvsphctais`) mediante:
```sql
select tablename from pg_publication_tables where pubname = 'supabase_realtime';
```
revela que las únicas tablas publicadas en el canal de replicación lógica de Supabase son: `categoria`, `empleados`, `entradas_producto`, `fabricante`, `movimiento_productos`, `movimientos`, `paises`, `presentacion_producto`, `productos`, `proveedores`, `tara`, `unidad_medida` y `usuarios`.

Las tablas secundarias `contactos_fabricante` y `contactos_proveedor` **no forman parte de `supabase_realtime`**.

**Modo de falla silencioso:**
El cliente `Supabase.Realtime` negocia y abre el canal websocket para la tabla solicitada sin arrojar excepciones. No obstante, PostgreSQL nunca emite eventos WAL hacia el slot de replicación para tablas que no pertenezcan a la publicación. Como consecuencia, las pantallas de contactos jamás reciben eventos de inserción, edición o borrado ejecutados desde otros clientes, generando un comportamiento ilusorio de reactividad y manteniendo canales websocket abiertos en vano.

**Riesgo:** Medio en consistencia de visualización multiusuario; degradación de arquitectura por código que presupone reactividad inexistente.

**Solución aplicada:** migración `20260906050000_resolucion_integral_p045_p049_p051_p052_p053.sql` — `ALTER PUBLICATION supabase_realtime ADD TABLE public.contactos_fabricante, public.contactos_proveedor;`. Se optó por la opción 1 (incorporar las tablas), no por retirar las suscripciones.

**Verificado 2026-09-06 contra la base viva:**
```sql
select tablename from pg_publication_tables
where pubname='supabase_realtime' and tablename in ('contactos_fabricante','contactos_proveedor');
```
Ambas tablas aparecen en el resultado. Las suscripciones `Observar("contactos_fabricante", …)` / `Observar("contactos_proveedor", …)` de los ViewModels ahora reciben eventos reales.

Queda sin abordar el punto 3 de la solución diseñada (verificación defensiva en `RealtimeService` para tablas fuera de la publicación) — no es necesario ya para este caso puntual, pero sigue siendo la red de seguridad genérica contra la próxima tabla que alguien suscriba sin publicar primero.

**Estado:** `[x]` Resuelto — verificado en BD. Ver [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]].

---

### ~~P-050~~ · ✅ La clave de caché de catálogos no incluía el tamaño de página — resuelto 2026-09-03

**Archivos:** `CapaDatos/Repositories/Catalogos/CachedCatalogoRepository.cs` (método `ConCache`), `CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs:222`
**Detectado en:** [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]]

`CachedCatalogoRepository.ConCache` decide cachear cuando `page == 1 && termino == ""`, y arma la clave como `catalogos:{tabla}` (o `catalogos:{tabla}:{alcance}`). **El `size` no forma parte de la clave.**

`SelectorCatalogoModal.CargarPaginaAsync` (línea 222) llama `_cfg.Cargar(_query, _page, _cfg.PageSize, ...)`. En la ruta `ForzarPaginacion` —que usa Reportería— la primera carga es exactamente `("", 1, 50)`, que colisiona con la clave que produce la lupa normal al pedir `("", 1, 200)`.

**Caso que falla:** un catálogo con más filas que el `PageSize` paginado pero menos que el umbral de memoria (por ejemplo 120 filas, `PageSize` 50, umbral 200).

1. El usuario abre la lupa normal → `Cargar("", 1, 200)` devuelve 120 filas, `Total = 120`. Como `120 <= 200`, se cachea bajo `catalogos:productos`.
2. En la misma sesión abre el selector de Reportería → `Cargar("", 1, 50)` **pega en la caché** y recibe las 120 filas.
3. `CargarPaginaAsync` hace `Dg.ItemsSource = pagina.Items` → la grilla muestra 120 filas en una página que declara ser de 50, y `TotalPages` calcula 3 páginas sobre un conjunto ya completo.

No se manifiesta cuando el catálogo entra entero en el `PageSize` (ambas llamadas devuelven lo mismo) ni cuando lo excede (con `Total > size` el predicado `esCacheable` impide guardar la entrada paginada).

**Solución aplicada (2026-09-03):** el tamaño pasó a formar parte de la clave. `ConCache` ahora arma `$"{TagsCache.CatalogosRaiz}:{tabla}:{ambito}:{size}"`, con `ambito` = el alcance o `"todos"`. La lupa y el selector paginado quedan en entradas separadas (`catalogos:paises:todos:200` y `catalogos:paises:todos:50`). Las etiquetas no cambian, así que la invalidación por evento y la purga consolidada siguen alcanzando ambas.

Fijado con `BimboProyecto.Tests/Cache/CachedCatalogoRepositoryTests.cs`. La prueba se verificó contra el código defectuoso: con la clave anterior falla (recibe 120 filas donde espera 50 y una sola llamada al repositorio en vez de dos), y pasa con el arreglo.

**Estado:** `[x]` Resuelto — ver [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]]

---

### ~~P-051~~ · ✅ Política `select_Usuarios` con `USING (true)` sobre PUBLIC — resuelto 2026-09-06

**Archivos:** política `select_Usuarios` sobre `public.usuarios` (Supabase, proyecto `bzmmrifjgzlvsphctais`)
**Detectado en:** [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]]

Al auditar por qué fallaban las suscripciones filtradas de notificaciones se inspeccionaron las políticas RLS involucradas. La única política de `public.usuarios` es:

```sql
polname:     select_Usuarios
roles:       {}          -- PUBLIC: aplica a todos los roles
using_expr:  true
```

Es decir, **cualquier usuario autenticado puede leer la tabla `usuarios` completa**, sin acotar a su propia fila ni a su ámbito.

**Cuidado al corregir:** la política es *load-bearing*. La política de `notificaciones_usuario` incluye un subquery `EXISTS (SELECT 1 FROM usuarios u WHERE u.id_usuario = ... AND u.uuid_usuario = auth.uid() AND u.id_estado = 1)` que depende de poder leer esa tabla. Endurecerla sin revisar los dependientes rompería la entrega de notificaciones en tiempo real y probablemente otras rutas.

**Solución aplicada:** migración `20260906050000_resolucion_integral_p045_p049_p051_p052_p053.sql` —
```sql
create policy "select_Usuarios" on public.usuarios
    for select
    using (
        uuid_usuario = auth.uid()
        or private.usuario_tiene_permiso_codigo('USUARIOS_CONSULTAR')
        or private.usuario_tiene_permiso_codigo('USUARIOS_VER')
    );
```
Cubre exactamente el `EXISTS` de la política de `notificaciones_usuario` que era la dependencia *load-bearing*: ese subquery filtra `u.uuid_usuario = auth.uid()`, que también es una de las ramas `OR` de la nueva política — la fila propia sigue siendo legible sin necesidad del `USING (true)` original.

**Verificado 2026-09-06 contra la base viva:**
```sql
select polname, pg_get_expr(polqual, polrelid) as using_expr
from pg_policy where polrelid = 'public.usuarios'::regclass;
```
Devuelve la expresión de arriba, ya no `true`. Confirmado además con `get_advisors` (tipo `security`) que la tabla no quedó marcada como insegura por esta política.

**Estado:** `[x]` Resuelto — verificado en BD. Ver [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]].

---

### ~~P-052~~ · ✅ Trigger de auditoría duplicaba bitácora y chocaba con el RBAC de las RPC `_seguro` de catálogos — resuelto 2026-09-06

**Archivos:** `log_upd_categoria()`, `log_upd_fabricante()` y el trigger `AFTER UPDATE` equivalente de `proveedores` (Supabase, proyecto `bzmmrifjgzlvsphctais`).
**Detectado en:** auditoría de las RPC de catálogos, 2026-09-03. Ver [[Plan de Migración de Presentaciones a RPC segura]].

Las tablas `categoria`, `fabricante` y `proveedores` conservaban su trigger `AFTER UPDATE` de auditoría legacy (`trg_upd_*`) **además** de haber migrado a `actualizar_<x>_seguro` / `cambiar_estado_<x>_seguro`, que ya auditan por su cuenta con `private.registrar_auditoria_rbac`. Consecuencias:

1. **Doble fila de bitácora** por cada actualización o cambio de estado: una desde la RPC (código granular, p. ej. `CATEGORIAS_MODIFICAR`) y otra desde el trigger (`id_accion = 2` = `PRODUCTOS_MODIFICAR`, `id_modulo = 1`).
2. **Conflicto de permiso:** `log_upd_categoria` exige `id_accion = 2` (`PRODUCTOS_MODIFICAR`). Un usuario con el permiso granular (`CATEGORIAS_MODIFICAR` / `CATEGORIAS_DESACTIVAR`) pero sin `PRODUCTOS_MODIFICAR` pasaba el chequeo de la RPC, se ejecutaba el `UPDATE`, y entonces el trigger lanzaba `42501` y **revertía toda la transacción**. La RPC `_seguro` quedaba rota justo para los roles que la motivan.

**Solución aplicada:** migración `20260906050000_resolucion_integral_p045_p049_p051_p052_p053.sql` — `DROP TRIGGER IF EXISTS` sobre `trg_upd_categoria`, `trg_upd_fabricante`, `trg_upd_proveedor` y `trg_upd_proveedores`, y `DROP FUNCTION IF EXISTS` sobre `log_upd_categoria()`, `log_upd_fabricante()`, `log_upd_proveedor()`.

**Verificado 2026-09-06 contra la base viva** (`bzmmrifjgzlvsphctais`, de solo lectura):
```sql
select t.tgname, p.proname
from pg_trigger t
join pg_class c on c.oid = t.tgrelid
join pg_proc p on p.oid = t.tgfoid
where c.relname in ('categoria','fabricante','proveedores') and not t.tgisinternal;
```
Los únicos triggers que quedan en las tres tablas son `trg_categoria_updated_at`, `trg_fabricante_updated_at` y `trg_proveedores_updated_at`, y los tres llaman a la misma función `actualizar_updated_at()` (solo fija `updated_at = now()`, sin bitácora ni chequeo de permiso). Ningún trigger ni función con `PRODUCTOS_MODIFICAR` o `id_accion = 2` sobrevive en el esquema `public`. El doble registro de bitácora y el `42501` que revertía la transacción no ocurren más.

**Estado:** `[x]` Resuelto — verificado en BD. Ver [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]].

---

### ~~P-053~~ · ✅ El alta múltiple de camiones escribía N INSERT sueltos sin transacción — resuelto 2026-09-06

**Archivo:** `CapaUI/Formularios/Principal/Pantallas/Pesaje/PesajeViewModel.cs` — `RegistrarCamionesAsync`
**Detectado en:** [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]] (introducido a conciencia en esa misma sesión)

`RegistroCamionesModal` confirma hasta 5 camiones de una vez, y el ViewModel los persiste con un `CrearCamionAsync` por camión. **No hay transacción**: si la red se corta en el tercero, quedan dos recepciones creadas y tres sin crear.

Es la misma familia que [[Deuda Técnica - Pendientes|P-032]] (`RepartirTaraExtraAsync` y sus N PATCH), y la solución es la misma: una RPC transaccional, en la línea de las que se desplegaron en [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]].

**Mitigación implementada (no lo cierra):**

- El pre-vuelo del modal valida **todo** antes de escribir nada: cupo del andén, duplicados internos, duplicados contra las recepciones abiertas y los largos de campo. El fallo más probable —datos mal cargados— no llega a producir escrituras parciales.
- Si igual se corta, el lote se detiene en el primer error y se informa cuántos entraron ("Se registraron 2 de 5 camiones. …").
- El modal queda abierto con lo que falta, descontándose las filas ya persistidas y refrescando su lista de recepciones abiertas (`AplicarGuardadoParcial`) — sin eso, reintentar duplicaría las que sí entraron.

> [!note] Lo que ya entró NO se deshace, y es deliberado
> Un rollback en la app tendría que anular movimientos recién creados que **quizá ya se están pesando** en otra terminal. Un camión de más se quita con el basurero de su fila, que es una acción normal de la pantalla; una recepción anulada por debajo de un operario que está pesando, no.

**Riesgo:** bajo. El resultado parcial es visible, informado y corregible desde la propia pantalla — no queda estado silenciosamente inconsistente.

**Solución aplicada:** RPC `registrar_camiones_lote_seguro(p_camiones jsonb, p_id_solicitud uuid, p_id_operacion uuid)` en `20260906050000_resolucion_integral_p045_p049_p051_p052_p053.sql` — recorre el arreglo JSON en un solo `LOOP` de PL/pgSQL; una excepción en cualquier fila (proveedor inválido, placa vacía o de más de 20 caracteres, observaciones de más de 500) aborta toda la transacción y no persiste ningún `INSERT` del lote. Registra bitácora por camión con `private.bitacora_pesaje` dentro de la misma transacción. Alias `registrar_camiones_lote` para nombre corto; ambos con `REVOKE ALL FROM public, anon` y `GRANT EXECUTE TO authenticated, service_role`.

Cableado end-to-end verificado en el código: `RegistroCamionesModal.Guardar_Click` → evento `Confirmado` → `PesajeView.AbrirRegistroCamionesModal` (`PesajeView.xaml.cs:667`) → `PesajeViewModel.RegistrarCamionesAsync` → `IPesajeRepository.RegistrarCamionesLoteAsync` → RPC. No quedó como código muerto ejercitado solo por tests: es la ruta real que usa el modal.

**Verificado 2026-09-06:** `get_advisors` (tipo `security`) confirma que solo `authenticated`/`service_role` pueden ejecutar la RPC — sin advisory de `anon_security_definer_function_executable` para ninguna de las dos funciones. `BimboProyecto.Tests/Pesaje/PesajeRepositoryLoteTests.cs` cubre el mapeo del resultado. Build 0/0, suite completa en verde.

> [!note] La mitigación de P-032/P-053 (guardado parcial, `AplicarGuardadoParcial`) sigue en el código
> Con la RPC atómica, una llamada exitosa crea siempre el lote completo y una fallida no crea nada — no hay estado intermedio real. La rama de "guardado parcial" en `PesajeView.xaml.cs` queda como manejo defensivo para el caso `!r.Success` con reintento manual del usuario, no porque el servidor pueda devolver un resultado parcial.

**Estado:** `[x]` Resuelto — verificado en BD y en código. Ver [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]].

---

### ~~P-054~~ · ✅ El debounce estaba copiado a mano en 4 lugares y 3 no disponían el `CancellationTokenSource` — resuelto 2026-09-08

**Archivos:** `CapaUI/Core/Controls/SuggestionDebouncer.cs`, `CapaUI/Core/Controls/SelectorCatalogoModal.xaml.cs`, `CapaUI/Formularios/Principal/Pantallas/Notificaciones/NotificacionesViewModel.cs`
**Detectado en:** [[Sesión 2026-09-08 - Fuga de memoria por contenedor DI y scope de sesión]]

> [!warning] El enunciado original de esta deuda era incorrecto
> Se registró como una fuga de memoria («abandona un CTS por evento»). **No lo es.** Un `CancellationTokenSource` al que nunca se le llama `CancelAfter` ni se le pide el `WaitHandle` no reserva timer ni handle del sistema: es basura recolectable como cualquier objeto. Lo que sí había era **higiene** (`Dispose()` faltante) y, sobre todo, **duplicación**.

El problema real: la misma mecánica de debounce estaba escrita a mano **cuatro veces**, y `SuggestionDebouncer` —la clase que existe justamente para no repetirla, resultado de P-026— **tenía el defecto adentro**.

| Sitio | ¿Disponía el CTS anterior? |
|---|---|
| `GhostTextBox.xaml.cs:164` | ✅ Sí — era la única implementación correcta |
| `SuggestionDebouncer.cs:59` | ❌ No |
| `SelectorCatalogoModal.xaml.cs:326` | ❌ No |
| `NotificacionesViewModel.cs:287` | ❌ No |

**Solución aplicada:** se extrajo `CapaUI/Core/Controls/Debouncer.cs` —la implementación de `GhostTextBox`, que ya era la correcta— y los otros tres pasaron a usarla. El orden `Cancel()` → `Dispose()` es el que exige la documentación de `CancellationTokenSource`: `Cancel` corre las registraciones (completa el `Task.Delay` en vuelo como cancelado) y recién entonces se libera. El reemplazo del token va por `Interlocked.Exchange` porque `NotificacionesViewModel` lo dispara desde el hilo de Realtime.

`SuggestionDebouncer` **conservó su API pública** (`DebounceMs`, `Cancelar()`, `EjecutarAsync(query, buscar, aplicar)`), así que los 9 ViewModels que lo consumen no se tocaron. `GhostTextBox` quedó sin migrar a propósito: ya era correcto y su cancelación se invoca desde varios puntos con semántica propia.

> [!note] Sin cobertura automatizada
> `BimboProyecto.Tests` no referencia `CapaUI` (solo Aplicación/Datos/Dominio), así que `Debouncer` no tiene pruebas. Su verificación es manual: buscadores, lupa de catálogo y campana.

**Estado:** `[x]` Resuelto — build 0/0, 286/286.

---

### ~~P-055~~ · ✅ El apagado del auto-refresh de Gotrue dependía de una llamada de red — resuelto 2026-09-08

**Archivos:** `CapaDatos/Conexion.cs` (el vivo), `ServicioConexión/Conexion/ConexionSupabase.cs` (gemelo), `CapaUI/Formularios/Principal/MainWindow.xaml.cs`
**Detectado en:** [[Sesión 2026-09-08 - Fuga de memoria por contenedor DI y scope de sesión]]

> [!warning] El enunciado original también era impreciso
> Se registró como «`ResetAsync` no libera el `Auth`, el timer enraiza el cliente viejo», como si el timer sobreviviera siempre. **No sobrevive siempre**: `SignOut()` emite `AuthState.SignedOut` y `TokenRefresh` detiene el timer. El defecto está en **de qué depende** ese apagado.

`SignOut()` es una **llamada de red** envuelta en `try/catch` dentro de `MainWindow.LimpiarRecursosAsync`. Logout sin conexión, endpoint lento o token ya inválido ⇒ no hay evento `SignedOut` ⇒ el timer sobrevive al cierre de sesión y **sigue pidiendo tokens de una sesión que ya no existe**. No había ningún apagado local determinista.

**Dos defectos concretos encontrados de paso:**

1. **El timeout del logout era decorativo.** Había un `using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5))` que **no se le pasaba a nada**: `IGotrueClient.SignOut(SignOutScope)` no acepta `CancellationToken` (verificado en la interfaz de `supabase.gotrue 6.0.3`). El `catch (OperationCanceledException)` con el mensaje «SignOut timeout — continuando» era inalcanzable por timeout, y con la red colgada el cierre se iba hasta el timeout por defecto de `HttpClient` (100 s) con la UI congelada detrás de un `await` en un `async void`.
2. **El comentario de `ResetAsync` prometía de más.** Decía que liberaba «el cliente Supabase completo». `Supabase.Client` **no implementa `IDisposable`** — la lista de miembros de `M:Supabase.Client.*` en el XML de `Supabase 1.1.1` es `#ctor`, `AdminAuth`, `From`, `GetAuthHeaders`, `InitializeAsync` y `Rpc`. No existe tal liberación.

**Solución aplicada:**

- `ResetAsync()` llama `old.Auth.Shutdown()` en su propio `try` antes de tocar el socket. Su documentación dice literalmente que detiene el hilo de fondo que refresca el token, y **no toca la red**: es el único apagado que no depende de que algo remoto funcione. Va en su propio `try` para que un fallo ahí no impida disponer el socket. Aplicado en las **dos** copias del archivo (ver P-056).
- `LimpiarRecursosAsync` impone el tope por fuera con `Task.WhenAny(signOut, Task.Delay(5s))`, observa la excepción del `SignOut` abandonado para que no quede sin manejar, y pasó de `Debug.WriteLine` a Serilog como el resto del cierre. Se mantiene el `SignOut` porque invalida el refresh token en el servidor, que importa en terminal compartida — pero ya no puede bloquear el cierre.
- Se corrigió el comentario de `ResetAsync` para que diga lo que realmente apaga.

**Verificación pendiente (manual):** cerrar sesión **con la red cortada**. Esperado: el logout no se cuelga, avisa por log que `SignOut` no respondió y vuelve al login.

**Estado:** `[x]` Resuelto — build 0/0, 286/286.

---

### P-056 · `ServicioConexión` es un proyecto huérfano que duplica el singleton de conexión con el mismo namespace

**Archivos:** `ServicioConexión/ServicioConexión.csproj`, `ServicioConexión/Conexion/ConexionSupabase.cs`
**Detectado en:** [[Sesión 2026-09-08 - Cierre de P-054 y P-055]]

Hay **dos** clases `ConexionSupabase` en el **mismo namespace** (`ServicioConexión.Conexion`), en ensamblados distintos:

- `CapaDatos/Conexion.cs` — **el vivo**. `CapaUI` referencia solo `CapaDominio`, `CapaAplicacion` y `CapaDatos`, así que este es el que se compila y el que usan los repositorios, `RealtimeService` y `MainWindow`.
- `ServicioConexión/Conexion/ConexionSupabase.cs` — **huérfano**: está en la solución y compila, pero **ningún `.csproj` lo referencia**.

**Riesgo:** es una trampa activa, no código inerte. El nombre del proyecto y el namespace hacen que parezca el archivo correcto; alguien (persona o agente) puede arreglar ahí un bug y ver que no cambia nada en ejecución — estuvo a punto de pasar al cerrar P-055.

**Mitigación aplicada:** el fix de P-055 se aplicó en **ambas** copias para que no divergan, como pide el comentario del propio archivo vivo.

**Solución propuesta:** sacar `ServicioConexión` de la solución y borrarlo, dejando una sola copia. Es un cambio estructural, así que se pospuso a conciencia (decisión de Fernando, 2026-09-08) para no chocar con otros agentes trabajando sobre el mismo árbol.

**Estado:** `[ ]` Pendiente

---

### ~~P-057 · 16 modales y 12 vistas sin previsualizarse en el diseñador de Visual Studio~~ ✅ Resuelto 2026-09-10

**Archivos:** los `*Modal.xaml` de `CapaUI/Formularios/Principal/Pantallas/**` menos `ProductosCargaModal` (ya migrado)
**Detectado en:** [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] (2026-09-09)

> [!check] Cierre de los modales — 2026-09-10
> Los **17 controles de tipo modal** (los 14 con el binding de raíz + `SelectorCatalogoModal` + `ConfiguracionEmpresaModal` + `RolModal`) más `WelcomeScreen` y `ConstructionScreen` se migraron aplicando la receta:
> 1. **Paso 1** — se sacó el `MaxWidth`/`MaxHeight` del raíz. El límite lo pone el host: `PesajeView.MostrarModal` ya llamaba `LimitarAlOverlay`; para las 9 vistas de catálogo se agregó `CapaUI.Core.ModalLayout.LimitarAlOverlay(modal, ModalOverlay)` (helper compartido nuevo, `CapaUI/Core/ModalLayout.cs`) al principio de cada `MostrarModal`. `ConfiguracionEmpresaModal` usa `MaxWidth`/`MaxHeight` **literales**, no necesita host.
> 2. **Paso 2** — `Converter={StaticResource RestarMargen}` → `{x:Static conv:RestarMargenConverter.Instancia}` en los 14. Ídem `BoolToVisibility` → `{x:Static conv:BoolToVisibilityConverter.Instancia}` en `ConfiguracionEmpresaModal` (se le agregó el singleton `Instancia`).
> 3. **Paso 3** — merge de `Styles.xaml` (forma corta) donde faltaba (`SelectorCatalogoModal`).
> 4. **Paso 4** — constructor sin parámetros en los que no lo tenían (todos menos `FormatoReporteModal`, `Camion`, `Registro`, `Fabricante`, `Producto`, que ya lo tenían de un pase anterior). El de `WelcomeScreen` además guarda `DesignerProperties.GetIsInDesignMode` antes de enganchar el `Loaded` que habla con `App.Services`.
>
> **Verificación:** arnés WPF con `new Application()` de `Resources` vacío (la condición del subrogado del diseñador), instanciando cada control por su ctor sin parámetros y midiéndolo. **19/19 renderizan a tamaño real, 0 colapsan a 0×0, 0 excepciones.** Build de la solución 0/0, suite 344/344.
>
> **Vistas — cerradas también el 2026-09-10.** Las 12 `*View.xaml` necesitaban **dos** cosas, no una (el barrido del 2026-09-09 solo vio la primera porque el parseo corta en la primera clave que falta):
> - **Merge de `Styles.xaml`** (paso 3): las 4 sin `<UserControl.Resources>` (`Categorias/Fabricantes/Presentaciones/Proveedores`) lo reciben directo; las 8 con recursos locales se envolvieron en `<ResourceDictionary>` + `MergedDictionaries`.
> - **Converters de `App.xaml` a `{x:Static}`** (paso 2): las vistas usan `AnchoMinimoAVisibilidad`, `TextoVacioConverter`, `BoolToVisibility`, `InverseBoolToVisibility` y (en `ReporteriaView`) `RestarMargen` — todos vía `{StaticResource}` a una clave de `App.xaml`. Se les agregó el singleton `Instancia` a los 5 converters y se cambiaron los usos a `{x:Static conv:XConverter.Instancia}`.
>
> Con eso el arnés da **32/32** (19 modales/pantallas + 13 vistas), 0 colapsos, 0 excepciones. Ya se puede **sacar los 6 converters de `App.xaml`** — quedan definidos como singleton en su propia clase y ningún XAML los referencia por clave. `RestarMargen` incluido.

Los 16 modales del proyecto llevan en su elemento raíz `MaxWidth`/`MaxHeight` con `Converter={StaticResource RestarMargen}` y `RelativeSource AncestorType=Border`. **Eso son dos problemas encimados**: el `{StaticResource}` revienta el parseo, y una vez arreglado eso el binding engancha un `Border` del propio Visual Studio, mide 0 y colapsa el modal a 0×0 — en silencio, porque `FallbackValue` nunca entra. `RestarMargen` vive en `App.xaml`. Como los atributos del elemento raíz se aplican **antes** de que se pueble su propio `<UserControl.Resources>`, ese `{StaticResource}` no se puede resolver localmente y el diseñador —que no ejecuta `App.xaml`— revienta al parsear: lienzo en blanco y `TaskCanceledException` en Salida. El detalle está en [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]].

`ProductosCargaModal` se migró como piloto el 2026-09-09 (commit `11f0366`). Un barrido del ensamblado ese mismo día mostró que son **dos problemas distintos**:

- **16 modales**: el bloqueo previo es que **ninguno tiene constructor sin parámetros**, así que el diseñador ni llega a instanciarlos. Recién después pesa el `{StaticResource}` del raíz.
- **12 vistas** (`CategoriasView`, `ProductosView`, `PesajeView`, `ReporteriaView`…): ya tienen ctor y **no** usan el converter en el raíz. Fallan en contenido interno (`IconTag`, `IconBox`, `CeldaCentrada`) por falta del merge de `Styles.xaml`. Excepción: `ReporteriaView` y `UniversalSearchView` fallan en `BoolToVisibility`, que está en `App.xaml` y no en `Styles.xaml`.

**Riesgo:** bajo y acotado a productividad — no afecta runtime ni datos. Pero es persistente: cada vez que alguien abre uno de esos `.xaml` en el diseñador pierde el rato hasta acordarse de que "eso no anda", y ya se gastaron dos intentos fallidos de arreglarlo apuntando a la causa equivocada.

**Solución:** aplicar la receta y las dos tablas de réplica de [[Anatomia compartida de los modales]] (sección *Que el modal se vea en el diseñador de Visual Studio*). Las vistas son la fruta al alcance de la mano: una línea de merge cada una. Cuatro de los modales (`RegistroCamionesModal`, `CamionModal`, `ProductoModal`, `FabricanteModal`) además tenían `App.Services.GetRequiredService<ICatalogoRepository>()` en el constructor, contra la regla 9 de `AGENTS.md` — el pase de 2026-09-10 los dejó con ctor sin parámetros y sin DI en el ctor de diseño.

**Estado:** `[x]` Resuelto 2026-09-10 — 17 modales + 2 pantallas + 13 vistas, verificado con arné (**33/33**, incluye `RolesView`). Los **6 converters salieron de `App.xaml`** y de `DesignTimeResources.xaml` en el mismo pase: todos tienen `public static readonly X Instancia` y ningún `.xaml` los referencia por clave (`MainWindow`, `RolesView`, `RegistroCamionesModal`, `RolesResources` migrados). Sesión: [[Sesión 2026-09-10 — Cierre de P-057 (previsualización de UI en el diseñador)]]. Convenciones consolidadas en [[Convenciones de UI (WPF) — leer antes de tocar XAML]]. **Pendiente de prueba manual de Fernando** (los converters se usan app-wide; el arné no cubre runtime ni `{StaticResource}` en templates diferidos).

---

### ~~P-058 · El módulo de Login sin ViewModel y con llamadas directas a Supabase desde la UI~~ ✅ Resuelto 2026-09-09

**Archivos:** `CapaUI/Formularios/InicioSesion/` (los cuatro code-behind), `CapaAplicacion4/Auth/Interfaces/`, `CapaDatos/Auth/`
**Detectado en:** auditoría de módulos del 2026-09-09

Eran **dos problemas encimados**, y el segundo era el grave:

1. `LoginWindow` no tenía ViewModel: 858 líneas de lógica de autenticación en code-behind, manipulando el árbol visual por `x:Name`. El flujo no se podía probar sin levantar una `Window`.
2. Los tres paneles de recuperación **le hablaban directo a Supabase desde la capa de UI** (`ConexionSupabase.GetClientAsync()` → `client.Auth.ResetPasswordForEmail` / `VerifyOTP` / `Update`), salteando `CapaAplicacion` y `CapaDatos` — y además a través del proyecto huérfano de P-056.

**Resolución**

- **Capa de aplicación:** contrato nuevo `IRecuperacionPasswordService` (`EnviarCodigoAsync`, `VerificarCodigoAsync`, `CambiarPasswordAsync`), devolviendo `Result` como `IAuthService`.
- **Capa de datos:** `RecuperacionPasswordService` contra Gotrue, con la misma convención de log interno y mensaje corto al usuario. Registrado en DI.
- **UI:** `LoginViewModel` para el ingreso y `SolicitarCodigoViewModel` / `VerificarCodigoViewModel` / `NuevaPasswordViewModel` para los tres pasos. Todos **sin un solo tipo de WPF**. `LoginWindow` recibe el servicio por constructor y se lo presta a los paneles (regla 9).

> [!check] Evidencia de verificación
> - **58 pruebas nuevas** en `BimboProyecto.Tests/Auth/` (26 de ingreso + 32 de recuperación). Suite completa: **344/344**.
> - `grep -rn "ConexionSupabase\|client.Auth" CapaUI/` sobre `InicioSesion/`: **cero coincidencias**.
> - Build: 0 errores, 0 advertencias.
> - **La separación se verifica sola:** los ViewModels se enlazan al proyecto de pruebas, que apunta a `net8.0` y no a `net8.0-windows`. Si alguien les mete un `Visibility` o un `Brush`, las pruebas dejan de compilar — no depende de que alguien lo revise.

**Lo que se dejó en el code-behind a propósito:** chrome de la ventana, efectos de foco, spinners, bitmap del logo y el intercambio `PasswordBox`/`TextBox`. `PasswordBox` no se puede bindear — es una decisión de seguridad de WPF —, así que la vista empuja la contraseña al ViewModel.

**Queda fuera de este ítem:** `MainWindow.xaml.cs:717` sigue llamando a `ConexionSupabase` directo para el cierre de sesión. Es otro módulo y otro flujo; se anota acá para que no se pierda.

**Estado:** `[x]` Resuelto — **pendiente de prueba manual de Fernando**: login real, login con credenciales malas, y el flujo completo de recuperación con un correo de verdad.

---

### P-059 · La recuperación de contraseña no verifica que la cuenta esté habilitada

**Archivos:** `CapaDatos/Auth/RecuperacionPasswordService.cs`
**Detectado en:** al poner el flujo detrás de un contrato (P-058), 2026-09-09

`AuthService.LoginAsync` rechaza a los usuarios deshabilitados (`usuario.idEstado != 1` → `SignOut` y error). **El flujo de recuperación no hace esa verificación**: una cuenta deshabilitada puede pedir un código, verificarlo y cambiarse la contraseña.

**Riesgo: acotado, no es una brecha activa.** Aunque cambie su contraseña, el usuario deshabilitado **sigue sin poder entrar** — `LoginAsync` lo frena igual. Lo que sí permite es que una cuenta dada de baja consuma envíos de correo y OTP, y deja el flujo sin defensa en profundidad: si alguien alguna vez relaja el chequeo del login, este camino ya estaría abierto.

**Solución:** verificar el estado de la cuenta en `VerificarCodigoAsync` (o antes, en `EnviarCodigoAsync`), reusando `RepositorioUsuario`. Ojo con no romper la no-enumeración de cuentas: el mensaje al usuario tiene que seguir siendo el mismo exista o no la cuenta.

**No se cerró en el mismo pase porque cambia el comportamiento** (alguien deshabilitado dejaría de poder resetear) y eso es decisión del dueño del producto, no un efecto colateral de un refactor. Se dejó planteado explícitamente a Fernando el 2026-09-09.

**Estado:** `[ ]` Pendiente

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
| P-025 | Repositorios de movimientos duplicados sin uso | ✅ Resuelto | [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]] |
| P-026 | Puente VM → SuggestionSearchBox duplicado 9× | ✅ Resuelto | [[Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)]] |
| P-027 | Verificación funcional del RBAC con rol Consulta | ✅ Resuelto | [[Sesión 2026-08-09 - Implementación RBAC visual y gestión de roles]] |
| P-028 | Verificar en runtime el rediseño de Roles | `[ ]` Pendiente | [[Sesión 2026-08-11 - Rediseño de Gestión de Roles]] |
| P-029 | Cancelación ausente en 7 ViewModels + timer fantasma | ✅ Resuelto | [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]] |
| P-030 | Verificación en runtime de la pantalla de Roles | `[ ]` Pendiente | [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]] |
| P-031 | Frenos de rendimiento de toda la aplicación | ✅ Resuelto (11/11) | [[Sesión 2026-09-06 - Cierre integral de Deuda Tecnica P-031 y P-042]] |
| P-032 | Reparto de tara extra sin transacción (N updates) | ✅ Resuelto | [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]] |
| P-033 | Verificar si el trigger de pesajes cubre UPDATE | `[x]` Resuelto 2026-08-24 | [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]] |
| P-034 | Invalidación de caché sobre tablas no publicadas en Realtime | ✅ Resuelto | [[Sesión 2026-09-06 - Cierre Cuatro Entregables P-034 P-037 P-039 P-042 P-047]] |
| P-035 | Configuración de empresa lista; validar flujo manual y trigger de `updated_at` | `[~]` Parcial | [[Sesión 2026-08-14 - Módulo de configuración de empresa y tema dinámico]] |
| P-036 | Tara y Presentaciones sin pantalla CRUD — combo de unidad filtrado a masa sin dónde vivir | `[~]` Parcial — Presentaciones ✅, Tara pendiente | [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]] |
| P-037 | Paginación: code-behind duplicado 9× sin clamp de `Page` ni binding del resaltado | ✅ Resuelto | [[Sesión 2026-09-06 - Cierre Cuatro Entregables P-034 P-037 P-039 P-042 P-047]] |
| P-038 | Modelos C# desalineados del esquema + `ErrorCarga` no llega al usuario | ✅ Resuelto | [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]] |
| P-039 | Búsqueda sin tildes solo en Productos — faltan 7 tablas | ✅ Resuelto | [[Sesión 2026-09-06 - Cierre Cuatro Entregables P-034 P-037 P-039 P-042 P-047]] |
| P-040 | Carga inicial de `icono_sidebar` pendiente en Storage | `[x]` Resuelto | [[Sesión 2026-08-15 - Icono dinámico del sidebar]] |
| P-041 | Conteos de Fabricantes/Categorías filtrados por estado — las 3 pastillas dejan de informar | ✅ Resuelto | [[Sesión 2026-09-06 - Auditoría del cierre masivo P-025 P-029 P-031 P-032 P-038 P-041]] |
| P-042 | `PesajeModalStyles.xaml` duplica `ModalInput`/`ModalCombo`/`ModalSegBtn` sin foco ni validación por campo | ✅ Resuelto | [[Sesión 2026-09-06 - Cierre integral de Deuda Tecnica P-031 y P-042]] |
| P-043 | `ModalInput`/`InputBox` globales: mismo bug de `VerticalAlignment` fijo que ya se corrigió en `MInput` | `[x]` Resuelto | [[Sesión 2026-09-06 - Tres frenos de rendimiento cerrados y VerticalAlignment fijo en ModalInput]] |
| P-044 | Multiselección de `SelectorCatalogoModal` no responde a teclado (Space no tilda el checkbox) | `[x]` Resuelto — Space conmuta / confirma y Enter atajo rápido | [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] |
| P-045 | Campos de texto de Pesaje sin límites en UI, Dominio ni BD | `[x]` Resuelto — las 3 tablas en las 3 capas | [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]] |
| P-046 | Validación visual de descripciones de estado de Pesaje en Bitácora | `[ ]` Pendiente | [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]] |
| P-047 | Divergencia de diseño y comportamiento entre `ModalInput` e `InputBox` | ✅ Resuelto | [[Sesión 2026-09-06 - Cierre Cuatro Entregables P-034 P-037 P-039 P-042 P-047]] |
| P-048 | Fuga de datos y permisos entre sesiones en terminal compartida (CatalogoCache / RolPermiso) | `[x]` Resuelto | [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]] |
| P-049 | Suscripciones Realtime inactivas en Contactos (tablas no publicadas en publicación) | `[x]` Resuelto | [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]] |
| P-050 | Clave de caché de catálogos sin el tamaño de página (colisión lupa 200 / paginado 50) | `[x]` Resuelto | [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]] |
| P-051 | Política `select_Usuarios` con `USING (true)` sobre PUBLIC | `[x]` Resuelto | [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]] |
| P-052 | Trigger de auditoría legacy duplicaba bitácora y chocaba con el RBAC de las RPC `_seguro` (categoría/fabricante/proveedor) | `[x]` Resuelto | [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]] |
| P-053 | Alta múltiple de camiones: N INSERT sueltos sin transacción (misma familia que P-032) | `[x]` Resuelto | [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]] |
| P-054 | Debounce copiado a mano en 4 lugares; 3 sin `Dispose()` del CTS (no era fuga: duplicación) | `[x]` Resuelto | [[Sesión 2026-09-08 - Cierre de P-054 y P-055]] |
| P-055 | El apagado del auto-refresh de Gotrue dependía de `SignOut()` (llamada de red) + timeout decorativo en el logout | `[x]` Resuelto | [[Sesión 2026-09-08 - Cierre de P-054 y P-055]] |
| P-056 | `ServicioConexión` huérfano duplica `ConexionSupabase` con el mismo namespace | `[ ]` Pendiente | [[Sesión 2026-09-08 - Cierre de P-054 y P-055]] |
| P-057 | 16 modales y 12 vistas sin previsualización en el diseñador de VS | `[x]` Resuelto 2026-09-10 (33/33 en arné; converters fuera de App.xaml) | [[ADR-028 - Previsualizacion de UserControls en el disenador de VS]] |
| P-058 | Login sin ViewModel y paneles de recuperación llamando a Supabase desde la UI | ✅ Resuelto | 58 pruebas nuevas; suite 344/344 |
| P-059 | La recuperación de contraseña no verifica que la cuenta esté habilitada | `[ ]` Pendiente | Detectado al resolver P-058 |

---

## Relaciones

- [[Plan de CI-CD y Actualizaciones Remotas]] — backlog de entrega futura y decisiones pendientes; no implementado, no cierra deudas P-NNN
- [[Módulo Productos]] — módulo auditado
- [[Sesión 2026-05-28 - Fix Búsqueda Multi-Campo Productos]] — sesión donde se realizó la auditoría
- [[Paginación y Búsqueda - Arquitectura Detallada]] — arquitectura de referencia
- [[Sesión 2026-07-23 - Reconciliación Animación Sidebar (ContentAreaBorder) y Regresión BrandBlock]] — origen de P-009 a P-012
- [[Sesión 2026-07-23 - Revisión QA Módulo Usuarios y Refactor de Sesión (Emanuel)]] — origen de P-013 a P-021
- [[Sesión 2026-07-28 - Fix Refresco del Popup de Sugerencias (9 módulos)]] — origen de P-026
- [[Sesión 2026-07-28 - Refactor del Buscador de Sugerencias (P-026)]] — resolución de P-026
- [[Sesión 2026-08-09 - Implementación RBAC visual y gestión de roles]] — origen de P-027
- [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]] — origen de P-034
- [[Sesión 2026-08-14 - Realtime en columnas de join de Productos]] — resolución parcial de P-034
- [[Sesión 2026-08-14 - Logo de empresa dinamico en login]] — origen de P-035
- [[Sesión 2026-08-14 - Módulo de configuración de empresa y tema dinámico]] — resolución parcial de P-035
- [[Sesión 2026-08-14 - Catalogo de unidad_medida con categoria]] — origen de P-036
- [[Sesión 2026-08-14 - Regresion la grilla mostraba la pagina anterior]] — origen de P-037
- [[Sesión 2026-08-14 - Modelo desalineado del esquema tumbaba paginas enteras]] — origen de P-038
- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] — origen de P-039
- [[Sesión 2026-08-15 - Icono dinámico del sidebar]] — origen de P-040
- [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] — origen de P-042, P-043 y P-044
- [[Anatomía compartida de los modales]] — tabla de estilos globales contra la que se auditó P-042
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] — tres capas de validación y reglas de dominio
- [[Sesión 2026-09-02 - Validación de longitud máxima en campos de texto]] — origen de P-047 y actualización de P-042/P-045
- [[ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime]] — diseño integral de caché L1 e invalidación reactiva que mitiga P-048 y P-049
- [[Plan de Migración de Presentaciones a RPC segura]] — origen de P-052; migración de Presentaciones a la familia `_seguro`

- [[Sesión 2026-09-03 - Implementación de ADR-026 y socket Realtime autenticado]] — origen de P-050 y P-051
- [[Sesión 2026-09-05 - Alta múltiple de camiones y topes de texto en movimientos]] — origen de P-053; resolución parcial de P-045 y agravamiento de P-042
- [[Sesión 2026-09-06 - Resolucion integral P-045 P-049 P-051 P-052 P-053]] — resolución de P-045 (RPC), P-049, P-051, P-052 y P-053
- [[Sesión 2026-09-08 - Fuga de memoria por contenedor DI y scope de sesión]] — origen de P-054 y P-055
- [[Sesión 2026-09-08 - Cierre de P-054 y P-055]] — resolución de P-054 y P-055; origen de P-056
- [[Sesión 2026-09-06 - Tres frenos de rendimiento cerrados y VerticalAlignment fijo en ModalInput]] — cierra G7/G8/G9 de P-031 y resuelve P-043
