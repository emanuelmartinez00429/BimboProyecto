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
- Quedan **16 modales y 12 `*View.xaml`** sin previsualizarse → [[Deuda Técnica - Pendientes|P-057]]. El barrido del 2026-09-09 mostró que son dos problemas distintos: a los modales les falta antes que nada el **constructor sin parámetros** (ninguno lo tiene), mientras que las vistas ya lo tienen y solo les falta el merge. Detalle en [[Anatomia compartida de los modales]].
- Cuando los 16 estén migrados, `RestarMargen` sale de `App.xaml`. Ahí conviene revisar los otros cinco converters y aplicar la idea de la opción D **solo a converters**, nunca a los colores `Empresa*`.
## Addendum 2026-09-09 — la opción A vuelve, acotada a los colores

Quitar la excepción **no alcanzó** para que el lienzo sirviera. El control cargaba bien, pero se veía un rectángulo vacío, y un lienzo que parece vacío es indistinguible de uno roto.

El motivo: el marco degradado y los acentos usan `{DynamicResource EmpresaPrimaryColor}` y familia, que viven en `Application.Resources` porque el tema los reescribe ahí. Sin `App`, esos `DynamicResource` no resuelven — **degradan sin excepción**, que es lo correcto — y el marco sale transparente. Y como este modal está diseñado sobre ese marco azul, **casi todo su texto es blanco**: sin el azul, queda blanco sobre blanco.

Se agregó entonces `CapaUI/Properties/DesignTimeResources.xaml` con los seis colores `Empresa*`, sus brushes y los seis converters de `App.xaml`, marcado en el `.csproj` con `ContainsDesignTimeResources`. **Esta es la opción A, y acá sí corresponde**, por lo mismo que la hacía mala como solución general: esas claves *pertenecen* al ámbito `Application`. Es el único lugar donde se pueden definir sin eclipsar el tema en runtime — exactamente lo contrario de meterlas en `Styles.xaml`.

Dos cosas verificadas que contradicen la receta clásica y conviene no re-descubrir:

1. **La `Condition="'$(BuildingProject)'!='true'"` no sirve en un proyecto SDK-style.** `BuildingProject` todavía no está definido cuando MSBuild evalúa el cuerpo del `.csproj` (lo define `Microsoft.Common.CurrentVersion.targets`, que se importa después), así que da verdadera igual y el archivo entra en el build normal.
2. **Tampoco haría falta condicionarlo.** El diseñador de VS no usa el build de diseño para esto: copia a su caché el `CapaUI.dll` del build **normal**. Si el diccionario no está en ese ensamblado, el lienzo no lo ve. Queda entonces sin `Condition`, y el BAML (~1 KB) vive en el ensamblado de forma **inerte**: `App.xaml` no lo referencia y nadie lo carga en runtime.

Con esto el lienzo muestra el modal entero: marco azul, cabecera, tarjeta blanca y las tres filas de muestra.
- ~~Sigue abierta una pregunta de diseño más de fondo, que este ADR **no** resuelve: que el modal se ate a `RelativeSource AncestorType=Border` para calcular su propio tamaño invierte el contrato de layout.~~ **Eso no era una pregunta de diseño: era el bug.** Ver el Addendum 2.

## Addendum 2 · 2026-09-09 — la causa real: fuga del árbol visual y colapso a 0×0

**El bug tenía dos capas y el cuerpo de este ADR solo resolvió la primera.** Vale dejarlo escrito porque la primera capa es la vistosa —tira excepción, se ve en el log— y la segunda es la que de verdad dejaba el lienzo vacío, en silencio.

| | Capa 1 | Capa 2 |
|---|---|---|
| **Qué pasa** | `Converter={StaticResource RestarMargen}` en un atributo del raíz | El binding ya parsea, se evalúa… **y resuelve** |
| **Por qué** | Referencia hacia adelante; en el diseñador no hay `App` | El diseñador aloja el control dentro del árbol visual **del propio Visual Studio**, que también tiene `Border`. `RelativeSource AncestorType=Border` se escapa del control y engancha uno de esos |
| **Síntoma** | `XamlParseException` → el subrogado se cae → `TaskCanceledException` en Salida | Ninguno. `FallbackValue` **nunca entra**, porque el binding no falla |
| **El daño** | No se instancia | Primer measure: ese `Border` mide 0 → `Math.Max(0, 0-48) = 0` → `MaxWidth=0` → **el modal colapsa a 0×0 y no se recupera** |

El recuadro de 880×620 que se veía en el lienzo era el artboard de `d:DesignWidth`. Adentro había un control de cero por cero.

Medido (mismo binario, misma `Application` vacía):

```
sin ancestro Border  -> MaxWidth=880   880x620     <- lo que reproducía el arnés
con ancestro Border  -> MaxWidth=0       0x0       <- lo que pasa en el diseñador
idem, 5 ciclos       -> MaxWidth=0       0x0       <- no se recupera
```

> [!warning] Lección de método, no de WPF
> El arnés que reproducía «la condición del diseñador» siempre armaba el `Border` **ya dimensionado**, y por eso daba verde mientras el lienzo seguía vacío. La condición a reproducir no era *«sin ancestro»* sino *«con ancestro todavía sin medir»*. Un arnés que confirma la hipótesis que uno ya tiene no es evidencia.

**Decisión:** el raíz del modal no lleva `MaxWidth`/`MaxHeight`. La restricción la aplica **quien hospeda**, por referencia directa y sin búsqueda de ancestro — `PesajeView.LimitarAlOverlay(modal)` ata el modal contra `ModalOverlay` al mostrarlo. Sin ancestro que buscar no hay nada que se escape del control, y el tamaño lo decide el contenedor, que es el contrato de layout natural de WPF.

No alcanza con `Margin="24"` en el overlay, que es lo primero que uno propone: con `Width="880"` fijo, un margen no encoge el modal en ventana angosta — lo recorta. El binding contra el overlay conserva el encogido exacto (900×700 → 852×620, 600×400 → 552×352).

### Y otra cosa que quedó comprobada: el diseñador no ejecuta el code-behind

Con el modal ya renderizando, el título, la placa, el proveedor y las filas salen **vacíos** — y todo eso lo asigna el constructor. O sea: **el diseñador arma el árbol parseando el XAML y no corre el code-behind del documento raíz.**

Consecuencia práctica: **sembrar datos de muestra en el constructor no sirve** para la vista previa. Se quitó de `ProductosCargaModal` por eso — era código muerto disfrazado de ayuda al diseño. Si alguna vez se quiere una tabla con filas en el lienzo, hay que hacerlo declarativo (`ItemsSource` bindeado + datos de diseño), no imperativo.

Queda una pregunta abierta que el próximo modal que se migre responde gratis: **si el diseñador no instancia la clase, ¿hace falta el constructor sin parámetros?** Se dejó porque es barato y porque WPF pide que un tipo con `x:Class` sea instanciable sin argumentos, pero no está comprobado que el lienzo lo necesite.

---

## Relaciones

- [[WPF - StaticResource en atributos del elemento raiz y el disenador de Visual Studio]] — el hecho de WPF y la evidencia
- [[Anatomia compartida de los modales]] — receta y plan de réplica
- [[Deuda Técnica - Pendientes]] — P-057 (modales que faltan), P-042 (familia paralela de estilos)
- [[Arquitectura Actual]]
