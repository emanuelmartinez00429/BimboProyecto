---
title: "Plan de Mejora — Módulo Productos (Revisión QA)"
tags:
  - qa
  - revision
  - plan-mejora
  - productos
  - seguridad
  - arquitectura
  - optimizacion
date: 2026-08-21
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente QA) — Sesión Fernando
revisor: Antigravity QA
triaje: Claude Code — 2026-08-21
estado: Triado
---

> [!warning] Triaje contra el código — 2026-08-21
> Cada hallazgo se verificó contra el código real antes de ejecutar nada. **Dos son
> falsos positivos** y dos estaban mal dimensionados. Resumen:
>
> | Fase | Veredicto | Motivo |
> | :--- | :--- | :--- |
> | 1 | ⏭️ **Absorbida** | Real, pero el hueco es de los **8 módulos**, no de Productos, y ya lo cubre [[Plan de Seguridad - Roadmap 10-10]] 2.2 |
> | 2 | ❌ **Falso positivo** | La entidad no está desfasada: es una proyección de búsqueda deliberada |
> | 3.1 | ✅ **Hecho** | Confirmado — y peor de lo reportado: el `ct` tampoco llegaba al repositorio |
> | 3.2 | ✅ **Hecho** | Confirmado — y no era optimización sino un bug funcional |
> | 4.1 | ⚠️ **Reformulada** | No son duplicados; la duplicación real eran **10 vistas** |
> | 4.2 | ⚠️ **Corregida** | El proyecto ya existía y una de las tres aserciones no aplica |

# Plan de Mejora — Módulo Productos (Revisión QA)

> [!abstract] Resumen Ejecutivo
> Plan de acción derivado de la revisión QA exhaustiva del **Módulo de Productos** realizada el **2026-08-21**. El módulo presenta una alta madurez arquitectónica y operativa (*Clean Architecture*, *MVVM*, *Realtime* con desuscripción automática, paginación server-side y búsqueda predictiva). Este plan estructura los hallazgos en **4 fases priorizadas por criticidad** con sus respectivos **entregables técnicos** para elevar el módulo al estándar de producción definitivo.

---

## 1. Diagnóstico de la Revisión QA por Aspectos

| Aspecto | Estado | Observación Principal | Tras el triaje |
| :--- | :---: | :--- | :--- |
| 🛡️ **Seguridad** | 🟡 **ALERTA** | `CreateAsync` auditado; `UpdateAsync` / `DeleteAsync` sin bitácora transaccional. | Confirmado, pero es de los **8 módulos** → va por Seguridad 2.2 |
| 🏗️ **Arquitectura** | 🟢 **SÓLIDO** | *Clean Architecture* + *MVVM*; Entidad de Dominio (`Producto.cs`) levemente desfasada. | ❌ La entidad **no** está desfasada: es proyección de búsqueda |
| 📐 **Mantenibilidad** | 🟢 **SÓLIDO** | Reglas de negocio centralizadas; falta `CancellationToken` en timeout de carga. | ✅ Corregido — el `ct` tampoco llegaba al repositorio |
| ⚡ **Optimización** | 🟢 **SÓLIDO** | RPC de conteos + Caché en memoria; `ProductoSearchRepository` no usa `busqueda_producto`. | ✅ Corregido — era un **bug funcional**, no optimización |

---

## 2. Fases del Plan de Mejora y Entregables

### ⏭️ FASE 1: Seguridad & Auditoría Transaccional — ABSORBIDA POR SEGURIDAD 2.2
**Objetivo:** Eliminar la asimetría de auditoría donde `CreateAsync` escribe en `bitacora` mediante RPC pero `UpdateAsync` e inactivación modifican PostgREST directamente sin trazabilidad.

> [!note] No se ejecuta acá — 2026-08-21
> La asimetría es real (confirmada en `ProductoCrudRepository.cs`: RPC en la línea
> 137, `.Update()` directo en 184 y 194). Pero **no es un defecto de Productos**:
> ningún módulo tiene RPC de actualizar/inactivar con bitácora. Los ocho
> (Categorías, Fabricantes, Presentaciones, Proveedores, Contactos ×2, Empleados,
> Productos) tienen únicamente `ingresar_*_tabla_bitacora`. Es un hueco sistémico.
>
> [[Plan de Seguridad - Roadmap 10-10]] **Fase 2.2** ya planifica la solución
> transversal: tabla `audit_log` + `IAuditoriaService` inyectado en los repositorios
> CRUD. Los dos RPC a medida de esta fase serían trabajo que ese servicio reemplaza,
> y por la vía de RPCs son 16 funciones PL/pgSQL a mantener en vez de un servicio.
>
> **El hueco queda registrado donde se va a cerrar, no acá.**

- [ ] **Entregable 1.1 — RPC `actualizar_producto_tabla_bitacora` (PostgreSQL / Supabase):**
  - Función PL/pgSQL transaccional que recibe los campos modificables de `ProductoDto`, `p_usuario_modificando` y registra en `bitacora` la acción `Modificar Producto` junto con los valores anteriores/nuevos.
- [ ] **Entregable 1.2 — RPC `inactivar_producto_tabla_bitacora` (PostgreSQL / Supabase):**
  - Función PL/pgSQL que realiza el *soft delete* (`id_estado = 2`) y registra la acción `Inactivar Producto` en `bitacora`.
- [ ] **Entregable 1.3 — Integración en `ProductoCrudRepository.cs`:**
  - Reemplazar las llamadas directas `.Update()` en `UpdateAsync` y `DeleteAsync` por invocaciones a las nuevas RPCs (`client.Rpc(...)`), extrayendo el `idUsuario` desde `_sesionService.SesionActual`.

---

### ❌ FASE 2: Arquitectura & Sincronización de Dominio — FALSO POSITIVO
**Objetivo original:** Alinear `CapaDominio.Entities.Producto` con el esquema vivo de la base y los DTOs de aplicación.

> [!failure] Descartada — 2026-08-21
> La entidad **no está desfasada**: es una *proyección de lectura del buscador
> universal*, no un espejo de la tabla. Por eso guarda `Fabricante`, `Categoria`,
> `Pais` y `Presentacion` como **nombres (string)** y no como FKs — el buscador
> muestra nombres, no ids.
>
> [[CLAUDE]] lo documenta como decisión deliberada en *"Dos rutas para Productos"*:
> la ruta del buscador usa `Producto` (dominio) y la del formulario usa
> `ProductoDto` (aplicación). Confirmado por uso: la entidad solo la consumen
> `ProductoSearchStrategy`, `ProductoSearchRepository` y el registro de DI.
>
> Agregarle `IdFabricante`, `IdTara`, `CreatedAt`, etc. la convertiría en un clon de
> `ProductoDto` y borraría esa separación. **No se toca.**

- [x] ~~Entregable 2.1 — Actualización de `CapaDominio/Entities/Producto.cs`~~ — descartado
- [x] ~~Entregable 2.2 — Homologación de Mappings~~ — descartado

---

### ✅ FASE 3: Optimización & Cancelación Asíncrona — HECHA
**Objetivo:** Evitar consumo innecesario de sockets y peticiones huérfanas en situaciones de latencia de red o cambios rápidos de página.

- [x] **Entregable 3.1 — Propagación de `CancellationToken`** ✅ 2026-08-21
  - El hallazgo se quedaba corto: el `ct` **tampoco llegaba al repositorio**.
    `GetPagedAsync` lo aceptaba y lo tiraba (`TryAsync(() => GetPagedInternal(page, size, filtros), …)`),
    y `GetPagedInternal` ni siquiera lo recibía. Arreglarlo solo en el VM no habría
    hecho nada.
  - `ProductosViewModel`: `CancellationTokenSource` por carga con `CancelAfter(TimeoutMs)`,
    reemplazando el `Task.WhenAny(task, Task.Delay(10s))` que dejaba la petición viva
    y un timer huérfano por carga. La generación nueva cancela la anterior de verdad.
    Se libera en `OnDispose()`.
  - `ProductoCrudRepository`: `GetPagedInternal` recibe el `ct` y lo pasa al `Get(ct)`.
  - `RepositorioBase.TryAsync` ya excluía `OperationCanceledException`
    (`when (ex is not OperationCanceledException)`), así que la cancelación propaga
    limpia hasta el VM — el contrato estaba bien, solo faltaba usarlo.
- [x] **Entregable 3.2 — Unificación de Búsqueda Universal** ✅ 2026-08-21
  - **No era una optimización: era un bug funcional.** El buscador universal era el
    único sensible a acentos y mayúsculas — «azucar» no encontraba «AZÚCAR», pero sí
    lo encontraba el formulario. Misma app, dos comportamientos.
  - `ProductoSearchRepository.SearchAsync` pasa al filtro único contra
    `busqueda_producto` con `TextoBusqueda.Normalizar(term)`.
  - **Se acepta perder la búsqueda por `contenido`** (el `OR` anterior sí lo cubría):
    la columna generada es `sin_tildes(nombre + ' ' + codigo)` y no lo incluye. Se
    verificó contra la base — el campo está vacío o nulo en **171 de 178 productos**,
    y los pocos que lo tienen guardan la unidad («500 g»). Si algún día se llena de
    verdad, la salida es extender la columna generada (y de paso lo ganan el
    formulario y el picker), no volver al `OR` crudo.

---

### ✅ FASE 4: Mantenibilidad & Higiene de Código — HECHA (reformulada)
**Objetivo:** Eliminar duplicidad en recursos visuales e incorporar pruebas automatizadas de regresión.

- [x] **Entregable 4.1 — Estilos de celda** ✅ 2026-08-21 — **el diagnóstico estaba al revés**
  - `ProductCellStyle` y `ProductCellStyle1` **no son duplicados**: difieren en
    `HorizontalAlignment` (`Center` vs `Left`), y el propio XAML lo dice
    (*"para alineacion a la izquierda"*). Unificarlos habría roto la alineación de
    la grilla.
  - La duplicación real, que la revisión no vio: **ese mismo par estaba copiado en
    10 vistas** bajo 4 nombres distintos y con contenido idéntico —
    `ProductCellStyle`/`ProductCellStyle1` (incluso en Categorías y Proveedores,
    residuo de copiar y pegar), `BitacoraCellStyle`/`…Left`,
    `EmpleadoCellStyle`/`…Left` y `UserCellStyle`.
  - Promovidos a `CapaUI/Resources/Styles.xaml` como **`CeldaCentrada`** y
    **`CeldaIzquierda`** —nombres que dicen qué hacen— y borradas las 19 copias
    locales. Continuación de la consolidación de estilos de la sesión 2026-08-21.
- [x] **Entregable 4.2 — Suite de Pruebas Unitarias** ✅ 2026-08-21 — **corregido**
  - `BimboProyecto.Tests` **ya existía** y estaba en la solución (con
    `ReportStrategyTests`); no había que crearlo.
  - La aserción de **«precios negativos» no aplica**: esa regla no existe.
    `PrecioPorKg` es `new ReglaCampo(Formato: FormatoCampo.Decimal)`, y `FormatoCampo`
    documenta que `Decimal` es **solo declarativo** — el parseo (y cualquier chequeo
    de signo) vive en presentación a propósito, porque depende del `CultureInfo`.
  - Tampoco alcanza al validador: el proyecto es `net10.0` y referencia
    CapaAplicacion/CapaDatos/CapaDominio, **no CapaUI** (WPF `net10.0-windows`), donde
    vive `ValidadorFormulario`. Y testear `ReglasProducto` directo sería testear una
    constante.
  - Se testeó la lógica pura que sí es alcanzable y sí tiene comportamiento:
    - `Dominio/ReglasFormatoTests.cs` — Correo, RTN y Teléfono, **incluyendo el
      contrato sutil de que el vacío es válido** (la obligatoriedad se declara aparte
      en `ReglaCampo.Obligatorio`); es fácil "arreglar" una de esas funciones y volver
      obligatorio, sin querer, todo campo opcional con formato. Cubre además el largo
      máximo de 50 del código de producto, que era lo válido del pedido original.
    - `Busqueda/TextoBusquedaTests.cs` — `TextoBusqueda.Normalizar`, que **tiene que
      coincidir con `public.sin_tildes()` de Postgres**; si divergen, la búsqueda
      devuelve de menos sin dar ningún error. Es el invariante del que depende el
      entregable 3.2.
  - **38 tests en verde** (`dotnet test BimboProyecto.Tests`).

---

## 3. Criterios de Aceptación y Verificación

| Entregable | Criterio de Éxito | Método de Validación | Estado |
| :--- | :--- | :--- | :---: |
| **Fase 1** | Toda edición o inactivación genera fila en `bitacora` con usuario real. | Consulta SQL a `bitacora` post-edición desde UI. | ⏭️ Difiere a Seguridad 2.2 |
| **Fase 2** | ~~Entidad con el 100% de los campos de negocio.~~ | — | ❌ Descartado |
| **Fase 3.1** | Cancelación real de tareas en vuelo al cambiar de página rápido. | Logs Serilog: no llegan respuestas de páginas abandonadas. | ✅ |
| **Fase 3.2** | «azucar» encuentra «AZÚCAR» en el buscador universal, igual que en el formulario. | Búsqueda manual desde el buscador global. | ✅ |
| **Fase 4.1** | Un solo par de estilos de celda en `Styles.xaml`; las 10 grillas se ven igual. | Recorrido visual de las 10 vistas. | ✅ |
| **Fase 4.2** | Suite xUnit en verde. | `dotnet test BimboProyecto.Tests` → **38/38**. | ✅ |

---

## 4. Relaciones

- [[Módulo Productos]] — Documentación viva del módulo
- [[Conocimiento Principal]] — Dashboard maestro
- [[Arquitectura Actual]] — Estado de la arquitectura
- [[Plan de Seguridad - Roadmap 10-10]] — Requisitos de auditoría y bitácora
- [[Plan de Tests Unitarios]] — Plan maestro de pruebas automatizadas
- [[ADR-018 - Busqueda insensible a mayusculas y tildes con columna generada]]
- [[Sesión 2026-08-21 - Ajustes de layout y scroll lateral en Pesajes y consolidación global de estilos]]
