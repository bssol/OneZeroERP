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

## Fiscal Years implementation slice

The first GL page is now implemented at `/gl/fiscal-years` using the shared permission-aware paged-page foundation.

Implemented:

- SQL Server-backed `erp.Erp_FiscalYears` persistence with unique codes.
- Date-range validation and overlap prevention.
- Open/closed status field.
- Permission-controlled view, add, edit, and delete actions.
- Responsive paged grid and simple-entity edit dialog.
- Delete confirmation and consistent error states.
- Append-only `audit.Audit_Events` rows written with Fiscal Year mutations.
- Development seed administrator claims for the Fiscal Years resource only.

The current development path uses the reviewed `InitialDataAccess` EF migration against SQL Server. Existing databases created by the earlier `EnsureCreated` bootstrap path require an explicit migration-baseline procedure before shared use. Add integration coverage against the deployment SQL Server instance before shared or production deployment.
## Domain model foundation

The initial domain-only foundation now contains:

- Fiscal Year aggregate with normalized codes, date-range validation, open/closed state, and containment checks.
- Chart of Accounts `Account` model with account type, parent reference, posting eligibility, and active state.
- Standard voucher types OPV, JVV, CPV, CRV, BPV, and BRV with bank/cash requirements.
- Draft `Journal` aggregate and `JournalLine` model with decimal-safe amounts, balanced-posting enforcement, fiscal-year/day-lock checks, and posted-state immutability.
- `DayLock` aggregate for date-level transaction control.
- `BankAccount`, `CashAccount`, and `BankReconciliation` models with GL linkage and completion-balance validation.
- `Budget` and `BudgetLine` models with period validation and approval locking.
- Configurable `FinancialReportDefinition` mappings for Notes to Accounts, Profit and Loss, and Balance Sheet without hardcoded account IDs.

The persistence boundary currently remains intentionally limited to the fiscal-year and chart-of-accounts slices. The shared permission-aware frontend now exposes both setup pages, while journal, posting, locking, budget, reconciliation, and report persistence remain behind their respective domain/application boundaries. The next implementation step is to add reviewed mappings and contracts for Voucher Types without weakening the domain invariants.
## Voucher Type model decision

Voucher types are configurable company-scoped master data, not an enum. `VoucherType` stores an auditable string `Code`, `Description`, active state, and operational requirements such as bank or cash account usage. The initial OPV, JVV, CPV, CRV, BPV, and BRV values are seed records only. Journals reference `VoucherTypeId`, allowing additional voucher types to be configured without recompiling the domain.
## Chart of Accounts data-access foundation

The Chart of Accounts backend increment has SQL Server persistence mappings, application contracts, audited activation/deactivation, parent existence and one-level hierarchy validation, the `AddChartOfAccounts` migration, and the permission-aware `/gl/chart-of-accounts` setup page. Voucher Types now have the corresponding company-scoped persistence mapping, audited application service, unique company/code constraint, `AddVoucherTypes` migration, and invariant tests; the setup page and standard-type seed orchestration remain the next UI/startup increment. Accounting posting behavior remains out of scope.
