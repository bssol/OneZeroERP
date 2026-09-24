# Bootstrap Plan — Before GL Feature Implementation

## Objective

Create a secure, testable, observable modular-monolith foundation without implementing GL business features. The bootstrap is complete only when the application can run locally, authenticate a test user, apply an empty database schema, execute a protected request, and prove transactional audit/outbox behavior.

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
- [ ] Local SQL Server starts reproducibly.
- [ ] Empty database migration applies successfully.
- [ ] Protected endpoint accepts an authorized test identity and rejects an unauthorized one.
- [ ] Custom `AppUsers` authentication issues short-lived JWTs and securely handles refresh/revocation.
- [ ] Blazor PWA installs, updates, and clears sensitive client state on logout/account change.
- [ ] Offline behavior, if enabled, is limited to approved drafts and never represents a posted transaction.
- [ ] Audit and outbox records commit atomically in a test transaction.
- [ ] CI runs formatting, static analysis, unit tests, integration tests, architecture tests, and migration checks.
- [ ] Local setup, test data, secrets, and troubleshooting are documented.
- [ ] Owner signs off that GL implementation may begin.

## Foundation implementation status

The current bootstrap implementation has completed the solution skeleton, Blazor PWA shell, custom `AppUsers` authentication boundary, cookie-backed browser session, JWT API validation, module registry, application dashboard, placeholder GL dashboard, three themes, responsive layout, and focused unit/component smoke tests. Database persistence, production identity storage, full permissions, audit persistence, and GL business features remain intentionally deferred.

The development seed user is disabled unless a password is supplied through environment-specific configuration. The preferred PowerShell variable is `$env:DevelopmentSeed__Password`; the implementation also accepts `$env:DevelopmentSeed_Password` for compatibility with an already-configured local shell. The value is read at startup and is not stored in the repository. Never commit a real password or reuse the development signing key in a shared or production deployment.

SQL Server is the current database decision. PostgreSQL remains a future migration option and must be evaluated through a separate compatibility and cutover plan after the first production baseline is stable.

## Explicit non-goals

Bootstrap does not implement fiscal years, accounts, vouchers, journals, reports, budgets, bank reconciliation, or GL screens. Those begin only under the roadmap after this plan's acceptance checklist is met.
