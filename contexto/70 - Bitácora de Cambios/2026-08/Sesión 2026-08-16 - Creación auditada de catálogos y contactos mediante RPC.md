---
title: "Sesión 2026-08-16 — Creación auditada de catálogos y contactos mediante RPC"
tags:
  - sesion
  - supabase
  - auditoria
date: 2026-08-16
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Codex (sesión gestionada por Emanuel)
---

# Sesión 2026-08-16 — Creación auditada de catálogos y contactos mediante RPC

> [!success] Resultado
> Los seis submódulos dejaron de insertar directamente desde C# y ahora crean sus registros mediante funciones PostgreSQL que también reciben el usuario de auditoría.

---

## Problema / motivo

Proveedores, fabricantes, categorías, presentaciones y los contactos de proveedor y fabricante todavía utilizaban `client.From<T>().Insert(...)`, sin enviar el usuario activo a la operación de creación auditada.

## Cambios aplicados

- Se inyectó `IUsuarioSesionService` en los seis repositorios.
- Cada `CreateAsync` exige `SesionActual.IdUsuario` y respeta cancelación antes y después de la llamada remota.
- Se sustituyeron los inserts por las RPC `ingresar_proveedor_tabla_bitacora`, `ingresar_fabricante_tabla_bitacora`, `ingresar_categoria_tabla_bitacora`, `ingresar_presentacion_tabla_bitacora`, `ingresar_contacto_fabricante_tabla_bitacora` e `ingresar_contacto_proveedor_tabla_bitacora`.
- Cada repositorio valida que la RPC devuelva una PK entera positiva.
- Se actualizó el estado vivo en [[Arquitectura Actual]], [[Módulos de Catálogos Administrativos]] y [[Módulo Contactos (Drill-down)]].

## Verificación

- `dotnet build BimboProyecto.sln` → 0 errores, 67 advertencias preexistentes.
- `git diff --check` → sin errores de whitespace.
- Búsqueda estática → no quedan inserts directos en los seis `CreateAsync`.
- No se ejecutó una prueba remota contra Supabase; requiere una sesión válida y permisos `EXECUTE` sobre las seis funciones.

## Lo que NO cambió

- `UpdateAsync`, bajas lógicas, DTO, interfaces, ViewModels y modales.
- Creación de usuarios.
- Flujos de productos y empleados ya migrados previamente.

---

## Relaciones

- [[Arquitectura Actual]]
- [[Módulos de Catálogos Administrativos]]
- [[Módulo Contactos (Drill-down)]]
- [[Módulo Bitácora]]
