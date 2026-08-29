---
title: .NET — Normalización Unicode (FormD→FormC) para quitar acentos
type: referencia
status: vigente
tags:
  - referencia
  - dotnet
  - unicode
  - csharp
date: 2026-07-26
updated: 2026-07-26
summary: "Patrón correcto para \"Muñoz\" → \"munoz\" (emails auto-generados, slugs, identificadores desde nombres)."
scope: []
symbols:
  - CharUnicodeInfo
  - Rune
  - StringInfo
lifecycle: verified
---

# .NET — Normalización Unicode (FormD→FormC) para quitar acentos

Patrón correcto para "Muñoz" → "munoz" (emails auto-generados, slugs, identificadores desde nombres).

## El patrón correcto

```csharp
static string Normalizar(string s)
{
    var formD = s.Normalize(NormalizationForm.FormD);   // "ñ" → "n" + U+0303 (tilde combinante)
    var sb = new StringBuilder(formD.Length);
    foreach (var c in formD)                            // iterar CHARS, jamás bytes
        if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            sb.Append(c);
    return sb.ToString().Normalize(NormalizationForm.FormC)  // recomponer
             .ToLowerInvariant().Trim();
}
```

Verificado: `Muñoz→munoz`, `María→maria`, `Gómez→gomez`, `Ángel→angel`, `Peña→pena`.

## Los tres niveles de "letra" — por qué el bug ocurre

| Nivel | Qué es | Ejemplo con "ñ" |
|---|---|---|
| **byte UTF-8** | Ladrillo crudo de almacenamiento | 2 bytes: `0xC3 0xB1` |
| **char (UTF-16)** | Unidad de código de .NET | 1 char `ñ`, o en FormD: 2 chars (`n` + `◌̃`) |
| **grafema** | Lo que un humano ve como letra | 1 letra |

**El bug clásico (P-016):** convertir el string a bytes UTF-8 y castear cada `byte` a `char` para preguntarle su categoría Unicode. Un byte NO es una letra — `GetUnicodeCategory((char)b)` sobre bytes de una secuencia multi-byte da resultados sin sentido y el filtrado de acentos se vuelve azaroso.

**Regla:** las APIs de categoría Unicode (`CharUnicodeInfo`) operan sobre `char`/`Rune`, nunca sobre bytes.

## Conceptos

- **FormD (descomposición canónica):** separa letra base y marcas combinantes → permite filtrar las marcas.
- **NonSpacingMark:** la categoría Unicode de tildes/diacríticos combinantes.
- **FormC (composición canónica):** recompone a la forma normal — buen hábito para comparar/almacenar.
- Para textos con emojis/surrogates usar `StringInfo`/`Rune`; para nombres latinos el patrón char-a-char basta.

## Dónde se usa en Bimbo

- `CapaUI/.../Usuarios/UsuarioModal.xaml.cs` → `Normalizar()` (generación de email `nombre.apellido@empresa.com`). Corregido 2026-07-26 (P-016).

## Relaciones

- [[Sesión 2026-07-26 - Resolución Deuda Técnica P-013 a P-021]]
- [[Convenciones C#]]
