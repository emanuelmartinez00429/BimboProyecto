---
title: "QA — Configuración de Empresa (Caché Inmediata, Vista Completa y Realtime)"
tags:
  - qa
  - revision
  - configuracion
  - cache
  - realtime
  - rendimiento
  - tests
date: 2026-09-17
branch: feat/fase8-MaquetadodeRoles
autor_cambios: Antigravity (agente QA) — Sesión Fernando
revisor: DeepInvestigator QA
estado: Cerrado
---

# QA — Configuración de Empresa (Caché Inmediata, Vista Completa y Realtime)

> [!success] Resultado
> Se certificó la migración del módulo de Configuración de Empresa desde un modal a una vista estándar (`ConfiguracionEmpresaView`), verificando el cumplimiento de las directrices de los 6 documentos de investigación QA. Se diseñó y ejecutó una suite de 24 métodos de prueba (46 ejecuciones unitarias) en `ConfiguracionEmpresaWhiteBoxTests.cs`, logrando **498/498 tests aprobados** en la solución con 0 advertencias. Se erradicó el bloqueo Win32 de archivos en caché de disco y se implementó renderizado a 0 ms.

---

## 1. Alcance de la Revisión QA

| Componente | Archivo | Responsabilidad Evaluada |
|---|---|---|
| Vista Principal | `ConfiguracionEmpresaView.xaml` | Zero-Shader, renderizado Direct3D, ClearType subpíxel, layout idéntico a maqueta. |
| Code-Behind | `ConfiguracionEmpresaView.xaml.cs` | Compatibilidad `ADR-028` (diseñador VS), cero acoplamiento visual, FileDialog desacoplado. |
| ViewModel | `ConfiguracionEmpresaViewModel.cs` | Dirty Tracking tipado (`ChangeTracker<T>`), ciclo de vida `RealtimeAwareViewModel`, normalización hex. |
| Gestión de Caché | `ConfiguracionEmpresaViewModel.cs` | Carga 0 ms, `MemoryStream` + `BitmapCacheOption.OnLoad` + `.Freeze()`. Cero fugas de descriptores. |
| Shell y Navegación | `MainWindow.xaml` / `.cs` | Retiro de overlay, enrutamiento `Routes.Configuracion` y actualización reactiva de sidebar. |
| Infraestructura WAL | `RealtimeService.cs` y migración SQL | Mapeo de `id_empresa` y publicación en `supabase_realtime`. |

---

## 2. Matriz de Pruebas de Caja Blanca (`ConfiguracionEmpresaWhiteBoxTests.cs`)

**Total suite módulo: 46 ejecuciones — 46/46 ✅ (0 fallos)**

### 🔴 P0 · Crítico — Caché, Fugas de Memoria y Descriptores de Archivo
| Caso de Prueba | Qué Verifica |
|---|---|
| `CargarBitmapCongelado_LiberaDescriptorDeArchivo` | La carga de mapas de bits no retiene bloqueo Win32 sobre el `.png` local; permite reescritura/borrado inmediato. |
| `CargarBitmapCongelado_BitmapEstaCongelado` | `IsFrozen == true`, permitiendo acceso seguro entre hilos sin sobrecarga en el UI Thread. |
| `CargarCacheLocalSinRed_CargaInmediata` | Renderiza logo e ícono en 0 ms desde `%APPDATA%` sin requerir conectividad remota previa. |

### 🟠 P1 · Alto — Integridad de Datos y Dirty Tracking
| Caso de Prueba | Qué Verifica |
|---|---|
| `ChangeTracker_ReversionCircular_VuelveAClean` | Al volver a teclear el valor original, `IsDirty` pasa a `false` inmediatamente sin llamadas RPC innecesarias. |
| `NormalizarHex_FormateoConsistente` (9 variantes) | Normaliza códigos de 6 u 7 caracteres a formato `#RRGGBB` en mayúsculas, previniendo falsos sucios. |
| `Validaciones_FormatoCampos` (RTN, Teléfono, Correo, Dominio) | Acepta formatos válidos y rechaza entradas inválidas en `DatosValidos()`. |

### 🟡 P2 · Medio — Realtime y Detección de Modificaciones Externas
| Caso de Prueba | Qué Verifica |
|---|---|
| `RealtimeWal_SimulacionPayload_CoincideCaseInsensitive` | Payloads WAL con disparidad de casing o prefijo no disparan falsas alarmas de modificación externa. |
| `RealtimeService_PkEmpresaMapeada` | Mapeo exacto de `["empresa"] = "id_empresa"` para identificación de filas en WebSockets. |
| `CooldownLocal_EvitaFalsosEcos` | Ignora ecos del propio cliente durante 3 segundos posteriores al guardado. |

### 🟢 P3 · Leve — Invariantes XAML, Zero-Shader y Diseñador
| Caso de Prueba | Qué Verifica |
|---|---|
| `Xaml_ZeroShader_NoContieneDropShadowEffect` | Cero presencia de `DropShadowEffect` en `ConfiguracionEmpresaView.xaml`. |
| `Xaml_MergedDictionary_IncluyeStyles` | Cumplimiento estricto de `ADR-028` para resolución autónoma en diseñador de Visual Studio. |
| `VistaCodeBehind_NoEjecutaCargaEnModoDiseno` | `OnLoaded` protegido por `DesignerProperties.GetIsInDesignMode(this)`. |
| `Xaml_BotonCancelar_Eliminado` | Verificación de que el botón Cancelar fue completamente purgado de la vista y del ViewModel. |

---

## 3. Dictamen Final de QA

- **Estado:** ✅ **Aprobado sin reservas**.
- **Resultado de Compilación:** 0 advertencias, 0 errores.
- **Resultado de Tests:** 498/498 pasando en toda la solución.
