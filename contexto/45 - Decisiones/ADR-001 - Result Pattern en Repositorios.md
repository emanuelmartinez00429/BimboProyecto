# ADR-001 — Result Pattern en Repositorios

**Fecha:** 2026-05-21  
**Estado:** Aceptado

---

## Contexto

Los repositorios originales retornaban datos directamente o propagaban excepciones al ViewModel. El manejo de errores era inconsistente: algunas vistas tenían `try/catch`, otras no, y los errores de red o Supabase llegaban sin información útil al usuario.

---

## Opciones consideradas

1. **Try/catch en cada ViewModel** — sencillo de implementar pero duplica lógica en cada capa. Los errores quedan acoplados a quien llama.
2. **Excepciones personalizadas** — permite tipado de errores pero sigue usando excepciones como flujo de control, lo cual es costoso y dificulta el razonamiento.
3. **Result\<T\> con base repository TryAsync** — todos los repositorios heredan de `RepositorioBase`, que centraliza el try/catch en un método `TryAsync`. Los VMs reciben `Result<T>` y deciden qué hacer.

---

## Decisión

**Opción 3: Result\<T\> con base repository.**

`RepositorioBase.TryAsync<T>` envuelve cualquier operación Supabase y retorna `Result<T>` con éxito o error. Los repositorios concretos no tienen try/catch propios. Los ViewModels comprueban `r.IsSuccess` y actúan en consecuencia.

---

## Consecuencias

- **Positivo:** errores explícitos en el tipo de retorno, sin excepciones inesperadas en producción.
- **Positivo:** un solo lugar donde agregar logging, telemetría o reintentos en el futuro.
- **Positivo:** los ViewModels son más limpios y predecibles.
- **Negativo:** requiere que todos los repositorios nuevos hereden `RepositorioBase` — es una convención que hay que recordar.
- **Negativo:** los `Result<T>` anidados pueden ser verbosos en operaciones compuestas.

---

## Archivos clave

- `CapaDatos/Repositories/RepositorioBase.cs` — `TryAsync<T>`
- `CapaAplicacion/Common/Result.cs` — tipo `Result<T>`
