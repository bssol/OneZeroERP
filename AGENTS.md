# OneZero ERP Engineering Guide

## Purpose

OneZero ERP is a greenfield, AI-enabled ERP. The first business area is General Ledger (GL). This repository is being bootstrapped as a modular monolith; do not introduce distributed services until a documented operational or scaling requirement justifies them.

## Source of truth

- Business rules supplied by the product owner are authoritative.
- `docs/implementation/ERP-VISION.md` defines product boundaries and approved assumptions.
- `docs/implementation/ARCHITECTURE.md` defines technical boundaries and cross-cutting policies.
- `docs/implementation/GL-ROADMAP.md` defines GL sequencing and acceptance gates.
- `docs/implementation/BOOTSTRAP-PLAN.md` defines the pre-feature foundation work.

When implementation begins, update the relevant decision record before changing a foundational rule.

## Non-negotiable accounting controls

- Posted journals are immutable from the user interface and application commands. Corrections use reversal/adjustment journals.
- A journal cannot be posted unless debits equal credits exactly in the selected currency precision.
- Fiscal period status and day locks are enforced server-side in the transaction that posts or changes financial data.
- All important financial and security changes are auditable.
- Reports read posted journal lines, not mutable drafts or denormalized report totals.
- Monetary values use decimal-safe database and application types. Never use binary floating point for money.
- The current database target is SQL Server through EF Core. Keep provider-specific behavior in infrastructure so a later PostgreSQL migration does not leak into domain or application code.
- Financial statements use configurable account mappings and report definitions; never hardcode account IDs.

## Working conventions

- Keep business modules isolated. A module exposes application contracts, not its database tables.
- Centralize reusable page mechanics such as permission checks, pagination, loading/error states, confirmation dialogs, and common action visibility in shared base components; entity pages should contain only entity-specific behavior.
- Prefer explicit commands and queries, meaningful domain names, and small aggregate boundaries over generic repositories.
- Use UTC timestamps for technical events and an explicit organization/business timezone for accounting dates.
- Every schema change is a reviewed, forward-only migration. Never edit an applied migration.
- Add unit tests for domain invariants, integration tests for persistence and authorization, and end-to-end tests for critical posting/reporting paths.
- Do not add GL functionality before the bootstrap gates in `BOOTSTRAP-PLAN.md` are complete.
- Do not put secrets, production connection strings, or real financial data in the repository.

## Git workflow

- This repository must remain under Git version control from the beginning. Initialize Git before substantial implementation work if `.git` does not exist.
- Inspect `git status --short` before modifying files and review the final diff before reporting completion.
- Preserve user-created files and uncommitted changes. Do not use `git reset --hard`, `git checkout --`, `git clean`, recursive deletion, or equivalent destructive operations unless the user explicitly requests the exact operation and target.
- Keep commits focused and reviewable. Use imperative commit subjects with a clear scope, for example `Add application shell foundation` or `Fix login validation`.
- Do not commit secrets, certificates, local database files, generated build output, IDE state, user-specific settings, or production data. Keep `.gitignore` current when a new generated or sensitive artifact is introduced.
- Do not create commits, branches, tags, push to remotes, or open pull requests unless the user explicitly requests that action.
- Before a commit is requested, validate the affected solution and review staged files to ensure only intended changes are included.
- Keep migrations, documentation, source code, and tests reviewable in the same history; never rewrite shared history without explicit approval.

## Validation expected for changes

At minimum, run formatting, static analysis, unit tests, integration tests covering the changed boundary, and migration validation. For financial changes, include an evidence-based reconciliation or trial-balance assertion.

## Documentation

Document decisions, assumptions, externally visible behavior, and operational procedures. Prefer short ADRs under `docs/decisions/` once implementation starts. Update the roadmap when dependencies or acceptance gates change.
