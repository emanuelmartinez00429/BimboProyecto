---
title: "Sesión 2026-09-02 - Permisos integrados en el detalle del rol"
tags: [sesion, bimbo, roles, rbac, wpf, supabase]
date: 2026-09-02
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-09-02 - Permisos integrados en el detalle del rol

> [!success] Resultado
> Gestión de Roles quedó como una sola ruta con cuadrícula y detalle interno. Los permisos se administran desde la tarjeta del rol, identificada siempre por `IdRol`, sin pestañas, selector duplicado ni consultas adicionales.

---

## Objetivo

Simplificar la navegación de roles y permisos para que seleccionar una tarjeta abra directamente el catálogo de acciones del rol, preservando el contrato RBAC, las restricciones del Administrador y el reemplazo atómico existente en Supabase.

## Trabajo realizado

- Se retiraron las pestañas `Roles` y `Permisos por rol`, el `ComboBox` `CmbRol` y su sincronización imperativa en code-behind.
- `RolesViewModel` usa `MostrandoDetalle`, `AbrirDetalleRolCommand(int idRol)` y `VolverAListaCommand`; la carga inicial deja la cuadrícula sin seleccionar automáticamente un rol.
- Cada tarjeta abre por `IdRol`. El nombre se conserva como dato informativo, por lo que renombrar un rol no altera la navegación.
- Crear un rol agrega el resultado al resumen en memoria y abre inmediatamente su detalle con el ID retornado.
- El detalle muestra nombre, estado, distintivo de sistema, usuarios asignados, permisos activos y acciones agrupadas por módulo.
- Cada acción expone textualmente `Activa` o `Inactiva`, además de su indicador visual.
- La edición exige simultáneamente `Asignar Permisos a Rol`, rol activo y `EsSistema == false`. Los roles inactivos y el Administrador permanecen consultables en modo de solo lectura.
- El guardado reutilizable actualiza el snapshot `_guardadoPorRol`, el contador de permisos y el aviso de renovación de sesión.
- Al volver con cambios pendientes se ofrecen `Guardar`, `Descartar` y `Seguir editando`. Un error remoto mantiene abierto el detalle; descartar restaura el snapshot.
- Las tarjetas son botones WPF con clic sobre toda la superficie, teclado (`Enter`/`Espacio`), cursor, hover, presión, foco visible y nombre accesible `Administrar permisos del rol [nombre]`.
- `Editar` y `Activar/Desactivar` permanecen como botones hermanos superpuestos para no ejecutar la apertura de la tarjeta.

## Archivos modificados

- `CapaUI/Formularios/Principal/Pantallas/Roles/RolesView.xaml`
- `CapaUI/Formularios/Principal/Pantallas/Roles/RolesView.xaml.cs`
- `CapaUI/Formularios/Principal/Pantallas/Roles/RolesViewModel.cs`
- `CapaUI/Formularios/Principal/Pantallas/Roles/RolesResources.xaml`
- `contexto/40 - Proyecto Bimbo/Módulo Usuarios.md`
- `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`
- `contexto/00 - MOC/Conocimiento Principal.md`

## Decisiones

- Se reutiliza `RolPermisoRepository.ObtenerResumenAsync`; abrir un detalle no consulta nuevamente Supabase.
- No se modificó `IRolPermisoRepository`, el esquema ni las RPC.
- No se creó ADR: es una simplificación del flujo de navegación, no una decisión arquitectónica ni un cambio del contrato de persistencia.

## Pruebas y validaciones

- `dotnet build BimboProyecto.sln --no-restore` → compilación correcta, 0 errores y 51 advertencias nullable preexistentes.
- `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj --no-build --no-restore` → 113/113 pruebas superadas.
- `git diff --check` → sin errores; únicamente avisos informativos de normalización LF/CRLF.
- Prueba remota transaccional con `ROLLBACK` → `PRUEBA_TRANSACCIONAL_DETALLE_ROL_OK`.
- La prueba remota confirmó guardado válido de un rol ordinario y rechazo sin permiso, de rol inactivo, del Administrador y de acciones inexistentes.
- Revisión estática → no quedan `ApartadoRoles`, comandos de pestañas, `CmbRol` ni `SeleccionarRol` en la pantalla.

## Pendientes

- Ejecutar QA visual autenticada de clic, teclado, hover, presión, foco, accesibilidad, botones hermanos, filtros, guardado y las tres decisiones al volver. No había una instancia autenticada de `CapaUI` abierta durante esta sesión.

## Lo que NO cambió

- No se creó ni aplicó una migración.
- No se cambió el contrato de `reemplazar_permisos_rol_seguro`.
- No se realizó commit ni push.
- Se preservaron los demás cambios locales existentes en la rama.

## Relaciones

- [[Módulo Usuarios]]
- [[Arquitectura Actual]]
- [[ADR-024 - Rol Administrador inmutable con acceso total]]
- [[Sesión 2026-09-01 - Gestión auditable de roles]]
- [[Sesión 2026-09-02 - Administrador inmutable con acceso total]]
