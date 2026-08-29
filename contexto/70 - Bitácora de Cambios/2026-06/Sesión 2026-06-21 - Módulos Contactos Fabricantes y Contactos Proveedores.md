---
title: Sesión 2026-06-21 — Módulos Contactos Fabricantes y Contactos Proveedores
type: sesion
status: vigente
tags:
  - sesion
  - contactos
  - drill-down
  - realtime
date: 2026-06-21
updated: 2026-06-21
summary: "Implementación completa de dos módulos de gestión de contactos con patrón drill-down: el usuario navega desde una lista de Fabricantes (o Proveedores) hacia los…"
scope:
  - CapaAplicacion4/Contactos/Fabricantes/Dtos
  - CapaAplicacion4/Contactos/Fabricantes/Interfaces
  - CapaAplicacion4/Contactos/Proveedores/Dtos
  - CapaAplicacion4/Contactos/Proveedores/Interfaces
  - CapaDatos/Modelados/Contactos
  - CapaDatos/Realtime
symbols:
  - BaseModel
  - ConstructionVM
  - ContactosFabricantesVM
  - ContactosFabricantesViewModel
  - ContactosProveedoresVM
  - ContactosProveedoresViewModel
  - ControlTemplate
  - CreateAsync
  - DataTemplate
  - DataTrigger
branch: feat/fase6-IntegracionWpf/MenuPrincipal
---

# Sesión 2026-06-21 — Módulos Contactos Fabricantes y Contactos Proveedores

## Resumen

Implementación completa de dos módulos de gestión de contactos con patrón drill-down: el usuario navega desde una lista de Fabricantes (o Proveedores) hacia los contactos de ese registro. Un único `UserControl` alterna entre dos paneles usando `IsViewingContacts`.

---

## Archivos creados

### CapaDatos — Modelos

| Archivo | Descripción |
|---|---|
| `CapaDatos/Modelados/Contactos/ContactoFabricanteModel.cs` | `[Table("contactos_fabricante")]`, hereda `BaseModel`. PK `id_contacto_fabricante`, FK `id_fabricante`. |
| `CapaDatos/Modelados/Contactos/ContactoProveedorModel.cs` | `[Table("contactos_proveedor")]`, hereda `BaseModel`. PK `id_contacto_proveedor`, FK `id_proveedor`. |

### CapaAplicacion4 — Contratos

| Archivo | Descripción |
|---|---|
| `CapaAplicacion4/Contactos/Fabricantes/Dtos/ContactoFabricanteDto.cs` | `{ int Id, int IdFabricante, string Nombre, string Telefono, string Correo, int IdEstado }` — props `init` |
| `CapaAplicacion4/Contactos/Fabricantes/Interfaces/IContactoFabricanteRepository.cs` | `GetByFabricanteAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync` — todos con `Result<T>` |
| `CapaAplicacion4/Contactos/Proveedores/Dtos/ContactoProveedorDto.cs` | Mismo patrón con `IdProveedor` |
| `CapaAplicacion4/Contactos/Proveedores/Interfaces/IContactoProveedorRepository.cs` | Mismo patrón |

### CapaDatos — Repositorios

| Archivo | Descripción |
|---|---|
| `CapaDatos/Repositories/Contactos/ContactoFabricanteCrudRepository.cs` | `: RepositorioBase, IContactoFabricanteRepository`. Usa `TryAsync` en todos los métodos. Soft-delete vía `UPDATE id_estado`. |
| `CapaDatos/Repositories/Contactos/ContactoProveedorCrudRepository.cs` | Mismo patrón para proveedores. |

### CapaUI — Vistas y ViewModels

| Archivo | Descripción |
|---|---|
| `ContactosFabricantesViewModel.cs` | `: RealtimeAwareViewModel`. Inyecta `IFabricanteRepository` + `IContactoFabricanteRepository`. Buscador con debounce 300ms. |
| `ContactosFabricantesView.xaml(.cs)` | Drill-down con `PanelFabricantes` / `PanelContactos`. Paginación. Modal overlay. |
| `ContactoFabricanteModal.xaml(.cs)` | Modal crear/editar. Usa `TemplateBinding Tag` para el texto del botón Guardar. |
| `ContactosProveedoresViewModel.cs` | Mismo patrón para proveedores. |
| `ContactosProveedoresView.xaml(.cs)` | Idéntico al de fabricantes. |
| `ContactoProveedorModal.xaml(.cs)` | Idéntico al de fabricantes. |

---

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `CapaDatos/DependencyInjection.cs` | Registra `IContactoFabricanteRepository` y `IContactoProveedorRepository` |
| `CapaUI/App.xaml.cs` | Registra `ContactosFabricantesViewModel` y `ContactosProveedoresViewModel` |
| `CapaUI/Formularios/Principal/MainViewModel.cs` | Reemplaza placeholders `ConstructionVM` por marcadores reales. Agrega clases `ContactosFabricantesVM` y `ContactosProveedoresVM`. |
| `CapaUI/Formularios/Principal/MainWindow.xaml` | Agrega `DataTemplate` para los dos nuevos marcadores. |
| `CapaDatos/Realtime/RealtimeService.cs` | Agrega entradas en `_pkColumns`: `contactos_fabricante` y `contactos_proveedor`. |

---

## Decisiones técnicas

### 1. Drill-down con panel toggle, no con navegación de ventana
Un solo `UserControl` alterna entre dos `Grid` (`PanelFabricantes` / `PanelContactos`) controlados por `IsViewingContacts` (computed property del ViewModel). Más simple que navegar a una nueva vista; el contexto del fabricante seleccionado se mantiene en el ViewModel sin pasar parámetros entre vistas.

### 2. Soft-delete (no DELETE físico)
`DeleteAsync` hace `UPDATE SET id_estado = 2`. `GetByFabricanteAsync` filtra `id_estado = 1` desde la BD. Los registros inactivos no aparecen nunca en la UI pero se preservan en la BD para auditoría.

### 3. CreateAsync usa ORM, no SQL raw
`client.From<ContactoFabricanteModel>().Insert(nuevo)` — ORM de Supabase. La PK nueva se obtiene de `resultado.Models.First().idContactoFabricante`.

### 4. Realtime simplificado (sin heurística de paginación)
La lista de contactos por fabricante es pequeña → no pagina → `OnCambioContacto` solo llama `CargarContactosAsync()`. Sigue el patrón §5 del [[Checklist - Replicar Módulo con Realtime]].

### 5. TemplateBinding Tag para el botón Guardar del modal
Un `TextBlock` dentro de un `ControlTemplate` no es accesible por nombre desde el code-behind. Solución: `Text="{TemplateBinding Tag}"` + `BtnGuardar.Tag = _esNuevo ? "Agregar" : "Guardar cambios"` en `OnLoaded`.

### 6. Márgenes — doble Grid igual que Productos
`Margin="22,18,22,0"` en el Grid interno. El `ModalOverlay` vive fuera como hermano con `Panel.ZIndex="100"` para cubrir el área completa incluyendo los márgenes.

---

## Bugs corregidos durante la sesión

| # | Bug | Fix |
|---|---|---|
| 1 | `LetterSpacing` no existe en WPF | Removido del XAML del modal |
| 2 | `TxtBtnGuardar` inaccesible desde code-behind (está en `ControlTemplate`) | Reemplazado por `TemplateBinding Tag` |
| 3 | Vista inicial con converters inexistentes (`StringToVisibilityConverter`, `EstadoToBrushConverter`, etc.) | Reescritura usando `DataTrigger` igual que `FabricantesView` |
| 4 | Tabla y buscador "pegados" al borde del panel | Aplicado patrón doble Grid con `Margin="22,18,22,0"` |

---

## Cumplimiento de arquitectura

Verificado contra la bóveda antes de documentar:

- ✅ Clean Architecture — dependencias correctas entre capas
- ✅ Result Pattern — todos los métodos del contrato retornan `Result<T>`
- ✅ TryAsync (RepositorioBase) — sin try/catch manual en repositorios
- ✅ EstadoRegistro — sin magic numbers
- ✅ RealtimeAwareViewModel — lifecycle y auto-unsubscribe
- ✅ Checklist P-008 — `_pkColumns` actualizado
- ✅ CommunityToolkit.Mvvm — `partial`, source generators
- ✅ Debounce 300ms — con `OperationCanceledException` correctamente ignorada
- ✅ Navegación — Route key → VM marker → DataTemplate

---

## Relaciones

- [[Módulo Contactos (Drill-down)]] — documentación del patrón
- [[Módulo Productos]] — módulo de referencia
- [[Checklist - Replicar Módulo con Realtime]] — guía seguida para el handler Realtime
- [[Arquitectura Actual]] — tabla de módulos actualizada
