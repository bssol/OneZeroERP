using System.Data;
using Microsoft.EntityFrameworkCore;
using OneZeroErp.Application;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Application.Time;
using OneZeroErp.Domain;
using OneZeroErp.Domain.GeneralLedger;
using OneZeroErp.Domain.GeneralLedger.Setup;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.Infrastructure.GeneralLedger;

public sealed class VoucherDraftService(IDbContextFactory<ErpDbContext> factory, IClock clock) : IVoucherDraftService
{
    public async Task<VoucherDraftOptions> GetOptionsAsync(Guid companyId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await RequirePermissionAsync(db, companyId, actorUserId, "CanView", cancellationToken);
        var types = await db.VoucherTypes.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive).OrderBy(x => x.Code)
            .Select(x => new VoucherDraftOption(x.Id, x.Code + " · " + x.Description, null, 2,
                x.Code == "CPV" || x.Code == "BPV" ? VoucherEntryMode.FastPayment :
                x.Code == "CRV" || x.Code == "BRV" ? VoucherEntryMode.FastReceipt : VoucherEntryMode.Standard,
                x.RequiresBankAccount, x.RequiresCashAccount)).ToListAsync(cancellationToken);
        var years = await db.FiscalYears.AsNoTracking().Where(x => x.CompanyId == companyId && x.Status == FiscalYearStatus.Open).OrderByDescending(x => x.StartDate)
            .Select(x => new VoucherDraftOption(x.Id, x.Code)).ToListAsync(cancellationToken);
        var currencies = await db.Currencies.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive && x.IsBaseCurrency).OrderBy(x => x.Code)
            .Select(x => new VoucherDraftOption(x.Id, x.Code, null, x.DecimalPlaces)).ToListAsync(cancellationToken);
        var accounts = await db.ChartOfAccounts.AsNoTracking().Where(x => x.IsActive && x.IsPostingAccount).OrderBy(x => x.AccountNo)
            .Select(x => new GlPostingAccountOption(x.Id, x.AccountNo, x.Title)).ToListAsync(cancellationToken);
        var banks = await db.BankAccounts.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive).OrderBy(x => x.Code)
            .Select(x => new VoucherDraftOption(x.Id, x.Code + " · " + x.Name, x.GlAccountId)).ToListAsync(cancellationToken);
        var cash = await db.CashAccounts.AsNoTracking().Where(x => x.CompanyId == companyId && x.IsActive).OrderBy(x => x.Code)
            .Select(x => new VoucherDraftOption(x.Id, x.Code + " · " + x.Name, x.GlAccountId)).ToListAsync(cancellationToken);
        return new(types, years, currencies, accounts, banks, cash);
    }

    public async Task<PagedResult<VoucherDraftListItem>> GetPageAsync(Guid companyId, Guid actorUserId, PagedQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await RequirePermissionAsync(db, companyId, actorUserId, "CanView", cancellationToken);
        var source = db.Journals.AsNoTracking().Where(x => x.CompanyId == companyId && x.Status == "Draft");
        var count = await source.CountAsync(cancellationToken);
        var page = Math.Max(1, query.PageNumber);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var rows = await source.OrderByDescending(x => x.UpdatedAtUtc).ThenByDescending(x => x.Number)
            .Skip((page - 1) * size).Take(size)
            .Select(x => new VoucherDraftListItem(x.Id, x.Number, x.TransactionDate,
                db.VoucherTypes.Where(t => t.Id == x.VoucherTypeId).Select(t => t.Code).First(), x.Description,
                db.Currencies.Where(c => c.Id == x.CurrencyId).Select(c => c.Code).First(),
                x.Lines.Sum(l => l.Debit), x.Lines.Sum(l => l.Credit), x.Lines.Count, x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        return new(rows, page, size, count);
    }

    public async Task<VoucherDraftEditModel?> GetAsync(Guid companyId, Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await RequirePermissionAsync(db, companyId, actorUserId, "CanView", cancellationToken);
        var journal = await db.Journals.AsNoTracking().Include(x => x.Lines)
            .SingleOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.Status == "Draft", cancellationToken);
        if (journal is null) return null;
        return new VoucherDraftEditModel
        {
            Id = journal.Id,
            CompanyId = companyId,
            VoucherTypeId = journal.VoucherTypeId,
            FiscalYearId = journal.FiscalYearId,
            CurrencyId = journal.CurrencyId,
            TransactionDate = journal.TransactionDate,
            Description = journal.Description,
            Mode = VoucherEntryMode.Standard,
            Lines = journal.Lines.OrderBy(x => x.LineNumber).Select(x => new VoucherDraftLineModel
            { AccountId = x.AccountId, Narration = x.Narration, Debit = x.Debit, Credit = x.Credit }).ToList()
        };
    }

    public async Task<VoucherDraftOperationResult> SaveAsync(VoucherDraftEditModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (model.CompanyId == Guid.Empty || model.VoucherTypeId == Guid.Empty || model.FiscalYearId == Guid.Empty || model.CurrencyId == Guid.Empty)
            return new(false, "Select a voucher type, fiscal year and currency.");
        if (model.Description?.Length > 500 || model.Lines.Count > 500)
            return new(false, "Voucher text or line count exceeds the allowed limit.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await RequirePermissionAsync(db, model.CompanyId, actorUserId, model.Id.HasValue ? "CanEdit" : "CanAdd", cancellationToken);
        var type = await db.VoucherTypes.SingleOrDefaultAsync(x => x.Id == model.VoucherTypeId && x.CompanyId == model.CompanyId && x.IsActive, cancellationToken);
        if (type is null) return new(false, "Select an active voucher type.");
        var year = await db.FiscalYears.SingleOrDefaultAsync(x => x.Id == model.FiscalYearId && x.CompanyId == model.CompanyId, cancellationToken);
        if (year is null || year.Status != FiscalYearStatus.Open || model.TransactionDate < year.StartDate || model.TransactionDate > year.EndDate)
            return new(false, "The voucher date must be within an open fiscal year.");
        var day = await db.LockDates.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId && x.FiscalYearId == year.Id && x.Date == model.TransactionDate, cancellationToken);
        if (day is null || day.IsLocked) return new(false, "The voucher date is locked or has not been initialized.");
        var currency = await db.Currencies.SingleOrDefaultAsync(x => x.Id == model.CurrencyId && x.CompanyId == model.CompanyId && x.IsActive && x.IsBaseCurrency, cancellationToken);
        if (currency is null || currency.DecimalPlaces is < 0 or > 4) return new(false, "Select an active base currency with up to four decimal places.");

        IReadOnlyList<VoucherDraftLineModel> lines;
        try { lines = VoucherDraftComposer.Compose(model); }
        catch (ArgumentException ex) { return new(false, ex.Message); }
        if (lines.Count == 0 || lines.Count > 500) return new(false, "Add between one and 500 voucher lines.");
        if (model.Mode != VoucherEntryMode.Standard)
        {
            var expected = type.Code is "CPV" or "BPV" ? VoucherEntryMode.FastPayment :
                type.Code is "CRV" or "BRV" ? VoucherEntryMode.FastReceipt : VoucherEntryMode.Standard;
            if (expected != model.Mode || !(type.RequiresBankAccount || type.RequiresCashAccount))
                return new(false, "Fast entry is available only for matching payment or receipt voucher types.");
        }

        var accountIds = lines.Select(x => x.AccountId).Distinct().ToArray();
        var validAccounts = await db.ChartOfAccounts.Where(x => accountIds.Contains(x.Id) && x.IsActive && x.IsPostingAccount)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        if (accountIds.Length != validAccounts.Count) return new(false, "Select active posting accounts for every line.");
        var bankIds = await db.BankAccounts.Where(x => x.CompanyId == model.CompanyId && x.IsActive).Select(x => x.GlAccountId).ToListAsync(cancellationToken);
        var cashIds = await db.CashAccounts.Where(x => x.CompanyId == model.CompanyId && x.IsActive).Select(x => x.GlAccountId).ToListAsync(cancellationToken);
        if (type.RequiresBankAccount && !lines.Any(x => bankIds.Contains(x.AccountId)) ||
            type.RequiresCashAccount && !lines.Any(x => cashIds.Contains(x.AccountId)))
            return new(false, "Include an active bank or cash account required by the voucher type.");
        if (model.Mode != VoucherEntryMode.Standard &&
            (type.RequiresBankAccount && !bankIds.Contains(model.SourceAccountId) || type.RequiresCashAccount && !cashIds.Contains(model.SourceAccountId)))
            return new(false, "The selected source account is not an active bank or cash account.");

        var now = clock.UtcNow;
        var domain = Gl_Journal.Create(model.CompanyId, actorUserId, now, type.Id, "DRAFT", model.TransactionDate, year.Id, model.Description);
        foreach (var line in lines)
        {
            if (line.Narration?.Length > 500 || line.Debit > 999999999999999.9999m || line.Credit > 999999999999999.9999m ||
                decimal.Round(line.Debit, currency.DecimalPlaces) != line.Debit || decimal.Round(line.Credit, currency.DecimalPlaces) != line.Credit)
                return new(false, "Line amounts or narration exceed the selected currency precision or storage limit.");
            try { domain.AddLine(actorUserId, now, line.AccountId, line.Narration, line.Debit, line.Credit); }
            catch (DomainRuleException ex) { return new(false, ex.Message); }
        }

        GL_JournalEntity entity;
        if (model.Id.HasValue)
        {
            entity = await db.Journals.SingleOrDefaultAsync(x => x.Id == model.Id.Value && x.CompanyId == model.CompanyId, cancellationToken)
                ?? throw new InvalidOperationException("The voucher no longer exists.");
            if (entity.Status != "Draft") return new(false, "Only draft vouchers can be edited.");
            if (entity.VoucherTypeId != type.Id || entity.FiscalYearId != year.Id)
                return new(false, "Voucher type and fiscal year cannot change after a draft number is assigned.");
            if (entity.TransactionDate != model.TransactionDate)
            {
                var originalDay = await db.LockDates.SingleOrDefaultAsync(x => x.CompanyId == model.CompanyId &&
                    x.FiscalYearId == year.Id && x.Date == entity.TransactionDate, cancellationToken);
                if (originalDay is null || originalDay.IsLocked)
                    return new(false, "The original voucher date is locked; its draft cannot be moved.");
            }
            await db.JournalLines.Where(x => x.JournalId == entity.Id).ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var numberLock = $"voucher-number:{model.CompanyId:N}:{type.Id:N}:{year.Id:N}";
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @lockResult int;
                EXEC @lockResult = sys.sp_getapplock @Resource = {numberLock}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @lockResult < 0 THROW 51000, 'Unable to reserve a voucher number. Please retry.', 1;
                """, cancellationToken);
            var next = (await db.Journals.Where(x => x.CompanyId == model.CompanyId && x.VoucherTypeId == type.Id && x.FiscalYearId == year.Id)
                .MaxAsync(x => (int?)x.SequenceNumber, cancellationToken) ?? 0) + 1;
            entity = new GL_JournalEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = model.CompanyId,
                SequenceNumber = next,
                Number = $"{type.Code}-{year.Code}-{next:000000}",
                CreatedAtUtc = now,
                CreatedByUserId = actorUserId
            };
            db.Journals.Add(entity);
        }
        entity.VoucherTypeId = type.Id;
        entity.FiscalYearId = year.Id;
        entity.CurrencyId = currency.Id;
        entity.TransactionDate = model.TransactionDate;
        entity.Description = model.Description?.Trim() ?? string.Empty;
        entity.UpdatedAtUtc = now;
        entity.UpdatedByUserId = actorUserId;
        var lineNumber = 0;
        foreach (var line in lines)
            db.JournalLines.Add(new GL_JournalLineEntity
            {
                Id = Guid.NewGuid(),
                JournalId = entity.Id,
                LineNumber = ++lineNumber,
                AccountId = line.AccountId,
                Narration = line.Narration?.Trim() ?? string.Empty,
                Debit = line.Debit,
                Credit = line.Credit
            });
        db.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = model.CompanyId,
            ActorUserId = actorUserId,
            EntityId = entity.Id,
            EntityType = "Journal",
            Action = model.Id.HasValue ? "DraftUpdated" : "DraftCreated",
            OccurredAtUtc = now,
            AfterSummary = $"Number={entity.Number};Date={entity.TransactionDate:O};Lines={lineNumber};Debit={domain.TotalDebit};Credit={domain.TotalCredit}"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, Id: entity.Id, Number: entity.Number);
    }

    private static async Task RequirePermissionAsync(ErpDbContext db, Guid companyId, Guid actorUserId, string action, CancellationToken cancellationToken)
    {
        var allowed = await db.AppUsers.AnyAsync(x => x.Id == actorUserId && x.CompanyId == companyId && x.IsActive &&
            x.Permissions.Any(p => p.Permission == "gl.vouchers:" + action || p.Permission == "*:" + action), cancellationToken);
        if (!allowed) throw new UnauthorizedAccessException("Voucher permission is required.");
    }
}
