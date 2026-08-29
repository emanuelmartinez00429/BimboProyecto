---
title: Sesión 2026-05-22 — Refactor Arquitectural Fase 1
type: sesion
status: vigente
tags:
  - bitacora
  - arquitectura
  - clean-architecture
  - refactor
date: 2026-05-22
updated: 2026-05-22
summary: "Tres archivos causaban esta dependencia ilegal: GestorRealtime, ServicioLogo, ServicioPerfilUsuario."
scope:
  - CapaDatos/Logo
  - CapaDatos/Realtime
  - CapaDominio/Notificaciones
symbols:
  - GestorNotificaciones
  - GestorRealtime
  - IPerfilUsuarioService
  - LoginWindow
  - MainViewModel
  - MainWindow
  - Notificacion
  - RepositorioUsuario
  - ServicioLogo
  - ServicioNotificaciones
---

# Sesión 2026-05-22 — Refactor Arquitectural Fase 1

## Problema corregido

`CapaServicios.csproj` referenciaba `CapaDatos.csproj`, violando la regla de dependencias de [[Clean Architecture]]:

```
CapaServicios (namespace CapaDominio)
    ↓ referencia directa  ← VIOLACIÓN
CapaDatos
    ↓
CapaDominio
```

Tres archivos causaban esta dependencia ilegal: `GestorRealtime`, `ServicioLogo`, `ServicioPerfilUsuario`.

---

## Movimientos realizados

### → CapaDominio (conceptos puros de dominio, sin dependencias externas)

| Archivo | Origen | Destino |
|---|---|---|
| `Notificacion.cs` | `CapaServicios/` | `CapaDominio/Notificaciones/` |
| `ServicioNotificaciones.cs` | `CapaServicios/` | `CapaDominio/Notificaciones/` |

**Por qué:** No tienen dependencias externas. `Notificacion` es una entidad de dominio; `ServicioNotificaciones` es estado in-memory puro. El Dominio es el lugar correcto.

### → CapaDatos (infraestructura — Supabase SDK, repositorios)

| Archivo | Origen | Destino |
|---|---|---|
| `GestorRealtime.cs` | `CapaServicios/` | `CapaDatos/Realtime/` |
| `GestorNotificaciones.cs` | `CapaServicios/` | `CapaDatos/Realtime/` |
| `ServicioLogo.cs` | `CapaServicios/` | `CapaDatos/Logo/` |

**Por qué:** Usan Supabase SDK, modelos de BD y repositorios concretos → pertenecen a Infrastructure. `GestorNotificaciones` va junto a `GestorRealtime` porque depende de él; y puede acceder a `ServicioNotificaciones` (ahora en CapaDominio) porque CapaDatos ya referencia CapaDominio.

---

## Resultado en CapaServicios.csproj

```xml
<!-- ANTES -->
<ItemGroup>
  <ProjectReference Include="..\CapaDatos\CapaDatos.csproj" />
</ItemGroup>

<!-- DESPUÉS — referencia eliminada -->
```

`CapaServicios` ahora solo contiene archivos sin dependencias a CapaDatos:
- `PesoCalculator.cs` — lógica pura de pesaje
- `ServicioBuscador.cs` — buscador de módulos (hardcodeado)
- `servicioSesionActual.cs` — estado de sesión
- `SesionActual.cs` — legado (BimboPesaje lo usa)
- `ServicioPerfilUsuario.cs` — **pendiente Fase 2** (aún usa RepositorioUsuario)

---

## Grafo de dependencias corregido

```
ANTES:
CapaServicios ──→ CapaDatos ──→ CapaDominio

DESPUÉS:
CapaServicios ──→ (ninguno)    [solo Supabase NuGet]
CapaDatos     ──→ CapaDominio
CapaDominio   ──→ (ninguno)
```

---

## Sin cambios en BimboPesaje

BimboPesaje usa `GestorRealtime`, `GestorNotificaciones` y `ServicioLogo` en 7 archivos. Todos siguen en `namespace CapaDominio` y BimboPesaje ya referenciaba `CapaDatos.csproj` — los encuentra en el mismo namespace sin modificación alguna.

---

## Deuda pendiente — Fase 2

`ServicioPerfilUsuario.cs` sigue en `CapaServicios` referenciando `RepositorioUsuario` directamente. La corrección completa requiere:
- Definir `IPerfilUsuarioService` en `CapaAplicacion4`
- Convertir a servicio injectable (singleton)
- Actualizar `MainViewModel`, `LoginWindow`, `MainWindow`, `WelcomeScreen` en CapaUI

Ver [[Capa de Dominio]] — sección "Hoja de ruta".
