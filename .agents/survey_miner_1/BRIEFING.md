# BRIEFING — 2026-09-02T17:50:22Z

## Mission
Probe, analyze, and document the Domain Layer and Database Schema definitions for maximum text length validations, database constraints, entity rules, and schema drift / unit test requirements.

## 🔒 My Identity
- Archetype: teamwork_preview_spec_miner
- Roles: Teamwork specialist, specification miner
- Working directory: D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_miner_1
- Original parent: 9e4750ff-0c86-44d7-aa45-ab43466b3848
- Milestone: Survey & Specification Mining for Domain & Database Schema

## 🔒 Key Constraints
- Specification miner only: do NOT implement code in the main project.
- Thoroughly discover and probe domain rules, entity models, database schemas, R1 and R5 requirements.
- Document full interface, constraints, edge cases, error behaviors, and validation rules.

## Current Parent
- Conversation ID: 9e4750ff-0c86-44d7-aa45-ab43466b3848
- Updated: not yet

## Task Summary
- **What to build**: Comprehensive survey report on domain rules (ReglasEntidades, ReglasFormato, ReglaCampo), entity models, database schemas/migrations, R1 mapping, and R5 test design for Postgres schema drift detection, offline theory tests, and reflection audit.
- **Success criteria**: Detailed survey report written to `survey_report.md` covering all entities, properties, DB column types/lengths, UI caps, format rules, existing test structure, and testing blueprints.
- **Interface contracts**: `CapaDominio/Reglas/ReglasEntidades.cs`, `CapaDominio/Reglas/ReglasFormato.cs`, `CapaDominio/Reglas/ReglaCampo.cs`, and `BimboProyecto.Tests/`.
- **Code layout**: Layered WPF architecture (.NET 8): CapaDominio, CapaDatos, CapaLogica, CapaUI, BimboProyecto.Tests.

## Loaded Skills
- None explicitly assigned.

## Key Decisions Made
- Will inspect all domain files in `CapaDominio/` (Reglas, Entidades, Enums, DTOs).
- Will inspect any schema/sql/migration files in repo and investigate database layer mappings.
- Will inspect `BimboProyecto.Tests/` to understand existing test conventions, runners, and dependencies (e.g. xUnit, Npgsql).

## Artifact Index
- D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_miner_1\survey_report.md — Comprehensive Survey Report for Domain Layer & Database Schema.
- D:\Proyectos\Proyecto de BIMBO\BimboProyecto\.agents\survey_miner_1\handoff.md — Self-contained Handoff Report.
