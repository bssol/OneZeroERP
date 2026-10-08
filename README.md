# OneZero ERP

.NET 10 modular monolith with a Blazor Server PWA, HTTP API, and SQL Server/EF Core.
Bootstrap acceptance is tracked in [BOOTSTRAP-EVIDENCE](docs/implementation/BOOTSTRAP-EVIDENCE.md).
Voucher draft creation is available at `/gl/vouchers`; posting remains a separate
roadmap gate. See [ADR 0002](docs/decisions/0002-voucher-draft-entry.md).

## Local prerequisites

- .NET SDK selected by `global.json` (10.0.401 or a later patch in that feature band).
- SQL Server LocalDB and PowerShell 7 on Windows. Tests allocate their own databases.
- Edge for browser tests on Windows; on other platforms install the matching
  Playwright Chromium build and use an isolated SQL Server accessible to the tests.

## Start a new development installation

Use environment variables; do not put credentials in JSON, scripts, or Git:

```powershell
sqllocaldb start MSSQLLocalDB
dotnet dev-certs https --check --trust
$env:ConnectionStrings__Default = 'Server=(localdb)\MSSQLLocalDB;Database=OneZeroErp;Integrated Security=true;TrustServerCertificate=true'
$env:Company__DefaultCompanyId = [guid]::NewGuid().ToString()
$env:DevelopmentSeed__AdminPassword = Read-Host 'New development admin password (12–256 characters)' -MaskInput
$env:Authentication__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet restore OneZeroErp.slnx
dotnet run --project src/OneZeroErp.Web --launch-profile https
```

Web listens on `https://localhost:7081` and `http://localhost:5219`. Sign in as
`admin`. Development startup applies migrations and only seeds users whose
passwords are provided. Other optional seed settings are
`DevelopmentSeed__FiscalManagerPassword`, `DevelopmentSeed__AccountantPassword`,
and `DevelopmentSeed__ViewerPassword`. Do not reuse these accounts in production.

The Web development profile also selects the generic `Manufacturing` chart of
accounts seed. It adds the audited manufacturing hierarchy only when the chart is
empty and never changes or supplements an existing chart. Clear
`DevelopmentSeed__ChartOfAccountsProfile` to disable it, or set it to
`Manufacturing` when starting another host against a new development database.

For an existing installation, retain its company ID and connection string.
Seeding does **not** overwrite existing passwords. Existing development users
receive newly added permissions on startup even when no seed password is set.
The bootstrap migration adds
company membership; configured seed users receive the configured company if their
membership is empty. Other existing users need an explicitly reviewed membership
backfill before signing in. An empty membership fails closed.

To recover an existing local `admin` account after its stored hash was manually
changed, use the Development-only command below. First set the connection string
to the **existing application database** and set its existing company ID; do not
point it at a fresh test instance. Run in an interactive PowerShell terminal:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__Default = '<connection string for the existing OneZeroErp database>'
$env:Company__DefaultCompanyId = '<existing company GUID>'
dotnet run --project src/OneZeroErp.Web --no-launch-profile -- reset-admin-password
```

The command prompts twice without echo. Use 12–256 characters. It changes only
the active `admin` account belonging to that company, hashes the new password,
clears its lockout, revokes its sessions/reset grants and writes an audit event.
Sign in again afterward. `DevelopmentSeed__AdminPassword` does not reset an
existing admin password. If the configured SQL instance does not start, repair or
restore **that instance** first; a new LocalDB instance has a different database.

To replace the passwords of all four existing development users together, set
`DevelopmentSeed__AdminPassword`, `DevelopmentSeed__FiscalManagerPassword`,
`DevelopmentSeed__AccountantPassword`, and `DevelopmentSeed__ViewerPassword` as
temporary environment variables, then run:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/OneZeroErp.Web --no-launch-profile -- sync-development-passwords
```

Use the existing database connection string and company ID. The command verifies
that all four users are active with the expected roles before changing any of
them. It binds empty company memberships, hashes the new passwords, clears
lockouts, revokes sessions and reset grants, and writes security audit records
in one transaction. Remove the temporary environment variables afterward.

Voucher drafts use only the active base currency for now. Standard entry allows
unbalanced drafts, and fast entry supports the seeded cash/bank payment and receipt
types. Saving validates the fiscal year and day lock. Neither mode posts journals
or changes reports.

Administrators can open `/admin/demo-data` from the **Demo data** item in the
Workspace navigation. After explicit confirmation, the page completes an
idempotent GL walkthrough pack with a fiscal calendar, demo chart, currencies,
voucher types, bank/cash links, tax setup, and three balanced draft vouchers.
It never posts journals or replaces matching business setup. There is no bulk
reset because other records may later reference the walkthrough data.

Run the API separately with `dotnet run --project src/OneZeroErp.Api`. Supply the
same environment configuration when sharing the development database. Without a
configured development signing key each host generates an ephemeral key; restarting
invalidates its JWTs. Production requires an externally managed key and persistent,
protected ASP.NET Core Data Protection keys.

## Validate

```powershell
dotnet restore OneZeroErp.slnx
dotnet build OneZeroErp.slnx --no-restore --warnaserror
dotnet test OneZeroErp.slnx --no-build --no-restore
./scripts/check-format.ps1
./scripts/check-secrets.ps1
dotnet list OneZeroErp.slnx package --vulnerable --include-transitive
```

Tests use synthetic users and `OneZeroErp_Test_<guid>` databases, then remove only
the databases they created. They never use `ConnectionStrings:Default` from the
application. `ONEZERO_TEST_SQL` can select a dedicated test server with database
creation permissions. The backup/restore test assumes SQL Server can access the
test runner's temporary directory (as LocalDB does). `ONEZERO_BROWSER_CHANNEL`
can select another installed Chromium browser.

The CI workflow performs dependency and secret scanning, formatting of changed C#
files, warning-free build, unit/architecture checks, SQL migration/recovery checks,
HTTP authorization checks, and headless browser tests. Local formatting also
includes existing uncommitted C# edits; do not overwrite somebody else's changes
to resolve an unrelated formatting failure.

## Security and operations

See [the bootstrap runbook](docs/implementation/BOOTSTRAP-RUNBOOK.md) for session
and reset behavior, migration recovery, outbox delivery, PWA updates, and secrets.
