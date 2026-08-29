---
title: "ADR-004 — GhostTextBox: Autocompletado de Dominio en Login"
type: adr
status: vigente
tags:
  - decisión
  - UI
  - login
  - control-compartido
date: 2026-06-15
updated: 2026-06-15
summary: "Al iniciar sesión, el usuario debe escribir su correo completo (ej. fabarahona280@gmail.com). El dominio de correo es el mismo para todos los usuarios de la…"
scope:
  - CapaDatos/Modelados
  - CapaUI/Core/Controls
symbols:
  - CancellationTokenSource
  - ControlTemplate
  - FormattedText
  - GhostSuffix
  - GhostTextBox
  - IngresarAsync
  - InnerGotFocus
  - InnerLostFocus
  - Loaded
  - Margin
estado: implementado
---

# ADR-004 — GhostTextBox: Autocompletado de Dominio en Login

## Contexto

Al iniciar sesión, el usuario debe escribir su correo completo (ej. `fabarahona280@gmail.com`). El dominio de correo es el mismo para todos los usuarios de la empresa. Se necesita:

1. **Auto-completar visualmente** el dominio mientras se escribe (ghost text gris, patrón Windows 11)
2. **Tab / →** para aceptar la sugerencia
3. **Dominio configurable** desde la tabla `empresa` de Supabase (si cambia, se refleja en todas las PCs)
4. **No revelar** si un usuario existe (el dominio se muestra siempre, no solo cuando el alias es válido)

---

## Decisión

Implementar un **UserControl reutilizable `GhostTextBox`** en `CapaUI/Core/Controls/`.

---

## ¿Por qué un UserControl y no las otras opciones?

Se evaluaron 3 alternativas:

| Alternativa | Descartada porque |
|---|---|
| **TextBlock superpuesto (Plan A)** | Funciona, pero hay que duplicar el Grid+TextBlock+handlers en cada lugar que se use. No escala. |
| **Adorner custom (Plan B)** | `FormattedText` es verboso, ClearType se comporta raro con fondos transparentes, y el Adorner es más código (~70 líneas) sin ventaja visual sobre un TextBlock |
| **Librería NuGet** | No existe librería gratuita que haga ghost text inline. Todas (WPFTextBoxAutoComplete, WPF Toolkit, HandyControl) usan dropdown o selección azul, no texto gris fantasma |

**El UserControl (Plan C) gana porque:**

1. **Ya tenemos el patrón** — `SuggestionSearchBox` se hizo exactamente igual: UserControl + DependencyProperties + evento. El equipo ya sabe mantenerlo.
2. **Cero código por cada uso nuevo** — en cualquier vista futura es una línea de XAML: `<controls:GhostTextBox GhostSuffix="@dominio.com"/>`
3. **Fidelidad Windows 11** — ghost text gris real, no selección azul
4. **Separación limpia** — el TextBox.Text nunca contiene el ghost text hasta que el usuario lo acepta. No hay estados fantasma en el binding.

---

## Diseño técnico

### Componentes

```
CapaUI/Core/Controls/
  GhostTextBox.xaml        ← Grid con TextBox ghost (read-only) + TextBox input
  GhostTextBox.xaml.cs     ← DependencyProperties + debounce + overlap detection
```

### DependencyProperties expuestas

| Propiedad | Tipo | Modo | Descripción |
|---|---|---|---|
| `Text` | `string` | TwoWay | Texto que el usuario escribe (sin el sufijo) |
| `GhostSuffix` | `string` | OneWay | El dominio a mostrar como ghost (ej. `@gmail.com`) |
| `Placeholder` | `string` | OneWay | Watermark cuando el campo está vacío |

### Eventos

| Evento | Cuándo se dispara |
|---|---|
| `SuffixAccepted` | Cuando el usuario presiona Tab o → para aceptar |
| `InnerGotFocus` | Cuando el TextBox interno recibe foco |
| `InnerLostFocus` | Cuando el TextBox interno pierde foco |
| `TextChanged` | Cuando el texto cambia |

### Método público

| Método | Retorno | Descripción |
|---|---|---|
| `GetFullText()` | `string` | Devuelve alias + sufijo faltante (siempre el correo completo) |

### Comportamiento

| Acción | Resultado |
|---|---|
| Escribir texto | Ghost aparece 300ms después de parar — muestra solo el sufijo que falta |
| Tab o → (al final del texto) | Acepta: el sufijo faltante se concatena al Text |
| Escribir parte del dominio (`@gma`) | Ghost completa solo lo que falta (`il.com`) |
| Backspace | Borra normalmente, ghost se recalcula |
| Campo vacío | Muestra el Placeholder instantáneamente |

### Uso en LoginWindow

```xml
<controls:GhostTextBox x:Name="TxtEmail"
    Placeholder="Usuario"
    InnerGotFocus="Input_GotFocus" InnerLostFocus="Input_LostFocus"
    TextChanged="Fields_Changed"/>
```

---

## Cambios en Supabase

```sql
ALTER TABLE empresa ADD COLUMN IF NOT EXISTS dominio_correo text DEFAULT '@gmail.com';
```

---

## Ejecución — Sesión 2026-06-15

> [!success] Implementado y compilado con 0 errores

### Archivos creados

| Archivo | Descripción |
|---|---|
| `CapaUI/Core/Controls/GhostTextBox.xaml` | UserControl — Grid con TextBox ghost + TextBox input |
| `CapaUI/Core/Controls/GhostTextBox.xaml.cs` | Lógica: 3 DPs, eventos, debounce 300ms, overlap detection, `GetFullText()` |

### Archivos modificados

| Archivo | Cambio |
|---|---|
| `CapaDatos/Modelados/Empresa.cs` | +1 propiedad: `[Column("dominio_correo")] public string? DominioCorreo` |
| `LoginWindow.xaml` | xmlns `controls` agregado; `TextBox TxtEmail` → `GhostTextBox TxtEmail` |
| `LoginWindow.xaml.cs` | +`LoginWindow_Loaded` carga dominio; `IngresarAsync` usa `GetFullText()` |

### Migración Supabase

```
Nombre: add_dominio_correo_to_empresa
Estado: ✅ Aplicada
```

### Flujo resultante

```
1. LoginWindow se abre
2. Loaded → RepositorioEmpresa.ObtenerAsync() → lee dominio_correo
3. TxtEmail.GhostSuffix = "@gmail.com" (o lo que diga la tabla)
4. Usuario escribe "fabarahona280" → 300ms después aparece ghost "@gmail.com"
5. Tab/→ acepta, o hace clic en Entrar sin aceptar
6. IngresarAsync() → GetFullText() → "fabarahona280@gmail.com"
7. AuthService.LoginAsync("fabarahona280@gmail.com", password)
```

---

## Fixes post-implementación

### Fix 1 — Desalineación vertical del ghost text

**Problema:** El `TextBlock` ghost tenía las letras ligeramente desplazadas verticalmente respecto al TextBox real.

**Causa raíz:** `TextBlock` y `TextBox` usan pipelines de renderizado distintos. El TextBox tiene un `ScrollViewer` interno (`PART_ContentHost`) que introduce un offset vertical que no puede igualarse desde XAML con `Margin` o `Padding` en un `TextBlock`.

**Fix:** Reemplazar el `TextBlock` ghost por un segundo `TextBox` con las mismas propiedades pero no interactivo. Al compartir el mismo `ControlTemplate`, el texto se renderiza en la posición idéntica pixel a pixel.

```xml
<!-- ❌ TextBlock (desalineado) -->
<TextBlock Margin="12,0" VerticalAlignment="Center" Foreground="#B0B8C4"/>

<!-- ✅ TextBox idéntico pero no interactivo -->
<TextBox Padding="12,0,12,0" VerticalContentAlignment="Center"
         Foreground="#B0B8C4" Background="Transparent"
         IsReadOnly="True" Focusable="False" IsHitTestVisible="False"/>
```

**Regla para el futuro:** Si necesitas superponer texto sobre un `TextBox` en WPF, siempre usar otro `TextBox` (read-only), nunca un `TextBlock`. Los pipelines difieren y el offset del `ScrollViewer` interno no es predecible desde fuera.

---

### Fix 2 — Ghost aparecía instantáneamente

**Problema:** El ghost se mostraba en cada tecla sin ningún delay, lo que resultaba en un parpadeo visual agresivo.

**Fix:** Debounce de 300ms con `CancellationTokenSource`. El ghost se oculta mientras el usuario escribe y aparece solo cuando para 300ms. El placeholder y "ya completado" siguen siendo instantáneos.

```csharp
_ghostDebounce?.Cancel();
_ghostDebounce = new CancellationTokenSource();
_ = DebounceGhostAsync(input, _ghostDebounce.Token);

private async Task DebounceGhostAsync(string input, CancellationToken ct)
{
    await Task.Delay(300, ct);          // cancela si el usuario sigue escribiendo
    Dispatcher.Invoke(() => ShowGhostFor(InnerBox.Text));
}
```

---

### Fix 3 — Overlap de dominio (`hola@gma@gmail.com`)

**Problema:** Si el usuario escribía `hola@gma`, el ghost concatenaba el sufijo completo encima: `hola@gma@gmail.com`.

**Causa raíz:** El ghost siempre añadía `GhostSuffix` completo sin detectar si el usuario ya había empezado a escribirlo.

**Fix:** `GetRemainingSuffix(input)` busca el overlap más largo entre el final del input y el inicio del sufijo, y devuelve solo lo que falta:

| Input escrito | GhostSuffix | Ghost muestra |
|---|---|---|
| `hola` | `@gmail.com` | `hola@gmail.com` |
| `hola@` | `@gmail.com` | `hola@gmail.com` (overlap `@`) |
| `hola@gma` | `@gmail.com` | `hola@gmail.com` (overlap `@gma`) |
| `hola@gmail.com` | `@gmail.com` | _(oculto, ya completado)_ |

```csharp
private string GetRemainingSuffix(string input)
{
    int maxOverlap = Math.Min(input.Length, GhostSuffix.Length);
    for (int len = maxOverlap; len >= 1; len--)
    {
        if (input[^len..].Equals(GhostSuffix[..len], StringComparison.OrdinalIgnoreCase))
            return GhostSuffix[len..];
    }
    return GhostSuffix;
}
```

---

## Seguridad

El dominio se muestra **siempre** desde que se abre la ventana — no revela si un alias existe. Se carga una sola vez en `Loaded`, no por keystroke. Fallback a `@gmail.com` si Supabase no responde.

---

## Reutilización futura

```xml
<controls:GhostTextBox GhostSuffix="-HN" Placeholder="Código de producto"/>
<controls:GhostTextBox GhostSuffix="@bimbo.hn" Placeholder="Correo empleado"/>
```
