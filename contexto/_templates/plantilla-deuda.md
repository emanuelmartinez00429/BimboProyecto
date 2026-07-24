# Cómo registrar deuda técnica (NO es un archivo suelto)

La deuda técnica se agrega como un ítem **dentro** de
`40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`, no como archivo nuevo.

## Pasos

1. Elegí el siguiente número `P-NNN` libre (mirá el último usado en el documento).
2. Insertá el ítem en la sección de severidad correcta:
   - 🔴 **Críticos** — se propagan en cascada o afectan datos/seguridad.
   - 🟡 **Importantes** — no bloquean pero generan deuda.
   - 🟢 **Menores** — aceptables por ahora.
3. Agregá una fila a la **tabla de historial** al final del documento.

## Formato del ítem (copiar dentro del documento)

```markdown
### P-NNN · Título corto del problema

**Archivo:** `CapaX/ruta/Archivo.cs` — método/función
**Introducido en:** commit `xxxxxxx` (autor, fecha), o "preexistente". Ver [[Sesión …]].

Descripción de qué está mal y por qué.

**Riesgo:** Qué puede fallar y con qué severidad.

**Solución:** Qué se propone hacer.

**Estado:** `[ ] Pendiente`
```

## Fila de historial (al final del documento)

```markdown
| P-NNN | Descripción corta | `[ ]` Pendiente | [[Sesión origen]] |
```

## Al resolverla

- Tachá el título: `### ~~P-NNN · Título~~ ✅ Resuelto AAAA-MM-DD`
- Cambiá **Estado** a `[x] Resuelto` con nota de verificación.
- Actualizá la fila de historial a `✅ Resuelto`.
