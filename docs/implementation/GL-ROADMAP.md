# General Ledger Roadmap

This roadmap sequences GL work after the platform bootstrap. It is intentionally a plan, not an implementation specification that authorizes feature coding.

## Phase 0 — decisions and foundation

Dependencies: product-owner approval of the decisions in `ERP-VISION.md`; completion of `BOOTSTRAP-PLAN.md`.

- confirm organization/entity, approval, period, currency, bank import, and identity decisions;
- establish solution skeleton, CI, local environment, database connection, migrations, identity boundary, audit/outbox primitives, and test harness;
- define accounting terminology, precision/rounding policy, timezone policy, identifiers, and error conventions.

Exit gate: an authenticated user can reach a protected health endpoint in a local environment, a migration can be applied to an empty database, and a test proves an audit event/outbox record is transactionally linked to a sample state change.

## Phase 1 — fiscal calendar and access controls

Dependencies: Phase 0.

- organizations and accounting settings;
- fiscal years and periods with open/closed status;
- day locking and controlled unlock operation;
- permission catalog, roles, organization membership, and authorization policies.

Exit gate: unauthorized users are rejected server-side; closed periods and locked days reject financial mutations; all lock/unlock changes are audited.

## Phase 2 — chart of accounts and voucher configuration

Dependencies: Phase 1.

- hierarchical chart of accounts with account type/status and posting eligibility;
- voucher types for OPV, JVV, CPV, CRV, BPV, and BRV;
- numbering rules, default behaviors, required fields, and validation configuration.

Exit gate: hierarchy and posting-account rules are validated; configuration changes are permissioned, versioned where needed, and audited.

## Phase 3 — journal lifecycle and posting engine

Dependencies: Phases 1–2.

- draft journal creation/edit/delete according to permissions;
- line-level debit/credit validation, precision, base-currency handling, and balancing;
- approval decision if selected;
- posting to immutable journal lines;
- reversal/adjustment workflow for posted journals;
- traceability and audit events.

Exit gate: property and integration tests prove no unbalanced, unauthorized, out-of-period, or locked-day journal can post; posted data cannot be edited/deleted through application commands.

## Phase 4 — bank and cash

Dependencies: Phase 3.

- bank/cash accounts linked to eligible GL accounts;
- CPV/CRV/BPV/BRV operational behavior;
- opening balances and controlled adjustments;
- statement import staging and duplicate detection.

Exit gate: bank/cash postings reconcile to GL and imports are replay-safe and auditable.

## Phase 5 — bank reconciliation

Dependencies: Phase 4.

- statement lines, matching rules, manual matching, exceptions, and reconciliation sessions;
- period/day controls for reconciliation changes;
- reconciliation reporting and audit trail.

Exit gate: a completed reconciliation has a reproducible balance, unresolved exceptions are visible, and finalized results cannot be silently changed.

## Phase 6 — budgeting

Dependencies: Phases 1–2; journal/report read model from Phase 3.

- budget versions, periods, accounts, dimensions if approved, workflow, and variance calculations;
- permissioned import/export and audit.

Exit gate: budget versions are immutable after approval or changed only through a new version, and variance results are reproducible.

## Phase 7 — configurable financial reports

Dependencies: Phase 3; finalized account/report configuration model.

- trial balance and drill-down foundation;
- configurable Notes to Accounts;
- configurable Profit and Loss;
- configurable Balance Sheet;
- effective-dated account mappings, grouping, sign/presentation rules, comparative periods, and export/print permissions.

Exit gate: reports calculate from posted lines, produce a balanced trial balance, honor effective configuration, and support drill-down to journal lines.

## Cross-cutting acceptance criteria

- decimal-safe totals and deterministic rounding;
- authorization checks on every mutation and protected read/export;
- audit evidence for configuration, permissions, locks, approval, posting, reversal, import, reconciliation, and report publication;
- migration, backup/restore, performance, accessibility, and security checks appropriate to the release;
- no report logic contains hardcoded account IDs.

## Key dependencies and risks

- Approval and multi-currency decisions affect journal lifecycle and database design.
- Dimensions/cost centers, tax, and intercompany requirements may be required before budgeting and statements; confirm scope before Phase 6.
- Bank feeds and file formats depend on selected institutions/providers.
- Report configuration needs governance and validation before users can publish mappings.
