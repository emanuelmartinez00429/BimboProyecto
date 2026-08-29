---
title: Guardado sin Refetch — Aplicar en memoria la respuesta del servidor
type: patron
status: vigente
tags:
  - patron
  - performance
  - supabase
  - wpf
date: 2026-08-20
updated: 2026-08-20
summary: "Un patrón muy fácil de caer en él: \"guardé algo → para que la UI quede consistente, recargo todo desde el servidor\". Es simple de escribir y parece correcto…"
scope:
  - CapaDatos/Repositories/Pesaje
symbols:
  - RepartirTaraExtraAsync
  - RepositorioBase
  - Result<T>
  - Task<bool>
aliases:
  - Guardado optimista con confirmación
  - Evitar refetch tras escritura
---

# Guardado sin Refetch — Aplicar en memoria la respuesta del servidor

> [!abstract] El problema central
> Un patrón muy fácil de caer en él: "guardé algo → para que la UI quede consistente, recargo todo desde el servidor". Es simple de escribir y **parece** correcto porque garantiza que la UI refleja la BD. El costo que esconde: cada guardado pasa a costar N round trips en vez de 1, y con latencia de red real (no localhost) eso es la diferencia entre "instantáneo" y "colgado".

---

## El problema, con números reales

Detectado en [[Módulo Pesaje]] — el botón "Seguir pesando" tardaba varios segundos en responder. El diagnóstico (con [[Base Repository con TryAsync|cronometraje]] agregado a `RepositorioBase`, y verificado contra la BD real vía MCP de Supabase: índices correctos, triggers triviales, RLS plano) descartó la base de datos — las queries se resuelven en microsegundos. El costo era **arquitectónico**:

```
Guardar un pesaje (antes):
  1. AnularEntradaAsync   (solo si edita)  ── round trip
  2. CrearEntradaAsync    (INSERT)         ── round trip
  3. GetProductosAsync → movimiento_productos + productos(*) ── round trip
  4. GetProductosAsync → vista de tara                       ── round trip
  5. GetProductosAsync → entradas_producto                   ── round trip

  4-5 round trips HTTPS para reflejar UNA fila que ya conocíamos.
```

A ~200–350 ms por viaje (red de planta, no localhost), eso son 1–5 segundos de pantalla congelada por cada pesada — multiplicado por cada pesaje del día.

## La idea

**La respuesta de la propia escritura ya trae lo que hace falta para actualizar la UI.** Un `INSERT` vía Supabase/PostgREST devuelve la fila resultante — incluidos los valores que calculó un trigger `BEFORE INSERT`. Tirar esa respuesta y volver a pedir "todo el padre" para leer exactamente esos mismos valores es el desperdicio.

```
Guardar un pesaje (después):
  1. AnularEntradaAsync   (solo si edita)  ── round trip
  2. CrearEntradaAsync    (INSERT, devuelve EntradaDto completo) ── round trip

  1-2 round trips. La UI se actualiza con el DTO que ya llegó.
```

No es un guardado optimista clásico (que muestra algo *antes* de que el servidor confirme, y lo revierte si falla). Acá **nada se muestra hasta que la BD confirmó** — la diferencia es que, una vez confirmado, se aplica el resultado a la colección en memoria en vez de volver a preguntarle al servidor algo que acaba de contestar.

---

## Implementación

### 1. El repositorio deja de descartar la respuesta del INSERT

```csharp
// Antes — CapaDatos/Repositories/Pesaje/PesajeRepository.cs
public Task<Result<int>> CrearEntradaAsync(...) =>
    TryAsync(async () => {
        var r = await client.From<EntradaProducto>().Insert(nueva);
        return r.Models.First().idPesaje;   // se tira todo lo demás
    }, "Registrar pesaje");

// Después
public Task<Result<EntradaDto>> CrearEntradaAsync(...) =>
    TryAsync(async () => {
        var r = await client.From<EntradaProducto>().Insert(nueva);
        // trg_calcular_pesos_entrada es BEFORE INSERT: la fila que vuelve
        // ya trae tara_individual/tara_total/neto calculados por la BD.
        return MapEntrada(r.Models.First());   // reutiliza el mapper que ya existía
    }, "Registrar pesaje");
```

> [!important] Confirmar que el trigger es BEFORE, no AFTER
> Esto solo funciona si el cálculo corre **antes** del INSERT (`BEFORE INSERT`) — así la fila que PostgREST devuelve ya lo tiene. Un trigger `AFTER INSERT` modifica la fila después de que la respuesta ya se armó, y el patrón no aplica sin un segundo viaje. Se verificó con el MCP de Supabase (`information_schema.triggers`) antes de asumirlo — ver [[Sesión 2026-08-20 - Guardado de pesajes sin refetch]].

### 2. El ViewModel aplica el DTO en vez de recargar

```csharp
// CapaUI/.../PesajeViewModel.cs
public async Task<bool> GuardarEntradaAsync(ProductoCamion producto, EntradaPesaje snapshot, EntradaPesaje? editando)
{
    if (editando is not null)
    {
        var ra = await _repo.AnularEntradaAsync(editando.Id);
        if (!ra.Success) { /* toast */ return false; }
        producto.Entradas.Remove(editando);   // se quita de memoria, no se recarga
    }

    var r = await _repo.CrearEntradaAsync(...);
    if (!r.Success) { /* toast; si veníamos de editar, ahí sí toca refetch de rescate */ return false; }

    AgregarEntradaEnMemoria(producto, r.Value!);   // aplica el DTO confirmado
    return true;
}
```

Cambiar la firma de `Task` a `Task<bool>` importa: quien llama necesita saber si el guardado salió bien para decidir si limpia el formulario o lo deja con los datos escritos para reintentar.

### 3. El modal se queda abierto y se limpia solo tras éxito

Un efecto colateral casi obligado: si guardar ya no dispara un refetch que reemplaza los objetos, **no hay motivo para cerrar el modal**. Antes "Seguir pesando" cerraba el modal (para forzar la recarga limpia) y el operador tenía que reabrirlo para la siguiente pesada — un problema de UX que el refetch venía escondiendo. Ver el detalle del ciclo de vida del modal en [[Sesión 2026-08-20 - Guardado de pesajes sin refetch]].

---

## Cuándo NO aplica

- **El servidor puede transformar el dato de formas que el cliente no puede recalcular sin otra consulta** (ej. un cálculo que depende de filas hermanas que no vinieron en la respuesta). Acá sí hace falta el refetch — parcial si se puede, completo si no.
- **La escritura es un UPDATE de N filas relacionadas** (ej. `RepartirTaraExtraAsync`, que reparte una tara extra entre varias pesadas ya existentes): cada fila cambia por un cálculo que depende de las demás, así que el refetch sigue siendo lo más simple y no se tocó al aplicar este patrón en Pesaje — es una decisión de alcance, no un olvido.
- **El camino de error necesita el estado real de la BD**, típicamente tras una edición fallida a mitad de camino (se anuló la vieja, la nueva no se pudo insertar): ahí sí vale el refetch completo, porque la memoria y la BD pueden haber divergido.

---

## Alternativas consideradas

| Alternativa | Por qué no |
|---|---|
| **RPC en Postgres que hace INSERT + devuelve estado completo** | Resuelve lo mismo con un solo viaje incluso en escrituras más complejas, pero exige migración a la BD. Quedó fuera de alcance en la sesión que originó este patrón — ver "Fuera de alcance" en la sesión asociada. |
| **Guardado optimista clásico (mostrar antes de confirmar, revertir si falla)** | Más rápido aún, pero el neto/tara los calcula la BD (trigger) — mostrar un valor calculado en el cliente antes de tener la confirmación real arriesga a mostrar un número que no es el que quedó guardado. |
| **Caché con invalidación fina** | Overkill para el volumen de datos de este proyecto (decenas de filas por camión); la complejidad de mantener el caché sincronizado no se paga sola a esta escala. |

---

## Relaciones

- [[Módulo Pesaje]] — origen y contexto completo del problema
- [[Base Repository con TryAsync]] — el cronometraje que confirmó que el costo era round trips, no consultas lentas
- [[Result Pattern]] — el DTO viaja envuelto en `Result<T>`, mismo contrato que cualquier otra operación
- [[Sesión 2026-08-20 - Guardado de pesajes sin refetch]] — la sesión que aplicó este patrón por primera vez
