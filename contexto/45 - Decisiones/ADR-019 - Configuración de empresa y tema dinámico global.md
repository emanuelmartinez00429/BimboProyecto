---
title: "ADR-019 — Configuración de empresa y tema dinámico global"
tags: [adr, decision, configuracion, tema, rls, storage]
date: 2026-08-14
estado: aceptado
---

# ADR-019 — Configuración de empresa y tema dinámico global

## Contexto

`empresa` ya contenía datos generales, dominio, logo y color, pero solo el login consumía dominio/logo. El engranaje del shell no tenía comportamiento y los azules de marca estaban repetidos en decenas de XAML, impidiendo aplicar `color_empresa` durante la ejecución.

La lectura de empresa y logo debe funcionar antes del login. En cambio, cualquier escritura debe exigir la acción real `Modificar Configuración`, incluso ante llamadas directas a Supabase.

## Decisión

- Tratar `empresa` como singleton funcional y ofrecer solo edición.
- Abrir un modal global desde `MainWindow` con ViewModel y repositorio inyectados.
- Centralizar la marca en recursos dinámicos derivados de un solo `#RRGGBB`, con caché local para evitar parpadeo inicial.
- Mantener colores de estado/acento independientes del color empresarial.
- Versionar logos por nombre, actualizar primero la referencia y reconciliar Storage después.
- Aplicar la misma estrategia a `icono_sidebar`: ruta independiente, caché separado y conservación simultánea de logo principal e ícono lateral en el bucket.
- Mantener lectura pública pre-login y proteger escrituras con una función de permiso en esquema privado y RLS con `USING`/`WITH CHECK`.
- No enviar desde C# el timestamp gestionado por la base.

## Alternativas consideradas

| Opción | Pro | Contra | ¿Elegida? |
|---|---|---|---|
| Recursos dinámicos semánticos | Cambio inmediato y una sola fuente de color | Requiere migrar los literales existentes | ✅ |
| Reescribir XAML al reiniciar | Implementación inicial menor | No repinta en vivo y duplica valores | ❌ |
| Reutilizar siempre el mismo nombre de logo | Nunca acumula archivos | Rompe la invalidación por nombre aceptada en ADR-016 | ❌ |
| Proteger solo la UI | Sin migración | Una llamada directa podría modificar datos/Storage | ❌ |

## Consecuencias

- El color se aplica a login, shell, pantallas y modales sin reiniciar.
- Agregar UI nueva exige usar los recursos semánticos, no nuevos azules literales.
- La operación logo+fila no es una transacción distribuida; se usa compensación y reconciliación.
- Las validaciones de negocio quedan para una fase posterior.
- La verificación visual y de reemplazo real de Storage sigue siendo manual.

## Relaciones

- [[Arquitectura Actual]]
- [[Módulo Configuración de Empresa]]
- [[Sesión 2026-08-15 - Icono dinámico del sidebar]]
- [[ADR-016 - Logo de empresa dinamico en login con cache por nombre de archivo]]
- [[Sesión 2026-08-14 - Módulo de configuración de empresa y tema dinámico]]
