# OneZero ERP Architecture Proposal

## Bootstrap security/runtime update — 2026-10-06

[ADR 0001](../decisions/0001-bootstrap-completion.md) and the
[bootstrap runbook](BOOTSTRAP-RUNBOOK.md) supersede historical statements below that
refresh persistence, transactional outbox, or SQL-backed identity are unimplemented.
Both hosts now use SQL-backed sessions; JWTs validate issuer/audience and session
state, browser cookies revalidate identity, audit events enqueue outbox rows in the
same transaction, and a local idempotent consumer records delivery. Public-asset
PWA caching excludes financial data, and offline drafts remain disabled. Remaining
acceptance gates are listed in [BOOTSTRAP-EVIDENCE.md](BOOTSTRAP-EVIDENCE.md).

## Recommended technology stack

| Concern | Recommendation | Rationale |
|---|---|---|
| Backend | ASP.NET Core on the current supported .NET LTS (target .NET 10 LTS when bootstrap occurs) | Strong enterprise support, mature web APIs, dependency injection, background work, and good team availability |
| Language | C# with nullable reference types and analyzers enabled | Type safety and maintainability for accounting rules |
| Web UI | Blazor Web App delivered as a Progressive Web App (PWA), using C# and an accessible component library for data grids/forms | Installable, responsive ERP experience with shared validation and domain-facing types while remaining in the .NET ecosystem |
| API contract | REST/JSON for application APIs, OpenAPI-generated client/types | Simple integration boundary; add specialized exports later |
| Database | Microsoft SQL Server, one database with module-owned schemas | Strong enterprise tooling, transactional consistency, decimal support, mature backup/HA options, and a practical first deployment target |
| Data access | EF Core for aggregates and migrations; SQL/Dapper for intentionally optimized reporting queries | Clear domain model plus efficient read paths without hiding SQL where it matters |
| Identity | Custom `AppUsers` table with ASP.NET Core authentication and JWT access tokens | Keeps user ownership and authentication data inside the ERP while providing stateless API authentication; requires disciplined credential and token security |
| Jobs | SQL Server-backed or hosted background jobs initially; introduce a queue when workload warrants it | Keeps deployment small while allowing asynchronous imports/exports |
| Observability | OpenTelemetry traces/metrics/log correlation, structured logs, health checks | Production diagnosis and audit-supporting operational evidence |
| Delivery | Containerized application, CI/CD with migration validation and automated tests | Repeatable promotion across environments |
| Testing | xUnit, FluentAssertions, integration tests against real SQL Server containers or an isolated SQL Server instance, Playwright for critical UI flows | Tests accounting behavior at the right boundaries |

The exact framework versions, hosting provider, and identity product are approval items. The principle is to target a supported LTS release and avoid coupling business logic to a vendor.

## PWA operating model

The Blazor client should provide a web app manifest, service worker, secure cache strategy, installability, responsive layouts, and update notifications. Cache only application assets and explicitly safe reference data. Never cache access tokens, refresh tokens, sensitive financial records, or audit payloads in the service worker cache. Browser storage used for drafts must be minimized, protected from cross-user reuse, and cleared on logout or account change.

The PWA may support offline navigation to the application shell and offline draft composition where the product owner approves it. It must not post journals, change locks, approve transactions, finalize reconciliations, or perform other authoritative financial mutations while offline. These operations require a live server transaction so current permissions, fiscal-period status, day locks, concurrency, numbering, and balance rules are re-evaluated at commit time.

If offline drafts are enabled, drafts must have an explicit local-only state, an online resynchronization step, conflict detection, idempotency keys, visible failure/review status, and no implication that a draft is posted. The server remains the sole source of truth. PWA update failures and stale clients must be handled through version checks and a clear reload/update flow.

## Implemented bootstrap foundation

The initial solution uses a Blazor Web App with interactive server rendering and a PWA shell. The browser experience uses an ASP.NET Core protected session cookie for reliable server-side navigation, while the custom `AppUsers` authentication service issues short-lived JWT access tokens for API and future integration callers. Tokens are not placed in browser local storage, and API authorization remains server-side.

The current foundation contains typed application dashboard and module-registry contracts, a shared UI component library, an in-memory development user store, PBKDF2 password hashing, JWT issuance/validation, protected routes, login/logout endpoints, three themes, responsive shell states, and placeholder GL dashboard data. It now contains the initial SQL Server persistence foundation for AppUsers, normalized user permissions, Fiscal Years, and audit events. Role/permission administration, refresh-token persistence, and accounting behavior remain deferred.

### Current solution structure

```text
OneZeroErp.slnx
src/
  OneZeroErp.Web/             # Blazor host, HTTP endpoints, shell, PWA assets
  OneZeroErp.Application/     # typed use-case contracts and dashboard models
  OneZeroErp.Domain/          # domain/core abstractions
  OneZeroErp.Infrastructure/  # platform service implementations and registries
  OneZeroErp.IdentityAccess/  # AppUsers, password hashing, JWT issuance
  OneZeroErp.SharedUi/         # reusable Blazor UI components
tests/
  OneZeroErp.UnitTests/       # authentication and application service tests
  OneZeroErp.ComponentTests/  # component-project smoke tests; expand with browser tests
```

The first GL implementation should add independently organized GL contracts/domain/application/infrastructure folders or projects behind the same application boundary. It must not move accounting rules into `Web` or `SharedUi`.

## Modular monolith

The deployable unit is one backend and one web application, but the code is divided into business modules with explicit contracts. Each module owns its tables and transaction rules. Modules communicate through application contracts and in-process domain/integration events, never by reaching into another module's tables.

### Initial modules

- `IdentityAccess`: users, roles, permissions, organization membership, authorization policy.
- `GeneralLedger`: fiscal calendar, accounts, voucher types, journals, posting, locks, budgets, bank/cash, reconciliation, and financial report definitions.
- `Audit`: immutable audit events, actor/context, before/after summaries, and correlation links.
- `Platform`: tenant/organization context, clock, IDs, outbox, file storage abstraction, notifications, and shared technical concerns.

Future modules may include Accounts Payable, Accounts Receivable, Sales, Procurement, Inventory, Fixed Assets, Payroll, and Tax. They should consume GL contracts/events rather than duplicate posting logic.

### Boundary rules

- Domain code does not depend on UI, EF Core, or external providers.
- Application handlers coordinate use cases and authorization; domain objects enforce invariants.
- Infrastructure implements persistence, identity adapters, file storage, bank imports, and telemetry.
- A module publishes stable integration events such as `JournalPosted`, `JournalReversed`, `PeriodLocked`, and `BudgetApproved`.
- Integration events are delivered through an outbox in the same transaction as the state change.

## Request and posting flow

1. Authenticate the caller and establish organization context.
2. Authorize the operation and verify the requested permission.
3. Load the relevant fiscal period/day-lock and journal aggregate.
4. Validate dates, voucher type, account status, dimensions, currency/precision, and balanced totals.
5. Persist the state change and audit event transactionally.
6. For posting, write immutable posted journal lines and an outbox event in the same transaction.
7. Reports query only posted lines and an approved, effective report configuration.

The application must re-check authorization and locks at commit time; UI checks are advisory only.

## Data strategy

Use a single SQL Server database initially, with schemas such as `platform`, `identity`, `gl`, and `audit`. Every business table has a stable identifier, created/updated metadata where applicable, and organization scope where applicable. Use SQL Server `decimal(19,4)` or an explicitly approved organization precision for amounts; never use `float`/`real`/`double` for money. Keep bounded configuration in normalized tables or validated JSON text rather than coupling the domain to PostgreSQL-specific JSONB behavior.

The application should use EF Core with the SQL Server provider for migrations and persistence. Keep provider-specific SQL isolated in infrastructure, avoid PostgreSQL-only features in the first schema, and maintain a provider-neutral domain/application layer so a later PostgreSQL migration remains a database/infrastructure concern rather than a module rewrite. The migration to PostgreSQL is a future project with its own compatibility, data-conversion, performance, and cutover plan; it is not part of the current bootstrap.

Keep journal headers and lines normalized. Store account hierarchy with a parent ID plus a materialized path or closure strategy only if query needs justify it. Start with parent IDs and indexed recursive queries; add a derived path/read model when volume requires it. Use effective-dated, versioned tables for report layouts and account mappings.

Backups, point-in-time recovery, restore drills, encryption, retention, and production access procedures are deployment decisions that must be documented before go-live.

## Authentication, authorization, and audit

Authentication uses a custom `AppUsers` table and JWT access tokens. Passwords must be stored only as slow, salted password hashes; plaintext passwords and reversible encryption are prohibited. Access tokens should be short-lived and contain only the minimum claims needed for authentication and organization context. Use securely stored, rotated, revocable refresh tokens for longer sessions, with token-family reuse detection and logout/revocation support. Signing keys must be stored outside source control and rotated through configuration/secret management.

The application owns user lifecycle, account status, password reset, login throttling/lockout, token issuance, refresh-token persistence, revocation, and security-event auditing. If external SSO is required later, it should be added behind an authentication adapter without changing module authorization policies.

Authorization is policy-based and evaluated at organization, module, resource, and operation scope. The initial rights are `CanView`, `CanAdd`, `CanEdit`, `CanDelete`, `CanPrint`, and `CanExport`. Sensitive actions such as post, reverse, unlock, approve, change report mappings, and import bank statements are separate named permissions even if they are not exposed in the first UI matrix. JWT claims are not a substitute for current server-side permission checks; permission and lock decisions must be re-evaluated for protected operations.

Audit records include actor, organization, timestamp, action, entity type/id, correlation/request ID, source, reason/comment where required, and a safe before/after summary. Do not store credentials or excessive personal data in audit payloads. Audit records are append-only to normal application users; privileged retention access is separately controlled and itself audited.

## AI boundary

AI features are adapters behind an `IAiAssistant`-style application contract. They may summarize, explain, classify, search, detect anomalies, or draft a journal for review. They may not bypass permission checks, day locks, balance validation, approval, posting, or audit creation. Prompts and outputs are treated as untrusted input, and sensitive data handling follows the approved deployment/data policy.

## Non-functional targets to confirm

- availability and recovery objectives;
- expected organizations, users, journals, and report concurrency;
- retention and legal requirements for audit and financial records;
- supported browsers and accessibility level;
- localization, timezone, numbering, and tax requirements.

## Centralized entity-page workflow

Entity maintenance screens use the shared `PermissionPagedPageBase<TEntity>` and `PagedGrid<TEntity>` foundation. Each page supplies only its resource name, paged loader, entity-specific delete operation, row columns, and simple-versus-complex navigation behavior.

The shared page foundation is responsible for:

- Loading the current user permissions once and exposing `CanView`, `CanAdd`, `CanEdit`, `CanDelete`, `CanPrint`, and `CanExport` checks.
- Loading entity rows through `PagedQuery` and `PagedResult<TEntity>` rather than loading an unbounded collection.
- Consistent loading, empty, unauthorized, retry, and error states.
- Showing only actions allowed by the current permission set.
- Requesting confirmation before delete and handling delete errors in one place.
- Providing a shared responsive grid and pagination experience.

Permission checks in the UI are a presentation concern. Every command and query must repeat authorization and business-rule checks on the server. Permission claims use the `onezero:permission` claim type and the `resource:PermissionAction` format; wildcard resource claims are supported. A missing claim is denied by default.

Simple entities may render an entity form in a shared dialog. Complex entities should navigate to a dedicated route. The choice belongs to the entity page, while dialog lifecycle, confirmation, errors, and notifications remain shared behavior.
## Shared page contracts

`PagedGrid<TItem>` is a display-only generic component. It receives a strongly typed, already-paged list and render templates for headers, rows, and optional row actions. Pagination, loading, empty/error/unauthorized states, confirmation dialogs, and action orchestration remain in `PermissionPagedPage<TItem>` and `PermissionPagedPageBase<TItem>`.

Entity callbacks return `PageActionResult` rather than writing page-level notifications. The shared base interprets the result, displays success or error feedback, closes simple-entity dialogs after success, refreshes the current page, and combines nested exception messages through the centralized formatting extensions. This keeps entity pages focused on their own queries, commands, and fields.

`FormattingExtensions` provides shared date, time, Pakistan date-format, decimal-formatting, and exception-message helpers. Financial and display formatting must use these helpers consistently; monetary persistence remains decimal-safe and independent of display formatting.
## UI folder organization

The web host organizes Razor pages by purpose and module:

```text
src/OneZeroErp.Web/Components/
├── Layout/                         # application layouts and shell chrome
├── Pages/
│   ├── ApplicationPages/           # home and cross-module dashboards
│   ├── AuthenticationPages/        # login and authentication flows
│   ├── SystemPages/                # error, not-found, and unauthorized pages
│   └── ModulePages/
│       └── GL/
│           ├── SetupPages/         # fiscal years, chart of accounts, voucher setup
│           ├── TransactionPages/   # journals and operational transactions
│           ├── BankingPages/       # bank/cash and reconciliation
│           └── ReportingPages/     # configurable GL reports
```

The Fiscal Year page is the first module page and is located at `Pages/ModulePages/GL/SetupPages/SetupFiscalYearPage.razor`. Its existing route remains `/gl/fiscal-years` so navigation links and bookmarks remain compatible. Shared reusable UI components remain in `OneZeroErp.SharedUi`; they are not copied into individual module folders.
## Domain model boundaries

GL aggregates live under `OneZeroErp.Domain.GeneralLedger` and have no dependency on EF Core, SQL Server, Blazor, JWT, or infrastructure services. They enforce accounting invariants locally: journal balance, fiscal-year range, day-lock and closed-period protection, posted-journal immutability, budget approval locking, reconciliation completion, and configurable report mappings.

Persistence entities and application contracts may project these models for storage and transport, but application authorization, audit recording, and transaction coordination remain outside the domain. Domain models do not calculate financial statements; reporting services will later query posted journal lines using the configured mappings.
## AuditableEntity convention

All persisted domain entities inherit the domain `AuditableEntity` base. It provides `CompanyId`, `CreatedOn`, `CreatedBy`, `UpdatedOn`, `UpdatedBy`, `DeletedOn`, and `DeletedBy`. Mutating domain methods require an actor ID and update the metadata. Soft deletion is represented by deletion metadata; posted financial records remain immutable and must be corrected through reversal or adjustment workflows.

This metadata is not a replacement for the append-only audit history. Application commands must still write an audit event containing the action, entity, actor, company, timestamp, and safe before/after summaries in the same transaction as the business change.

## Chart of Accounts hierarchy

`ChartOfAccount` represents one row of the requested `ChartOfAccounts` structure:

- `CompanyId`
- `AccountNo`
- `ParentAccountNo`
- `ParentAccountTitle`
- `AccountLevel`
- `AccountType`
- `Title`
- `Description`
- `CreatedOn`
- `CreatedBy`
- `IsActive`

Root accounts use account level `0` and no parent. Child accounts reference `ParentAccountNo` and must be exactly one level below their parent. `ChartOfAccounts` validates company ownership, duplicate account numbers, parent existence, and level consistency, and exposes a tree projection for UI/reporting use. `ParentAccountTitle` is retained as a denormalized display snapshot and must be refreshed when parent titles change.

New account numbers are generated server-side. The first root for an account type uses `100000000` and the display title for that type (for example, `Assets`). Once a non-posting parent is selected, the next grouping child uses the next sequential number after that parent; a posting child uses the next available number by incrementing the first available digit from the left (for example, `100000000` becomes `110000000`). The UI displays these values as read-only suggestions, but the application service recalculates them during creation. The Add Account dialog presents active non-posting accounts as a recursive tree; the selected parent supplies the new account's type and hierarchy level.

The Chart of Accounts page also accepts CSV imports through the downloadable `chart-of-accounts-import.csv` sample. The required columns are `AccountType`, `ParentAccountNo`, `Title`, `Description`, `IsPostingAccount`, and `IsActive`. Parent rows must appear before child rows. Account numbers are intentionally omitted from the upload and generated by the application service using the same root, child, posting, duplicate, parent-type, and audit rules as interactive account creation. Validation completes before any row is persisted.

## Validation library decision

Do not add FluentValidation to the domain project at this stage. Domain constructors and methods enforce invariants so they cannot be bypassed by another caller. Once Application commands and UI/API request models stabilize, FluentValidation may be added to the Application boundary for input shape, cross-field messages, and user-facing validation. Those validators must complement—not replace—the domain rules.
## Voucher Type model decision

Voucher types are configurable company-scoped master data, not an enum. `VoucherType` stores an auditable string `Code`, `Description`, active state, and operational requirements such as bank or cash account usage. The initial OPV, JVV, CPV, CRV, BPV, and BRV values are seed records only. Journals reference `VoucherTypeId`, allowing additional voucher types to be configured without recompiling the domain.
## Centralized clock and timezone

All runtime time access is centralized through `OneZeroErp.Application.Time.IClock`. `UtcNow` is used for persisted technical timestamps, audit events, token expiry, posting timestamps, and lock metadata. `CurrentDateTime`, `CurrentDate`, and `CurrentTime` use the configured company timezone for accounting and UI behavior.

The default timezone is `Pakistan Standard Time`, configured by `Company:TimeZoneId`, with `Asia/Karachi` supported for Linux environments. Domain aggregates do not call system time directly; application services obtain the timestamp from `IClock` and pass it into domain commands. Tests can provide deterministic time through a custom `TimeProvider`.
## Data-access foundation status

The first persistence increment uses EF Core migrations against SQL Server. The initial migration is `InitialDataAccess`; the follow-up naming migration uses module-prefixed tables such as `erp.Erp_FiscalYears`, `erp.Erp_LockDate`, `gl.Gl_Setup_ChartOfAccounts`, `identity.Identity_AppUsers`, and `audit.Audit_Events`. Development startup applies pending migrations before seeding the environment-provided development administrator.

`EnsureCreated` is not used for ongoing schema evolution. Any database created by the earlier bootstrap-only `EnsureCreated` path must be treated as a disposable development database or explicitly baselined through a reviewed migration procedure before shared use; it must not be silently dropped or overwritten.
## Chart of Accounts data-access boundary

Chart of Accounts persistence is exposed through `IChartOfAccountService` and `ChartOfAccountEntity` in the General Ledger application/infrastructure boundaries. The service owns paged reads, parent-account validation, account-number uniqueness, activation changes, and audit recording. It does not expose EF entities to the UI and does not implement journal posting or financial reporting.
