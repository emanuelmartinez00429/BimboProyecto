---
title: "Sesión 2026-07-01 — Pesaje Fase 2: Persistencia Real (movimientos/entradas)"
date: 2026-07-01
tags:
  - bitácora
  - pesaje
  - persistencia
  - supabase
  - trigger
branch: feat/fase6-IntegracionWpf/MenuPrincipal
status: Fase 2 completada (falta verificación con login)
---

# Sesión 2026-07-01 — Pesaje Fase 2: Persistencia Real

## Contexto

Se quitó la **semilla en memoria** de la pantalla de Recepción de Materia Prima (ver [[Sesión 2026-07-01 - Pantalla Pesaje WPF y Buscador por Proveedor (Fase 1)]]) y se cableó a las tablas reales `movimientos` / `movimiento_productos` / `entradas_producto`.

Decisiones del usuario: **"Quitar" = anular por estado** (id_estado = 9), **tara desde la BD del producto**, **sin realtime**.

## Descubrimientos clave (reuso)

- **Ya existían** modelos en `CapaDatos/Modelados/Pesajes/` (Movimiento, MovimientoProducto, EntradaProducto, Tara, MovProductoResumen, Proveedores) y la constante [[EstadosPesaje]] (`Abierto=7`, `Cerrado=8`, `Anulado=9`, `Activo=1`).
- Existe la **vista** `v_mov_productos_resumen` (indicadores calculados) y un **trigger** `trg_calcular_pesos_entrada` → `calcular_pesos_entrada` que al insertar un pesaje calcula:
  - `peso_tara_individual = tara.peso_tara_envalaje` (PLANA, del producto vía `productos.id_tara`; **no** por bulto),
  - `peso_tara_total = peso_tara_extra + tara_individual`,
  - `peso_neto = peso_bruto − peso_tara_total` (con `CHECK peso_neto > 0`).
- Datos reales: 2 camiones abiertos, 8 movimiento_productos, tara poblada (503/505 productos con `id_tara`).

## Implementación (patrón Result/TryAsync, consistente con Productos)

**Nuevos:**
- `CapaAplicacion4/Pesaje/Dtos/PesajeDtos.cs` — `CamionDto`, `MovProductoDto`, `EntradaDto`.
- `CapaAplicacion4/Pesaje/Interfaces/IPesajeRepository.cs` — camiones/productos/pesajes (get + crear + actualizar + cerrar + anular).
- `CapaDatos/Repositories/Pesaje/PesajeRepository.cs` — implementación que reutiliza los modelos/vista/estados/trigger. Lee camiones (`id_estado IN 7,8` + join proveedores), productos (`movimiento_productos` filtrando anulados + join `productos`), tara por producto (modelo ligero `ProductoTaraConsulta`) y entradas (`id_estado ≠ 9`). Inserta pesajes enviando solo bruto/tara_extra/bultos (el trigger completa neto/tara).
- `CapaDatos/Modelados/Pesajes/ProductoTaraConsulta.cs` — lectura de tara para la previsualización del modal.
- Registro DI: `services.AddTransient<IPesajeRepository, PesajeRepository>();`

**Modificados:**
- `PesajeViewModel` — **inyecta `IPesajeRepository`**, se eliminó `Seed()`; `CargarAsync()` en el `Loaded` y todas las mutaciones ahora son **async contra la BD** con recarga y manejo de errores (Toast). Mapea DTO → modelos de UI.
- `PesajeView.xaml.cs` — handlers async, carga inicial, sincronización de selección.
- `ProductoCamionModal` / `ProductoResult` — llevan `IdProducto` (necesario para insertar entradas).
- `PesajeModal` — tara individual **plana** (`producto.TaraUnitaria`), igual que el trigger.

## Anular vs borrar

Todo "Quitar" hace UPDATE `id_estado = 9` (Anulado): camión (`AnularCamionAsync`), producto (`AnularProductoAsync`), pesaje (`AnularEntradaAsync`). Las lecturas filtran anulados, así que desaparecen de la UI sin romper FKs ni perder auditoría. Los agregados (bultos restantes / % restante) se calculan en cliente desde las entradas activas cargadas.

## Verificación

- `dotnet build CapaUI.csproj` → **0 errores**.
- Trigger y esquema verificados por SQL; el mapeo de columnas replica el de los repos previos (`RepositorioMovimiento/MovimientoProducto/Entrada`).
- **Pendiente:** prueba end-to-end con login (registrar camión, agregar producto con el picker, pesar, cerrar, anular) — no ejecutable headless. No se hicieron INSERTs de prueba en la BD real para no ensuciar datos.

## Relaciones

- [[Sesión 2026-07-01 - Pantalla Pesaje WPF y Buscador por Proveedor (Fase 1)]]
- [[Buscador Universal Bimbo]] · [[Paginación y Búsqueda - Arquitectura Detallada]] · [[Result Pattern]]
