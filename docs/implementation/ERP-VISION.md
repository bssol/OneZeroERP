# OneZero ERP Vision and Product Baseline

## Purpose

OneZero ERP will provide a controlled, auditable financial system that can grow from General Ledger into additional ERP capabilities without prematurely splitting into distributed services. AI capabilities are assistants around governed ERP data; they do not bypass accounting controls or post silently.

## First release boundary

The first business area is General Ledger, including:

- fiscal years and fiscal periods;
- hierarchical chart of accounts;
- voucher types and journal lifecycle;
- OPV, JVV, CPV, CRV, BPV, and BRV journals;
- bank and cash accounts;
- bank reconciliation;
- day locking;
- permission-based rights: CanView, CanAdd, CanEdit, CanDelete, CanPrint, CanExport;
- budgeting;
- configurable Notes to Accounts, Profit and Loss, and Balance Sheet definitions.

The system must distinguish draft, approved (if approval is enabled), posted, reversed, and void/cancelled states. Posted accounting effects are append-only.

## Product principles

1. Control before convenience: posting, locks, permissions, and auditability are server-side rules.
2. One accounting truth: reports derive from posted journal lines and approved configuration.
3. Configuration over hardcoding: fiscal calendars, voucher behavior, account mappings, and statement layouts are data-driven.
4. Traceability: a user can navigate from a report balance to account, journal, source document, and audit history.
5. Safe assistance: AI may explain, classify, search, suggest, or draft; a permitted human remains responsible for approval and posting.
6. Expandable boundaries: future modules integrate through explicit contracts and domain events, not direct table access.

## Assumptions

- The first deployment is for one legal entity, with architecture able to support multiple organizations/tenants.
- A fiscal year contains configurable periods; the default calendar can be configureable.
- Each journal has a transaction date, posting date, voucher number, currency, exchange-rate policy, description, and balanced lines.
- The initial accounting currency is one base currency per organization. Multi-currency is a design requirement but its exact scope is an approval item.
- Users, roles, and permissions are centrally managed, while authorization is evaluated within organization and module scope.
- Bank reconciliation begins with imported or manually entered bank statement lines; bank-feed integrations are future work.
- Statement configurations are versioned/effective-dated so published reports remain reproducible.

## Decisions requiring approval

1. Legal-entity and tenant model: single organization first with a tenant-ready schema, or true multi-entity from day one.
2. Approval workflow: direct posting for authorized users, or draft → approval → posting as a mandatory workflow.
3. Multi-currency depth: base-currency only initially, or transaction currency plus realized/unrealized FX from the first GL release.
4. Period calendar: calendar year only, or configurable fiscal-year start and non-calendar periods.
5. Deployment and identity: managed cloud identity/database, or self-hosted identity and SQL Server for the current phase. PostgreSQL is a later migration option.
6. Bank statement ingestion formats and providers.
7. AI scope and data policy: permitted use cases, provider, retention, redaction, and whether customer data may leave the deployment boundary.

## Alternatives considered

- Microservices now: rejected for the first phase because it adds deployment, consistency, and reporting complexity before module boundaries are proven.
- Event sourcing for all accounting: rejected as the default because conventional relational journal lines plus immutable posting/audit records are easier to report, operate, and reconcile. Domain events remain available for integration.
- Hardcoded financial statements: rejected because account structures differ by organization and must be configurable.
- Floating-point monetary values: rejected because they can create rounding and reconciliation errors.

## Risks

- Ambiguous posting/approval semantics can cause rework; resolve before GL implementation.
- Multi-entity and multi-currency requirements can alter every aggregate and report; preserve extension points now and decide scope explicitly.
- Configuration without governance can make reports inconsistent; use versioning, validation, effective dates, and audit trails.
- AI suggestions can create control risk; keep AI outside the posting authority and require explicit user confirmation.
