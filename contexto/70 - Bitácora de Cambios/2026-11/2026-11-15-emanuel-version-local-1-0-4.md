---
title: "Versión local 1.0.4 para aplicación e instalador"
type: sesion
status: vigente
date: 2026-11-15
updated: 2026-11-15
summary: "Se alinea la versión local del ensamblado con el empaquetado Velopack 1.0.4."
autor: emanuel
agente: codex
rama: main
commits: []
tags: [sesion]
---

# Versión local 1.0.4

- Se establece `CapaUI/CapaUI.csproj` en `1.0.4` a solicitud del usuario.
- Login y ventana principal muestran `VersionTexto`; el servicio obtiene la versión informacional del ensamblado y elimina el sufijo de commit. No se fija una versión literal en XAML.
- `dotnet publish` Release para win-x64 finalizó correctamente. La versión de producto del ensamblado publicado, sin sufijo, es `1.0.4`.
- Las 12 pruebas de actualización pasaron. Suite completa: 735 aprobadas, 3 omitidas, 0 fallidas.
- El instalador se empaqueta con Velopack `1.2.161` y versión `1.0.4` (ProductVersion MSI `1.0.4.0`). Por autorización del usuario, se reemplazó únicamente `BimboPesaje-win.msi` en la release GitHub `v1.0.4`. Se verificaron estado `uploaded`, tamaño de 65.429.992 bytes y SHA-256 `e8a2d03a588ad0944e01e8ed0b0bdac08616ae574332d95ddfbd2d37189e9e4d` contra el archivo local. Los demás assets, incluidos los paquetes y manifiestos de actualización, permanecen intactos; este reemplazo no distribuye el icono mediante actualización automática a instalaciones existentes.
- El workflow conserva su política de versiones `1.0.GITHUB_RUN_NUMBER`; esta sesión no modifica las versiones de futuras ejecuciones de CI.
- La aprobación visual del nuevo icono queda pendiente. Se preservó el cambio previo del usuario en `App.config`. No se realizó commit ni push.

## Relaciones

- [[Arquitectura Actual]]
