# Auditor Dispatch
Assigned Role: teamwork_preview_auditor
Focus: Forensic integrity audit, zero code alteration check, ADR-015 immutability check, allowed file scope verification
Working Directory: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\auditor_1
Original Request: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md

## 2026-09-03T05:10:02Z
You are the Forensic Integrity Auditor (teamwork_preview_auditor).
Your working directory is: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\auditor_1
The project workspace is: d:\Proyectos\Proyecto de BIMBO\BimboProyecto
Path to ORIGINAL_REQUEST.md: d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\ORIGINAL_REQUEST.md (MUST read section ## 2026-09-03T04:53:04Z).

MISSION:
Perform a strict forensic integrity audit on the entire repository and work products:
1. ZERO CODE VIOLATION CHECK: Verify using `git status` and `git diff` that absolutely NO source code or build configuration files were created, modified, or touched (`.cs`, `.xaml`, `.csproj`, `.sln`, `.sql`).
2. SCOPE CONTAINMENT CHECK: Verify that ONLY the following 3 files were modified or created:
   - `contexto/45 - Decisiones/ADR-026 - Cache en memoria con FusionCache e invalidacion por Realtime.md`
   - `contexto/40 - Proyecto Bimbo/Deuda Técnica - Pendientes.md`
   - `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`
   Touching any other file is an INTEGRITY VIOLATION.
3. ADR-015 INTEGRITY CHECK: Verify that `contexto/45 - Decisiones/ADR-015 - Cache de catalogos mostrar y revalidar.md` has NOT been modified in any way and retains `estado: aceptado`.
4. AUTHENTICITY & CHEATING AUDIT: Verify that ADR-026 and Deuda Técnica items are genuine, authentic, and substantive engineering documents, not facades, mocks, or placeholders. Verify that all 12 traps, 3 silent bugs, and R1-R4 requirements are genuinely addressed.
5. Deliver your BINARY VETO VERDICT in `d:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\auditor_1\handoff.md`:
   - Either: `VERDICT: CLEAN`
   - Or: `VERDICT: INTEGRITY VIOLATION` (with exhaustive evidence)
Notify the orchestrator via send_message when done.
