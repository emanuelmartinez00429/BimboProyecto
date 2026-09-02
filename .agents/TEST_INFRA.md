# E2E Test Infra: Bimbo Honduras MaxLength & Validation Alignment

## Test Philosophy
- Opaque-box and requirement-driven testing.
- Derives verification from PostgreSQL schema constraints, domain validation rules, UI prevention topes, and user interaction flows.
- Methodology: Category-Partition + BVA (Boundary Value Analysis) + Reflection Integrity Audit + Live Schema Drift Inspection.

## Feature Inventory
| # | Feature | Source (requirement) | Tier 1 | Tier 2 | Tier 3 |
|---|---|---|:---:|:---:|:---:|
| 1 | Domain Rules Alignment (10 entities, 34 fields) | ORIGINAL_REQUEST §R1 | 5 | 5 | ✓ |
| 2 | Automatic MaxLength Derivation (`TopePreventivo`) | ORIGINAL_REQUEST §R2 | 5 | 5 | ✓ |
| 3 | Modal Validation & Form Registrations | ORIGINAL_REQUEST §R2 | 5 | 5 | ✓ |
| 4 | `GenerarEmail` Local Truncation (50 char cap) | ORIGINAL_REQUEST §R2 | 5 | 5 | ✓ |
| 5 | `GhostTextBox` DP MaxLength & Scroll Sync | ORIGINAL_REQUEST §R3 | 5 | 5 | ✓ |
| 6 | `GhostTextBox` Foreign Domain `@` Suffix Fix | ORIGINAL_REQUEST §R3 | 5 | 5 | ✓ |
| 7 | `GhostTextBox` Clamp & CTS Debouncer Dispose | ORIGINAL_REQUEST §R3 | 5 | 5 | ✓ |
| 8 | Login & Recovery Window Length Limits (50/72) | ORIGINAL_REQUEST §R4 | 5 | 5 | ✓ |
| 9 | `ConfiguracionEmpresa` Defensive Length Checks | ORIGINAL_REQUEST §R4 | 5 | 5 | ✓ |
| 10 | Schema Drift Test (Postgres `information_schema`) | ORIGINAL_REQUEST §R5 | 5 | 5 | ✓ |
| 11 | Pinned Values Unit Tests (Offline / CI `[Theory]`) | ORIGINAL_REQUEST §R5 | 5 | 5 | ✓ |
| 12 | Reflection Audit Test (`ReglaCampo` coverage) | ORIGINAL_REQUEST §R5 | 5 | 5 | ✓ |
| 13 | Boundary Tests Expansion (`ReglasFormatoTests`) | ORIGINAL_REQUEST §R5 | 5 | 5 | ✓ |
| 14 | Obsidian Vault Documentation Protocols | ORIGINAL_REQUEST §R6 | 5 | 5 | ✓ |

## Test Architecture
- **Test Runner**: `dotnet test BimboProyecto.Tests/BimboProyecto.Tests.csproj`
- **Pass/Fail Semantics**: 100% tests must pass with exit code 0.
- **Directory Layout**:
  - `BimboProyecto.Tests/Dominio/ReglasEntidadesTests.cs` (Test A, Test B, Test C)
  - `BimboProyecto.Tests/Dominio/ReglasFormatoTests.cs` (Boundary test cases)
  - `BimboProyecto.Tests/Dominio/ReglasLoginTests.cs`, `ReglasContrasenaTests.cs`
  - `BimboProyecto.Tests/Aplicacion/...` (Services & RBAC)

## Real-World Application Scenarios (Tier 4)
| # | Scenario | Features Exercised | Complexity |
|---|---|---|---|
| 1 | Full CRUD modal data entry under boundary lengths | F1, F2, F3, F10, F11 | High |
| 2 | Long name email auto-generation with 50-char Postgres limit | F4, F1, F11 | Medium |
| 3 | Login typing with third-party domain and horizontal scrolling | F5, F6, F7, F8 | High |
| 4 | Offline CI execution with reflection and pinned values | F11, F12, F13 | Medium |
| 5 | Enterprise company configuration save with max lengths | F9, F1 | Medium |

## Coverage Thresholds
- Tier 1: ≥5 per feature
- Tier 2: ≥5 per feature (where boundaries exist)
- Tier 3: Pairwise coverage of major feature interactions
- Tier 4: ≥5 realistic application scenarios
