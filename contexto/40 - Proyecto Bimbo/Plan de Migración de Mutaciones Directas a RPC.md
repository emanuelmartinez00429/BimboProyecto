---
title: "Plan de Migración de Mutaciones Directas a RPC"
tags:
  - plan
  - supabase
  - rpc
  - offline-first
  - migracion
date: 2026-08-30
estado: propuesto
---

# Plan de Migración de Mutaciones Directas a RPC

> [!warning] Estado documental
> Este plan prepara las 27 rutas activas que todavía escriben directamente en tablas de Supabase. No representa trabajo implementado ni autoriza cambios en código o base de datos. Es independiente de [[Plan Offline-First de Pesaje]] y no modifica su contenido.

## Objetivo y definición de compatibilidad

Eliminar gradualmente el DML directo de los repositorios empresariales activos y hacer que cada mutación central pase por una RPC específica, segura, auditable e idempotente.

Una ruta queda **compatible con offline-first** cuando:

- usa una RPC empresarial específica y versionada;
- recibe `id_solicitud` desde la capa que origina el comando, sin generarlo dentro del gateway remoto;
- acepta versión esperada y devuelve la versión canónica resultante cuando modifica una entidad existente;
- obtiene la identidad mediante `auth.uid()` y valida usuario, RBAC y alcance central;
- ejecuta cambio, incremento de versión y bitácora exitosa dentro de una sola transacción;
- devuelve un resultado canónico suficiente para actualizar SQLCipher;
- puede ser invocada posteriormente por la partición de outbox del usuario que originó la operación.

Compatibilidad no significa habilitación offline. Catálogos y administración permanecerán online-only en la primera versión aunque dejen de usar DML directo.

## Alcance confirmado

- Código activo registrado mediante DI en `CapaDatos/DependencyInjection.cs`.
- Repositorios bajo `CapaDatos/Repositories/`.
- 27 rutas activas de mutación directa: 6 de Pesaje, 14 de catálogos y 7 administrativas/técnicas.
- Las API de Supabase Auth y Storage no son DML empresarial y no se reemplazan por RPC PostgreSQL.
- Los repositorios históricos bajo `CapaDatos/Repositorios/productos_movimientos/` no están registrados en DI; se documentan como deuda separada y no forman parte de las 27 rutas.

## Contrato común para RPC empresariales v2

### Entrada mínima

- `p_id_solicitud uuid` obligatorio.
- UUID estable de entidad para creaciones sincronizables.
- Identificador central cuando ya exista.
- Parámetros empresariales tipados.
- `p_expected_version bigint` para modificaciones y transiciones.
- Motivo completo para anulaciones.
- Referencia a la solicitud original cuando exista resolución supervisada.
- Identidad de dispositivo/concesión cuando esa infraestructura central sea aprobada.

### Resultado mínimo

- `id_solicitud`.
- estado de la solicitud;
- UUID estable e ID central de la entidad;
- versión central resultante;
- payload canónico necesario para actualizar SQLCipher;
- indicador de replay idempotente.

### Orden transaccional

1. Validar mínimamente `auth.uid()`.
2. Construir el hash canónico de parámetros.
3. Consultar o reclamar `id_solicitud`.
4. Devolver inmediatamente un resultado completado si coinciden usuario, RPC y hash.
5. Rechazar reutilización del UUID con datos diferentes.
6. Para una solicitud nueva, validar usuario activo, alcance, RBAC, versión y transición.
7. Bloquear las filas necesarias.
8. Ejecutar el cambio, incrementar versión y generar bitácora.
9. Completar la solicitud dentro de la misma transacción.

No se crearán funciones sobrecargadas ni una RPC genérica que acepte cualquier cambio de estado. Cerrar, reabrir, activar, desactivar y anular serán comandos distintos cuando representen transiciones empresariales diferentes.

## Matriz de las 27 rutas

### A. Pesaje — 6 rutas prioritarias

| # | Ruta actual | DML actual | RPC objetivo | Requisito particular |
|---|---|---|---|---|
| 1 | `PesajeRepository.AgregarProductoAsync` | `INSERT movimiento_productos` | `agregar_producto_movimiento_pesaje_v2` | Aceptar UUID estable del producto de movimiento y dependencia con el movimiento padre. |
| 2 | `PesajeRepository.ActualizarProductoAsync` | `UPDATE movimiento_productos` | `actualizar_producto_movimiento_pesaje_v2` | Validar movimiento abierto y `expected_version`. |
| 3 | `PesajeRepository.AnularProductoAsync` | `UPDATE id_estado` | `anular_producto_movimiento_pesaje_v2` | Motivo obligatorio y validación de entradas relacionadas. |
| 4 | `PesajeRepository.SetEstadoProductoAsync` | `UPDATE id_estado` | `cerrar_producto_movimiento_pesaje_v2` y `reabrir_producto_movimiento_pesaje_v2` | Eliminar el booleano/genérico de estado del contrato remoto. |
| 5 | `PesajeRepository.ActualizarTaraExtraEntradaAsync` | Uno o varios `UPDATE entradas_producto` | `actualizar_entrada_producto_pesaje_v2` o `repartir_tara_extra_pesaje_v2` | Una transacción para todo el reparto; el servidor recalcula derivados. |
| 6 | `PesajeRepository.AnularEntradaAsync` | `UPDATE id_estado` | `anular_entrada_producto_pesaje_v2` | Motivo obligatorio, versión esperada y conservación del historial. |

#### Prerrequisitos adyacentes de Pesaje

No cuentan como rutas directas porque ya usan RPC, pero deben modernizarse antes de habilitar outbox:

- crear movimiento;
- actualizar movimiento;
- cerrar movimiento;
- anular movimiento;
- registrar entrada.

Los contratos actuales generan `Guid.NewGuid()` dentro del repositorio, no reciben `expected_version` y agrupan algunas transiciones. Sus variantes v2 recibirán el UUID generado por la operación local y separarán los comandos empresariales.

El flujo de edición de una pesada que actualmente anula y crea otra se reemplazará por modificación de la misma identidad. Solo se conservará “anular y reemplazar” si una regla empresarial explícita lo exige.

### B. Catálogos — 14 rutas

| # | Entidad | Ruta | RPC objetivo |
|---|---|---|---|
| 7 | Producto | `UpdateAsync` | `actualizar_producto_v2` |
| 8 | Producto | `DeleteAsync` lógico | `desactivar_producto_v2` |
| 9 | Proveedor | `UpdateAsync` | `actualizar_proveedor_v2` |
| 10 | Proveedor | `DeleteAsync` lógico | `desactivar_proveedor_v2` |
| 11 | Fabricante | `UpdateAsync` | `actualizar_fabricante_v2` |
| 12 | Fabricante | `DeleteAsync` lógico | `desactivar_fabricante_v2` |
| 13 | Categoría | `UpdateAsync` | `actualizar_categoria_v2` |
| 14 | Categoría | `DeleteAsync` lógico | `desactivar_categoria_v2` |
| 15 | Presentación | `UpdateAsync` | `actualizar_presentacion_v2` |
| 16 | Presentación | `DeleteAsync` lógico | `desactivar_presentacion_v2` |
| 17 | Contacto de fabricante | `UpdateAsync` | `actualizar_contacto_fabricante_v2` |
| 18 | Contacto de fabricante | `DeleteAsync` lógico | `desactivar_contacto_fabricante_v2` |
| 19 | Contacto de proveedor | `UpdateAsync` | `actualizar_contacto_proveedor_v2` |
| 20 | Contacto de proveedor | `DeleteAsync` lógico | `desactivar_contacto_proveedor_v2` |

Las desactivaciones nunca serán eliminaciones físicas. Cada RPC validará versión, RBAC, referencias y reglas particulares de la entidad.

Las RPC actuales de creación se conservan inicialmente para el comportamiento online, pero antes de permitir outbox en esos módulos deberán versionarse para incorporar `id_solicitud`, identidad del servidor, UUID estable y respuesta canónica.

Los catálogos necesarios para Pesaje se replicarán hacia SQLCipher, pero su modificación seguirá requiriendo conexión en v1.

### C. Administración y operaciones técnicas — 7 rutas

| # | Ruta actual | RPC/estrategia objetivo | Política v1 |
|---|---|---|---|
| 21 | `EmpleadoCrudRepository.UpdateAsync` | `actualizar_empleado_v2` | Online-only. |
| 22 | `EmpleadoCrudRepository.CambiarEstadoAsync` | `activar_empleado_v2` / `desactivar_empleado_v2` | Online-only. |
| 23 | `UsuarioRepository.ActualizarAsync` | `actualizar_usuario_v2` | Online-only; protege rol, alias, estado y autoadministración. |
| 24 | `UsuarioRepository.CambiarEstadoAsync` | `activar_usuario_v2` / `desactivar_usuario_v2` | Online-only; nunca se encola en v1. |
| 25 | `RolPermisoRepository.GuardarAsignacionesAsync` | `reemplazar_permisos_rol_v2` | Una transacción con el conjunto completo; online-only. |
| 26 | `UsuarioSesionService.ActualizarUltimoAccesoAsync` | Evaluar y usar `sync_ultimo_acceso` | Operación técnica best-effort; nunca entra en outbox empresarial. |
| 27 | `EmpresaRepository.GuardarAsync` | `actualizar_configuracion_empresa_v2` | Metadatos online-only; Storage se coordina por separado. |

La RPC de roles recibirá el conjunto final de acciones y hará altas, reactivaciones y desactivaciones de forma atómica. No reproducirá desde el cliente una secuencia de `UPDATE` e `INSERT`.

La configuración de empresa separará dos responsabilidades: archivo en Storage y metadatos PostgreSQL. La orquestación no confirmará una ruta de logo/icono inexistente y definirá compensación para archivos subidos que no lleguen a publicarse.

## Cambios futuros por capa

### `CapaAplicacion4`

- Sustituir argumentos primitivos dispersos por comandos empresariales tipados.
- Añadir `RequestId`, referencia de entidad, versión esperada y motivo donde corresponda.
- Devolver un resultado de operación con estado, versión y valores canónicos.
- Mantener separadas las interfaces empresariales de los detalles de Supabase y SQLCipher.

### `CapaDatos`

- Convertir las implementaciones actuales en gateways RPC sin DML directo.
- No generar `id_solicitud` dentro del gateway.
- Mapear errores de red, permiso, regla y conflicto a resultados distinguibles.
- Mantener temporalmente una bandera por módulo para seleccionar RPC o ruta heredada; nunca ejecutar escritura doble.
- Retirar la ruta heredada y después revocar DML directo cuando la fase esté verificada.

### `CapaUI`

- No requiere reescritura general porque consume interfaces.
- Recibirá resultados tipados para mostrar pendiente, confirmado, rechazado o conflicto cuando el módulo se conecte a outbox.
- Operaciones online-only deberán explicar que requieren conexión y no crearán una falsa operación pendiente.

## Fases y métricas

| Fase | Entregable | Métrica de salida |
|---|---|---|
| 0. Caracterización | Pruebas y contrato actual de las 27 rutas | Matriz 27/27 con tabla, permiso, trigger, bitácora y resultado documentados. |
| 1. Infraestructura común | Idempotencia corregida, versionado, errores y auditoría de rechazados | Concurrencia, replay y rollback validados antes de migrar repositorios. |
| 2. Pesaje | 6 rutas directas migradas y RPC existentes modernizadas | 0 `Insert/Update` directo en `PesajeRepository`; resultado canónico en todas las mutaciones. |
| 3. Catálogos | 14 rutas migradas | 0 DML directo en los siete repositorios; escrituras offline aún deshabilitadas. |
| 4. Administración | 7 rutas migradas o justificadas como API externa/técnica | Roles atómicos; sesión sin outbox; empresa coordinada con Storage. |
| 5. Retiro | Rutas heredadas retiradas y permisos reducidos | Búsqueda estática y pruebas confirman cobertura 27/27; DML directo revocado por módulo validado. |

No se habilita una outbox con escrituras reales por el solo hecho de terminar esta migración. Pesaje deberá cumplir también las puertas de seguridad, snapshot, multiusuario y recuperación definidas en [[Plan Offline-First de Pesaje]].

## Estrategia de despliegue y reversión

1. Desplegar la RPC y sus pruebas sin cambiar el cliente.
2. Habilitar el gateway RPC mediante bandera por módulo.
3. Verificar resultados, bitácora, latencia, conflictos y reintentos.
4. Ante falla, volver temporalmente a la ruta online heredada; nunca hacer dual-write.
5. Cuando el módulo sea estable, retirar el DML del repositorio.
6. Revocar permisos DML directos solamente después de confirmar que no quedan consumidores legítimos.

Una operación ya creada con el contrato de outbox no cambia a la ruta heredada: conserva su `id_solicitud` y se procesa únicamente con su RPC versionada.

## Pruebas obligatorias

### Contrato común

- Ejecución autorizada exitosa.
- Usuario inactivo, permiso revocado y alcance inválido.
- Versión esperada diferente.
- Transición empresarial inválida.
- Mismo UUID con mismos parámetros.
- Mismo UUID con parámetros, usuario o RPC diferentes.
- Dos llamadas simultáneas con el mismo UUID.
- Timeout después del commit y replay del resultado.
- Error de bitácora revierte el cambio.
- Resultado canónico suficiente para actualizar SQLCipher.
- Funciones `SECURITY DEFINER` sin ejecución para `PUBLIC` o `anon` y con `search_path` fijo.

### Casos especiales

- Reparto de tara totalmente atómico.
- Operaciones dependientes de Pesaje con UUID locales.
- Editar una pesada conserva su identidad.
- Motivos de anulación completos.
- Reemplazo de permisos de rol en una transacción.
- Un usuario no puede modificar su propio rol o estado.
- `ultimo_acceso` no crea outbox ni bloquea el inicio de sesión si falla.
- Fallo de Storage no deja metadatos apuntando a un archivo inexistente.
- Una comprobación estática falla si reaparece `.Insert()` o `.Update()` directo en un repositorio migrado.

## Criterios de aceptación

- Las 27 rutas tienen propietario, RPC/estrategia objetivo y política online/offline explícita.
- Pesaje se migra primero y no conserva DML directo.
- Ninguna RPC genera silenciosamente un nuevo UUID durante un reintento.
- No existe una RPC genérica de cambio de estado.
- Las modificaciones críticas usan control optimista de concurrencia.
- Las anulaciones conservan motivo e historial.
- Los repositorios no insertan directamente bitácora autoritativa.
- Roles se actualizan atómicamente.
- `ultimo_acceso`, Auth y Storage no se confunden con comandos empresariales de outbox.
- No se revoca DML antes de validar y retirar todos los consumidores del módulo.
- La migración completa queda demostrada por pruebas y búsqueda estática 27/27.

## Decisiones pendientes

- Matriz empresarial definitiva de operaciones permitidas offline; este plan solo fija que catálogos y administración quedan online-only en v1.
- Reglas exactas para reabrir un producto de movimiento.
- Si editar una pesada debe conservar identidad o generar formalmente una sustitución auditada; se adopta conservar identidad como valor por defecto.
- Política de retención y compensación de archivos huérfanos de Storage.
- Momento exacto para revocar DML directo a `authenticated` por módulo.

## Relaciones

- [[Plan Offline-First de Pesaje]]
- [[ADR-022 - Persistencia local-first con SQLCipher y sincronización por outbox]]
- [[Módulo Pesaje]]
- [[Arquitectura Actual]]
- [[Deuda Técnica - Pendientes]]

