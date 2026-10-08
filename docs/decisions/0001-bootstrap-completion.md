# ADR 0001: Complete bootstrap before voucher transactions

Date: 2026-10-06
Status: Implementation baseline; owner acceptance pending; draft-entry exception recorded in ADR 0002

The product owner explicitly requires completion of the bootstrap gates before
voucher implementation. Existing GL setup work is retained, but no new voucher
behavior is authorized until the acceptance evidence is reviewed and signed off.
Voucher scope remains the Debit/Credit grid plus fast payment/receipt input.

On 2026-10-07, after requesting a recheck of bootstrap work, the owner directed
voucher creation to continue. ADR 0002 records that later direction and the
draft-only implementation. The remaining bootstrap release checks stay open.

## Foundation decisions

- Retain the existing .NET 10 modular monolith, SQL Server, custom AppUsers,
  interactive-server Blazor, and configured company timezone.
- Operate with one configured company initially. Persist user company membership
  and include it in authenticated sessions. Multi-company switching is deferred.
- Use 15-minute audience/issuer-bound JWTs, server-side sessions, hashed rotating
  refresh credentials, family revocation on reuse, account lockout, and audited
  administrative password-reset grants. Browser authentication stays in HttpOnly
  cookies; API refresh tokens never enter browser storage.
- Record security changes and their outbox messages atomically. Delivery is at
  least once; handlers must use the message ID for transactional deduplication.
- Offline financial drafts are disabled. Cache only explicitly listed public
  assets, and require user action before activating an application update.
- Validate clean installation and forward upgrade on disposable SQL databases.
  Recovery uses backups/restore, never editing applied migrations.

## Explicit deferrals

Approval workflow, full FX accounting, period configuration, bank imports,
dimensions, AI data processing, production hosting, retention, backup objectives,
performance targets, mail delivery, role-administration UI, and production key
rotation remain separate decisions before the relevant feature or deployment.
Bootstrap establishes technical controls; it does not authorize posting or
external delivery. No AI or external messaging is enabled.

## Acceptance

Record commands, results, unresolved checks, and sign-off in
`docs/implementation/BOOTSTRAP-EVIDENCE.md`. Do not infer owner sign-off from
successful tests. The historical checklist item about a solution without GL
behavior is assessed as "no new GL behavior in this bootstrap increment" because
the repository already contains GL setup screens and domain models.
