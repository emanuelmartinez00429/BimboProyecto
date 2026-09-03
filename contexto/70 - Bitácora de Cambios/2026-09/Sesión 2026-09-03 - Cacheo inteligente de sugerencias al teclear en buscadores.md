---
title: "Sesión 2026-09-03 — Cacheo inteligente de sugerencias al teclear en buscadores"
tags:
  - sesion
  - cache
  - buscador
  - rendimiento
  - fusioncache
date: 2026-09-03
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Fernando (agente)
---

# Sesión 2026-09-03 — Cacheo inteligente de sugerencias al teclear en buscadores

> [!success] Resultado
> Implementado el cacheo en memoria L1 con `ZiggyCreatures.FusionCache` para las sugerencias de texto al escribir en el buscador compartido `SuggestionSearchBox`. Se optimizó `SuggestionDebouncer` para descartar consultas de 1 sola letra, se normalizaron deterministamente los términos (`>= 3` caracteres) y se conectó la invalidación reactiva por tabla a `supabase_realtime`. Compilación limpia (0 errores, 0 advertencias) y 238/238 pruebas unitarias superadas.

---

## Problema / Motivo

En los 10 módulos con buscador (`Productos`, `Proveedores`, `Fabricantes`, `Categorías`, `Presentaciones`, `Empleados`, etc.), el operario interactúa con el `SuggestionSearchBox`.

Dos ineficiencias medibles afectaban el consumo de red hacia Supabase:
1. **Efecto Backspace / Oscilación de teclado:** Escribir `"harina"`, borrar una letra y volver a teclearla lanzaba dos consultas HTTP idénticas a Supabase.
2. **Peticiones prematuras de una sola letra:** Al presionar una sola tecla (`"a"`), el debouncer esperaba 200 ms y disparaba una consulta buscando por `"a"`, descargando 10 registros genéricos que no aportaban valor.
3. **Términos de alta frecuencia repetidos:** En planta, el 80% de las búsquedas giran sobre los mismos 20 términos cotidianos. Cada terminal realizaba consultas remotas completas para obtener siempre las mismas filas.

---

## Cambios Aplicados

### 1. UI: Optimización en `SuggestionDebouncer.cs` (`CapaUI/Core/Controls/`)
- Se incorporó un guard para descartar búsquedas si `q.Length < 2`:
  ```csharp
  var q = query.Trim();
  if (q.Length < 2) { aplicar(null); return; }
  ```
  Esto cierra de inmediato el popup si el usuario borra el texto o sólo ha ingresado una letra, ahorrando consultas HTTP innecesarias.

### 2. Infraestructura: Política `PoliticasCache.Sugerencias` (`CapaDatos/Cache/PoliticasCache.cs`)
- Se configuró la política específica para sugerencias efímeras con tolerancia de contingencia:
  ```csharp
  public static readonly PoliticaCache Sugerencias = new(
      Duracion:                 TimeSpan.FromMinutes(10),
      Jitter:                   TimeSpan.FromMinutes(2),
      MaxViejo:                 TimeSpan.FromHours(1),
      EsperaEntreReintentos:    TimeSpan.FromSeconds(15),
      UmbralRefrescoAnticipado: null);
  ```

### 3. Repositorios: Integración en `BuscarSugerenciasAsync` (`CapaDatos/Repositories/`)
Se inyectó `ICacheService` y se aplicaron las 4 reglas de coherencia (longitud mínima `>= 3`, normalización sin tildes con `TextoBusqueda.Normalizar`, discriminación de filtros activos y tags de Realtime):
- **`ProductoCrudRepository`**: Clave `sug:productos:{aguja}:{estado}:{prov}:{fab}:{cat}:{pais}` con tag `catalogos:productos`.
- **`ProveedorCrudRepository`**: Clave `sug:proveedores:{aguja}:{estado}` con tag `catalogos:proveedores`.
- **`FabricanteCrudRepository`**: Clave `sug:fabricante:{aguja}:{estado}:{pais}` con tag `catalogos:fabricante`.
- **`CategoriaCrudRepository`**: Clave `sug:categoria:{aguja}:{estado}` con tag `catalogos:categoria`.
- **`PresentacionCrudRepository`**: Clave `sug:presentacion:{aguja}:{estado}` con tag `catalogos:presentacion_producto`.
- **`EmpleadoCrudRepository`**: Clave `sug:empleados:{aguja}:{estado}` con tag `catalogos:empleados`.

### 4. Tags e Invalidación Reactiva
- En `TagsCache.cs` se formalizó `TablaEmpleados = "empleados"`.
- En `InvalidadorCacheRealtime.cs` se mapeó `TagsCache.TablaEmpleados` a `TagsCache.DeTabla(TagsCache.TablaEmpleados)` para que cualquier evento en `empleados` purgue reactivamente las sugerencias cacheadas.
- Las zonas Zero-Cache (Bitácora, Pesajes transaccionales) permanecen intactas y sin caché.

---

## Verificación

1. **Compilación:**
   ```bash
   dotnet build BimboProyecto.sln
   ```
   Resultado: 0 errores, 0 advertencias.

2. **Pruebas unitarias:**
   Se incorporó `BimboProyecto.Tests/Cache/SugerenciasBuscadorCacheTests.cs` cubriendo:
   - Equivalencia determinista: `"Harina"`, `"harina"` y `"Harína "` comparten entrada y solo invocan 1 vez al proveedor.
   - Bypass para cadenas cortas (`< 3` letras).
   - Purga reactiva inmediata al invalidar la etiqueta de la tabla.
   ```bash
   dotnet test BimboProyecto.sln
   ```
   Resultado: 238/238 pruebas superadas (100%).
