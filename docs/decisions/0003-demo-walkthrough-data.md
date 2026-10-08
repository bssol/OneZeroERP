# ADR 0003: In-app demo walkthrough data

Date: 2026-10-07
Status: Implemented for development walkthroughs

The owner requested a separate in-app section that prepares data for walking
through implemented General Ledger screens. The Administration > Demo data page
is available only to an authenticated company user with the existing
`identity.users:CanEdit` permission. The service repeats the same authorization
check against current SQL data before making changes.

The walkthrough pack creates missing setup records with recognizable `DEMO-`
codes: an open fiscal year and accounting-day calendar, a small chart of
accounts, standard bank/cash/journal voucher types, base and foreign currency
setup, bank and cash registrations, sales tax, and three balanced voucher
drafts. If the company already has a suitable open fiscal year or base currency,
the pack uses it. It does not overwrite matching business setup.

Seeding runs in a serializable SQL transaction with a company-scoped application
lock. Repeating the action is idempotent. Sample journals remain `Draft`; the
feature has no posting path and therefore does not affect financial statements.
Each created sample journal and each material seed operation writes audit/outbox
evidence. There is deliberately no in-app reset or bulk-delete action because
later user activity may reference the walkthrough records.
