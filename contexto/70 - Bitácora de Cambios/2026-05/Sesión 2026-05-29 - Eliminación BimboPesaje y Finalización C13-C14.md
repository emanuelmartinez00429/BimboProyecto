---
title: "Sesión 2026-05-29 — Eliminación BimboPesaje y Finalización C13–C14"
date: 2026-05-29
tags:
  - bitácora
  - arquitectura
  - limpieza
  - c13
  - c14
status: Completado
---

# Sesión 2026-05-29 — Eliminación BimboPesaje y Finalización C13–C14

## Contexto

Continuación de la auditoría del 2026-05-29. Dos ítem del plan de remediación habían quedado pendientes en la sesión anterior:

- **C13** — `App.Services = null` cuando BimboPesaje era el ejecutable de entrada (CapaUI.App.OnStartup nunca se invocaba desde WinForms)
- **C14** — Eliminar `GestorRealtime` + `GestorNotificaciones` (gestor Realtime obsoleto de 6 canales fijos con static events)

El usuario dio permiso explícito para tocar y eliminar BimboPesaje.

---

## Investigación previa a eliminar

Antes de tocar ningún archivo se realizó un inventario completo del grafo de dependencias:

### Lo que BimboPesaje usaba de otras capas

| Símbolo | Capa origen | Usado solo por BimboPesaje? |
|---|---|---|
| `GestorRealtime` | CapaDatos/Realtime | ✅ Sí |
| `GestorNotificaciones` | CapaDatos/Realtime | ✅ Sí |
| `ServicioLogo` | CapaDatos/Logo | ✅ Sí |
| `servicioSesionActual` | CapaServicios | ❌ También CapaUI — NO eliminar |
| `ProductosView` (clase) | CapaUI | ❌ Definida en CapaUI — NO eliminar |

### Lo que otras capas usaban de BimboPesaje

**Nada.** Cero referencias desde CapaUI, CapaAplicacion, CapaDatos o CapaDominio hacia el namespace `BimboPesaje`.

---

## C13 — App.Services lazy init

**Problema:** `App.Services` era una propiedad con setter privado asignada en `OnStartup`. Si el exe de entrada era BimboPesaje, `CapaUI.App.OnStartup` nunca se invocaba y `Services = null!` permanecía null — cualquier `App.Services.GetRequiredService<T>()` explotaba.

**Solución:** Property lazy (`??=`) con backing field privado:

```csharp
// Antes:
public static IServiceProvider Services { get; private set; } = null!;
// ...
Services = ConfigureServices(); // en OnStartup

// Después:
private static IServiceProvider? _services;
public static IServiceProvider Services => _services ??= ConfigureServices();
// OnStartup: _ = Services; // warm-up en el hilo UI
```

**Efecto:** Aunque `OnStartup` no se ejecute (o aunque se ejecute tarde), el primer acceso a `App.Services` inicializa el contenedor. Thread-safe en el caso de uso real (UI thread único).

**Archivo:** `CapaUI/App.xaml.cs`

---

## C14 — Eliminación de BimboPesaje completo

### Archivos eliminados

**Proyecto completo:**
- `BimboPesaje/` — carpeta entera (44 archivos .cs, XAML, Designer.cs, resources)
  - `Program.cs` — entry point WinForms
  - `Formularios/InicioSesion/` — FrmInicioSesion, FrmLogin, UcLogin*, UcForgot*
  - `Formularios/MenuPrincipal/` — FrmMenuPrincipal, UcMenuShell, UcMiUsuario
  - `Formularios/Productos/` — GestionProductos, GestionCategorias, GestionFabricantes, GestionProveedores, ProductosView (duplicado WPF embebido), ProductoModal (duplicado)
  - `Formularios/Usuarios/` — GestionUsuarios, GestiónEmpleados, agregarEditarEmpleado/Usuario
  - `Formularios/Movimientos/` — MovimientosyEntradas, RegistrarPesos, FrmCrearMovimiento, AgregarVehiculo

**Archivos en otras capas (solo usados por BimboPesaje):**
- `CapaDatos/Realtime/GestorRealtime.cs` — gestor obsoleto de 6 canales WebSocket estáticos
- `CapaDatos/Realtime/GestorNotificaciones.cs` — puente entre GestorRealtime y ServicioNotificaciones
- `CapaDatos/Logo/ServicioLogo.cs` — servicio de logo; la carpeta `Logo/` también eliminada

**Solución `.sln`:**
- Eliminada la entrada `Project(...)...EndProject` de BimboPesaje
- Eliminadas las 4 líneas de configuración de plataforma (GUID `DDCA4EBA-907B-4DA3-B590-7A12EE266D83`)

---

## Estado post-limpieza

### Proyectos restantes en `BimboProyecto.sln`

| Proyecto | GUID |
|---|---|
| CapaDatos | A5E5E810 |
| CapaServicios | DE30F22C |
| CapaDominio | D1C2B3A4 |
| ServicioConexión | 07D22A87 |
| CapaUI | 9E8D7C6B |
| CapaAplicacion | C81D7194 |

### Archivos Realtime en CapaDatos

Solo queda `RealtimeService.cs` (la implementación nueva y correcta del SDK).

---

## Impacto

| Área | Antes | Después |
|---|---|---|
| Ejecutables | BimboPesaje.exe (host) + CapaUI.dll embebida | CapaUI.exe (único) |
| WebSocket channels al inicio | 6 canales fijos (GestorRealtime) + canales on-demand (RealtimeService) | Solo canales on-demand, sin abre-todo en startup |
| Código de formularios WinForms | ~44 archivos .cs | 0 |
| App.Services cuando OnStartup no corre | NullReferenceException | Lazy-init transparente |
| Deuda arquitectural de la sesión | C13 parcial, C14 bloqueado | C13 ✅, C14 ✅ |

---

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `CapaUI/App.xaml.cs` | Property `Services` → lazy con `_services ??= ConfigureServices()` (C13) |
| `BimboProyecto.sln` | Eliminada entrada y config de plataforma de BimboPesaje |
| `BimboPesaje/` (carpeta) | **ELIMINADA** |
| `CapaDatos/Realtime/GestorRealtime.cs` | **ELIMINADO** |
| `CapaDatos/Realtime/GestorNotificaciones.cs` | **ELIMINADO** |
| `CapaDatos/Logo/ServicioLogo.cs` | **ELIMINADO** |
| `CapaDatos/Logo/` (carpeta) | **ELIMINADA** |

---

## Relaciones

- [[Sesión 2026-05-29 - Auditoría Profunda y Plan de Remediación Completo]] — sesión previa con el plan C1–C15
- [[Arquitectura Actual]] — actualizada para reflejar la eliminación
