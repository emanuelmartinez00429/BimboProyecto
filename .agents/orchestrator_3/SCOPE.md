# Scope: ADR-026 Formalization & Technical Caching Plan

## Architecture
- Target Platform: .NET 8 (net8.0-windows), WPF, Supabase C# Client (`supabase-csharp`).
- Proposed Caching: In-memory FusionCache L1 with reactive invalidation via Supabase Realtime (`InvalidadorCacheRealtime`).
- Scope boundary: STRICTLY TECHNICAL DESIGN & OBSIDIAN DOCUMENTATION. ZERO SOURCE CODE MODIFICATIONS.

## Allowed Write Scope
1. `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md` (new file created).
2. `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md` (added P-048 and P-049 in matching table and detail format).
3. `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md` (added concise callout referencing ADR-026 as proposed architecture).

## Explicit Forbidden Actions
- Modifying any file outside the 3 allowed files (no .cs, .xaml, .csproj, .sln, or other .md files). Confirmed 0 violations.
- Touching the frontmatter or content of `[[ADR-015 - Cache de catalogos mostrar y revalidar]]` (it remains `estado: aceptado`). Confirmed 0 violations.
- Recommending or implementing L2 cache without explicit justification of why it was discarded. Confirmed formally rejected.

## Feature Inventory & Requirements
| # | Requirement | Description | Scope / Milestone | Status | Source |
|---|---|---|---|---|---|
| 1 | R1 | Auditoría adversarial y contraste contra el código existente (CatalogoCache, CatalogoRepository, SelectorCatalogoModal, RolPermisoRepository, EmpresaRepository, RealtimeService vs InvalidadorCacheRealtime, MainWindow) | M1 | DONE | ORIGINAL_REQUEST §R1 |
| 2 | R2 | Validación de 13 trampas específicas (ICacheService Singleton, CatalogoRepository concreto por tipo, CancellationToken.None en fábrica, retiro alRevalidar, tablas no publicadas, Result<T> no serializable, permisos no cacheados, reconexión, re-suscripción en ciclo de vida, tags compuestos, catch defensivo de cancelación) | M1 | DONE | ORIGINAL_REQUEST §R2 |
| 3 | R3 | Selección y verificación de dependencias de FusionCache (ZiggyCreatures.FusionCache 2.0.2 compatible con RemoveByTag, ClearAsync, net8.0 y Microsoft.Extensions 8.x) | M1 | DONE | ORIGINAL_REQUEST §R3 |
| 4 | R4 | Redacción formal de ADR-026 en Obsidian con frontmatter YAML estricto, 5 fases, matriz TTL/FailSafe, P-048, P-049 y callout en Arquitectura Actual | M1 | DONE | ORIGINAL_REQUEST §R4 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|---|---|---|---|
| M1 | ADR-026 Design & Documentation | Investigation (R1-R3), Obsidian files update (R4), Verification & Auditing | None | **DONE** |

## Key Outputs
- `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md`: 403 lines, authentic and exhaustive architectural specification.
- `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`: P-048 and P-049 detailed, registered in resolution history table, and linked in relations.
- `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`: Callout inserted, roadmap note updated, relations linked.
- Solution build: 0 errors, 0 warnings.
- Test suite: 223/223 passed (100%).
- Forensic Integrity Audit: VERDICT: CLEAN.
