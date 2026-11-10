---
title: "Auditoría — Cómo Consultar"
type: referencia
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
agente: antigravity
tags: [auditoria, sql, supabase, consultas, seguridad, worm]
summary: "Guía de consultas SQL completas y documentadas para el archivo de auditoría inmutable en Supabase."
---

# Auditoría — Cómo Consultar

> [!important] Propósito del Archivo y Preservación Histórica
> **Borrar notas de sesión del repositorio NO las borra del historial de Git.**
> El objetivo exclusivo de archivar sesiones antiguas hacia la base de datos de auditoría (`bimbo-auditoria-dev`) es **mantener el contexto de arranque del proyecto liviano y enfocado** (menos de 25k tokens para colaboradores y agentes de IA), mientras se provee un motor de búsqueda indexado y permanente. No se oculta ni se destruye información: cualquier commit histórico sigue siendo 100% inspeccionable con `git log` y `git show`.

---

## 1. Arquitectura y Modelo de Seguridad (WORM)

El archivo de auditoría reside en una base de datos PostgreSQL en Supabase independiente (`bimbo-auditoria-dev`), desacoplada de la base operativa de producción de la báscula.

### Garantías de Seguridad:
1. **Inmutabilidad WORM (*Write Once, Read Many*):** Un disparador estricto (`trg_sesiones_inmutables` y `trg_deuda_resuelta_inmutable`) rechaza incondicionalmente cualquier comando `UPDATE` o `DELETE` con la excepción:  
   `'Los registros de auditoria son inmutables'`.
2. **Row Level Security (RLS):** RLS está activado en todas las tablas del esquema `auditoria`. Solo los usuarios con rol `authenticated` cuyo correo electrónico figure en la tabla de control `auditoria.lectores` pueden ejecutar consultas `SELECT`. El rol anónimo `anon` tiene acceso denegado por defecto.
3. **Indexación de Texto Completo (FTS):** Índices GIN con el diccionario `spanish` permiten búsquedas léxicas rápidas e insensibles a inflexiones verbales o plurales.

---

## 2. Consultas SQL de Ejemplo

Todas las consultas deben ejecutarse conectándose a la base `bimbo-auditoria-dev` con un usuario autenticado autorizado o mediante el SQL Editor de Supabase.

### 2.1. Sesiones por persona (autor)

Obtiene las sesiones gestionadas por un integrante específico del equipo (ej. `fernando`, `emanuel`, `carlos`):

```sql
SELECT 
    date,
    autor,
    agente,
    modelo,
    titulo,
    summary,
    sha256
FROM auditoria.sesiones
WHERE autor = 'fernando'
ORDER BY date DESC;
```

### 2.2. Sesiones por agente de IA

Permite filtrar las sesiones ejecutadas por un agente determinado (`claude-code`, `antigravity`, `codex`, `opencode`):

```sql
SELECT 
    date,
    autor,
    agente,
    modelo,
    titulo,
    summary,
    ruta_original
FROM auditoria.sesiones
WHERE agente = 'claude-code'
ORDER BY date DESC;
```

### 2.3. Sesiones por rango de fechas

Consulta todas las sesiones comprendidas en un periodo específico:

```sql
SELECT 
    date,
    autor,
    agente,
    titulo,
    summary,
    sha256
FROM auditoria.sesiones
WHERE date BETWEEN '2026-05-01' AND '2026-08-31'
ORDER BY date ASC;
```

### 2.4. Sesiones que abordaron un ítem de Deuda Técnica (`P-NNN`)

Aprovecha el índice GIN sobre el array de texto `deuda`:

```sql
SELECT 
    date,
    autor,
    agente,
    titulo,
    deuda,
    summary,
    commits
FROM auditoria.sesiones
WHERE 'P-013' = ANY(deuda)
ORDER BY date DESC;
```

### 2.5. Sesiones que implementaron un Registro de Decisión (`ADR-NNN`)

Filtra por decisiones arquitectónicas referenciadas en el array `adr`:

```sql
SELECT 
    date,
    autor,
    agente,
    titulo,
    adr,
    summary
FROM auditoria.sesiones
WHERE 'ADR-001' = ANY(adr)
ORDER BY date DESC;
```

### 2.6. Búsqueda de Texto Completo en Español (`FTS` con ranking)

Busca términos o frases en lenguaje natural a lo largo de título, resumen y cuerpo de las notas utilizando `websearch_to_tsquery`:

```sql
SELECT 
    date,
    autor,
    agente,
    titulo,
    summary,
    ts_rank(
        to_tsvector('spanish', coalesce(titulo, '') || ' ' || coalesce(summary, '') || ' ' || coalesce(cuerpo_md, '')),
        websearch_to_tsquery('spanish', 'TryAsync repositorios error')
    ) AS relevancia
FROM auditoria.sesiones
WHERE to_tsvector('spanish', coalesce(titulo, '') || ' ' || coalesce(summary, '') || ' ' || coalesce(cuerpo_md, ''))
      @@ websearch_to_tsquery('spanish', 'TryAsync repositorios error')
ORDER BY relevancia DESC;
```

### 2.7. Consulta de ítems de Deuda Técnica Resuelta

Inspecciona los ítems de deuda histórica resueltos cerrados hace más de 60 días:

```sql
SELECT 
    id_p,
    titulo,
    prioridad,
    cerrada_en,
    sha256
FROM auditoria.deuda_resuelta
WHERE cerrada_en <= '2026-09-10'
ORDER BY cerrada_en DESC;
```

### 2.8. Búsqueda de Texto Completo en Deuda Resuelta

Localiza problemas específicos y cómo fueron resueltos en el histórico:

```sql
SELECT 
    id_p,
    titulo,
    prioridad,
    cerrada_en,
    cuerpo_md
FROM auditoria.deuda_resuelta
WHERE to_tsvector('spanish', coalesce(id_p, '') || ' ' || coalesce(titulo, '') || ' ' || coalesce(cuerpo_md, ''))
      @@ websearch_to_tsquery('spanish', 'TRUNCATE permisos authenticated')
ORDER BY cerrada_en DESC;
```

---

## 3. Consulta desde el Historial de Git (Sin base de datos)

Como el repositorio Git conserva el árbol de confirmaciones íntegro, cualquier nota archivada puede examinarse directamente mediante comandos nativos:

### Localizar notas por contenido o identificador:
```bash
git log -S "P-013" -- "contexto/70 - Bitácora de Cambios"
```

### Ver el contenido de una nota tal como existía antes de ser archivada:
```bash
git show HEAD~1:"contexto/70 - Bitácora de Cambios/2026-05/Sesión 2026-05-21 - Implementación RepositorioBase + TryAsync.md"
```

### Comparar diferencias de la nota en su último commit:
```bash
git log -p -1 -- "contexto/70 - Bitácora de Cambios/2026-05/Sesión 2026-05-21 - Implementación RepositorioBase + TryAsync.md"
```

---

## 4. Control de Acceso: Gestión de Lectores

Para habilitar a un nuevo integrante del equipo para consultar la base de datos de auditoría:

```sql
INSERT INTO auditoria.lectores (email)
VALUES ('integrador@example.com')
ON CONFLICT (email) DO NOTHING;
```

---

## 5. Puesta en marcha del proyecto `bimbo-auditoria-dev`

Los pasos los hace un integrador (Fernando) al crear el proyecto. Ningún agente los hace.

1. Aplicar `auditoria/migrations/0001_esquema_auditoria.sql`.
2. Agregar `auditoria` a **Settings → API → Exposed schemas**. Sin esto, el script de archivo falla con `Content-Profile: auditoria`.
3. Cargar en GitHub los secretos `AUDIT_SUPABASE_URL` y `AUDIT_SUPABASE_SERVICE_KEY` (los carga Emanuel, que es admin del repo).
4. Probar la inmutabilidad: un `UPDATE`, un `DELETE` y un `TRUNCATE` sobre `auditoria.sesiones` tienen que fallar con "Los registros de auditoria son inmutables".
5. Correr primero `node scripts/contexto/archivar-bitacora.js --dry-run`, revisar la lista y recién después el workflow con `--aplicar`.

El script sube con `resolution=ignore-duplicates`: si se reenvía una nota ya archivada, se ignora (no se intenta un UPDATE que el trigger rechazaría). La verificación consulta solo los hashes enviados, en lotes de 100.

---

## Relaciones

- [[INDEX]]
- [[Deuda - Resuelta]]
- [[Arquitectura Actual]]
