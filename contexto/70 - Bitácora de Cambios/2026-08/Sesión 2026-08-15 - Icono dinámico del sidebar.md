---
title: "Sesión 2026-08-15 - Icono dinámico del sidebar"
tags: [sesion, configuracion, empresa, sidebar, storage]
date: 2026-08-15
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex
---

# Sesión 2026-08-15 - Icono dinámico del sidebar

## Objetivo

Reemplazar el `bimbo-logo.png` hardcodeado del menú por `empresa.icono_sidebar` y permitir editarlo desde Configuración de Empresa.

## Trabajo realizado

- Se agregó `IconoSidebar` al modelo y DTO de empresa.
- `EmpresaRepository.GuardarAsync` acepta logo e ícono por separado, crea nombres versionados y conserva ambas rutas durante la limpieza del bucket.
- Se agregó selector, validación y vista previa independiente en el modal.
- `MainWindow` descarga, cachea y aplica el ícono dinámico; el recurso empacado permanece como fallback.
- Se añadió `IconoSidebarCache` y su registro en DI.

## Pruebas y validaciones

- `dotnet build BimboProyecto.sln --no-restore --nologo`: 0 errores; warnings preexistentes.
- Todos los XAML parsean como XML válido.
- `git diff --check`: sin errores.

## Archivos modificados

- `CapaAplicacion4/Empresa/`, `CapaDatos/Modelados/Empresa.cs` y `CapaDatos/Repositories/Empresa/EmpresaRepository.cs`.
- `CapaUI/Core/Empresa/IconoSidebarCache.cs`, `MainWindow` y el modal/ViewModel de Configuración.

## Decisiones

- Reutilizar `empresa-logos`, pero conservar dos rutas vigentes durante la reconciliación.
- Mantener `Resources/bimbo-logo.png` como fallback offline o ante error de descarga.

## Bloqueo externo

La columna ya existe y contiene `sin_icono` para `id_empresa = 1`. La carga inicial de `Resources/bimbo-logo.png` no se completó: el MCP dedicado está configurado `read_only=true` y el canal administrativo falló por transporte. No se creó la política temporal autorizada, no se subió ningún objeto y no se actualizó la fila.

## Próximo paso recomendado

Con un canal Supabase de escritura disponible, subir `bimbo-logo.png` con nombre versionado al bucket `empresa-logos`, actualizar exclusivamente `empresa.icono_sidebar` y verificar descarga pública. Alternativamente, iniciar sesión en la aplicación con `Modificar Configuración` y seleccionarlo desde el nuevo control del modal.

## Pendientes

- [[Deuda Técnica - Pendientes#P-040 · Carga inicial de `icono_sidebar` pendiente en Storage|P-040]] — completar la carga inicial y verificarla en runtime.

## Relaciones

- [[Módulo Configuración de Empresa]]
- [[ADR-019 - Configuración de empresa y tema dinámico global]]
