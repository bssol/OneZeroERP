# Bootstrap Plan — Before GL Feature Implementation

## Objective

Create a secure, testable, observable modular-monolith foundation without implementing GL business features. The bootstrap is complete only when the application can run locally, authenticate a test user, apply an empty database schema, execute a protected request, and prove transactional audit/outbox behavior.

## Current acceptance record (2026-10-06)

See [BOOTSTRAP-EVIDENCE.md](BOOTSTRAP-EVIDENCE.md) and
[ADR 0001](../decisions/0001-bootstrap-completion.md) for the current implementation,
local validation, explicit deferrals, and remaining acceptance items. The owner has
requested bootstrap completion before vouchers. Historical status paragraphs below
describe earlier increments; they do not override the current evidence record.
Hosted CI execution, native PWA install verification, and owner sign-off remain open.
On 2026-10-07 the owner directed voucher creation to continue after local
verification. ADR 0002 scopes that work to drafts; the open items remain release
gates and are not marked complete by this exception.

## Workstream 1 — approve the baseline

Record decisions for organization/entity scope, approval workflow, currency, fiscal calendar, identity provider, deployment target, bank inputs, AI data policy, retention, and non-functional targets. Capture each as an ADR with owner and date.

## Workstream 2 — create the solution skeleton

Create a solution with these projects/boundaries:

```text
src/
  OneZeroErp.Api/                    # HTTP composition, endpoints, auth middleware
  OneZeroErp.Web/                    # Blazor Web App delivered as a PWA
  OneZeroErp.SharedKernel/           # small approved primitives only
  OneZeroErp.Platform.Domain/
  OneZeroErp.Platform.Application/
  OneZeroErp.Platform.Infrastructure/
  OneZeroErp.IdentityAccess.Domain/
  OneZeroErp.IdentityAccess.Application/
  OneZeroErp.IdentityAccess.Infrastructure/
  OneZeroErp.GeneralLedger.Domain/
  OneZeroErp.GeneralLedger.Application/
  OneZeroErp.GeneralLedger.Infrastructure/
  OneZeroErp.Audit.Domain/
  OneZeroErp.Audit.Application/
  OneZeroErp.Audit.Infrastructure/
tests/
  OneZeroErp.UnitTests/
  OneZeroErp.IntegrationTests/
  OneZeroErp.ApiTests/
  OneZeroErp.ArchitectureTests/
docs/
  implementation/
  decisions/
deploy/
```

Do not create GL endpoints, aggregates, migrations, screens, or report implementations in bootstrap. Create only the empty module boundaries and wiring needed to validate them.

## Workstream 3 — database and migration foundation

- provision local SQL Server through a repeatable development setup;
- configure the development web host to listen on both `http://localhost:5219` and `https://localhost:7081`;
- verify the local .NET HTTPS development certificate before starting the HTTPS profile;
- use EF Core with the SQL Server provider for schema migrations and persistence;
- keep SQL Server-specific queries and types inside infrastructure so a later PostgreSQL migration does not affect domain/application contracts;
- create schemas for platform, identity, gl, and audit only when their foundational tables are needed;
- establish migration ownership and naming conventions;
- add organization context, audit metadata, outbox, and concurrency primitives;
- verify clean install, upgrade from previous migration, and rollback/recovery procedure in a disposable environment;
- define seed policy: deterministic reference data only, never production-like user or financial records.

The identity foundation must include the custom `AppUsers` table and any approved supporting tables for roles, permissions, organization membership, refresh tokens, token revocation, password-reset workflows, and security events. Passwords are stored only as slow, salted hashes. JWT signing keys and other secrets are supplied through environment-specific secret management and never committed to the repository.

## Workstream 4 — security foundation

- implement authentication against the custom `AppUsers` table;
- issue short-lived JWT access tokens with minimal claims;
- implement securely stored, rotated, revocable refresh tokens with token-family reuse detection;
- implement password reset, login throttling/lockout, logout/revocation, and account-status checks;
- map authenticated user → organization membership;
- define policy evaluation and permission names;
- implement protected endpoint and deny-by-default test;
- audit login, failed login, password changes/resets, token revocation, role/permission changes, and account-status changes;
- document local test identities and secret handling without committing secrets.

## Workstream 5 — Blazor PWA foundation

- create the Blazor Web App shell and responsive ERP layout;
- add the web app manifest, service worker, installability metadata, and controlled update/reload flow;
- define a secure cache strategy that excludes access tokens, refresh tokens, financial records, and audit payloads;
- verify logout/account switching clears sensitive browser state;
- decide whether offline draft composition is enabled;
- if enabled, define local-only draft state, resynchronization, idempotency keys, conflict handling, and visible failure/review states;
- enforce that posting, approvals, locks, reconciliation finalization, and other authoritative financial mutations require a live server transaction.

## Workstream 6 — cross-cutting runtime

- request/correlation IDs and structured logs;
- UTC clock abstraction and business-date/timezone abstraction;
- consistent problem/error response format;
- health/readiness endpoints;
- OpenTelemetry instrumentation;
- transactional outbox dispatcher with idempotency contract;
- feature flags/configuration validation;
- file/export abstraction with safe limits.

## Workstream 7 — quality gates

- format and analyzer checks are required in CI;
- unit test and integration test commands run against a real SQL Server container or isolated SQL Server instance;
- architecture tests enforce dependency direction and module isolation;
- migration validation runs on an empty database and an upgrade database;
- dependency and secret scanning are enabled;
- critical authentication, authorization, audit, and transaction tests are required before merge.

## Conventions to establish

- C#: nullable enabled, analyzers treated as errors where practical, explicit access modifiers, one type per file when it improves navigation.
- Naming: singular domain concepts, UTC technical timestamps, explicit `Money`/amount semantics, and no ambiguous `Value` fields.
- API: versioned, documented contracts; idempotency for imports and externally retried commands; pagination and bounded exports.
- Database: snake_case names or one consistent convention, primary keys with stable IDs, foreign keys/indexes reviewed with each migration, `numeric` for money.
- Git: small focused commits, conventional commit style if approved, pull requests with test evidence and migration notes.
- Tests: invariant-focused unit tests, real-database integration tests, API authorization tests, and a small number of browser journeys.
- Docs: update implementation docs and ADRs with behavior/decision changes; keep runbooks close to deployment configuration.

## Bootstrap acceptance checklist

- [ ] Product and architecture decisions requiring approval are resolved or explicitly deferred.
- [ ] Solution builds with no GL feature behavior.
- [x] Local SQL Server starts reproducibly.
- [x] Empty database migration applies successfully.
- [x] Protected endpoint accepts an authorized test identity and rejects an unauthorized one.
- [x] Custom `AppUsers` authentication issues short-lived JWTs and securely handles refresh/revocation.
- [ ] Blazor PWA installs, updates, and clears sensitive client state on logout/account change.
- [x] Offline behavior, if enabled, is limited to approved drafts and never represents a posted transaction. (Financial offline entry is disabled.)
- [x] Audit and outbox records commit atomically in a test transaction.
- [ ] CI runs formatting, static analysis, unit tests, integration tests, architecture tests, and migration checks.
- [x] Local setup, test data, secrets, and troubleshooting are documented.
- [ ] Owner signs off that GL implementation may begin.

## Foundation implementation status

The current bootstrap implementation has completed the solution skeleton, Blazor PWA shell, custom `AppUsers` authentication boundary, cookie-backed browser session, JWT API validation, module registry, application dashboard, placeholder GL dashboard, three themes, responsive layout, and focused unit/component smoke tests. The initial database persistence and production-shaped AppUsers storage are now implemented through EF Core and SQL Server migrations. Full permission administration, refresh-token persistence, audit query administration, and GL business features remain intentionally deferred.

The development seed user is disabled unless a password is supplied through environment-specific configuration. The preferred PowerShell variable is `$env:DevelopmentSeed__Password`; the implementation also accepts `$env:DevelopmentSeed_Password` for compatibility with an already-configured local shell. The value is read at startup and is not stored in the repository. Never commit a real password or reuse the development signing key in a shared or production deployment.

SQL Server is the current database decision. PostgreSQL remains a future migration option and must be evaluated through a separate compatibility and cutover plan after the first production baseline is stable.

## Explicit non-goals

Bootstrap does not implement fiscal years, accounts, vouchers, journals, reports, budgets, bank reconciliation, or GL screens. Those begin only under the roadmap after this plan's acceptance checklist is met.

## Data-access foundation progress

The first backend data-access increment now includes SQL-backed `identity.Identity_AppUsers` and normalized user permissions, an EF Core `ErpDbContext`, SQL Server provider configuration, the `InitialDataAccess` and module-table naming migrations, and development startup migration/seed orchestration. Fiscal Year persistence uses `erp.Erp_FiscalYears` and no longer creates the schema per request. Authentication reads users asynchronously from the database while retaining the same application authentication contract.

The next data-access increment should add reviewed persistence mappings and application contracts for Chart of Accounts and Voucher Types, followed by integration tests against an isolated SQL Server database. Journal, posting, locking, budget, reconciliation, and report persistence must remain behind their respective domain/application boundaries.
