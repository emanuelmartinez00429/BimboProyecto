---
title: "Gestor Realtime — Diseño Arquitectónico"
tags:
  - patron
  - realtime
  - arquitectura
  - supabase
  - mvvm
date: 2026-05-24
---

# Gestor Realtime — Diseño Arquitectónico

> [!abstract] Resumen
> Diseño completo del nuevo gestor de Realtime para CapaUI. Reemplaza el `GestorRealtime` estático actual (6 canales permanentes, sin integración WPF) por un servicio inyectable con lifecycle management, suscripción por vista activa, y lógica inteligente de recarga basada en paginación server-side.

---

## Problema del diseño actual

El `GestorRealtime` actual (`CapaDatos/Realtime/GestorRealtime.cs`) tiene estos problemas:

| Problema | Impacto |
|---|---|
| Namespace incorrecto (`CapaDominio` pero depende de Supabase) | Viola Clean Architecture |
| 6 canales abiertos permanentemente desde el arranque | Overhead innecesario en servidor y cliente |
| Estado global estático | No testeable, no inyectable, no scopeable |
| Sin Dispatcher marshaling para WPF | Crash garantizado al modificar ObservableCollection desde background thread |
| No integrado en DI de CapaUI | Acoplamiento directo, imposible de mockear |
| No usado en CapaUI — solo en BimboPesaje | Código muerto en la arquitectura nueva |

---

## Principio de diseño

> **Solo la vista visible suscribe. Al navegar a otra vista, la anterior desuscribe y la nueva suscribe.**

En cualquier momento solo hay **N suscripciones activas**, siendo N las tablas que necesita la pantalla actual (típicamente 1-2).

---

## Diagrama de flujo completo

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          SUPABASE (PostgreSQL)                              │
│                                                                             │
│  tabla: productos    tabla: empleados    tabla: movimientos    ...           │
│       │                    │                    │                            │
│       └────────────────────┴────────────────────┘                           │
│                            │                                                │
│                    WebSocket Realtime                                        │
│                   (una sola conexión)                                        │
└────────────────────────────┬────────────────────────────────────────────────┘
                             │ PostgresChangesResponse
                             │ { event: "UPDATE", record: { id: 37, id_estado: 2 } }
                             ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                     CAPA DATOS (Infraestructura)                            │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │              RealtimeService : IRealtimeService                      │  │
│  │              [Singleton — DI]                                        │  │
│  │                                                                      │  │
│  │  Patrones: Facade + Mediator + Observer                              │  │
│  │                                                                      │  │
│  │  ┌──────────────────────────────────────────────┐                   │  │
│  │  │ _canales: Dictionary<string, RealtimeChannel>│                   │  │
│  │  │   "productos"  → channel activo              │                   │  │
│  │  │   "empleados"  → (no existe aún)             │                   │  │
│  │  └──────────────────────────────────────────────┘                   │  │
│  │                                                                      │  │
│  │  ┌──────────────────────────────────────────────────────┐           │  │
│  │  │ _suscriptores: Dictionary<string, List<Action<CR>>>  │           │  │
│  │  │   "productos" → [ ProductosVM.OnCambioProductos ]    │           │  │
│  │  └──────────────────────────────────────────────────────┘           │  │
│  │                                                                      │  │
│  │  Responsabilidades:                                                  │  │
│  │   1. Recibe PostgresChangesResponse crudo de Supabase               │  │
│  │   2. Extrae: operación, id_registro, nuevo_estado                   │  │
│  │   3. Construye CambioRealtime (DTO de aplicación)                   │  │
│  │   4. Dispatcher.InvokeAsync() → despacha al UI thread               │  │
│  │   5. Invoca handlers suscritos para esa tabla                       │  │
│  │   6. Gestiona ciclo de vida de canales:                             │  │
│  │      - Primer suscriptor → abre canal                               │  │
│  │      - Último desuscribe → cierra canal                             │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└────────────────────────────┬────────────────────────────────────────────────┘
                             │
                             │ implementa
                             │
┌────────────────────────────┴────────────────────────────────────────────────┐
│                   CAPA APLICACIÓN (Contratos)                               │
│                                                                             │
│  ┌─────────────────────────────────────────────┐                           │
│  │ IRealtimeService                            │                           │
│  │   Task SuscribirAsync(tabla, handler)       │ ← Facade: oculta canales, │
│  │   void Desuscribir(tabla, handler)          │   threads, WebSocket      │
│  └─────────────────────────────────────────────┘                           │
│                                                                             │
│  ┌─────────────────────────────────────────────┐                           │
│  │ record CambioRealtime(                      │                           │
│  │   string Operacion,   // "INSERT"|"UPDATE"  │                           │
│  │   long?  IdRegistro,  // PK del record      │                           │
│  │   int?   NuevoEstado  // id_estado nuevo    │                           │
│  │ )                                           │                           │
│  └─────────────────────────────────────────────┘                           │
└────────────────────────────┬────────────────────────────────────────────────┘
                             │
                             │ inyecta via DI
                             ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                        CAPA UI (Presentación)                               │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │                    MainViewModel                                     │  │
│  │                                                                      │  │
│  │  [ObservableProperty] object? _vistaActual;                         │  │
│  │                                                                      │  │
│  │  [RelayCommand]                                                      │  │
│  │  void Navigate(string? routeId)                                      │  │
│  │  {                                                                   │  │
│  │      if (VistaActual is IDisposable anterior)                       │  │
│  │          anterior.Dispose();  → desuscribe realtime anterior        │  │
│  │                                                                      │  │
│  │      VistaActual = factory(); → crea nuevo VM, suscribe al cargar   │  │
│  │  }                                                                   │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │   ProductosViewModel : ObservableObject, IDisposable                 │  │
│  │   [Transient — DI]                                                   │  │
│  │                                                                      │  │
│  │   ctor(IProductoRepository, IRealtimeService)                       │  │
│  │                                                                      │  │
│  │   CargarDatosAsync()                                                 │  │
│  │     → _rt.SuscribirAsync("productos", OnCambioProductos)            │  │
│  │     → carga página 1                                                │  │
│  │                                                                      │  │
│  │   OnCambioProductos(CambioRealtime cambio)                          │  │
│  │     → lógica inteligente (ver diagrama de decisión)                 │  │
│  │                                                                      │  │
│  │   Dispose()                                                          │  │
│  │     → _rt.Desuscribir("productos", OnCambioProductos)               │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Diagrama de decisión — OnCambioProductos

```
                    OnCambioProductos(CambioRealtime cambio)
                                    │
                    ┌───────────────┴───────────────┐
                    │ cambio.Operacion               │
                    └───────────────┬───────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    ▼                               ▼
               "INSERT"                         "UPDATE"
                    │                               │
                    ▼                               ▼
          CargarPaginaAsync()           ¿IdRegistro está en PageRows?
          (siempre — no puedes                      │
           saber en qué página             ┌────────┴────────┐
           caerá el nuevo)                 ▼                 ▼
                                          SÍ                NO
                                          │                  │
                                ┌─────────┴──────┐   ¿NuevoEstado cambió?
                                │                │          │
                        ¿NuevoEstado          NuevoEstado    ├──── SÍ ───▶ RefrescarConteosAsync()
                         cambió?             no cambió       │
                                │                │          └──── NO ──▶ No hacer nada
                        ┌───────┴───────┐        │
                        ▼               ▼        ▼
                  Filtro ≠ Todos   Filtro = Todos │
                        │               │        │
                        ▼               ▼        ▼
              CargarPaginaAsync()  CargarPaginaAsync()  CargarPaginaAsync()
              (producto desaparece (producto sigue     (nombre, precio, etc.
               de la vista         visible, solo        cambió — actualizar
               — BD llena el      cambia badge          la fila visible)
               hueco)             de estado)
                        │
                        ▼
               ┌────────────────────────┐
               │ ¿PageRows.Count == 0   │
               │  && _page > 1?         │
               │  SÍ → _page--         │
               │       CargarPaginaAsync│
               │  NO → fin              │
               └────────────────────────┘
```

> [!important] No hay DELETE físico
> Los productos nunca se eliminan de la BD. "Borrar" es un UPDATE de `id_estado = 1 → 2` (soft delete). Esto simplifica la lógica: solo existen INSERT y UPDATE en Realtime. No se necesita `REPLICA IDENTITY FULL`.

---

## Diagrama de ciclo de vida — Navegación

```
═══════════════════════════════════════════════════════════════════════
 TIEMPO ──────────────────────────────────────────────────────────▶

 USUARIO:  [Login]     [Click Productos]     [Click Empleados]    [Cerrar sesión]
              │               │                      │                  │
═══════════════════════════════════════════════════════════════════════
 MainVM:      │          Navigate("prod")      Navigate("emp")    CerrarSesion()
              │               │                      │                  │
              │         ┌─────┴──────┐         ┌─────┴──────┐          │
              │         │ Dispose    │         │ Dispose    │          │
              │         │ WelcomeVM  │         │ ProdVM     │          │
              │         │ (no-op)    │         │ Desuscribe │          │
              │         └─────┬──────┘         │ "productos"│          │
              │               │                └─────┬──────┘          │
              │         Crear ProdVM           Crear EmpVM             │
═══════════════════════════════════════════════════════════════════════
 Canales      │                                                        │
 activos:    [0]             [1]                    [1]               [0]
                        "productos"            "empleados"
═══════════════════════════════════════════════════════════════════════
```

---

## Casos de uso

### CU-01: Otro usuario modifica un producto visible

```
Actor:      Usuario A (en Productos, filtro Habilitados, página 1)
Trigger:    Usuario B modifica el nombre del producto #37

1. Supabase emite UPDATE { id: 37, nombre: "Nuevo Nombre", id_estado: 1 }
2. RealtimeService → CambioRealtime("UPDATE", 37, estado: 1)
3. Dispatcher.InvokeAsync → OnCambioProductos en UI thread
4. PageRows.Any(p => p.Id == 37) → TRUE
5. NuevoEstado (1) == enPagina.IdEstado (1) → estado NO cambió
6. CargarPaginaAsync() → recarga página con datos actualizados
7. UI muestra producto #37 con su nuevo nombre
```

### CU-02: Otro usuario deshabilita un producto visible

```
Actor:      Usuario A (en Productos, filtro Habilitados, página 2)
Trigger:    Usuario B deshabilita producto #82

1. Supabase emite UPDATE { id: 82, id_estado: 2 }
2. CambioRealtime("UPDATE", 82, estado: 2) → OnCambioProductos
3. PageRows.Any(p => p.Id == 82) → TRUE
4. NuevoEstado (2) ≠ enPagina.IdEstado (1) → estado SÍ cambió
5. Filtro = Habilitados (≠ Todos) → CargarPaginaAsync()
6. BD devuelve 50 habilitados para página 2 — llena el hueco con el siguiente
7. Conteos: Activos -1, Inactivos +1
```

### CU-03: Producto en otra página cambia de estado

```
Actor:      Usuario A (en Productos, filtro Habilitados, página 3)
Trigger:    Usuario B deshabilita producto #12 (página 1)

1. CambioRealtime("UPDATE", 12, estado: 2) → OnCambioProductos
2. PageRows.Any(p => p.Id == 12) → FALSE
3. NuevoEstado tiene valor → RefrescarConteosAsync()
4. Solo indicadores: Activos -1, Inactivos +1
5. La tabla NO se recarga (innecesario)
```

### CU-04: Último producto de la última página se deshabilita

```
Actor:      Usuario A (filtro Habilitados, página 5 con 1 producto)
Trigger:    Ese producto se deshabilita

1. CambioRealtime("UPDATE", 201, estado: 2)
2. PageRows.Any → TRUE, estado cambió, filtro ≠ Todos
3. CargarPaginaAsync() → Supabase devuelve 0 para página 5
4. PageRows.Count == 0 && _page > 1 → _page--
5. Recarga página 4 → usuario ve página 4 correctamente
```

### CU-05: Se inserta un nuevo producto

```
Actor:      Usuario A (filtro Habilitados, página 1)
Trigger:    Usuario B crea un producto

1. CambioRealtime("INSERT", 250, estado: 1)
2. Operación = "INSERT" → CargarPaginaAsync() siempre
3. Si cae en página 1 por sort → aparece visible
4. Si cae en otra página → conteos actualizados, vista sin cambio visual
```

### CU-06: Usuario navega de Productos a Empleados

```
1. Click "Empleados" en sidebar
2. MainViewModel.Navigate("empleados")
3. ProductosVM implementa IDisposable → Dispose()
4. _rt.Desuscribir("productos") → suscriptores = 0 → cierra canal
5. Crea EmpleadosVM → SuscribirAsync("empleados") → abre canal
6. Solo canal "empleados" activo
```

### CU-07: Cambio en tabla sin suscriptores

```
Trigger:    Alguien modifica un empleado mientras usuario está en Productos

1. Canal "empleados" no existe (nadie suscrito)
2. Supabase no envía el evento — cero procesamiento
3. Al navegar a Empleados → carga datos frescos del servidor
```

---

## Patrones por componente

| Componente | Patrones | Razón |
|---|---|---|
| `IRealtimeService` | Facade, Mediator | Oculta Supabase, threads, canales. Desacopla productor de consumidor |
| `RealtimeService` | Observer, Singleton, Facade | Gestiona suscriptores por tabla, una conexión WebSocket, traduce eventos |
| `ProductosViewModel` | Observer, Dispose Pattern | Suscriptor de cambios, limpieza determinista al navegar |
| `MainViewModel` | Dispose Pattern | Orquesta lifecycle — dispone VM anterior antes de crear el nuevo |
| `CambioRealtime` | DTO, Inmutable (record) | Transfiere datos entre capas sin acoplamiento |
| DI Container | Singleton + Transient | RealtimeService uno solo; ViewModels uno por navegación |

---

## Relaciones

- [[Observer Pattern]] — Patrón central del sistema de suscripción
- [[Repository Pattern]] — Los repositorios coexisten con Realtime (queries vs notificaciones)
- [[Clean Architecture]] — IRealtimeService en Application, implementación en Infrastructure
- [[Caso 01 - CRUD con Paginación]] — La lógica inteligente depende de la paginación server-side
- [[Caso 04 - WPF MVVM Clean Architecture]] — DI, ViewModels inyectables, Dispose lifecycle
- [[Plan de Implementación - Gestor Realtime]] — Fases de implementación
