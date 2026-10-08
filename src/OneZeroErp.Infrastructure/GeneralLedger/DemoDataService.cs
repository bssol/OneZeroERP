using System.Data;
using Microsoft.EntityFrameworkCore;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.Domain.GeneralLedger.Setup;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class DemoDataService(IDbContextFactory<ErpDbContext> factory, IClock clock) : IDemoDataService
{
    private const string Marker = "[DEMO]";
    private static readonly string[] DemoAccountNumbers =
    [
        "DEMO-1000", "DEMO-1100", "DEMO-1200", "DEMO-1300",
        "DEMO-2000", "DEMO-2100", "DEMO-3000", "DEMO-3100",
        "DEMO-4000", "DEMO-4100", "DEMO-5000", "DEMO-5100"
    ];
    private static readonly string[] VoucherTypeCodes = ["JV", "BPV", "BRV", "CPV", "CRV"];
    private static readonly string[] DraftDescriptions =
    [
        "[DEMO] Opening capital deposited in bank",
        "[DEMO] Office supplies paid from cash",
        "[DEMO] Customer receipt collected in cash"
    ];

    public async Task<DemoDataStatus> GetStatusAsync(Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await RequireAdministratorAsync(db, companyId, actorUserId, cancellationToken);
        return await ReadStatusAsync(db, companyId, cancellationToken);
    }

    public async Task<DemoDataSeedResult> SeedAsync(Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await RequireAdministratorAsync(db, companyId, actorUserId, cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock @Resource = {"demo-data:" + companyId.ToString("N")},
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @lockResult < 0 THROW 51000, 'Unable to acquire the demo-data seed lock. Please retry.', 1;
            """, cancellationToken);

        var now = clock.UtcNow;
        var today = clock.CurrentDate;
        var created = 0;

        var year = await db.FiscalYears.SingleOrDefaultAsync(x => x.CompanyId == companyId &&
            x.StartDate <= today && today <= x.EndDate, cancellationToken);
        if (year is not null && year.Status != FiscalYearStatus.Open)
            return new(false, "The fiscal year containing today is closed. Open it before creating demo data.",
                await ReadStatusAsync(db, companyId, cancellationToken));
        if (year is null)
        {
            var start = new DateOnly(today.Year, 1, 1);
            var end = new DateOnly(today.Year, 12, 31);
            if (await db.FiscalYears.AnyAsync(x => x.CompanyId == companyId && start <= x.EndDate && x.StartDate <= end, cancellationToken))
                return new(false, "An existing fiscal year partially overlaps the demo calendar. Complete that fiscal-year setup first.",
                    await ReadStatusAsync(db, companyId, cancellationToken));
            year = new Erp_SetupFiscalYearEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = $"DEMO-FY{today.Year}",
                StartDate = start,
                EndDate = end,
                Status = FiscalYearStatus.Open,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.FiscalYears.Add(year);
            created++;
        }

        var existingDates = await db.LockDates.Where(x => x.CompanyId == companyId && x.FiscalYearId == year.Id)
            .Select(x => x.Date).ToHashSetAsync(cancellationToken);
        for (var date = year.StartDate; date <= year.EndDate; date = date.AddDays(1))
        {
            if (existingDates.Contains(date)) continue;
            db.LockDates.Add(new Erp_LockDateEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                FiscalYearId = year.Id,
                Date = date,
                IsLocked = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            created++;
        }

        var accounts = await db.ChartOfAccounts.Where(x => DemoAccountNumbers.Contains(x.AccountNo))
            .ToDictionaryAsync(x => x.AccountNo, cancellationToken);
        foreach (var definition in AccountDefinitions())
        {
            if (accounts.TryGetValue(definition.Number, out var existing))
            {
                if (existing.AccountType != definition.Type || existing.IsPostingAccount != definition.IsPosting)
                    return new(false, $"Account {definition.Number} already exists with incompatible demo settings.",
                        await ReadStatusAsync(db, companyId, cancellationToken));
                continue;
            }
            var account = new GL_ChartOfAccountEntity
            {
                Id = Guid.NewGuid(),
                AccountNo = definition.Number,
                ParentAccountNo = definition.Parent,
                ParentAccountTitle = definition.Parent is null ? null : accounts[definition.Parent].Title,
                AccountLevel = definition.Parent is null ? 1 : 2,
                AccountType = definition.Type,
                Title = definition.Title,
                Description = "Walkthrough account created by Demo Data.",
                IsPostingAccount = definition.IsPosting,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.ChartOfAccounts.Add(account);
            accounts[definition.Number] = account;
            created++;
        }

        var currency = await db.Currencies.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.IsBaseCurrency, cancellationToken);
        if (currency is null)
        {
            currency = await db.Currencies.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Code == "PKR", cancellationToken);
            if (currency is not null)
                return new(false, "PKR already exists but is not the base currency. Complete currency setup before creating demo data.",
                    await ReadStatusAsync(db, companyId, cancellationToken));
            currency = new GL_CurrencyEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = "PKR",
                Name = "Pakistan Rupee",
                Symbol = "Rs",
                DecimalPlaces = 2,
                IsBaseCurrency = true,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Currencies.Add(currency);
            created++;
        }
        if (!currency.IsActive || currency.DecimalPlaces is < 0 or > 4)
            return new(false, "The existing base currency must be active and use up to four decimal places.",
                await ReadStatusAsync(db, companyId, cancellationToken));

        var foreignCode = currency.Code == "USD" ? "PKR" : "USD";
        var foreign = await db.Currencies.SingleOrDefaultAsync(x => x.CompanyId == companyId && x.Code == foreignCode, cancellationToken);
        if (foreign is null)
        {
            foreign = new GL_CurrencyEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = foreignCode,
                Name = foreignCode == "USD" ? "US Dollar" : "Pakistan Rupee",
                Symbol = foreignCode == "USD" ? "$" : "Rs",
                DecimalPlaces = 2,
                IsBaseCurrency = false,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.Currencies.Add(foreign);
            created++;
        }
        if (foreign.IsActive && !await db.ExchangeRates.AnyAsync(x => x.CompanyId == companyId && x.CurrencyId == foreign.Id && x.EffectiveDate == year.StartDate, cancellationToken))
        {
            db.ExchangeRates.Add(new GL_ExchangeRateEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                CurrencyId = foreign.Id,
                EffectiveDate = year.StartDate,
                RateToBase = 278.50m,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            created++;
        }

        var voucherTypes = await db.VoucherTypes.Where(x => x.CompanyId == companyId && VoucherTypeCodes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, cancellationToken);
        foreach (var definition in VoucherTypeDefinitions())
        {
            if (voucherTypes.ContainsKey(definition.Code)) continue;
            var type = new GL_VoucherTypeEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = definition.Code,
                Description = definition.Description,
                RequiresBankAccount = definition.RequiresBank,
                RequiresCashAccount = definition.RequiresCash,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            db.VoucherTypes.Add(type);
            voucherTypes[type.Code] = type;
            created++;
        }

        if (!await db.BankAccounts.AnyAsync(x => x.CompanyId == companyId && x.Code == "DEMO-BANK", cancellationToken))
        {
            db.BankAccounts.Add(new GL_BankAccountEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                GlAccountId = accounts["DEMO-1100"].Id,
                Code = "DEMO-BANK",
                Name = "Demo Operating Account",
                BankName = "OneZero Demo Bank",
                AccountNumber = "DEMO-0001",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            created++;
        }
        if (!await db.CashAccounts.AnyAsync(x => x.CompanyId == companyId && x.Code == "DEMO-CASH", cancellationToken))
        {
            db.CashAccounts.Add(new GL_CashAccountEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                GlAccountId = accounts["DEMO-1200"].Id,
                Code = "DEMO-CASH",
                Name = "Demo Main Cash",
                Location = "Head Office",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            created++;
        }
        if (!await db.TaxConfigurations.AnyAsync(x => x.CompanyId == companyId && x.Code == "DEMO-GST18", cancellationToken))
        {
            db.TaxConfigurations.Add(new GL_TaxConfigurationEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                Code = "DEMO-GST18",
                Name = "Demo sales tax 18%",
                TaxType = GL_TaxType.Sales,
                RatePercent = 18m,
                EffectiveFrom = year.StartDate,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            created++;
        }

        var existingDrafts = await db.Journals.Where(x => x.CompanyId == companyId && DraftDescriptions.Contains(x.Description))
            .Select(x => x.Description).ToHashSetAsync(cancellationToken);
        var sequences = new Dictionary<Guid, int>();
        foreach (var draft in DraftDefinitions(accounts))
        {
            if (existingDrafts.Contains(draft.Description)) continue;
            var type = voucherTypes[draft.VoucherType];
            if (!type.IsActive) continue;
            if (!sequences.TryGetValue(type.Id, out var sequence))
                sequence = await db.Journals.Where(x => x.CompanyId == companyId && x.VoucherTypeId == type.Id && x.FiscalYearId == year.Id)
                    .MaxAsync(x => (int?)x.SequenceNumber, cancellationToken) ?? 0;
            sequence++;
            sequences[type.Id] = sequence;
            var journal = new GL_JournalEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                VoucherTypeId = type.Id,
                FiscalYearId = year.Id,
                CurrencyId = currency.Id,
                SequenceNumber = sequence,
                Number = $"{type.Code}-{year.Code}-{sequence:000000}",
                TransactionDate = today,
                Description = draft.Description,
                Status = "Draft",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            db.Journals.Add(journal);
            var lineNumber = 0;
            foreach (var line in draft.Lines)
                db.JournalLines.Add(new GL_JournalLineEntity
                {
                    Id = Guid.NewGuid(),
                    JournalId = journal.Id,
                    LineNumber = ++lineNumber,
                    AccountId = line.Account.Id,
                    Narration = line.Narration,
                    Debit = line.Debit,
                    Credit = line.Credit
                });
            db.AuditEvents.Add(new AuditEventEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ActorUserId = actorUserId,
                EntityId = journal.Id,
                EntityType = "Journal",
                Action = "DemoDraftCreated",
                OccurredAtUtc = now,
                AfterSummary = $"Number={journal.Number};Date={today:O};Lines={lineNumber};Debit={draft.Lines.Sum(x => x.Debit)};Credit={draft.Lines.Sum(x => x.Credit)}"
            });
            created++;
        }

        if (created > 0)
            db.AuditEvents.Add(new AuditEventEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                ActorUserId = actorUserId,
                EntityId = companyId,
                EntityType = "DemoData",
                Action = "Seeded",
                OccurredAtUtc = now,
                AfterSummary = $"CreatedRecords={created};PostedJournals=0"
            });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var status = await GetStatusAsync(companyId, actorUserId, cancellationToken);
        return new(true, created == 0 ? "The walkthrough dataset is already complete." :
            $"Walkthrough data is ready. {created} missing records were created; all sample vouchers remain drafts.", status);
    }

    private static async Task RequireAdministratorAsync(ErpDbContext db, Guid companyId, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty || actorUserId == Guid.Empty ||
            !await db.AppUsers.AnyAsync(x => x.Id == actorUserId && x.CompanyId == companyId && x.IsActive &&
                x.Permissions.Any(p => p.Permission == "identity.users:CanEdit" || p.Permission == "*:CanEdit"), cancellationToken))
            throw new UnauthorizedAccessException("Administrator permission is required to manage demo data.");
    }

    private async Task<DemoDataStatus> ReadStatusAsync(ErpDbContext db, Guid companyId, CancellationToken cancellationToken)
    {
        var today = clock.CurrentDate;
        var fiscalYears = await db.FiscalYears.CountAsync(x => x.CompanyId == companyId && x.Status == FiscalYearStatus.Open && x.StartDate <= today && today <= x.EndDate, cancellationToken);
        var accounts = await db.ChartOfAccounts.CountAsync(x => DemoAccountNumbers.Contains(x.AccountNo), cancellationToken);
        var types = await db.VoucherTypes.CountAsync(x => x.CompanyId == companyId && VoucherTypeCodes.Contains(x.Code), cancellationToken);
        var baseCurrencyCode = await db.Currencies.Where(x => x.CompanyId == companyId && x.IsBaseCurrency)
            .Select(x => x.Code).SingleOrDefaultAsync(cancellationToken);
        var foreignCode = baseCurrencyCode == "USD" ? "PKR" : "USD";
        var currencies = await db.Currencies.CountAsync(x => x.CompanyId == companyId && (x.IsBaseCurrency || x.Code == foreignCode), cancellationToken);
        var banks = await db.BankAccounts.CountAsync(x => x.CompanyId == companyId && x.Code == "DEMO-BANK", cancellationToken);
        var cash = await db.CashAccounts.CountAsync(x => x.CompanyId == companyId && x.Code == "DEMO-CASH", cancellationToken);
        var taxes = await db.TaxConfigurations.CountAsync(x => x.CompanyId == companyId && x.Code == "DEMO-GST18", cancellationToken);
        var drafts = await db.Journals.CountAsync(x => x.CompanyId == companyId && DraftDescriptions.Contains(x.Description) && x.Status == "Draft", cancellationToken);
        return new(fiscalYears > 0 && accounts == DemoAccountNumbers.Length && types == VoucherTypeCodes.Length &&
            currencies >= 2 && banks == 1 && cash == 1 && taxes == 1 && drafts == DraftDescriptions.Length,
            fiscalYears, accounts, types, currencies, banks, cash, taxes, drafts);
    }

    private static IEnumerable<(string Number, string? Parent, AccountType Type, string Title, bool IsPosting)> AccountDefinitions()
    {
        yield return ("DEMO-1000", null, AccountType.Asset, "Demo Assets", false);
        yield return ("DEMO-1100", "DEMO-1000", AccountType.Asset, "Demo Bank", true);
        yield return ("DEMO-1200", "DEMO-1000", AccountType.Asset, "Demo Cash on Hand", true);
        yield return ("DEMO-1300", "DEMO-1000", AccountType.Asset, "Demo Accounts Receivable", true);
        yield return ("DEMO-2000", null, AccountType.Liability, "Demo Liabilities", false);
        yield return ("DEMO-2100", "DEMO-2000", AccountType.Liability, "Demo Accounts Payable", true);
        yield return ("DEMO-3000", null, AccountType.Equity, "Demo Equity", false);
        yield return ("DEMO-3100", "DEMO-3000", AccountType.Equity, "Demo Owner Capital", true);
        yield return ("DEMO-4000", null, AccountType.Revenue, "Demo Revenue", false);
        yield return ("DEMO-4100", "DEMO-4000", AccountType.Revenue, "Demo Sales Revenue", true);
        yield return ("DEMO-5000", null, AccountType.Expense, "Demo Expenses", false);
        yield return ("DEMO-5100", "DEMO-5000", AccountType.Expense, "Demo Office Expense", true);
    }

    private static IEnumerable<(string Code, string Description, bool RequiresBank, bool RequiresCash)> VoucherTypeDefinitions()
    {
        yield return ("JV", "Journal voucher", false, false);
        yield return ("BPV", "Bank payment voucher", true, false);
        yield return ("BRV", "Bank receipt voucher", true, false);
        yield return ("CPV", "Cash payment voucher", false, true);
        yield return ("CRV", "Cash receipt voucher", false, true);
    }

    private static IEnumerable<(string VoucherType, string Description, (GL_ChartOfAccountEntity Account, string Narration, decimal Debit, decimal Credit)[] Lines)> DraftDefinitions(
        IReadOnlyDictionary<string, GL_ChartOfAccountEntity> accounts)
    {
        yield return ("JV", DraftDescriptions[0],
        [
            (accounts["DEMO-1100"], "Capital deposited", 100000m, 0m),
            (accounts["DEMO-3100"], "Owner capital", 0m, 100000m)
        ]);
        yield return ("CPV", DraftDescriptions[1],
        [
            (accounts["DEMO-5100"], "Office supplies", 2500m, 0m),
            (accounts["DEMO-1200"], "Cash payment", 0m, 2500m)
        ]);
        yield return ("CRV", DraftDescriptions[2],
        [
            (accounts["DEMO-1200"], "Cash received", 15000m, 0m),
            (accounts["DEMO-1300"], "Customer account", 0m, 15000m)
        ]);
    }
}
