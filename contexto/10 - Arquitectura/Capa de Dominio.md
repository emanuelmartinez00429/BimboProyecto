---
title: "Capa de Dominio — Qué es, qué contiene y cómo está en Bimbo"
type: arquitectura
status: vigente
tags:
  - arquitectura
  - clean-architecture
  - dominio
  - ddd
  - dotnet
date: 2026-05-23
updated: 2026-05-23
summary: "La capa de Dominio es el corazón del software. Representa los conceptos del negocio, la información sobre la situación del negocio y las reglas del negocio. No…"
scope:
  - CapaDominio/Entities
symbols:
  - BaseEntity
  - Email
  - Empleado
  - GestorRealtime
  - HttpClient
  - IRepository
  - IRepository<T>
  - IServiceCollection
  - MovimientoRegistradoEvent
  - PedidoConfirmadoIntegrationEvent
aliases:
  - Domain Layer
  - CapaDominio
---

# Capa de Dominio

> [!abstract] Concepto central
> La capa de Dominio es el **corazón del software**. Representa los conceptos del negocio, la información sobre la situación del negocio y las reglas del negocio. No depende de ninguna otra capa — ni de infraestructura, ni de UI, ni de Application.
>
> — Microsoft Docs, .NET Microservices Architecture

---

## Fuentes

- [Microsoft Docs — DDD y CQRS Patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/)
- [Jason Taylor's Clean Architecture template](https://github.com/jasontaylordev/CleanArchitecture)
- [eShopOnContainers — Microsoft reference app](https://github.com/dotnet-architecture/eShopOnContainers)
- [Milan Jovanović — Value Objects in .NET](https://www.milanjovanovic.tech/blog/value-objects-in-dotnet-ddd-fundamentals)
- [Ardalis Clean Architecture](https://github.com/ardalis/CleanArchitecture)
- Eric Evans — *Domain-Driven Design* (libro base del DDD)
- Vaughn Vernon — *Effective Aggregate Design*

---

## Regla de dependencia

```
Domain  ←  Application  ←  Infrastructure  ←  UI
```

El Dominio **no referencia nada**. Ni Entity Framework, ni Supabase SDK, ni HttpClient, ni ningún otro proyecto de la solución. Solo puede usar el BCL de .NET y NuGet puros (ej. `MediatR.Contracts`).

```
Domain.csproj
└── NO tiene ProjectReferences
└── NO tiene NuGet de infraestructura (EF, HTTP, BD)
```

---

## Estructura canónica

Basada en los tres proyectos de referencia más reconocidos:

```
Domain/
├── Common/
│   ├── BaseEntity.cs             → Id + colección de Domain Events
│   ├── BaseAuditableEntity.cs    → + Created, CreatedBy, LastModified
│   ├── BaseEvent.cs              → Clase base para Domain Events
│   └── ValueObject.cs            → Base class para Value Objects complejos
│
├── Entities/                     → Clases con identidad + comportamiento
├── ValueObjects/                 → Inmutables, igualdad por valor, sin ID
├── Enums/                        → Enumeraciones con significado de negocio
├── Events/                       → Hechos del pasado (naming en pasado verbal)
├── Exceptions/                   → Violaciones de invariantes de negocio
├── Services/                     → Domain Services (lógica cross-entity)
└── Interfaces/                   → IRepository<T>, IUnitOfWork, IAggregateRoot
```

---

## 1. Entidades (Entities)

Objetos definidos por su **identidad**, que persiste en el tiempo. Contienen **datos Y comportamiento** — no son solo bolsas de propiedades (eso es el modelo anémico, un antipatrón según Martin Fowler).

### Base class (Jason Taylor's template)

```csharp
// Domain/Common/BaseEntity.cs
public abstract class BaseEntity
{
    public int Id { get; init; }

    private readonly List<BaseEvent> _domainEvents = new();

    [NotMapped] // No persistir la colección de eventos
    public IReadOnlyCollection<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(BaseEvent domainEvent)    => _domainEvents.Add(domainEvent);
    public void RemoveDomainEvent(BaseEvent domainEvent) => _domainEvents.Remove(domainEvent);
    public void ClearDomainEvents()                      => _domainEvents.Clear();
}

// Domain/Common/BaseAuditableEntity.cs
public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTimeOffset Created      { get; set; }
    public string?        CreatedBy    { get; set; }
    public DateTimeOffset LastModified { get; set; }
    public string?        LastModifiedBy { get; set; }
}
```

### Entidad concreta con comportamiento

```csharp
// Entidad RICA — la lógica de negocio vive aquí, no en un servicio externo
public class Producto : BaseAuditableEntity
{
    public string CodigoInterno { get; private set; } = "";
    public string Nombre        { get; private set; } = "";
    public int    IdEstado      { get; private set; } = 1;

    // Comportamiento de negocio encapsulado
    public void Deshabilitar()
    {
        if (IdEstado == 2)
            throw new ProductoDomainException("El producto ya está deshabilitado.");
        IdEstado = 2;
        AddDomainEvent(new ProductoDeshabilitadoEvent(this));
    }

    public void ActualizarNombre(string nuevoNombre)
    {
        if (string.IsNullOrWhiteSpace(nuevoNombre))
            throw new ProductoDomainException("El nombre del producto no puede estar vacío.");
        Nombre = nuevoNombre.Trim();
    }
}
```

> [!warning] Modelo Anémico vs Modelo Rico
> - **Anémico** (antipatrón): La entidad solo tiene getters/setters. Toda la lógica está en servicios externos (`ProductoService.Deshabilitar(producto)`).
> - **Rico** (correcto): La entidad tiene métodos con reglas de negocio. Los invariantes están protegidos dentro de la propia clase.

---

## 2. Value Objects

Objetos sin identidad conceptual. Se definen por sus **atributos**, son **inmutables** y su igualdad es por valor.

### Regla de identificación

> Si puedo intercambiar dos instancias con los mismos valores sin que nadie note la diferencia → es un Value Object.
> Si el objeto tiene historia, ciclo de vida propio o debe rastrearse individualmente → es una Entidad.

| Concepto | ¿Entidad o VO? | Razón |
|---|---|---|
| `Producto` | Entidad | Tiene ID único, historial de cambios |
| `Email` | Value Object | Dos emails iguales son intercambiables |
| `Peso(kg)` | Value Object | `Peso(10.5)` es igual a cualquier otro `Peso(10.5)` |
| `Dirección` | Depende | VO en eShop; Entidad si tiene ciclo de vida propio |
| `Empleado` | Entidad | Tiene identidad, historial, relaciones |

### Implementación con `record` (C# 9+, recomendado para casos simples)

```csharp
// Domain/ValueObjects/Email.cs
public record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email From(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@'))
            throw new DomainException($"'{value}' no es un email válido.");
        return new Email(value.ToLowerInvariant().Trim());
    }

    public static implicit operator string(Email email) => email.Value;
    public override string ToString() => Value;
}
```

### Implementación con base `ValueObject` (para invariantes complejas)

```csharp
// Domain/Common/ValueObject.cs
public abstract class ValueObject
{
    protected abstract IEnumerable<object> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        return GetEqualityComponents()
            .SequenceEqual(((ValueObject)obj).GetEqualityComponents());
    }

    public override int GetHashCode() =>
        GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);

    public static bool operator ==(ValueObject? l, ValueObject? r) => l?.Equals(r) ?? r is null;
    public static bool operator !=(ValueObject? l, ValueObject? r) => !(l == r);
}

// Domain/ValueObjects/Peso.cs
public sealed class Peso : ValueObject
{
    public decimal Kilos { get; }

    private Peso(decimal kilos) => Kilos = kilos;

    public static Peso Cero => new(0);

    public static Peso From(decimal kilos)
    {
        if (kilos < 0)
            throw new DomainException("El peso no puede ser negativo.");
        return new Peso(kilos);
    }

    public Peso Sumar(Peso otro) => new(Kilos + otro.Kilos);
    public Peso Restar(Peso otro) => From(Kilos - otro.Kilos); // valida >= 0

    protected override IEnumerable<object> GetEqualityComponents() { yield return Kilos; }
}
```

---

## 3. Aggregates y Aggregate Roots

Un **Aggregate** es un clúster de entidades y Value Objects tratados como una unidad de consistencia. El **Aggregate Root** es la única puerta de entrada — nadie modifica directamente los hijos.

```csharp
// Del eShopOnContainers — Order es el Aggregate Root
public class Order : BaseEntity, IAggregateRoot
{
    public Address Address  { get; private set; } // Value Object
    private int? _buyerId;                        // FK a otro aggregate: SOLO EL ID

    private readonly List<OrderItem> _orderItems = new();
    public IReadOnlyCollection<OrderItem> OrderItems => _orderItems;

    // Solo el Aggregate Root puede añadir items — encapsula la invariante
    public void AddOrderItem(int productId, string nombre, decimal precio, int unidades)
    {
        var existente = _orderItems.SingleOrDefault(i => i.ProductId == productId);
        if (existente != null)
            existente.AddUnits(unidades);
        else
            _orderItems.Add(new OrderItem(productId, nombre, precio, unidades));
    }
}

// Interfaz marcadora
public interface IAggregateRoot { }
```

> [!tip] Regla práctica
> Un repositorio por cada Aggregate Root. Nunca un repositorio por tabla de BD.

---

## 4. Domain Events

Hechos del pasado expresados como objetos inmutables. Permiten comunicar cambios de estado de forma desacoplada.

**Naming convention:** siempre en pasado verbal — `ProductoDeshabilitadoEvent`, `MovimientoRegistradoEvent`, `UsuarioCreadoEvent`.

```csharp
// Domain/Common/BaseEvent.cs
public abstract class BaseEvent { }

// Domain/Events/ProductoDeshabilitadoEvent.cs
public class ProductoDeshabilitadoEvent : BaseEvent
{
    public ProductoDeshabilitadoEvent(Producto producto) => Producto = producto;
    public Producto Producto { get; }
}
```

### Flujo de despacho (patrón deferred — recomendado por Jimmy Bogard)

```
1. Entidad: AddDomainEvent(new ProductoDeshabilitadoEvent(this))
       ↓ (el evento solo se encola, no se despacha)
2. Application Command Handler: await _repository.SaveChangesAsync()
       ↓
3. DbContext/UnitOfWork: despacha todos los eventos ANTES de persistir
       ↓
4. Application Event Handler: envía email, actualiza otro aggregate, etc.
```

Los **handlers** siempre viven en la capa de **Application**, no en Dominio.

### Domain Events vs Integration Events

| | Domain Event | Integration Event |
|---|---|---|
| Scope | Mismo bounded context | Entre sistemas/microservicios |
| Ejecución | In-process (MediatR) | Out-of-process (RabbitMQ, Service Bus) |
| Consistencia | Misma transacción | Eventual consistency |
| Ejemplo | `ProductoDeshabilitadoEvent` | `PedidoConfirmadoIntegrationEvent` |

---

## 5. Domain Services

Cuando una operación de negocio **no pertenece naturalmente a una entidad específica** o involucra múltiples entidades.

| Situación | Dónde va |
|---|---|
| Lógica que solo usa datos de UNA entidad | Método en la entidad |
| Lógica cross-entity sin dueño claro | Domain Service |
| Necesita acceder a repositorios | Application Service (Command Handler) |
| Cálculo puro sin acceso a BD | Domain Service |

```csharp
// Domain/Services/PesoCalculatorService.cs — lógica de pesaje pura
// (equivalente al PesoCalculator.cs que ya existe en el proyecto)
public static class PesoCalculatorService
{
    public static decimal PesoNeto(decimal bruto, decimal taraTotal) => bruto - taraTotal;
    public static decimal TaraTotal(decimal taraExtra, decimal taraInd) => taraExtra + taraInd;
    public static decimal DiferenciaPct(decimal recibido, decimal manifestado) =>
        manifestado == 0 ? 0 : Math.Round((recibido - manifestado) / manifestado * 100, 2);
}
```

> [!warning] Señal de alerta
> Si un Domain Service necesita inyectar un `IRepository`, probablemente debe ser un **Application Service** (Command Handler), no un Domain Service.

---

## 6. Interfaces en el Dominio

Las interfaces de repositorio se definen en Dominio (o Application — hay debate legítimo), pero **siempre se implementan en Infrastructure**.

**Postura de Microsoft (eShopOnContainers):** definirlas en Dominio.
**Postura de Jason Taylor:** definirlas en Application.
**Ambas son válidas.** La clave: Infrastructure implementa lo que Dominio/Application define, nunca al revés.

```csharp
// Domain/Interfaces/IRepository.cs — contrato genérico
public interface IRepository<T> where T : IAggregateRoot
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}

// Domain/Interfaces/IAggregateRoot.cs — interfaz marcadora
public interface IAggregateRoot { }
```

---

## 7. Enums y Excepciones de Dominio

```csharp
// Domain/Enums/EstadoRegistro.cs
public enum EstadoRegistro
{
    Habilitado   = 1,
    Deshabilitado = 2
}

// Domain/Exceptions/DomainException.cs
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

// Domain/Exceptions/ProductoDomainException.cs
public class ProductoDomainException : DomainException
{
    public ProductoDomainException(string message) : base(message) { }
}
```

---

## 8. ¿Qué NO debe estar en el Dominio?

| Lo que NO va | Dónde va |
|---|---|
| `[Column]`, `[Table]`, atributos de ORM | Infrastructure |
| Cliente Supabase, `HttpClient`, APIs externas | Infrastructure |
| DTOs de presentación, ViewModels | Application / CapaUI |
| Command Handlers, Query Handlers | Application |
| Event Handlers de domain events | Application |
| `IServiceCollection`, configuración de DI | Application / Infrastructure |
| Llamadas async a bases de datos | Infrastructure |
| Notificaciones push, emails, SMS | Infrastructure |
| Lógica de mapeo (AutoMapper) | Application / Infrastructure |

---

## 9. Estado actual en Bimbo — Inventario y análisis

### `CapaDominio/` (proyecto propio)

| Archivo | Tipo | ¿Correcto? |
|---|---|---|
| `Entities/Producto.cs` | Entidad POCO | ⚠️ Correcto ubicación, pero modelo anémico (solo propiedades, sin comportamiento) |
| `Entities/Empleado.cs` | Entidad POCO | ⚠️ Ídem |
| `Entities/Cliente.cs` | Entidad POCO | ⚠️ Ídem |
| `Interfaces/IRepository.cs` | Interfaz genérica `SearchAsync` | ✅ Correcto |
| `Class1.cs` | Clase vacía (artifact de VS) | ❌ Eliminar |

### `CapaServicios/` (proyecto aparte — namespace `CapaDominio`)

Todos estos archivos usan `namespace CapaDominio` pero están en un proyecto separado que **referencia `CapaDatos` directamente** — violando la regla de oro.

| Archivo | Tipo | ¿Dónde debería estar? |
|---|---|---|
| `PesoCalculator.cs` | Lógica de negocio pura, sin dependencias | ✅ **Debe quedar en Dominio** — es un Domain Service puro |
| `Notificacion.cs` (clase + enum) | Entidad + Enum de dominio | ✅ **Debe quedar en Dominio** — son conceptos puros |
| `ServicioBuscador.cs` | Buscador de módulos (datos hardcodeados) | ✅ **Puede quedar en Dominio** — sin dependencias externas |
| `ServicioNotificaciones.cs` | Estado en memoria, sin BD | ⚠️ **Podría estar en Application** — es orquestación, no regla de negocio |
| `servicioSesionActual.cs` | Estado de sesión con lifecycle | ⚠️ **Podría estar en Application** — depende de cuánto se acople a infra |
| `SesionActual.cs` | Estado hardcodeado (legado) | ❌ **Código muerto en CapaUI** — no eliminable porque BimboPesaje lo usa |
| `GestorRealtime.cs` | Supabase Realtime, referencias a `CapaDatos.Modelados` | ❌ **Debe estar en Infrastructure** — dependencia directa a Supabase SDK y modelos de BD |
| `GestorNotificaciones.cs` | Coordinador entre Realtime y Notificaciones | ❌ **Debe estar en Infrastructure o Application** — depende de GestorRealtime |
| `ServicioPerfilUsuario.cs` | Carga perfil desde BD | ❌ **Debe estar en Application** — llama a repositorio concreto de CapaDatos |
| `ServicioLogo.cs` | Descarga logos desde Supabase Storage | ❌ **Debe estar en Infrastructure** — llama a BD y al bucket de Supabase directamente |

### Problema central

`CapaServicios` **referencia `CapaDatos` directamente** pero usa `namespace CapaDominio`. Esto crea un acoplamiento oculto:

```
CapaServicios (namespace CapaDominio)
    ↓ referencia directa
CapaDatos
    ↓ referencia
CapaDominio

→ Violación: "CapaDominio" (vía CapaServicios) depende de CapaDatos
```

---

## 10. Hoja de ruta para el Dominio en Bimbo

### Lo que está bien
- `IRepository<T>` con solo `SearchAsync` — contrato limpio sin expresiones LINQ
- `PesoCalculator` — lógica pura sin dependencias externas
- Entidades en `CapaDominio/Entities/` — sin referencias a ORM ni infraestructura
- `CapaDominio.csproj` no tiene dependencias a otros proyectos de la solución

### Deuda técnica identificada

| Prioridad | Acción |
|---|---|
| Media | Agregar `BaseEntity` con colección de Domain Events |
| Media | Cambiar entidades a modelo rico (métodos con lógica de negocio) |
| Media | Extraer `GestorRealtime` y `ServicioLogo` a `CapaDatos` (Infrastructure) |
| Media | Mover `ServicioPerfilUsuario` a `CapaAplicacion` con DI |
| Baja | Agregar `enum EstadoRegistro` para reemplazar magic numbers (`idEstado == 1`) |
| Baja | Agregar excepciones de dominio tipadas por módulo |
| Baja | Eliminar `Class1.cs` |

---

## Relaciones

- [[Clean Architecture]] — El framework conceptual que define estas capas
- [[Repository Pattern]] — Implementación del acceso a datos
- [[Result Pattern]] — Manejo de errores sin excepciones como flujo de control
- [[Arquitectura Actual]] — Estado actual del proyecto Bimbo
- [[SOLID]] — Los principios que hacen posible este diseño
