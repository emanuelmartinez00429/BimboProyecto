---
title: "ADR-028 — Previsualización de UserControls en el diseñador de VS"
tags:
  - adr
  - decision
  - wpf
  - xaml
  - diseñador
date: 2026-09-09
estado: aceptado
---

# ADR-028 — Previsualización de UserControls en el diseñador de VS

## Contexto

Los modales y varias `*View.xaml` no se previsualizan: el lienzo queda en blanco con solo el recuadro del control, y en Salida aparece `TaskCanceledException` (el subrogado del diseñador que se cayó — el síntoma, no la causa).

El diagnóstico está en [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]]. En resumen, son dos cosas distintas:

1. El diseñador no ejecuta `App.xaml`, así que **ningún** `{StaticResource}` a una clave de `Application.Resources` resuelve.
2. Aparte, un `{StaticResource}` usado en un **atributo del propio elemento raíz** no lo puede rescatar ningún merge local: los atributos del raíz se aplican antes de que se pueble su `<UserControl.Resources>`, así que declarar la clave ahí es una referencia hacia adelante.

Los 16 modales del proyecto caen en (2) por el `Converter={StaticResource RestarMargen}` de `MaxWidth`/`MaxHeight`. Un intento previo (Engram #221, topic `pattern/wpf-designer-preview`) agregó `d:DesignWidth`, `FallbackValue`, un ctor sin parámetros y un `DesignerProperties.GetIsInDesignMode`, y después un merge local de `Styles.xaml` — nada de eso podía funcionar, porque ninguno ataca (2).

**Restricción del dueño del repo:** *no creo que hacer cosas que no se vean sea buena práctica.* Es decir: nada de mecanismos que arreglen el lienzo por un camino que no se lea en el código del control.

## Decisión

**Referenciar el converter con `{x:Static}` contra una instancia compartida del tipo**, y **conservar en cada control el merge de los diccionarios que usa**.

```csharp
public sealed class RestarMargenConverter : IValueConverter
{
    public static readonly RestarMargenConverter Instancia = new();
}
```

```xml
Converter={x:Static conv:RestarMargenConverter.Instancia}
```

`{x:Static}` resuelve contra el tipo CLR, sin pasar por ningún diccionario de recursos: funciona igual en runtime, en el diseñador y en pruebas, y **se lee en la línea** — quien la lee ve de dónde sale el converter sin tener que saber qué archivo lo cargó.

Complementos que van en el mismo paquete:

- **Inyección por constructor** en vez de `App.Services.GetRequiredService` dentro del `.ctor` del modal (AGENTS.md, regla 9). Con eso el camino del ctor sin parámetros queda limpio **por construcción** y se elimina el `DesignerProperties.GetIsInDesignMode`, que era un parche.
- El **ctor sin parámetros pasa a ser el constructor de diseño**: siembra datos de muestra para que el lienzo muestre la pantalla real y no una tarjeta vacía.

## Alternativas consideradas

| Opción | Pro | Contra | ¿Elegida? |
|---|---|---|---|
| **A · `DesignTimeResources.xaml` a nivel proyecto** — archivo real, versionado, referenciado en el `.csproj` con `<ContainsDesignTimeResources>`, que mergea `Styles.xaml` y define los converters | Mecanismo **oficial** de WPF/Blend. Arregla los 16 modales y las `*View` de un saque. Cero costo en runtime. Es un archivo visible en el repo, análogo a `appsettings.Development.json` | Es una **copia paralela del cableado de `App.xaml`** que se desincroniza en silencio: agregás un converter a `App.xaml`, el diseñador sigue andando hasta que alguien lo usa en un raíz. No arregla la fragilidad de fondo — el control sigue sin poder instanciarse fuera de la app (pruebas, otro host). Y *solo lo carga el diseñador* es justo el tipo de mecanismo invisible que la restricción del dueño pide evitar: el `.xaml` del modal no da ninguna pista de por qué anda | ❌ |
| **B · Migrar `{StaticResource}` a `{DynamicResource}`** en los controles compartidos | Degrada sin excepción si la clave falta | **No puede arreglar el caso real.** `Binding` no es `DependencyObject`, su propiedad `Converter` no es `DependencyProperty` y no acepta referencias diferidas. Tampoco vale para `x:Key`, ni dentro de un `Freezable` ya sellado. Además paga búsqueda en runtime por cada uso | ❌ |
| **C · `SharedResourceDictionary`** (subclase que cachea la instancia por `Source`) para que cada control mergee sin re-parsear | Evitaría re-parsear 1454 líneas por control | El costo que quería resolver **no existe**: medido, `Styles.xaml` tarda **0,4 ms** y el modal entero 4–5 ms, en un clic del usuario. Es una clase de caché estática de más, con sus propios problemas conocidos en el subrogado. Y tampoco arregla el atributo del raíz | ❌ |
| **D · Mover lo de `App.xaml` a `Styles.xaml`** para que `Styles.xaml` sea autosuficiente, más alguna de las anteriores | Deja un solo diccionario que lo trae todo | Sigue **sin** arreglar el atributo del raíz: el converter quedaría igual en `UserControl.Resources`, léxicamente después del atributo. Y para los colores `Empresa*` es **activamente peligroso** — ver el aviso de abajo | ❌ |

> [!danger] Los colores `Empresa*` NO se pueden mover a `Styles.xaml`
> `EmpresaThemeService.Aplicar()` escribe el tema por empresa **directamente en `Application.Current.Resources`** (ver [[ADR-019 - Configuración de empresa y tema dinámico global]]). Un `{DynamicResource}` busca hacia arriba: recursos del elemento → ancestros → `Application`. Si `Styles.xaml` definiera `EmpresaPrimaryBrush` y un control lo mergeara localmente, **ganaría la copia local** y el control quedaría pintado con el azul por defecto, ignorando el color de la empresa — en silencio, sin error.
>
> Verificado el 2026-09-09: con `Application.Resources["EmpresaPrimaryBrush"]` en `#B1002E` y un diccionario mergeado en el control con `#1E3A8A`, el `DynamicResource` resuelve a **`#1E3A8A`**.
>
> Por eso la opción D solo sería aplicable a los **converters** (que no son tematizables), nunca a los colores.
| **E · `{x:Static}` más merge local de los diccionarios usados** | Ataca la causa exacta, en el único punto donde está. Se lee en la línea: nada invisible. Hace al control **autosuficiente** — se puede instanciar en el diseñador, en una prueba o en otro host. Sin costo relevante (0,4 ms, medido). Sin archivos ni infraestructura nueva | El merge local aparece en 16 archivos en vez de una vez sola. Un converter con singleton público es una convención que hay que sostener | ✅ |

> [!note] Por qué no A, dicho sin vueltas
> A no es una mala práctica — es el mecanismo oficial y en muchos proyectos es la respuesta correcta. Se descartó por dos razones concretas de **este** repo: (i) duplica en un segundo archivo un cableado que ya vive en `App.xaml`, y esa clase de duplicación acá ya produjo el problema documentado en [[Anatomia compartida de los modales]] (`PesajeModalStyles.xaml` como familia paralela, hoy [[Deuda Técnica - Pendientes|P-042]]); (ii) deja el control tan no-instanciable como antes, solo que con una muleta global encima. E arregla el control; A arregla el lienzo.

## Consecuencias

**Se gana**

- Los controles migrados se previsualizan, y además se pueden instanciar en una prueba sin levantar la app.
- Se elimina un `App.Services.GetRequiredService` del constructor de un modal (AGENTS.md regla 9) y con él la necesidad del `DesignerProperties.GetIsInDesignMode`.
- El diagnóstico queda reproducible con un arnés de diez líneas, sin depender del log del diseñador.

**Se sacrifica y queda pendiente**

- La regla de [[Anatomia compartida de los modales]] — *un modal nuevo no debe declarar ninguno de estos estilos en su `UserControl.Resources`* — sigue vigente y **no cambia**: prohíbe **redefinir** estilos, no mergear el diccionario compartido. Conviene leerlas juntas para que nadie interprete el merge como permiso para copiar estilos.
- Quedan **15 modales y las `*View.xaml`** con el patrón viejo → [[Deuda Técnica - Pendientes|P-057]].
- Cuando los 16 estén migrados, `RestarMargen` sale de `App.xaml`. Ahí conviene revisar los otros cinco converters y aplicar la idea de la opción D **solo a converters**, nunca a los colores `Empresa*`.
- **El lienzo se dibuja sin los colores de empresa.** El fondo degradado y los acentos usan `{DynamicResource EmpresaPrimaryColor}` y familia, que viven en `Application.Resources` porque el tema los reescribe ahí. Como el diseñador no instancia `App`, esos `DynamicResource` no resuelven — pero **degradan sin excepción**, que es justo para lo que sirve `DynamicResource`: la estructura, los estilos compartidos, el layout y los datos de muestra se ven bien; el marco sale sin color. Es aceptable: el lienzo sirve para maquetar, no para aprobar colores.
  Si algún día se quiere el color fiel en el lienzo, el mecanismo correcto es un `DesignTimeResources.xaml` que contenga **únicamente** los seis colores `Empresa*` por defecto. Ahí la opción A sí encaja y no es una muleta: esas claves *pertenecen* al ámbito `Application`, y un archivo que solo carga el diseñador es el único lugar donde se pueden definir sin eclipsar el tema en runtime. Nótese que es lo contrario de meterlas en `Styles.xaml`.
- Sigue abierta una pregunta de diseño más de fondo, que este ADR **no** resuelve: que el modal se ate a `RelativeSource AncestorType=Border` para calcular su propio tamaño invierte el contrato de layout (lo natural en WPF es que el contenedor limite al hijo, con `Padding` en el overlay y `MaxWidth` en el modal). Se dejó como está a propósito: cambiarlo toca el `ModalOverlay` que comparten todos los modales de la pantalla y `AplicarMarcoSelector`, que asigna `Width`/`Height` a mano. Es un refactor con su propia validación visual, no un efecto colateral de este fix.

---

## Relaciones

- [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]] — el hecho de WPF y la evidencia
- [[Anatomia compartida de los modales]] — receta y plan de réplica
- [[Deuda Técnica - Pendientes]] — P-057 (modales que faltan), P-042 (familia paralela de estilos)
- [[Arquitectura Actual]]
