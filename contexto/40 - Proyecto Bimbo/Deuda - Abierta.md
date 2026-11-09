---
title: "Deuda Técnica — Abierta"
type: deuda
status: vigente
date: 2026-05-28
updated: 2026-11-09
summary: "Inventario de 9 ítems abiertos de deuda técnica (P-023 a P-066) priorizados para arranque rápido"
autor: fernando
tags: [deuda, abierta, pendientes, calidad]
---

# Deuda Técnica — Abierta

> [!info] Contexto de Arranque
> Este archivo contiene **exclusivamente los 9 ítems abiertos/pendientes** de deuda técnica.
> Los ítems resueltos históricos viven en [[Deuda - Resuelta]].

## Resumen de Ítems Abiertos

| ID | Título | Prioridad | Sesión de Origen |
|:---|:---|:---:|:---|
| **P-023** | Catálogo de taras con datos de prueba | 🔴 Crítico | [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]] |
| **P-024** | Tara de empaque: plana en trigger vs por bulto en teóricos | Media | [[Sesión 2026-07-26 - Rediseño del flujo de Pesajes]] |
| **P-028** | Verificar en runtime rediseño de Roles | Media | [[Sesión 2026-08-11 - Rediseño de Gestión de Roles]] |
| **P-030** | Verificación en runtime de la pantalla de Roles | Media | [[Sesión 2026-08-12 - Estabilización de la pantalla de Roles]] |
| **P-035** | Configuración de empresa: validación manual y trigger `updated_at` | Media (Parcial) | [[Sesión 2026-08-14 - Módulo de configuración de empresa y tema dinámico]] |
| **P-036** | Tara y Presentaciones sin pantalla CRUD | Media (Parcial) | [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]] |
| **P-046** | Validación visual descripciones de estado en Bitácora | Baja | [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]] |
| **P-064** | Posible `Padding` duplicado en plantillas de `TextBox` | Baja | [[Sesión 2026-09-17 - Rediseño visual de Configuración de empresa]] |
| **P-066** | `ConstructionVM` y `ConstructionScreen` sin uso | Baja | [[Sesión 2026-09-20 - Preferencias por usuario (Fase 1 del escalado)]] |

---

## Detalle de Ítems Abiertos

### P-066 · `ConstructionVM` y `ConstructionScreen` quedaron sin ningún uso

**Archivos:** `CapaUI/Formularios/Principal/MainViewModel.cs` (clase `ConstructionVM`),
`CapaUI/Formularios/Principal/Pantallas/ConstructionScreen.xaml` (+ code-behind), y el
`DataTemplate` correspondiente en `MainWindow.xaml`.

Era la pantalla «en construcción» y su único consumidor era la ruta `Routes.MiUsuario`. Al
construirse `MiUsuarioView` (2026-09-20), esa ruta pasó a `MiUsuarioViewModel` y **nadie instancia
`ConstructionVM`**.

**Riesgo:** ninguno funcional. Es andamio que compila, se mantiene y no se usa.

**Decisión pendiente:** o se borra (VM + vista + `DataTemplate`, tres archivos), o se deja
explícitamente como plantilla para el próximo módulo sin terminar y se documenta como tal. No se
tocó en la sesión donde quedó huérfano porque borrarlo excedía el alcance de esa tarea.

**Estado:** `[ ] Pendiente` — detectado en [[Sesión 2026-09-20 - Preferencias por usuario (Fase 1 del escalado)]]

---

---

### P-064 · Posible `Padding` duplicado en plantillas de `TextBox` con `PART_ContentHost Margin="{TemplateBinding Padding}"`

**Archivos:** `CapaUI/Resources/Styles.xaml` — `InputBox`, `CeldaInput`, `ModalInput` y cualquier plantilla de `TextBox` que repita el patrón.

Al rediseñar `ConfiguracionEmpresaView`, una plantilla de `TextBox` con `Margin="{TemplateBinding Padding}"` en el `ScrollViewer` `PART_ContentHost` dejó el texto tipeado ~16px más adentro que el marcador, visto en un render con `RenderTargetBitmap`. `TextBoxBase` ya aplica el `Padding` dentro del host, así que el margen lo duplica. En esa vista se corrigió (host sin `Margin`). **No se auditaron los estilos compartidos**, que usan el mismo patrón y cuyo `controls:Placeholder` asume que marcador y cursor coinciden.

**Riesgo:** texto corrido respecto del placeholder e inputs con más sangría de la esperada en todas las pantallas que usan esos estilos.

**Cómo verificar:** renderizar un `TextBox` con texto y otro vacío con placeholder usando cada estilo y comparar la X de arranque. Si se confirma, quitar el `Margin` del host y ajustar el del marcador. Es un control compartido: grepear todos los usos antes de tocarlo.

**Estado:** `[ ] Pendiente` — detectado en [[Sesión 2026-09-17 - Rediseño visual de Configuración de empresa]]

---

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

---

### P-036 · Tara y Presentaciones no tienen pantalla CRUD — el combo de unidad filtrado a masa no tiene dónde vivir

**Detectado en:** [[Sesión 2026-08-14 - Catalogo de unidad_medida con categoria]]

`unidad_medida` ahora tiene categoría (`tipo_unidad`: Masa/Volumen/Conteo — ver [[ADR-017 - Catalogo real de unidad_medida con categoria y su uso en Tara]]) y `tara.id_unidad` ya es un dato real en la base (antes "kg" era solo la convención del nombre `peso_tara_envalaje`). `CatalogoRepository.GetUnidadesAsync(..., idTipoUnidad)` y `Catalogos.Unidades(r, idTipoUnidad)` ya están listos para poblar un combo de unidad filtrado a solo Masa.

El problema: **hoy no existe ninguna pantalla para crear o editar filas de `tara`** en la app — solo una lupa de solo lectura (`Catalogos.Taras` + `SelectorCatalogoModal`) para *elegir* una tara ya existente al editar un Producto. Los datos de `tara` se cargan directo en Supabase (por eso también es el origen del dato de prueba de P-023). El combo filtrado a Masa, entonces, no tiene ningún formulario donde mostrarse todavía. Mismo hueco existe para **Presentaciones** — confirmado por el usuario en la misma sesión, señalado a propósito para resolver después.

**Solución de fondo:** construir un CRUD mínimo de Tara (grilla + modal, mismo patrón que Categorías/Fabricantes) con el combo de unidad ya integrado (`Catalogos.Unidades(r, idTipoUnidadMasa)`), y el equivalente para Presentaciones. Conviene resolver junto con P-023 (limpiar el dato de prueba de `tara`), ya que ambos requieren tocar esa tabla.

**Estado:** `[~] Parcial — Presentaciones resuelto 2026-08-15, Tara sigue pendiente`

La mitad de Presentaciones se cerró en [[Sesión 2026-08-15 - Modulo CRUD de Presentaciones]]: `CapaUI/.../Pantallas/Presentaciones/` con grilla, filtros de estado y orden, y modal de alta/edición. Ese módulo sirve de plantilla directa para el de Tara — la diferencia es que Tara además necesita el combo de unidad filtrado a Masa y arrastra el dato de prueba de P-023.

---

---

### P-046 · Validar visualmente las descripciones de estado de Pesaje en Bitácora

**Archivos:** `supabase/migrations/202608240003_describir_estados_pesaje_en_bitacora.sql`, `CapaUI/Formularios/Principal/Pantallas/Bitacora/`
**Detectado en:** [[Sesión 2026-08-24 - RPC idempotentes auditadas de Pesajes]]

La traducción en `private.bitacora_pesaje` fue desplegada y validada mediante una transacción con rollback para las tres familias: recepción, producto de recepción y entrada. Emanuel confirmó que los demás cambios funcionales de la sesión operan correctamente, pero quedó pendiente comprobar el resultado en la grilla real de Bitácora.

La prueba manual debe ejecutar cambios de estado y confirmar que las columnas visibles muestran el significado de negocio —por ejemplo, `Recepción cerrada`, `Producto anulado` o `Pesaje anulado`— y nunca textos como `Estado 8`, `Estado 9` o variantes con el ID entre paréntesis.

**Estado:** `[ ] Pendiente` — validación visual en la aplicación.

---

---

## Relaciones

- [[Deuda - Resuelta]]
- [[Arquitectura Actual]]
- [[AGENTS]]
- [[INDEX]]
