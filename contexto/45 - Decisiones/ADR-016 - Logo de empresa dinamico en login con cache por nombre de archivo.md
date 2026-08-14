---
title: "ADR-016 — Logo de empresa dinámico en login: caché local versionada por nombre de archivo"
tags:
  - adr
  - decision
  - login
  - storage
  - cache
date: 2026-08-14
estado: aceptado
---

# ADR-016 — Logo de empresa dinámico en login: caché local versionada por nombre de archivo

## Contexto

El logo del login (`LoginWindow.xaml`) estaba hardcodeado a un recurso empacado en el `.exe`:

```xml
<Image Source="pack://application:,,,/CapaUI;component/Resources/bimbo_no_bg.png"/>
```

La tabla `empresa` ya tenía una columna `logo_empresa` (ruta de archivo dentro del bucket de Supabase Storage `empresa-logos`, ej. `logo_empresa_1.png`) y un método `RepositorioEmpresa.ActualizarLogoAsync` para reescribirla — pensado para un módulo de configuración administrativa que **todavía no existe**. Nada en el login leía esa columna: el logo de la app nunca reflejaba lo que hubiera en la base.

El pedido puntual: que el login muestre el logo de `empresa.logo_empresa`, bajado y guardado localmente, y que cuando ese logo cambie (vía el futuro módulo de configuración) el sistema lo detecte y lo actualice solo — sin construir todavía el módulo que lo sube.

Restricciones del punto de partida:
- `LoginWindow` corre **antes** de que exista sesión autenticada — ya hoy lee `empresa.dominio_correo` sin login previo (`RepositorioEmpresa.ObtenerAsync()`), así que la tabla `empresa` tiene lectura anónima permitida.
- No existe (todavía) un módulo que dispare un evento al cambiar el logo, ni una suscripción de Realtime sobre `empresa`.

## Decisión

**El nombre de archivo en Storage es la clave de versión.** No se compara hash, ETag ni fecha de modificación: se cachea localmente un archivo por cada nombre distinto que haya pasado por `logo_empresa`, y un nombre que no esté en caché es, por definición, una versión nueva.

Flujo (`LogoEmpresaCache.ObtenerRutaLocalAsync`, invocado desde `LoginWindow_Loaded`):

1. Leer `empresa.logo_empresa` (mismo `RepositorioEmpresa.ObtenerAsync()` que ya se usaba para `dominio_correo`).
2. Tomar solo el nombre de archivo (`Path.GetFileName`) y mapearlo a `%APPDATA%\BimboPesaje\LogoEmpresa\<nombre>`.
3. Si ese archivo ya existe localmente → se devuelve tal cual, **sin red**.
4. Si no existe → se descarga del bucket público `empresa-logos` (`Supabase.Storage`, `DownloadPublicFile` directo a disco) y se guarda con ese nombre.
5. Se borran del caché los archivos que no coincidan con el nombre vigente (mejor esfuerzo, no crítico si falla).
6. El login arma un `BitmapImage` desde la ruta local y reemplaza `LogoImage.Source`. Si el paso 1 no trae logo, o el 4 falla, se queda con `bimbo_no_bg.png` empacado — el login nunca se queda sin imagen.

**Se asumió que el bucket `empresa-logos` es de lectura pública**, por la misma razón que `empresa` tiene lectura anónima: el consumidor es una pantalla pre-login. Esto no se verificó contra la configuración real de Supabase — ver deuda P-035.

## Alternativas consideradas

| Opción | Pro | Contra | ¿Elegida? |
|---|---|---|---|
| **Nombre de archivo como clave de versión** (cache-hit por nombre) | No requiere comparar hash/fecha. La "detección de cambio" es gratis: viene incluida en cómo ya funciona `ActualizarLogoAsync` (guarda una ruta nueva, no sobrescribe la vieja). Cero infraestructura nueva (sin polling, sin Realtime). | Si el módulo de configuración algún día *reescribe* el mismo nombre de archivo (en vez de generar uno nuevo), el caché queda desactualizado hasta que se borre a mano. Hay que exigir esa convención cuando se construya ese módulo. | ✅ |
| Comparar hash/ETag del archivo remoto contra el local | Detecta cambios aunque el nombre se reutilice. | Obliga a pegarle a Storage en *cada* login para pedir metadata, aunque nada haya cambiado — exactamente el costo que la caché de catálogos (ADR-015) evitó para otro problema. Complejidad sin necesidad, dado que el propio flujo de subida ya versiona por nombre. | ❌ |
| Vencimiento por tiempo (TTL) | Trivial. | Mismo defecto que en ADR-015: deja una ventana con el logo viejo. El usuario ya rechazó este patrón una vez para catálogos; aplica el mismo argumento acá. | ❌ |
| Suscripción Realtime sobre `empresa` | Cambio reflejado sin esperar el próximo login. | `empresa` no está en la publicación `supabase_realtime` (mismo problema que motivó P-034). Login es una ventana de vida corta — no hay "sesión larga" donde un push tardío importe. Sobre-ingeniería para un valor que cambia rarísima vez. | ❌ |
| No cachear, bajar el logo en cada login | Siempre la última versión, cero lógica de caché. | Espera de red en cada apertura de login para un archivo que casi nunca cambia; el login ya tiene que sentirse instantáneo. | ❌ |

## Consecuencias

- **Se gana:** el login refleja el logo real de la base sin construir todavía el módulo de configuración — la mitad "consumo" queda resuelta de forma independiente de la mitad "administración".
- **Se sacrifica:** la detección de cambio depende de una convención (nombre de archivo nuevo por versión) que el futuro módulo de configuración **debe respetar**. Si ese módulo llega a sobrescribir el mismo nombre, hay que documentar ahí mismo que hace falta invalidar el caché a mano o cambiar de estrategia.
- **Sin verificar:** la política de lectura pública del bucket `empresa-logos` — se asumió por consistencia con `empresa`, no se confirmó contra el proyecto Supabase real.
- **Pendiente real:** el módulo de configuración que sube el logo y reescribe `logo_empresa` no existe — esto solo deja lista la lectura.

---

## Relaciones

- [[Sesión 2026-08-14 - Logo de empresa dinamico en login]] — implementación
- [[ADR-015 - Cache de catalogos mostrar y revalidar]] — mismo problema de fondo ("¿cuándo confiar en lo cacheado?") resuelto con otro criterio porque la naturaleza del dato es distinta (archivo versionado por nombre vs. lista que cambia de contenido)
- [[Deuda Técnica - Pendientes]] — P-035
- [[Arquitectura Actual]]
