---
title: "Plan de Tests Unitarios"
tags: [tests, calidad, plan, xunit]
date: 2026-08-14
---

# Plan de Tests Unitarios

> Estado: propuesto. No existe hoy ningún proyecto de tests en la solución (verificado 2026-08-14).

## 1. Objetivo

Cubrir con tests unitarios la lógica de negocio, los contratos de aplicación y los ViewModels del portal, priorizando el riesgo de negocio (pesaje, permisos, búsqueda) sin requerir una instancia de Supabase.

**Fuera de alcance:** tests de integración contra Supabase real, tests de UI/WPF (no hay harness; la verificación de UI sigue siendo build limpio + prueba visual manual).

## 2. Estado actual y barreras

| Hallazgo | Impacto |
|---|---|
| No existe proyecto de tests ni paquetes (xunit/nunit/Moq = 0 coincidencias) | Hay que crear el proyecto desde cero |
| `CapaDominio` y `CapaAplicacion4` son 100% testeables hoy (lógica pura + contratos limpios) | Arranque inmediato sin refactor |
| `ConexionSupabase` es **singleton estático sin interfaz** | Los repositorios reales no se pueden unit-testear sin refactor; solo `RepositorioBase` es testeable con mocks |
| Validadores de entrada viven en **code-behinds de modales** (acoplados a `MessageBox`/controles WPF) | Requieren refactor para extraer la lógica pura |
| Cachés estáticas + `SemaphoreSlim` (`CatalogoCache`, `RolRepository`, `RolPermisoRepository`) | Tests no paralelos contra ese estado; usar `[Collection]`/fixture por clase |
| `UsuarioSesionServiceHelper.ConstruirPermisosPorModulo` es `internal` | Se necesita `InternalsVisibleTo` en CapaDatos |
| `CapaUI` es `net10.0-windows` | El proyecto de test de VMs debe ser `net10.0-windows` (o `net10.0` para VMs desacoplados de tipos WPF) |

## 3. Proyecto de test a crear

```
BimboProyecto.Tests/            (xunit, net10.0)
├── BimboProyecto.Tests.csproj
├── Dominio/
├── Aplicacion/
├── Datos/
├── UI/
└── Shared/                     (fakes, builders de entidades)
```

**Paquetes:** `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `Moq` (o NSubstitute), `FluentAssertions` (opcional, mejora legibilidad).

**Referencias:** CapaDominio, CapaAplicacion4, CapaDatos (para `RepositorioBase` + helper interno), CapaUI (para VMs).

**Configuración requerida:**
- `[assembly: InternalsVisibleTo("BimboProyecto.Tests")]` en `CapaDatos` (para `UsuarioSesionServiceHelper`).
- `<IsPackable>false</IsPackable>`, `IsTestProject=true`.

## 4. Priorización (orden de ejecución)

### Fase 1 — Lógica pura (cero refactor, máximo valor inmediato)
| Componente | Ubicación | Notas |
|---|---|---|
| `PesoCalculator` | CapaDominio | 9 fórmulas; guardas de `pesoManifestado > 0`, redondeo 2 decimales |
| `PesajeCalc` | CapaUI | `RepartirTaraExtra`, `TaraExtraMaximaRepartible`, `BultosTeoricos` |
| `UsuarioSesion.TieneAccion` / `ModuloPermisos` | CapaDominio | O(1), case-sensitive Ordinal |
| `ServicioBuscador` | CapaDominio | puntuación y top 6 |
| `ServicioNotificaciones` | CapaDominio | tope 100 |
| `Result<T>` / `Result` / `EstadoRegistro` | CapaAplicacion4 | contratos base |

### Fase 2 — Capa de aplicación (mocks de contratos)
| Componente | Ubicación | Notas |
|---|---|---|
| `SearchStrategyRegistry` | CapaAplicacion4 | orden por Priority, `GetByType` OrdinalIgnoreCase |
| `UniversalSearchHandler` | CapaAplicacion4 | término < 2 → Empty; aislamiento de fallos por estrategia; cancelación se propaga |
| `SesionPermisos` | CapaUI | OR/AND/Tiene con mock de `IUsuarioSesionService` |
| `PermisoCatalogo` | CapaUI | 28 mapeos únicos sin duplicados; `IntentarResolver` exacto con tildes |

### Fase 3 — Capa de datos (lo que hoy es testeable)
| Componente | Ubicación | Notas |
|---|---|---|
| `RepositorioBase.TryAsync` | CapaDatos | mock `IConexionMonitor` + lambdas falsas; fail-fast SinConexion; prefijo de contexto; propagación de cancelación |
| `UsuarioSesionServiceHelper.ConstruirPermisosPorModulo` | CapaDatos | requiere `InternalsVisibleTo`; agrupación, orden por idModulo, acciones sin módulo |

### Fase 4 — ViewModels (mocks de repositorios/mediator)
| Componente | Notas |
|---|---|
| `ProductosViewModel` | `_loadGeneration` (respuestas obsoletas no pisan), `_pendingSelectionId`, `PageSize=50` |
| `UsuariosViewModel`, `EmpleadosViewModel`, `BitacoraViewModel` | mismo patrón |
| `PesajeViewModel` | `MaxCamiones=3`, validaciones de alta de camión |
| `UniversalSearchViewModel` | debounce 300 ms + `IMediator` (mock) |
| `RolesViewModel` | `AccionItemVm` (Alternar/PuedeEditar/Asignada) |
| `MainViewModel` | navegación por permisos, `EstadoTexto`/`EstadoBrush` según `EstadoConexion` |
| `RealtimeAwareViewModel` | suscripciones idempotentes, baja en Dispose, reconexión |
| `SuggestionDebouncer` | delay real 300 ms (o refactor menor para reloj inyectable) |

### Fase 5 — Refactor + tests (requiere tocar código productivo)
| Componente | Refactor necesario |
|---|---|
| Validadores de modales (`TryParseDecimal`, `Valido`, `ValidarPaso`, `UpdateStrength`/`Validate`, regex email) | Extraer la lógica pura a helpers (p.ej. `ValidacionesPesaje`, `ValidacionProducto`, `PoliticaContrasena`) y testear esa clase; el code-behind queda como orquestador fino de `MessageBox` |
| `ConexionSupabase` | Abstraer con `ISupabaseClientFactory` e inyectar en repos → habilita tests unitarios de repositorios con clientes falsos |

### Fase 6 — Pendientes documentados (no testear)
- `PickerService` (stub con TODO) y `ClienteRepository` (stub vacío): no hay lógica que testear; documentar en deuda técnica.
- ~~`DashboardVM`: KPI hardcodeados, no hay lógica real.~~ ✅ **Completado 2026-09-17**: Conectado a live data con DI y suite de pruebas unitarias en `BimboProyecto.Tests/Dashboard/DashboardIntegrationTests.cs` (mapeos, cálculos de merma, fallback `"—"` y lógica reactiva). Ver [[Sesión 2026-09-17 - Integración de datos en vivo del Dashboard con FusionCache y Realtime]].
- `PermisoBehavior`: requiere STA/VisualTree → dejar para tests de UI (fuera de alcance).

## 5. Matriz de casos clave por método (resumen)

### `PesoCalculator`
| Método | Casos |
|---|---|
| `TaraTotal` | valores positivos; cero; negativos (consistencia con restas) |
| `PesoNeto` | bruto > tara; bruto == tara; bruto < tara (neto negativo) |
| `PesoRecibido` | suma; lista vacía → 0; un solo elemento |
| `DiferenciaKg` | positivo (excedente); negativo (faltante); cero |
| `DiferenciaPct` | manifestado > 0 normal; **manifestado <= 0 → 0**; redondeo a 2 decimales |
| `DiferenciaUsd` | multiplicación; cero; negativo |
| `BultosRecibidos` | normal; **manifestado <= 0 → 0**; redondeo 2 dec |
| `BultosRestantes` | diferencia; bultos recibidos > teóricos (negativo) |
| `PctPesoRestante` | normal; **manifestado <= 0 → 0**; redondeo |

### `PesajeCalc` (lógica más delicada del módulo Pesaje)
| Método | Casos |
|---|---|
| `RepartirTaraExtra(total, n)` | n > 0: cuotas iguales, **suma EXACTA == total**, residuo en la última; n <= 0 → vacío |
| `TaraExtraMaximaRepartible` | margen menor determina el tope; `-0.01` de seguridad; floor 2 dec; entradas vacías → 0; márgenes <= 0 → 0 |
| `BultosTeoricos` | bruto <= 0 → null; teorico <= 0 → null; bruto < taraExtra → null; pesoPorBulto <= 0 → null; caso normal |
| `BultosSonAproximados` | taraExtra <= 0 → true |

### `RepositorioBase.TryAsync`
| Caso | Esperado |
|---|---|
| `Estado == SinConexion` | `Result.Fail("Sin conexión a internet.")` **sin ejecutar la operación** |
| Operación lanza excepción | `Fail($"{contexto}: {msg}")` |
| Operación lanza `OperationCanceledException` | **se propaga** (no se captura) |
| Operación exitosa | `Ok(value)` |

### `UniversalSearchHandler`
| Caso | Esperado |
|---|---|
| término vacío o < 2 chars | `UniversalSearchResult.Empty` |
| una estrategia falla | las demás resultados; la fallida → vacío (no tumba todo) |
| estrategia lanza `OperationCanceledException` | se re-lanza |
| término ≥ 2 | resultados combinados + `TotalCount` + `Elapsed` |

### `PermisoCatalogo`
| Caso | Esperado |
|---|---|
| 28 valores del enum | todos tienen nombre BD y no hay duplicados (los diccionarios rompen con duplicados) |
| `IntentarResolver("Modificar Configuración")` | resuelve `ModificarConfiguracion` (tildes exactas) |
| nombre inválido | false |

### `UsuarioSesionServiceHelper.ConstruirPermisosPorModulo`
| Caso | Esperado |
|---|---|
| acciones asignadas agrupadas por módulo | lista de `ModuloPermisos` agrupada |
| acciones sin módulo en el mapa | se ignoran |
| orden | por `idModulo` |

## 6. Convenciones de tests

- **Naming:** `[Clase]_[Metodo]_[Caso]` → p.ej. `PesoCalculator_DiferenciaPct_ManifestadoCeroDevuelveCero`.
- **AAA** (Arrange/Act/Assert) en cada test.
- Un test por comportamiento; sin asserts múltiples sobre caminos distintos.
- Mocks: Moq (o NSubstitute) sobre **interfaces de contrato** únicamente (`I*Repository`, `IMediator`, `IConexionMonitor`, `IRealtimeService`, `IUsuarioSesionService`).
- Estado estático (`CatalogoCache`, cachés de Rol/RolPermiso): agrupar con `[CollectionDefinition]` para impedir paralelismo entre clases que tocan ese estado.
- No usar `Thread.Sleep` en tests de `SuggestionDebouncer`; esperar la tarea con timeout razonable.
- Cobertura objetivo inicial: 100% en `PesoCalculator` y `PesajeCalc` (lógica de negocio crítica); luego ≥ 80% en lo testeable de las fases 2–4.

## 7. Verificación

```bash
dotnet test BimboProyecto.sln
```

Debe terminar en verde. La solución actual **no incluye** el proyecto de test (se agrega como proyecto nuevo en la solución).

## Relaciones

- [[Arquitectura Actual]] — capas y dependencias que determinan qué es testeable
- [[Módulo Pesaje]] — fórmulas cubiertas por PesoCalculator/PesajeCalc
- [[Deuda Técnica - Pendientes]] — refactors habilitantes (ConexionSupabase, validadores en code-behind, PickerService, ClienteRepository)
- [[Convenciones C#]] — reglas de código que aplican también a los tests
