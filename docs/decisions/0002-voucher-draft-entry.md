# ADR 0002: Voucher draft entry and local admin recovery

Date: 2026-10-07
Status: Implemented for local review; posting and release acceptance remain open

After review of the bootstrap implementation, the owner directed work to continue
on voucher creation. This supersedes the pause recorded in ADR 0001 for the draft
creation slice. Hosted CI, native PWA installation, and formal bootstrap release
acceptance are still outstanding; they are not represented as complete.

Voucher entry uses `Line | Account | Narration | Debit | Credit`. Standard mode
stores one-sided positive decimal lines and permits an unbalanced **draft**.
Fast payment/receipt mode selects a registered active bank/cash GL account and
counterpart accounts with amounts; it creates the opposite source line and saves
through the same draft service. Initial fast-mode eligibility follows the seeded
CPV/CRV/BPV/BRV codes. Custom voucher types use standard mode until direction
becomes explicit voucher-type configuration.

Draft numbers are generated per company, voucher type and fiscal year as
`TYPE-YEAR-000001`. A serializable SQL transaction, SQL Server application lock,
and unique constraints prevent duplicate persisted numbers. Editing retains the
assigned type and fiscal year.
Draft mutations require current user permissions and company membership, an open
fiscal year, an initialized unlocked day, active posting accounts, and an active
base currency. Amounts must fit its precision and SQL `decimal(19,4)`. The schema
references fiscal year, voucher type, currency and accounts, and each mutation
writes a transactional audit/outbox record. There is no posting command or report
read path for drafts. Full FX posting, fiscal periods, approval, reversal and
posted-journal reporting remain Phase 3 work.

An existing `admin` password is never replaced by development seeding. The
Development-only local recovery command requires an interactive terminal, an
explicit configured database and company, and a matching active Administrator.
It prompts without echo, stores a salted hash, revokes sessions/reset grants,
clears lockout, and writes audit/outbox evidence in one SQL transaction. It does
not create or reactivate an account. The operator must verify the configured
database and company before running it.

On 2026-10-07, the owner explicitly requested applying all four configured
development seed passwords to their matching existing SQL users. The local
Development maintenance operation checks every user name, role, active status,
and company before changing any account. Legacy empty company memberships are
bound to the configured company. All four password hashes, session/reset-grant
revocations, lockout clears, and audit/outbox records commit in one transaction.
Seed configuration alone continues to leave existing passwords unchanged.
