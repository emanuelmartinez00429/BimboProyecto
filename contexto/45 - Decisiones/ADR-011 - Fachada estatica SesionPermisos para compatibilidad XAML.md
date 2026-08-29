---
title: ADR-011 - Fachada estatica SesionPermisos para compatibilidad XAML
type: adr
status: vigente
tags:
  - adr
  - decision
  - bimbo
  - gestion-usuarios
  - xaml
  - compatibilidad
date: 2026-07-23
updated: 2026-07-23
summary: El módulo de usuarios movió la sesión a IUsuarioSesionService (DI Singleton). Pero el XAML existente usaba SesionPermisos.Tiene(Permiso.X) directamente en…
scope: []
symbols:
  - DataTrigger
  - IUsuarioSesionService
  - IsEnabled
  - SesionPermisos
estado: aceptado
---

# ADR-011 - Fachada Estática SesionPermisos para Compatibilidad XAML

**Módulo:** [[Módulo Usuarios]]
**Fecha:** Fase 7 (Julio 2026)

## Contexto

El módulo de usuarios movió la sesión a `IUsuarioSesionService` (DI Singleton). Pero el XAML existente usaba `SesionPermisos.Tiene(Permiso.X)` directamente en `DataTrigger` y `IsEnabled` bindings. Inyectar el servicio en cada ViewModel requería cambiar decenas de atributos XAML.

## Opciones consideradas

| # | Opción | Veredicto |
|---|--------|-----------|
| 1 | Eliminar la fachada e inyectar `IUsuarioSesionService` en cada VM | Rechazada — demasiados cambios XAML |
| 2 | Mantener fachada estática delegando internamente | **Elegida** |
| 3 | Crear XAML StaticResource con IValueConverter | Rechazada — más boilerplate |

## Decisión tomada

Clase estática `SesionPermisos` con `Configurar(IUsuarioSesionService)` llamada una vez al inicio. `Tiene(Permiso)` delega a `_sesionService?.SesionActual?.TieneAccion(permiso.ToString())`.

```csharp
public static class SesionPermisos
{
    private static IUsuarioSesionService? _sesionService;

    public static void Configurar(IUsuarioSesionService servicio)
        => _sesionService = servicio;

    public static bool Tiene(Permiso permiso)
        => _sesionService?.SesionActual?.TieneAccion(permiso.ToString()) ?? false;
}
```

## Por qué

- Cero cambios en XAML existente.
- Implementación mínima (~10 líneas).
- Transición suave, eliminable después.

## Consecuencias / Trade-offs

| Gana | Sacrifica |
|------|-----------|
| Cero cambios en XAML | Estado global estático (versión contenedora del mismo problema) |
| Código mínimo (~10 líneas) | `Configurar()` debe llamarse antes de cualquier binding |
| Transición suave | Doble indirección |
| Eliminable después | No mockeable para tests de XAML |

## Relaciones

- [[Módulo Usuarios]] — módulo que usa esta fachada
- [[ADR-007 - Servicio de Sesion Singleton vs SesionActual Estatico]] — servicio que la fachada delega
- [[ADR-010 - Permisos desde BD en vez de switch hardcodeado]] — permisos que la fachada consulta
