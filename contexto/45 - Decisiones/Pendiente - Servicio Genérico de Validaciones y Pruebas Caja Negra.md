---
title: "Pendiente — Servicio Genérico de Validaciones y Pruebas de Caja Negra"
tags:
  - pendiente
  - validaciones
  - testing
  - calidad
date: 2026-06-10
estado: pendiente
---

# Pendiente — Servicio Genérico de Validaciones y Pruebas de Caja Negra

> [!warning] Por hacer
> Este servicio aún no ha sido implementado. Registrado el 2026-06-10 para desarrollo en sesión futura.

---

## ¿Qué es?

Un **servicio genérico de validaciones** que centralice todas las reglas de negocio que se deben verificar en el sistema, diseñado para que las **pruebas de caja negra** se puedan aplicar de forma sistemática sobre cada módulo.

---

## ¿Por qué?

Actualmente las validaciones están dispersas o no existen formalmente. Antes de avanzar con más módulos, conviene tener un contrato claro de qué se valida y cómo, de modo que:

- Las pruebas no dependan de conocer el código interno (caja negra = solo entradas y salidas)
- Los errores de negocio sean predecibles y consistentes en toda la app
- Sea fácil agregar validaciones nuevas sin tocar lógica existente

---

## Alcance previsto

| Área | Ejemplos de validaciones |
|---|---|
| **Campos requeridos** | Nombre no vacío, campos obligatorios por módulo |
| **Longitudes** | Máximo de caracteres por campo |
| **Formatos** | RTN (Honduras), teléfono, correo |
| **Reglas de negocio** | No duplicar nombre de producto/fabricante/categoría |
| **Estado** | No operar sobre registros inactivos |
| **Relaciones** | FK válida antes de guardar (ej. proveedor existe) |

---

## Diseño sugerido (a validar antes de implementar)

```
CapaAplicacion/
  Validaciones/
    IValidacionService.cs       ← contrato genérico
    ValidacionResult.cs         ← { bool EsValido, IReadOnlyList<string> Errores }
    Reglas/
      ReglaRequerido.cs
      ReglaLongitudMaxima.cs
      ReglaFormatoRtn.cs
      ReglaFormatoEmail.cs
      ... (una clase por regla)
```

> [!info] Antes de implementar
> Plantear el diseño completo al usuario para que decida si adoptar este enfoque o uno diferente. No implementar sin validación previa.

---

## Relación con las pruebas de caja negra

Las pruebas de caja negra se aplicarán **sobre los modales y formularios** de cada módulo (Productos, Proveedores, Fabricantes, Categorías, y los que vengan). El servicio de validaciones es el punto de entrada que esas pruebas van a ejercitar:

- **Entrada:** datos del formulario (DTO o campos sueltos)
- **Salida esperada:** `ValidacionResult` con errores claros o `EsValido = true`
- **Sin conocer** cómo se guarda en Supabase ni cómo funciona el repositorio

---

## Módulos a cubrir (cuando se implemente)

- [ ] Productos
- [ ] Proveedores
- [ ] Fabricantes
- [ ] Categorías
- [ ] Empleados *(cuando se agregue el módulo)*
- [ ] Usuarios *(cuando se agregue el módulo)*

---

## Referencias

- [[ADR-001 - Result Pattern en Repositorios]] — el `ValidacionResult` debe ser consistente con el Result Pattern ya adoptado
- [[Deuda Técnica - Pendientes]] — enlazado desde ahí como pendiente de diseño
