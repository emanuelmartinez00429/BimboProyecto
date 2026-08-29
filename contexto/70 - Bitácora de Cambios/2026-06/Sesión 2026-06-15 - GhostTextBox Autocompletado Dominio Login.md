---
title: "Sesión 2026-06-15 — GhostTextBox: Autocompletado de Dominio en Login"
type: sesion
status: vigente
tags:
  - bitácora
  - UI
  - login
  - control-compartido
date: 2026-06-15
updated: 2026-06-15
summary: Nuevo UserControl GhostTextBox que muestra texto fantasma gris (patrón Windows 11) en el campo de email del login. El dominio de correo se carga desde la tabla…
scope:
  - CapaDatos/Modelados
  - CapaUI/Core/Controls
  - CapaUI/Formularios/InicioSesion
symbols:
  - CancellationTokenSource
  - FontFamily
  - FontSize
  - GhostSuffix
  - GhostTextBox
  - IngresarAsync
  - InnerGotFocus
  - InnerLostFocus
  - Padding
  - Placeholder
---

# Sesión 2026-06-15 — GhostTextBox: Autocompletado de Dominio en Login

## Resumen

Nuevo UserControl `GhostTextBox` que muestra texto fantasma gris (patrón Windows 11) en el campo de email del login. El dominio de correo se carga desde la tabla `empresa` de Supabase y se auto-completa visualmente. Tab o → lo acepta.

---

## Cambios realizados

### 1. Migración Supabase

```sql
ALTER TABLE empresa ADD COLUMN IF NOT EXISTS dominio_correo text DEFAULT '@gmail.com';
```

### 2. Modelo `Empresa.cs` — nueva propiedad

```csharp
[Column("dominio_correo")]
public string? DominioCorreo { get; set; }
```

### 3. `GhostTextBox` UserControl (nuevo)

**Archivos:** `CapaUI/Core/Controls/GhostTextBox.xaml` + `.xaml.cs`

- Grid con TextBox ghost (read-only) detrás de TextBox real (fondo transparente)
- 3 DependencyProperties: `Text` (TwoWay), `GhostSuffix`, `Placeholder`
- 4 eventos: `SuffixAccepted`, `InnerGotFocus`, `InnerLostFocus`, `TextChanged`
- Método `GetFullText()` → devuelve alias + sufijo completo
- Tab / → al final del texto acepta el ghost suffix
- Guard `_updatingText` evita loop infinito entre DP y TextChanged
- Debounce 300ms antes de mostrar el ghost (CancellationTokenSource)
- `GetRemainingSuffix()` detecta overlap para no duplicar lo ya escrito

### 4. `LoginWindow.xaml` — migración a GhostTextBox

```xml
<!-- Antes -->
<TextBox x:Name="TxtEmail" Style="{StaticResource InputTextBox}" ... />

<!-- Después -->
<controls:GhostTextBox x:Name="TxtEmail" Placeholder="Usuario"
    InnerGotFocus="Input_GotFocus" InnerLostFocus="Input_LostFocus"
    TextChanged="Fields_Changed"/>
```

### 5. `LoginWindow.xaml.cs` — carga de dominio + GetFullText

- `LoginWindow_Loaded`: carga `dominio_correo` de `RepositorioEmpresa.ObtenerAsync()` y lo asigna a `TxtEmail.GhostSuffix`. Fallback `@gmail.com` si falla.
- `IngresarAsync`: usa `TxtEmail.GetFullText()` en lugar de `TxtEmail.Text.Trim()`.

---

## Build

✅ 0 errores, 37 warnings (todos preexistentes — CS8618/CS8603).

---

## Fixes posteriores (misma sesión)

Se corrigieron 3 bugs reportados tras la implementación inicial:

### Bug 1 — Desalineación vertical del ghost text

**Causa:** Se usaba `TextBlock` como ghost. `TextBlock.Padding` y `TextBlock.VerticalAlignment="Center"` no coinciden con el pipeline de renderizado interno de `TextBox`, que tiene un `ScrollViewer` (`PART_ContentHost`) que introduce un offset no controlable desde XAML.

**Fix:** Reemplazar el `TextBlock` ghost por un segundo `TextBox` con las mismas propiedades (`Padding`, `FontFamily`, `FontSize`, `VerticalContentAlignment`) pero con `IsReadOnly="True"`, `Focusable="False"`, `IsHitTestVisible="False"`. Al compartir el mismo template interno, el texto se renderiza pixel a pixel en la misma posición.

```xml
<!-- Antes: TextBlock (desalineado) -->
<TextBlock x:Name="GhostDisplay" Margin="12,0,12,0" VerticalAlignment="Center" .../>

<!-- Después: TextBox idéntico al real pero no interactivo -->
<TextBox x:Name="GhostDisplay"
         Padding="12,0,12,0" VerticalContentAlignment="Center"
         IsReadOnly="True" Focusable="False" IsHitTestVisible="False" .../>
```

### Bug 2 — Ghost aparecía instantáneamente sin debounce

**Causa:** `UpdateGhost()` actualizaba el ghost en cada `TextChanged` sin ningún delay.

**Fix:** `ScheduleGhostUpdate()` oculta el ghost inmediatamente y lanza `Task.Delay(300, ct)` con `CancellationTokenSource`. Si el usuario sigue tecleando se cancela y reinicia. El placeholder y el "ya completado" siguen siendo instantáneos.

```csharp
_ghostDebounce?.Cancel();
_ghostDebounce = new CancellationTokenSource();
_ = DebounceGhostAsync(input, _ghostDebounce.Token);
// → muestra ghost solo si el usuario paró de escribir 300ms
```

### Bug 3 — Overlap de dominio (`hola@gma@gmail.com`)

**Causa:** Al escribir `hola@gma`, el ghost concatenaba el sufijo completo `@gmail.com`, resultando en `hola@gma@gmail.com` superpuesto.

**Fix:** Método `GetRemainingSuffix(input)` que busca el overlap más largo entre el final del input y el inicio del sufijo, y solo devuelve lo que falta:

```
input="hola"     → remaining="@gmail.com"  (sin overlap)
input="hola@"    → remaining="gmail.com"   (overlap "@")
input="hola@gma" → remaining="il.com"      (overlap "@gma")
input="hola@gmail.com" → ghost oculto     (completado)
```

```csharp
for (int len = maxOverlap; len >= 1; len--)
{
    if (input[^len..].Equals(suffix[..len], StringComparison.OrdinalIgnoreCase))
        return suffix[len..];
}
return suffix; // sin overlap → sufijo completo
```

---

## Decisión de diseño

Ver [[ADR-004 - GhostTextBox Autocompletado de Dominio en Login]] para el análisis completo de las 3 alternativas evaluadas y por qué se eligió el UserControl.

---

## Archivos

| Archivo | Tipo |
|---|---|
| `CapaUI/Core/Controls/GhostTextBox.xaml` | Nuevo |
| `CapaUI/Core/Controls/GhostTextBox.xaml.cs` | Nuevo |
| `CapaDatos/Modelados/Empresa.cs` | Modificado |
| `CapaUI/Formularios/InicioSesion/LoginWindow.xaml` | Modificado |
| `CapaUI/Formularios/InicioSesion/LoginWindow.xaml.cs` | Modificado |
