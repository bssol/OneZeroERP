# Bootstrap acceptance evidence — 2026-10-06

Status: Technical foundation locally verified; final release acceptance remains open.

## Recheck and voucher-draft continuation — 2026-10-07

The owner directed voucher creation to continue after rechecking the bootstrap
implementation. ADR 0002 records this draft-only scope. The whole-solution
run before credential synchronization passed **54 tests** (31 unit/architecture, 21 SQL/API integration, 2 browser),
with zero failures or skips. The additional tests cover local admin recovery,
voucher permission/precision/lock validation, audit/outbox persistence, fast
payment balancing, concurrent numbering, and browser entry. The new forward migrations create voucher
draft tables and add referential constraints; they were applied to disposable SQL
test databases. The application database was not migrated.
The 2026-10-07 NuGet vulnerability query listed no vulnerable direct or
transitive packages. Warning-free build, format check, tracked-credential check,
and EF pending-model check also passed.

## Local development credential synchronization — 2026-10-07

The owner requested that the four configured passwords be applied to the
existing application database. A Development-only command updated `admin`,
`fiscal.manager`, `gl.accountant`, and `gl.viewer` in a single transaction.
Read-only SQL verification found all four active accounts bound to company
`11111111-1111-1111-1111-111111111111`, unlocked, with zero active sessions
and one synchronization audit event each. The stored salted hashes were
verified against the configured values without displaying them. Plaintext
passwords were then cleared from both tracked Web settings files. This
operation did not apply the pending voucher migrations to the application
database.

The final whole-solution run passed **56 tests** (31 unit/architecture,
23 SQL/API integration, 2 browser), with zero failures or skips. The two new
SQL tests cover successful four-user synchronization and all-or-nothing
rejection of a mismatched role. Warning-free build, format, tracked-secret,
EF pending-model, and `git diff --check` checks passed.

The in-app demo-data section added on 2026-10-07 was verified in Release while
the developer's Debug host remained running. The full solution passed **61
tests** (32 unit/architecture, 26 SQL/API integration, 3 browser). Coverage
includes administrator authorization, idempotent seeding, transactional
audit/outbox records, balanced draft-only vouchers, and the complete browser
interaction. Warning-free build, formatting, EF pending-model, and diff checks
passed. The tracked-secret check currently fails because SQL credentials are
present in both Web appsettings files; move the connection string to environment
configuration or .NET user secrets before committing.

The default `MSSQLLocalDB` instance failed to start during the earlier recheck.
An isolated `OneZeroErpVerification` LocalDB instance ran the full suite. The
default instance was later started for the authorized credential synchronization.
No existing LocalDB database was deleted or replaced. Hosted CI, native PWA
installation and formal release acceptance remain open.

Prior bootstrap-only local test run: **46 passed, 0 failed, 0 skipped** (29 unit/architecture,
16 SQL/API integration, 1 browser journey). Build: **0 warnings, 0 errors**.
NuGet vulnerability query including transitive packages reported no vulnerable
packages. Formatting verification and tracked-credential checks passed. Test result
files are available locally under the ignored `TestResults/` directory.

Commands executed:

```powershell
dotnet build OneZeroErp.slnx --no-restore --warnaserror
dotnet test OneZeroErp.slnx --no-build --no-restore --logger trx --results-directory TestResults
./scripts/check-format.ps1
./scripts/check-secrets.ps1
dotnet list OneZeroErp.slnx package --vulnerable --include-transitive --format json
./.tools/dotnet-ef migrations has-pending-model-changes --project src/OneZeroErp.Infrastructure --no-build
```

The original bootstrap increment contained no voucher screens, posting endpoints,
or journal persistence. The later owner direction and draft implementation are
recorded above. Posting endpoints remain unimplemented.

## Implemented and locally verified

| Gate | Evidence |
| --- | --- |
| Current solution builds | Whole solution build with warnings as errors; API is now included in the solution. Existing GL setup remains as pre-existing work. |
| Reproducible local SQL | SQL Server LocalDB connected successfully; new-install instructions and isolated test provisioning are documented. |
| HTTPS development foundation | `dotnet dev-certs https --check` found valid certificates; browser tests run over HTTPS. |
| Clean database migration | Real SQL integration test applies all migrations to an empty uniquely named database. |
| Forward upgrade | Upgrade from `AddTaxConfigurations` preserves an existing synthetic user row. EF reports no pending model changes. |
| Recovery | SQL backup and restore verified on a disposable database; restored data matches the backup. |
| Protected request | Real HTTP tests prove anonymous=401, missing permission=403, authorized=200, revoked=401. |
| SQL-backed authentication | Password hashing, lockout, expiring sessions, hashed refresh rotation, concurrent reuse detection, logout, password-reset expiry/single use, permission refresh, company binding, and wrong JWT issuer/audience tested. |
| Audit/outbox | Business change, audit row, and outbox row commit/rollback together; audit updates are rejected; replay leaves one local delivery receipt. |
| Browser lifecycle | Headless Edge validates sign-in, HttpOnly/Secure cookie, antiforgery-protected logout, manifest, public-only cache, offline fallback, and user-controlled update activation. |
| Offline scope | Offline financial drafts and authoritative offline operations are disabled. |
| Architecture | Domain/application dependency direction and identity boundary tests pass. |
| Secret hygiene | Populated tracked development credential values removed; credential-configuration check passes. Values in earlier Git history were not rewritten. |
| Operational documentation | README, bootstrap runbook, ADR, and this evidence record added. |

Validation commands are recorded in README. Tests run against generated
`OneZeroErp_Test_<guid>` databases with synthetic users. The application database was
not migrated or changed during the initial bootstrap verification; the later
credential synchronization is recorded above. User-provided migration/fiscal-page edits
and import files were retained. No commits, branches, pushes, or PRs were created.

## Remaining acceptance items

- Run the new GitHub Actions workflow on the reviewed branch. CI configuration is
  present; local checks are evidence for the code, not a hosted CI run. Full-history
  Gitleaks scanning may identify credentials from earlier commits; assess and rotate
  real reused values without silently rewriting history.
- Verify installation/launch through the browser's operating-system app-install UI.
  The automated test validates the manifest and PWA behavior, not native installation.
- Owner review/sign-off of ADR 0001 and the bootstrap acceptance gate. This includes
  explicit deferrals of product/deployment decisions and retaining pre-existing GL
  setup while interpreting the historical "no GL behavior" gate as no new GL work.

These release items remain unchecked. Local test results do not mark bootstrap
release acceptance complete.

## Boundaries

OpenTelemetry tracing/metrics are registered; external telemetry export remains off
pending an approved destination/data policy. Outbox delivery is currently a local
durable audit feed, not email or integration delivery. Production secret rotation,
retention, role-management UI, deployment hardening, and production recovery targets
are explicitly deferred. Existing GL services still need their roadmap-specific
authorization/concurrency review before financial transactions are introduced.
