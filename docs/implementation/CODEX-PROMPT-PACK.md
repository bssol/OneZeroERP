# OneZero ERP Codex Prompt Pack

This file contains reusable prompts for continuing OneZero ERP development. Use one prompt per task and keep the scope to one vertical slice or one reviewable foundation change.

## How to use this pack

Before every task, paste the selected prompt into Codex. Codex must read `AGENTS.md` and the relevant implementation documents before changing code. It must inspect the current worktree because implementation may exist as uncommitted changes. Never assume that `HEAD` is the complete current solution.

The committed baseline is a .NET 10 modular monolith with a Blazor Web App/PWA, custom `AppUsers` authentication, cookie-backed browser sessions, JWT API validation, shared permission/paging UI primitives, a module registry/dashboard, and unit/component test projects. The current database target is SQL Server through EF Core. Treat the working tree as authoritative after checking its status.

## Prompt 1 — Start or resume any task

```text
Continue work on OneZero ERP in the current workspace.

Before changing anything:

1. Read AGENTS.md completely.
2. Read the relevant documents under docs/implementation.
3. Inspect git status --short and identify modified and untracked files.
4. Inspect the existing solution, project references, source patterns, and tests relevant to this task.
5. Treat existing user changes as intentional. Do not discard, reset, clean, overwrite, or reformat unrelated work.
6. Distinguish committed baseline code from current uncommitted code before making architectural conclusions.

Do not ask me to repeat information already present in AGENTS.md or the implementation documents. If a decision is genuinely missing, state the smallest decision needed and continue with a safe, documented assumption when possible.

First provide a concise assessment of the current state, the exact files likely to change, dependencies, risks, and acceptance criteria. Then implement only the requested scope.

Use the existing architecture and conventions. Keep business rules out of Web and SharedUi. Keep provider-specific behavior in Infrastructure. Use explicit commands/queries, server-side authorization, transactional validation, and tests at the appropriate boundary.

After implementation:

- run formatting and static checks;
- run affected unit, integration, component, and architecture tests that exist;
- validate migrations when persistence changes;
- review the final diff and git status;
- update the relevant documentation or ADR;
- report changed files, checks run, results, assumptions, and the recommended next task.

Do not commit, create branches, push, or open pull requests unless I explicitly request it.
```

## Prompt 2 — Bootstrap foundation

```text
Implement the next approved bootstrap item for OneZero ERP.

The bootstrap must remain feature-neutral: do not implement fiscal years, chart of accounts, vouchers, journals, banking, reports, budgeting, or GL screens unless this task explicitly names one of them.

Establish only the foundation needed for a secure modular monolith:

- .NET 10 and nullable C# conventions;
- module boundaries and dependency direction;
- SQL Server and EF Core migration infrastructure;
- organization context and business-date/timezone abstractions;
- authentication and server-side authorization;
- audit and transactional outbox primitives;
- correlation IDs, structured errors, health checks, and configuration validation;
- shared Blazor shell and reusable UI mechanics;
- test projects and architecture rules.

For every new abstraction, explain why it belongs in Domain, Application, Infrastructure, IdentityAccess, SharedUi, or Web. Do not create generic repositories or speculative framework layers.

Acceptance requires a clean build, relevant tests, migration validation, and documentation of any deferred decision.
```

## Prompt 3 — Build a GL vertical slice

```text
Implement one General Ledger vertical slice end-to-end.

Before coding, confirm its roadmap dependencies are complete and read ERP-VISION.md, ARCHITECTURE.md, GL-ROADMAP.md, BOOTSTRAP-PLAN.md, and UI-DESIGN-SYSTEM.md.

Implement the feature through all required boundaries:

1. Domain model and invariants
2. Application commands, queries, contracts, validation, and authorization
3. Infrastructure persistence, SQL Server mapping, indexes, and migration
4. API/application composition and error behavior
5. Blazor page and reusable SharedUi mechanics
6. Audit and outbox behavior where applicable
7. Unit, integration, authorization, and component tests
8. Documentation and a short ADR if a foundational decision is introduced

Do not expose module database entities directly to Web or SharedUi. Re-check authorization, fiscal status, day locks, concurrency, and business rules server-side at commit time. Keep UI permission checks advisory only.

Use the existing paging, permission, loading, error, confirmation, and navigation patterns. Entity pages should contain entity-specific behavior only.

Do not implement adjacent features or redesign the architecture without documenting the reason and impact.
```

## Prompt 4 — Fiscal years, periods, and day locking

```text
Implement the GL fiscal calendar and day-locking slice.

Support configurable fiscal years and periods with explicit status, accounting date validation, organization timezone handling, and audited open/close/reopen operations. Support day locks and a controlled unlock operation with separate authorization.

The server must reject create, update, delete, approval, posting, reversal, import, and other financial mutations when the fiscal period is closed or the accounting day is locked. UI restrictions are not sufficient.

Define clear behavior for:

- overlapping fiscal years or periods;
- invalid dates;
- closing a period with drafts or pending approvals;
- reopening or unlocking;
- concurrency between a lock and a financial command;
- organization scope and permissions;
- audit reason requirements.

Add tests proving unauthorized users, closed periods, locked days, and race-sensitive commands are rejected transactionally.
```

## Prompt 5 — Chart of Accounts

```text
Implement the hierarchical Chart of Accounts slice.

Support organization-scoped accounts with stable identifiers, code, name, account type, parent account, active status, posting eligibility, normal balance/sign semantics, and audit metadata. Separate grouping accounts from posting accounts.

Enforce:

- no cycles;
- no self-parenting;
- unique account codes within the approved scope;
- valid account type and parent relationships;
- inactive accounts cannot receive new postings;
- non-posting/group accounts cannot receive journal lines;
- safe behavior when an account has children or posted history;
- bounded and deterministic hierarchy queries.

Use parent IDs first; add a materialized path or read model only if query needs justify it. Build an accessible account-tree UI with search, expand/collapse, loading, empty, error, and unauthorized states. Do not hardcode account IDs into reports.
```

## Prompt 6 — Voucher types and journals

```text
Implement voucher types and the journal lifecycle for the approved voucher types: OPV, JVV, CPV, CRV, BPV, and BRV.

Support draft, approved if enabled, posted, reversed, and void/cancelled states. Drafts may be edited or deleted according to permissions. Posted accounting effects are immutable.

Enforce:

- balanced debit and credit totals using configured currency precision;
- valid posting accounts and active account status;
- fiscal period and day-lock rules;
- voucher-type-specific validation;
- numbering and duplicate prevention;
- organization and currency scope;
- optimistic concurrency and idempotency for retried commands;
- transactional journal lines, audit event, and outbox event;
- reversal/adjustment linkage to the original journal.

Provide a correction action that creates a linked reversal and, where appropriate, a new editable correction journal. Never overwrite or physically delete a posted journal.

Add invariant, integration, authorization, concurrency, and trial-balance/reconciliation tests. Do not allow AI, UI code, or import code to bypass the posting command.
```

## Prompt 7 — Bank, cash, and reconciliation

```text
Implement the bank and cash slice only after journal posting is available.

Link bank and cash accounts to eligible GL posting accounts. Implement CPV, CRV, BPV, and BRV operational rules without duplicating posting logic. Add opening balances and controlled adjustments according to the approved policy.

For reconciliation, support statement-line staging, duplicate detection, matching, manual matching, exceptions, reconciliation sessions, finalization, and audit history. Imports must be replay-safe and idempotent. Finalized reconciliation results must not be silently changed.

Test that bank/cash activity agrees with posted GL lines, unmatched items remain visible, locked periods reject prohibited changes, and a completed reconciliation has a reproducible balance.
```

## Prompt 8 — Configurable financial reports

```text
Implement the reporting foundation and one configurable financial statement.

Reports must read posted journal lines only. Build effective-dated, versioned report definitions and account mappings with validation, ordering, grouping, sign/presentation rules, comparative periods, permissions, publication status, and audit history.

Do not hardcode account IDs or rely on mutable denormalized balances. Provide drill-down from report totals to account and journal lines. Prove that the trial balance balances and that the Balance Sheet satisfies Assets = Equity + Liabilities for the selected scope and period.

Use bounded read queries optimized in Infrastructure. Keep report definitions reproducible after later configuration changes.
```

## Prompt 9 — Shared UI or CRUD screen

```text
Implement the requested entity screen using the existing SharedUi foundation.

Use PermissionPagedPageBase<TEntity>, PagedGrid<TEntity>, PageActionResult, shared feedback/state components, and FormattingExtensions where applicable. Keep PagedGrid<TEntity> display-only. Entity callbacks should return PageActionResult so the shared base owns success/error feedback and refresh behavior.

The page must provide:

- server-backed paging with bounded page size;
- CanView, CanAdd, CanEdit, CanDelete, CanPrint, and CanExport visibility;
- loading, empty, unauthorized, retry, validation, and error states;
- confirmation before destructive actions;
- accessible labels, keyboard behavior, responsive layout, and visible non-color status;
- navigation to a dedicated route for complex forms;
- no financial rule enforcement in the UI.

Repeat all authorization and business validation in the application command/query handlers.
```

## Prompt 10 — Review a proposed change

```text
Review the current change as a senior ERP architect and security-minded accountant.

Read AGENTS.md and the relevant implementation documents. Inspect the complete diff, not only changed lines. Report findings by priority:

- accounting correctness;
- authorization and tenant/organization isolation;
- posted-record immutability and auditability;
- transaction and concurrency safety;
- SQL Server/migration safety;
- module dependency violations;
- UI accessibility and shared-component violations;
- test gaps;
- documentation or operational gaps.

For each finding, cite the file and line, explain the failure scenario, and propose the smallest safe fix. Do not modify files during review unless explicitly asked.
```

## Prompt 11 — Test and validate a change

```text
Validate the current implementation without changing its behavior.

Run formatting, analyzers, build, affected unit tests, integration tests, component/API tests, architecture tests, and migration checks available in the repository. For financial changes, run an evidence-based balance, reconciliation, or trial-balance assertion.

Check both allowed and denied paths. Include unauthorized users, closed periods, locked days, inactive accounts, duplicate commands/imports, concurrent updates, and posted-record correction behavior where relevant.

Report exact commands, pass/fail results, warnings, environmental limitations, and any test that should be added next. Do not claim validation that could not actually run.
```

## Prompt 12 — Update documentation and ADR

```text
Update the implementation documentation for the completed change.

Keep ERP-VISION.md for product boundaries and approved assumptions, ARCHITECTURE.md for technical boundaries, GL-ROADMAP.md for sequencing and acceptance gates, and BOOTSTRAP-PLAN.md for foundation status. Add a short numbered ADR under docs/decisions/ when a foundational rule, data model, provider choice, authorization policy, or workflow is decided.

Record the decision, context, alternatives considered, consequences, operational impact, and rejected assumptions. Keep documentation concise and consistent with the code and tests. Do not silently change a product rule while documenting an implementation detail.
```

## Prompt 13 — Prepare a reviewable commit

```text
Prepare the current work for review, but do not create a commit.

Inspect git status --short and the complete diff. Separate intended changes from unrelated user work. Run the required validation for the affected boundaries. Check for secrets, certificates, local database files, build output, IDE state, production data, accidental generated files, and broad formatting churn.

Review migrations, tests, documentation, and source together. Summarize the proposed focused commit subject, files that belong in it, files that must remain unstaged, validation evidence, and remaining risks. Do not stage or commit unless explicitly requested.
```

## Prompt 14 — Diagnose a failure

```text
Diagnose the reported failure in OneZero ERP without making a speculative fix.

Read AGENTS.md, inspect the relevant code and tests, reproduce the failure if possible, and trace it through the correct boundary. Distinguish product-rule defects, implementation defects, test defects, environment/configuration problems, and stale/uncommitted worktree effects.

Report:

- minimal reproduction;
- observed versus expected behavior;
- root cause with file and line;
- accounting/security/data-integrity impact;
- safest fix options and tradeoffs;
- regression tests required.

Only implement the fix when I explicitly request it or when this task already authorizes implementation.
```

## Required completion report for future tasks

Every implementation task should finish with:

1. Outcome in one sentence.
2. Files changed.
3. Business rules implemented.
4. Tests and validation executed, with results.
5. Migration or data-impact notes.
6. Assumptions and deferred decisions.
7. Remaining risks.
8. Recommended next task.

Do not report a feature as complete when its server-side rules, persistence, authorization, audit behavior, tests, or documentation are still missing.
