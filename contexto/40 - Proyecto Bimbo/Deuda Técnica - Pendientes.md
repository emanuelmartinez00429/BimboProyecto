---
title: Deuda Técnica — Pendientes
type: deuda
status: vigente
tags:
  - pendiente
  - deuda-tecnica
  - auditoria
date: 2026-05-28
updated: 2026-08-23
summary: "Registro de la deuda técnica abierta P-NNN del portal. Los ítems ya cerrados se archivan en Deuda Técnica - Resueltas 2026."
summary_fijo: true
scope:
  - CapaDatos/Modelados
  - CapaDatos/Realtime
  - CapaDatos/Repositories/Categorias
  - CapaDatos/Repositories/Fabricantes
  - CapaDatos/Repositories/Pesaje
  - CapaDatos/Repositories/Productos
symbols:
  - ActualizarTaraExtraEntradaAsync
  - AddScoped
  - AnimarStep
  - Bitacora
  - BitacoraViewModel
  - BrandBlock
  - CalcularPaginas
  - CanExecute
  - CancellationTokenSource
  - CargarDatosAsync
---

# Deuda Técnica — Pendientes

> [!info] Origen
> Encontrados en auditoría del 2026-05-28 antes de replicar el módulo Productos como plantilla.
> Ver [[Sesión 2026-05-28 - Fix Búsqueda Multi-Campo Productos]].

> [!todo] Pendiente de diseño (no es deuda de código)
> [[Pendiente - Servicio Genérico de Validaciones y Pruebas Caja Negra]] — servicio aún no implementado, registrado 2026-06-10.

---

> [!info] Los ítems resueltos se archivaron
> Este documento contiene **solo la deuda abierta**. El historial completo de ítems cerrados vive en
> [[Deuda Técnica - Resueltas 2026]]. La tabla de historial al final sigue listando **todos** los `P-NNN`,
> abiertos y cerrados, para poder consultar el estado de cualquiera de un vistazo.

---

## 🔴 Críticos — resolver ANTES de replicar el módulo

Estos errores se propagarán en cascada a cada módulo nuevo si no se corrigen primero.

### P-023 · 🔴 Catálogo de taras con datos de prueba — afecta el peso que se le paga al proveedor

**Tablas:** `tara`, `productos.peso_teorico`
**Detectado en:** [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]]

La tabla `tara` tiene **una sola fila**: 20 kg, descripción *"tara de 20 kg"*, usada por **503 de 505 productos**. Pero **500 de esos productos tienen `peso_teorico` menor a 10 kg** — el empaque pesaría el doble o más que el producto que contiene. Los 5 restantes (1,000 a 10,000 kg) tienen nombres tipo *"Producto Number 1"*, *"Producto bien five"*.

Además existe una tabla `tarima` aparte con su propio `peso_tarima`, y `entradas_producto` ya tiene `id_tarima` — o sea las tarimas se manejan por separado, lo que refuerza que ese 20 kg no es un empaque real.

**Riesgo:** la tara se resta del peso bruto para calcular el neto, y **el neto es lo que se le paga al proveedor**. Con datos de relleno, todo cálculo de recepción es incorrecto. Además el indicador de bultos teóricos (Fase 8) va a dar números sin sentido hasta que se cargue el catálogo real.

**Solución:** poblar `tara` con los empaques reales y asignar el `id_tara` correcto a cada producto. Verificar también los `peso_teorico` de los 5 productos de prueba.

**Estado:** `[ ] Pendiente — requiere datos de planta`

---

### P-032 · 🔴 El reparto de tara extra no es transaccional (N updates sueltos)

**Detectado en:** [[Sesión 2026-08-13 - Pesaje solo bruto y tara extra pesada]]

`PesajeViewModel.RepartirTaraExtraAsync` reparte un total entre N pesadas con **N PATCH independientes** de PostgREST (`ActualizarTaraExtraEntradaAsync`, uno por entrada). No hay transacción: si la red se corta a mitad, unas filas quedan con la cuota nueva y otras con la vieja, y `Σ entradas` deja de ser el total real.

**Mitigaciones ya implementadas:**
- **Pre-vuelo**: se validan las N pesadas contra el `CHECK peso_neto > 0` **antes** de escribir ninguna, así el fallo más probable (una pesada de bruto chico) no produce escrituras parciales.
- **Auto-reparable**: como el total se deriva de `Σ entradas`, reabrir el modal muestra el total real que quedó; volver a aplicar reparte todo desde cero. No hay estado que reconciliar.
- Se avisa por Toast cuántas filas fallaron.

**Solución de fondo:** un RPC de Postgres que reciba `(id_mov_producto | id_movimiento, total)` y haga el reparto en una sola transacción del lado del servidor.

**Estado:** `[ ] Pendiente`

---

### P-038 · 🔴 Modelos C# desalineados del esquema + el error de carga no llega al usuario

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

**Estado:** `[ ] Pendiente`

---

## 🟡 Importantes — no bloquean pero generan deuda en cascada

### P-046 · 🟡 El repositorio vive dentro de OneDrive y compite con Obsidian por los archivos

**Detectado en:** auditoría de la bóveda del 2026-08-23.

El repo está en `C:\Users\fbara\OneDrive\Desktop\Proyecto de BIMBO`. OneDrive sincroniza esa carpeta al mismo
tiempo que Obsidian escribe en `contexto/` y que git escribe en `.git/`. El síntoma ya está documentado en el
código: el comentario de `.claude/hooks/vault-trigger.js` dice que el archivo *«parpadea» entre versiones por
conflicto de sync con Obsidian*, y por eso ese hook necesita **tres niveles de respaldo** (`origin/master`
recién fetcheado → `HEAD` local → archivo en disco) para leer un solo archivo con confianza.

Es una condición de carrera permanente, no un caso borde: tres procesos escribiendo la misma carpeta sin
coordinación. Además, cada worktree de agente bajo `.claude/worktrees/` es una copia completa del repo que
OneDrive también sincroniza.

**Solución de fondo:** mover el repo fuera de OneDrive — a otra unidad si existe, o a `C:\dev\BimboProyecto`.
Requiere: clone limpio en el destino, reabrir `contexto/` como bóveda en Obsidian, y actualizar la ruta del
MCP `obsidian` en `~/.codex/config.toml`. Los tres respaldos del hook pueden quedarse como cinturón de
seguridad, pero dejarían de ser la única defensa.

**Riesgo:** medio. No corrompe datos hoy, pero hace que ningún agente pueda confiar en una sola lectura del
disco — que es exactamente lo contrario de la regla de fuente viva.

**Estado:** `[ ] Pendiente`

---

### ~~P-034~~ · 🟡 Invalidación de caché apoyada en tablas que no publican en Realtime — parcialmente resuelto 2026-08-14

**Detectado en:** [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]]

`ProductosViewModel.cs:238` hacía `Observar("fabricante", _ => CatalogoCache.Invalidar("fabricantes"))`, pero la tabla no estaba en la publicación de Realtime. Verificado contra la base en su momento:

```sql
select tablename from pg_publication_tables where pubname = 'supabase_realtime';
-- categoria, empleados, entradas_producto, movimiento_productos,
-- movimientos, paises, productos, usuarios
```

Faltaban `presentacion_producto`, `fabricante`, `proveedores` y `tara`. **Ese handler no se ejecutaba nunca** y nadie se había dado cuenta: el modo de falla es silencioso.

Había además un desalineo latente: `RealtimeService.cs:38` mapeaba la PK bajo la clave `"taras"`, pero la tabla real se llama `tara`.

**Resuelto en [[Sesión 2026-08-14 - Realtime en columnas de join de Productos]]**, a raíz de un reporte relacionado pero distinto (las columnas de la grilla de Productos que salen de un join no se actualizaban en vivo). Se hicieron los dos primeros pasos de la solución de fondo:

1. ✅ `ALTER PUBLICATION supabase_realtime ADD TABLE fabricante, proveedores, presentacion_producto, tara, unidad_medida;` — migración `publicar_catalogos_en_realtime` aplicada y verificada (8 → 13 tablas).
2. ✅ Corregido `"taras"` → `"tara"` en `_pkColumns`, y agregado `["unidad_medida"] = "id_unidad"`.
3. ⬜ **Sigue pendiente.** Un suscriptor **de vida larga** a nivel de aplicación: `RealtimeService` cierra el canal con el último suscriptor, y los `Observar` viven en los ViewModels, así que un cambio hecho con **todas** las pantallas relevantes cerradas no lo escucharía nadie. No bloqueó el caso de Productos porque su suscripción vive con la pantalla y `CargarDatosAsync` reconsulta al reabrirla — pero si en el futuro otra caché global (no acotada a una pantalla activa) necesita invalidación por Realtime, este punto 3 vuelve a ser necesario.

`CatalogoCache` (la caché de las lupas) sigue sin depender de esto — [[ADR-015 - Cache de catalogos mostrar y revalidar]] la revalida en cada apertura por diseño, independientemente de si la publicación está al día.

**Estado:** `[~] Parcialmente resuelto — falta el punto 3 (suscriptor de vida larga), solo si se necesita`

---

## 🟢 Menores — aceptables por ahora

### P-006 · Lógica Realtime acoplada a paginación en el ViewModel

**Archivo:** `CapaUI/.../Productos/ProductosViewModel.cs`
**Método:** `OnCambioProducto` (~120 líneas)

Contiene lógica muy específica de "¿estoy en la última página?", "¿el INSERT puede caer en esta página?". Es correcta para Productos, pero si se copia a un módulo sin paginación o con distinta lógica de páginas, producirá bugs sutiles.

**Estado:** `[ ] Aceptar como deuda — documentar en cada módulo nuevo los puntos que deben adaptarse`

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
**Detectado en:** [[Sesión 2026-08-11 - Rediseño de Gestión de Roles]]

La pantalla se reescribió por completo pero **no se vio corriendo**: Visual Studio y la app estaban abiertos bloqueando los DLL de salida. Solo se comprobó que compila (0 errores) y que los BAML se generan.

Quedan dos verificaciones:

1. **Maquetado** — que el layout caiga como el mockup, en particular el `SpanningGridPanel` (grilla de 4 columnas en vista compacta / 2 en detalle, con el módulo ancho ocupando 2). Incluye probar el corte de responsividad: angostar la ventana hasta que aparezca la barra horizontal y confirmar que el texto de las tarjetas **no** se recorta ni antes ni después del corte (`MinColumnWidth="220"`; si 220 queda corto o sobra, es el número a ajustar).
2. **Indicador de carga** — que el spinner con leyenda "Cargando roles…" aparezca y desaparezca bien. El esqueleto con shimmer se probó y **se revirtió** el mismo día (ver [[WPF - Esqueleto con Shimmer (Skeleton Loading)]]); Productos volvió a su spinner original sin cambios.

**Estado:** `[ ] Pendiente`

---

### P-029 · Cancelación ausente en 7 ViewModels + timer fantasma del timeout

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

**Estado:** `[ ] Pendiente`

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

### P-031 · Frenos de rendimiento de toda la aplicación

**Detectado en:** [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]] (auditoría completa de CapaUI)

Hallazgos fuera de Roles, **no atacados** por decisión de alcance. Ordenados por impacto:

| | Hallazgo |
|---|---|
| **G1** | **Login: ~2,7 s de `Task.Delay` artificiales, en serie con la red** (`LoginWindow.xaml.cs:163-212`). `AnimarStep` se espera *antes* de `LoginAsync()`. Además 103 `Dispatcher.Invoke` innecesarios (ya está en el hilo UI). |
| **G2** | **`bimbo-logo.png` es 3000×1391 y se muestra a 50 px** (`MainWindow.xaml:151`), sin `DecodePixelWidth` → ~16,7 MB de RAM. `bimbo_no_bg.png` se recrea con `new BitmapImage(uri)` en cada apertura de modal (5 archivos). |
| **G3** | **`CacheMode="BitmapCache"` sobre `Sidebar` y `BrandBlock`, cuyo `Width` se anima** (`MainWindow.xaml:127`, `:528`). Peor caso de BitmapCache: re-rasteriza el bitmap completo por frame. |
| **G4** | **`DashboardView` tiene un `Storyboard RepeatBehavior="Forever"` que nunca se detiene** (`DashboardView.xaml:549`, sin `Unloaded`). Se acumula uno por cada visita. |
| **G5** | **`PesajeView` es la única vista que no llama `_vm.Dispose()`** y suscribe `PropertyChanged` con lambda anónima no desuscribible (`PesajeView.xaml.cs:44`, `:55-62`). |
| **G6** | **Guardar un proceso de descarga hace ~20 viajes de red en serie** (`PesajeViewModel.cs:189-238`). Son independientes → `Task.WhenAll`. |
| **G7** | **`<DropShadowEffect Opacity="0"/>` no apaga el shader** (`ContactoFabricanteModal.xaml:222`, `ContactoProveedorModal.xaml:213`). Lo correcto es `Value="{x:Null}"`. |
| **G8** | **`AddScoped` en WPF sin scopes** (`CapaAplicacion4/DependencyInjection.cs:15-18`) = singletons de facto. `CapaDatos` ya resolvió esto con Singleton explícito. |
| **G9** | **`MainViewModel` dispone `_searchVm`**, instancia compartida de toda la sesión → la siguiente búsqueda lanza `ObjectDisposedException` (`MainViewModel.cs:142`, `:172`). |
| **G10** | `ProductosView.ActualizarCarga()` y `CategoriasView.ActualizarCarga()` desreferencian `_vm` sin comprobar null. |
| **G11** | Recursos duplicados en 8 vistas, re-parseados en cada navegación. Solo `RolesResources.xaml` usa `po:Freeze`. |

**Estado:** `[ ] Pendiente`

---

### P-033 · Sin verificar: ¿el trigger de pesajes cubre UPDATE?

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

**Estado:** `[ ] Pendiente — verificación en BD`

---

### P-035 · Configuración de empresa implementada; falta validación manual y confirmar trigger de `updated_at`

**Detectado en:** [[Sesión 2026-08-14 - Logo de empresa dinamico en login]]

El módulo, el bucket y sus políticas ya se verificaron e implementaron. `empresa-logos` mantiene lectura pública; INSERT/UPDATE/DELETE exigen un usuario autenticado con `Modificar Configuración`. Cada reemplazo genera un nombre único, actualiza la fila y elimina versiones anteriores; el caché local también se actualiza.

Durante la inspección remota no apareció un trigger asociado a `public.empresa` en `information_schema.triggers`. Por instrucción funcional, la aplicación no escribe `updated_at` y tampoco se creó un trigger sustituto. Antes de considerar cerrado el flujo completo hay que confirmar el trigger en el entorno objetivo y ejecutar la prueba visual/manual.

**Solución de fondo:**

1. Confirmar que el trigger automático de `updated_at` esté adjunto a `public.empresa` en el entorno objetivo.
2. Probar manualmente apertura por permiso, guardado, reemplazo repetido del logo, reinicio de la aplicación y propagación del tema.

**Estado:** `[~] Parcialmente resuelto — implementación y seguridad listas; validación manual/trigger pendientes`

---

### P-036 · Tara y Presentaciones no tienen pantalla CRUD — el combo de unidad filtrado a masa no tiene dónde vivir

**Detectado en:** [[Sesión 2026-08-14 - Catalogo de unidad_medida con categoria]]

`unidad_medida` ahora tiene categoría (`tipo_unidad`: Masa/Volumen/Conteo — ver [[ADR-017 - Catalogo real de unidad_medida con categoria y su uso en Tara]]) y `tara.id_unidad` ya es un dato real en la base (antes "kg" era solo la convención del nombre `peso_tara_envalaje`). `CatalogoRepository.GetUnidadesAsync(..., idTipoUnidad)` y `Catalogos.Unidades(r, idTipoUnidad)` ya están listos para poblar un combo de unidad filtrado a solo Masa.

El problema: **hoy no existe ninguna pantalla para crear o editar filas de `tara`** en la app — solo una lupa de solo lectura (`Catalogos.Taras` + `SelectorCatalogoModal`) para *elegir* una tara ya existente al editar un Producto. Los datos de `tara` se cargan directo en Supabase (por eso también es el origen del dato de prueba de P-023). El combo filtrado a Masa, entonces, no tiene ningún formulario donde mostrarse todavía. Mismo hueco existe para **Presentaciones** — confirmado por el usuario en la misma sesión, señalado a propósito para resolver después.

**Solución de fondo:** construir un CRUD mínimo de Tara (grilla + modal, mismo patrón que Categorías/Fabricantes) con el combo de unidad ya integrado (`Catalogos.Unidades(r, idTipoUnidadMasa)`), y el equivalente para Presentaciones. Conviene resolver junto con P-023 (limpiar el dato de prueba de `tara`), ya que ambos requieren tocar esa tabla.

**Estado:** `[~] Parcial — Presentaciones resuelto 2026-08-15, Tara sigue pendiente`

La mitad de Presentaciones se cerró en [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]]: `CapaUI/.../Pantallas/Presentaciones/` con grilla, filtros de estado y orden, y modal de alta/edición. Ese módulo sirve de plantilla directa para el de Tara — la diferencia es que Tara además necesita el combo de unidad filtrado a Masa y arrastra el dato de prueba de P-023.

---

### P-037 · El control de paginación es code-behind duplicado 9× sin validación de rango

**Detectado en:** [[Sesión 2026-08-14 - Regresion la grilla mostraba la pagina anterior]]

Hallazgos de la investigación de esa regresión. Ninguno causó el bug reportado, pero los tres son fragilidad real del mismo componente:

1. **El setter de `Page` no clampea.** `ProductosViewModel.cs:179-192` acepta cualquier entero: no valida contra `TotalPages` ni contra `1`. Los botones numerados asignan `_vm.Page = pg` directo, y los `CanExecute` solo protegen las flechas `« ‹ › »`. Si `_page > TotalPages`, `CalcularPaginas` devuelve un árbol donde **ningún botón queda resaltado** (ningún `p == current`), y `PageInfo` calcula un rango inválido. Es alcanzable por Realtime: un DELETE que reduce el total mientras el usuario está en la última página deja `_page` apuntando a una página que ya no existe.

2. **El resaltado del botón activo es un snapshot de `Style`, no un binding.** Se decide una sola vez al construir el botón (`p == current ? ActivePageBtn : PageBtn`). No hay `case nameof(Page)` en el switch de `OnVmPropertyChanged`, así que el resaltado depende enteramente de que se dispare `PageRows` o `TotalPages` para corregirse.

3. **Está copiado literal en 9 archivos** (10 al momento de escribir esto, ver nota). `CalcularPaginas` y `RefrescarPaginacion` viven duplicados en Productos, Categorías, Fabricantes, Proveedores, ContactosFabricantes, ContactosProveedores, Usuarios, Empleados y Bitácora (más `SelectorCatalogoModal`). Se verificó que `CalcularPaginas` es **aritméticamente correcta** para todo `1 <= current <= total` (simulados 9 casos, sin repetidos ni fuera de rango ni elipsis dobles) y que las 9 copias son idénticas — pero cualquier corrección futura hay que aplicarla en todas. Es la misma queja de fondo que P-004 sobre este mismo code-behind.

   *Nota 2026-08-19:* esta lista originalmente sumaba también `SelectorProductosModal` (11 copias) — se retiró entero esa sesión (ver [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]), así que el conteo real bajó a 10. No se corrigió el número en el cuerpo del punto para no reescribir la investigación original de la sesión que lo detectó.

Además, `Usuarios`, `Empleados` y `Bitacora` todavía tienen `DgX.ItemsSource = _vm.PageRows` **dentro** de su `RefrescarPaginacion()` (la forma que causó la regresión de esta sesión). Hoy no exhiben el bug porque no tienen Realtime y por lo tanto no recibieron el `case TotalPages` — pero si algún día se les agrega Realtime siguiendo el checklist, hay que separar las responsabilidades primero.

**Solución de fondo:** extraer un `UserControl` de paginación compartido con `ItemsSource` bindeado a una colección calculada, y clampear `Page` en el setter (o en un único lugar del VM base). Eso cierra los tres puntos de una vez y elimina las 9 copias. Es un refactor, no un fix — por eso quedó fuera del alcance de la sesión que lo detectó.

**Estado:** `[ ] Pendiente`

---

### P-039 · Búsqueda insensible a tildes solo se aplicó a Productos — faltan 7 tablas

**Detectado en:** sesión 2026-08-14, junto con el filtro de Categoría y el rediseño de la barra de filtros.

Se implementó el patrón completo (función `sin_tildes()`, columna generada `STORED` + índice GIN de trigramas, `TextoBusqueda.cs` en C#) y se aplicó a Productos: buscador con sugerencias, picker de Pesaje, y los 4 combos de filtro vía `ComboFiltro`. Decisión y alternativas descartadas en [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]].

**Falta replicarlo en:**
- Fabricantes, Proveedores, Categorías, Empleados, Usuarios, Bitácora (cada uno con su propia migración: columna generada + índice, siguiendo el molde de `productos.busqueda_producto`)
- El buscador universal (`ProductoSearchRepository`, `EmpleadoRepository` en `CapaDatos/Repositories/Search/`)

Mientras tanto, buscar con tilde en cualquiera de esos módulos sigue sin encontrar resultados sin tilde (y viceversa) — inconsistente con Productos, que ya sí funciona.

**Riesgo:** bajo (no rompe nada, es una funcionalidad incompleta, no un bug) pero visible para el usuario — la inconsistencia entre módulos genera la pregunta de "¿por qué en Productos sí y acá no?".

**Estado:** `[ ] Pendiente`

---

### P-040 · Carga inicial de `icono_sidebar` pendiente en Storage

**Detectado en:** [[Sesión 2026-08-15 - Icono dinámico del sidebar]]

El código ya permite seleccionar, subir, cachear y mostrar `empresa.icono_sidebar`, conservando `Resources/bimbo-logo.png` como fallback. Sin embargo, la fila `empresa.id_empresa = 1` seguía con `icono_sidebar = 'sin_icono'` al último intento verificado. La carga automatizada no se completó porque el MCP dedicado estaba en modo solo lectura y el canal administrativo falló por transporte.

**Solución:** iniciar sesión con `Modificar Configuración`, seleccionar `CapaUI/Resources/bimbo-logo.png` en el nuevo campo del modal y guardar; alternativamente, repetir la carga inicial cuando exista un canal Supabase de escritura disponible. Verificar después que la ruta quede guardada y que el objeto sea descargable públicamente.

**Estado:** `[ ] Pendiente`

---

### P-041 · Fabricantes y Categorías pasan el filtro de estado al RPC de conteos y las tres pastillas dejan de informar

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

**Solución:** en ambos repositorios, construir el objeto de filtros de los conteos sin `IdEstado`, igual que Productos. Los RPC `contar_fabricantes` y `contar_categorias` ya tratan el parámetro como opcional (`DEFAULT NULL`), así que **no hace falta migración**: alcanza con no mandarlo.

**Estado:** `[ ] Pendiente`

---

### P-042 · `PesajeModalStyles.xaml` duplica parcialmente `Styles.xaml` global, con drift real (no solo nombres distintos)

**Archivo:** `CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales/PesajeModalStyles.xaml`
**Detectado en:** [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]

Auditoría pedida tras notar que `ProcesoDescargaModal` (y el resto de los modales de Pesaje) importan `PesajeModalStyles.xaml` en vez de usar los estilos centralizados de `CapaUI/Resources/Styles.xaml` documentados en [[Anatomía compartida de los modales]]. `MIcoSearch` y `MCombo` ya se sacaron por estar duplicados y sin uso (limpieza sin riesgo, misma sesión). Quedan dos con **divergencia real de comportamiento**, no solo de nombre:

- **`MInput`/`MCombo`** (Pesaje) vs **`ModalInput`/`ModalCombo`** (global): fuente 13.5px vs 16.5px, alto 36 vs 38, y los de Pesaje **no tienen** el aro verde de foco ni el borde rojo de `validacion:Validacion.TieneError` — un campo inválido en un modal de Pesaje no se distingue visualmente por campo, a diferencia del resto de la app.
- **`MSegBtn`** vs **`ModalSegBtn`**: mismo problema (sin aro de foco). El comentario que justificaba la copia local decía que `ModalSegBtn` "no es visible desde acá" — premisa falsa, está centralizado en `Styles.xaml` desde antes de esta sesión.

**Riesgo:** bajo en datos, medio en consistencia de UX — un usuario que corrige un campo inválido en Pesaje no recibe la misma señal visual que en Productos/Fabricantes/Usuarios. La fuente más chica de `MInput` podría ser deliberada (el wizard de `ProcesoDescargaModal` tiene tarjetas de producto densas, con varios campos chicos por fila) — fusionar a ciegas con `ModalInput` (16.5px) podría romper ese layout.

**Solución:** decisión explícita, no ejecutar sin confirmarla:
1. Si el tamaño compacto es deliberado → nombrar y documentar la variante (ej. `ModalInputCompacto`) en `Styles.xaml`, agregándole el aro de foco y `Validacion.TieneError` que le faltan, y que `PesajeModalStyles.xaml` deje de tener su propia copia.
2. Si no lo es → migrar directo a `ModalInput`/`ModalCombo`/`ModalSegBtn` y ajustar el layout de Pesaje donde haga falta.

**Estado:** `[ ] Pendiente`

---

### P-043 · `ModalInput`/`InputBox` (global) tienen el mismo bug de `VerticalAlignment` fijo que ya se corrigió en `MInput`

**Archivo:** `CapaUI/Resources/Styles.xaml` — estilos `ModalInput` (líneas ~208-296) e `InputBox` (líneas ~141-167)
**Detectado en:** [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]

El `ControlTemplate` de `MInput` (Pesaje) tenía `VerticalAlignment="Center"` **fijo** en el `PART_ContentHost`, ignorando la propiedad `VerticalContentAlignment` del `TextBox` sin importar qué se le pusiera local o por Setter — se descubrió porque el campo Observaciones de `ProcesoDescargaModal`, ya multilínea, mostraba el cursor centrado en vez de arriba pese a `VerticalContentAlignment="Top"`. Se corrigió atando `VerticalAlignment="{TemplateBinding VerticalContentAlignment}"`.

`ModalInput` e `InputBox`, los estilos **globales** que usan todos los modales CRUD, tienen el mismo `VerticalAlignment="Center"` hardcodeado en su propio `PART_ContentHost` — no se tocaron porque quedaban fuera del alcance de esa sesión (era sobre Pesaje).

**Ya hay un campo real afectado hoy:** `CapaUI/Formularios/Principal/Pantallas/Configuracion/ConfiguracionEmpresaModal.xaml:81`, el campo "Dirección" (`AcceptsReturn="True"`, `TextWrapping="Wrap"`, `MinHeight="72"`, `VerticalContentAlignment="Top"` puesto local) — el cursor arranca centrado en la caja, no arriba, exactamente el mismo síntoma que tenía Observaciones antes del fix.

**Riesgo:** bajo (cosmético), pero engañoso — un campo multilínea que centra el cursor en vez de arrancar arriba se ve raro apenas tiene más de una línea de texto.

**Solución:** mismo fix de una línea, dos veces — en cada `ControlTemplate`, cambiar `VerticalAlignment="Center"` del `PART_ContentHost` por `VerticalAlignment="{TemplateBinding VerticalContentAlignment}"`. Sin riesgo para los campos de una sola línea: siguen viniendo con `VerticalContentAlignment="Center"` por el `Setter` del propio estilo, así que no cambia nada para ellos — solo lo hereda quien lo pise local, como ya hace `ConfiguracionEmpresaModal`.

**Estado:** `[ ] Pendiente`

---

### P-044 · Multiselección de `SelectorCatalogoModal` no responde a teclado

**Archivo:** `CapaUI/Core/Controls/SelectorCatalogoModal.xaml`
**Detectado en:** [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]]

Con `CatalogoConfig.PermiteMultiple` (hoy solo el selector de Productos de `ProcesoDescargaModal`), cada fila tiene un `CheckBox` independiente. Marcar varias funciona con mouse; por teclado no — llegar a una fila con `↓`/`Tab` y apretar `Space` no tilda el checkbox, porque el foco de teclado que ya maneja `OnPreviewKeyDown` (navegación `↓`/`↑`/`Enter` entre buscador y tabla, ver [[Selector de Catálogo - Selector genérico y multiselección]]) no llega hasta el `CheckBox` de la celda.

**Riesgo:** bajo — accesibilidad/comodidad, no bloquea el flujo (con mouse funciona completo).

**Solución:** no diseñada todavía. Probablemente un `PreviewKeyDown` adicional que, con foco en una fila y `PermiteMultiple` activo, `Space` togglee el `Marcado` de la fila resaltada (mismo patrón que ya usa `Enter` para confirmar selección simple).

**Estado:** `[ ] Pendiente`

---

### P-045 · Pesaje quedó fuera de la validación centralizada: sus campos de texto no tienen tope en ninguna capa

**Archivos:** `CapaDominio/Reglas/ReglasEntidades.cs`, `CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales/ProcesoDescargaModal.xaml`, `CapaUI/Formularios/Principal/Pantallas/Pesaje/Modales/PesajeModal.xaml`
**Detectado en:** security review del rediseño visual de Pesaje (2026-08-20)

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

**Estado:** `[ ] Pendiente` — se evalúa al cerrar el módulo Pesaje (ver [[Módulo Pesaje]]).

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
| P-028 | Verificar en runtime el rediseño de Roles | `[ ]` Pendiente | [[Sesión 2026-08-11 - Rediseño de Gestión de Roles]] |
| P-029 | Cancelación ausente en 7 ViewModels + timer fantasma | `[ ]` Pendiente | [[Sesión 2026-08-11 - Rediseño de Gestión de Roles]] |
| P-030 | Verificación en runtime de la pantalla de Roles | `[ ]` Pendiente | [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]] |
| P-031 | Frenos de rendimiento de toda la aplicación | `[ ]` Pendiente 🔴 | [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]] |
| P-032 | Reparto de tara extra sin transacción (N updates) | `[ ]` Pendiente 🔴 | [[Sesión 2026-08-13 - Pesaje solo bruto y tara extra pesada]] |
| P-033 | Verificar si el trigger de pesajes cubre UPDATE | `[ ]` Pendiente | [[Sesión 2026-08-13 - Pesaje solo bruto y tara extra pesada]] |
| P-034 | Invalidación de caché sobre tablas no publicadas en Realtime | 🟡 Parcial | [[Sesión 2026-08-13 - Guardado fluido y caché de catálogos que no vencía]] → [[Sesión 2026-08-14 - Realtime en columnas de join de Productos]] |
| P-035 | Configuración de empresa lista; validar flujo manual y trigger de `updated_at` | `[~]` Parcial | [[Sesión 2026-08-14 - Módulo de configuración de empresa y tema dinámico]] |
| P-036 | Tara y Presentaciones sin pantalla CRUD — combo de unidad filtrado a masa sin dónde vivir | `[~]` Parcial — Presentaciones ✅, Tara pendiente | [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]] |
| P-037 | Paginación: code-behind duplicado 9× sin clamp de `Page` ni binding del resaltado | `[ ]` Pendiente | [[Sesión 2026-08-14 - Regresion la grilla mostraba la pagina anterior]] |
| P-038 | Modelos C# desalineados del esquema + `ErrorCarga` no llega al usuario | `[ ]` Pendiente 🔴 | [[Sesión 2026-08-14 - Modelo desalineado del esquema tumbaba paginas enteras]] |
| P-039 | Búsqueda sin tildes solo en Productos — faltan 7 tablas | `[ ]` Pendiente | [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]] |
| P-040 | Carga inicial de `icono_sidebar` pendiente en Storage | `[ ]` Pendiente | [[Sesión 2026-08-15 - Icono dinámico del sidebar]] |
| P-041 | Conteos de Fabricantes/Categorías filtrados por estado — las 3 pastillas dejan de informar | `[ ]` Pendiente | [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]] |
| P-042 | `PesajeModalStyles.xaml` duplica `ModalInput`/`ModalCombo`/`ModalSegBtn` sin foco ni validación por campo | `[ ]` Pendiente | [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] |
| P-043 | `ModalInput`/`InputBox` globales: mismo bug de `VerticalAlignment` fijo que ya se corrigió en `MInput` | `[ ]` Pendiente | [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] |
| P-044 | Multiselección de `SelectorCatalogoModal` no responde a teclado (Space no tilda el checkbox) | `[ ]` Pendiente | [[Sesión 2026-08-19 - Selector de proveedor por tabla y consolidacion de estilos]] |
| P-046 | Repo dentro de OneDrive: sync compite con Obsidian y git | `[ ]` Pendiente | auditoría de la bóveda 2026-08-23 |

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
