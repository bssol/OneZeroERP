# Bootstrap operations

## Identity

Browser login uses an HttpOnly, SameSite=Strict cookie; HTTPS cookies are Secure.
HTTP is allowed only for local development. Login and logout validate antiforgery
tokens. Login uses static server rendering to avoid losing input during circuit
startup. Sessions expire after eight hours without sliding renewal.

The API exposes `/api/v1/auth/login`, `/refresh`, `/logout`,
`/password-reset/{userId}`, `/password-reset/complete`, and `/password/change`.
Access tokens expire after 15 minutes and require the configured issuer/audience,
signature, and a current server session. Refresh tokens are random 256-bit values;
only SHA-256 hashes are stored. Refresh rotates the token under a SQL transaction.
Reusing any consumed token revokes its entire session, including newer tokens.
Clients must serialize refresh requests and sign in again after ambiguous retries.

Five failed passwords lock an account for 15 minutes. HTTP authentication endpoints
also enforce 20 requests per minute per direct client IP; a reverse-proxy deployment
must explicitly configure trusted forwarding before relying on client-IP limits.
Passwords use salted PBKDF2-SHA256 with 600,000 iterations. Legacy 120,000-iteration
hashes still verify and are replaced when the password changes. Password changes
and resets require 12–256 characters, revoke all sessions, consume pending reset
grants, and write audit events. Credentials and tokens are never placed in audit
summaries or request logs.

Password reset issuance requires a live session with `identity.users:CanEdit` and
matching company membership. The grant is single-use and expires after 15 minutes.
Only the authorized operator receives it; distribution through a verified channel
is an operational responsibility. No email/SMS is sent. There is no public username
lookup/reset-token endpoint. Password changes require the existing password.

Every HTTP-authenticated request revalidates the SQL session and reloads permissions.
Blazor circuits additionally revalidate every 30 seconds and end stale identities
when their claims change. Future financial commands must perform their own current
authorization inside the posting transaction; circuit revalidation is not a
substitute. Role/account administration UI remains deferred.

## Existing development credentials

Previously tracked development password/signing-key values have been removed from
working-tree configuration. This does not erase Git history or reset existing
accounts. Rotate any reused values through the password-change/reset workflow and
external secret configuration. The initial bootstrap pass did not change passwords.
For a locally inaccessible `admin` account, use the interactive Development-only
`reset-admin-password` command documented in README. It verifies company
membership, writes the replacement hash and audit/outbox evidence, revokes prior
sessions and grants, and never creates or reactivates an account. The development
seed password setting only initializes a missing account.

For the four existing local development accounts, the Development-only
`sync-development-passwords` command documented in README replaces their
password hashes as one transaction. It requires all four temporary password
settings, checks user names and roles, binds empty company memberships, revokes
sessions and reset grants, clears lockouts, and writes audit/outbox records.
Remove the plaintext settings after running it. Do not run it against a shared
or production database. On 2026-10-07 this command updated the four existing
users in the local `OneZeroErp` database.

## Migrations and recovery

The bootstrap introduced `CompleteBootstrapIdentityAndOutbox` and
`BindSessionsToCompany`; voucher drafts added `AddVoucherDrafts` and
`ProtectVoucherDraftReferences`. The second bootstrap migration deliberately
leaves old sessions unbound, so
they fail closed and require a fresh login. Applied migrations must never be edited.
The former startup `ALTER TABLE` repair has been removed; schema evolution is owned
by migrations. Existing migration edits supplied by the user were preserved.

Before a shared deployment, take and verify a database backup, generate/review the
forward SQL with `dotnet ef migrations script --idempotent --project
src/OneZeroErp.Infrastructure`, and apply it through the deployment operator.
Use the local `.tools/dotnet-ef` executable if that is the configured EF tool.
`ErpDesignTimeFactory` supplies a design-only connection; never assume it is the
deployment target. Pass the reviewed target explicitly for database operations.

On failure, stop writers, preserve diagnostics, restore the verified backup in the
approved recovery environment, verify migration history and representative data,
then restore service using the matching application release. Prefer a corrective
forward migration if restoring would lose subsequently committed business data.
Do not run destructive migration `Down` operations on a live financial database.

Automated tests exercise clean migration, upgrade from `AddTaxConfigurations`
with a preserved user row, pending-model validation, and backup/restore on a uniquely
named disposable database. These are development evidence, not production recovery
time/point guarantees. Production backup retention and restore objectives require
owner approval before deployment.

## Audit and outbox

Application audit rows are append-only. `ErpDbContext.SaveChanges` pairs every added
audit event with an outbox row, sharing the business transaction. The initial local
consumer records a durable delivery receipt and acknowledgement atomically; the
receipt's message ID makes replay idempotent. No external notifications are sent.

The hosted dispatcher checks every five seconds and processes at most 50 messages
per batch. SQL update locks prevent competing workers from delivering one row
concurrently. On an error it logs the error type and leaves pending work for retry.
`Outbox:Enabled=false` disables dispatch for isolated tests/maintenance. Inspect
pending rows and logs when delivery stops; do not delete audit/outbox evidence.
Future external consumers must deduplicate by message ID in their own transaction;
the current local receipt is not evidence of external delivery.

## Health and observability

`/health/live` is independent of SQL. `/health/ready` checks SQL connectivity and
pending migrations without exposing database details. The API's
`/api/v1/platform/health` requires `platform.diagnostics:CanView`; it is the protected
authorization probe, not a substitute for readiness. Missing permissions deny.

Responses contain `X-Correlation-ID`; structured request logs omit bodies, query
strings, passwords, and tokens. OpenTelemetry ASP.NET Core tracing/metrics are
registered in both hosts. No telemetry exporter or external collection endpoint is
enabled; configure a reviewed collector and data policy before export. See the
[OpenTelemetry hosting package](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/1.19.1)
and [ASP.NET Core instrumentation](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore/1.19.0).

## PWA and browser data

Only allowlisted public assets are cached. Financial pages, API responses, login
responses, cookies, and tokens are never cached by the worker. Offline navigation
shows a public reconnect page; offline drafts are disabled. A waiting application
update displays a save-work reminder and activates only when the user selects
"Reload to update". Old OneZero asset caches are removed on activation.

Logout revokes the server session, clears the cookie, and sends Clear-Site-Data for
cache/storage. Theme and sidebar preferences are the only application localStorage
values. The login page also removes obsolete OneZero storage keys. Never add
financial data to browser storage without a new approved decision.

For manual install acceptance, start the HTTPS profile, install OneZero ERP from
Edge's app menu, launch the installed window, sign in/out, and verify offline
reconnect behavior. Automated tests validate the manifest, login/logout, Secure and
HttpOnly cookies, cache contents, offline navigation, and waiting-update activation;
they do not click the operating-system install UI.
