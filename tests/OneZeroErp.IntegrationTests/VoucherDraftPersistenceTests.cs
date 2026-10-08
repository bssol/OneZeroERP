using Microsoft.EntityFrameworkCore;
using OneZeroErp.Application.GeneralLedger;
using OneZeroErp.Domain.GeneralLedger.Setup;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.GeneralLedger;
using OneZeroErp.Infrastructure.Identity;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.IntegrationTests;

public sealed class VoucherDraftPersistenceTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task Standard_draft_is_numbered_audited_and_does_not_require_balance()
    {
        var setup = await SetupAsync();
        var service = new VoucherDraftService(fixture, fixture.Clock);
        var options = await service.GetOptionsAsync(fixture.CompanyId, setup.UserId);
        Assert.Contains(options.VoucherTypes, x => x.Id == setup.TypeId);
        var model = Model(setup);
        model.Lines = [new() { AccountId = setup.ExpenseId, Debit = 25m }, new() { AccountId = setup.BankGlId, Credit = 20m }];
        var result = await service.SaveAsync(model, setup.UserId);
        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.StartsWith("BPV-FY", result.Number);
        await using var db = fixture.CreateDbContext();
        var journal = await db.Journals.Include(x => x.Lines).SingleAsync(x => x.Id == result.Id);
        Assert.Equal("Draft", journal.Status);
        Assert.Equal(25m, journal.Lines.Sum(x => x.Debit));
        Assert.Equal(20m, journal.Lines.Sum(x => x.Credit));
        var audit = await db.AuditEvents.SingleAsync(x => x.EntityId == journal.Id && x.Action == "DraftCreated");
        Assert.True(await db.OutboxMessages.AnyAsync(x => x.AuditEventId == audit.Id));
        Assert.Contains((await service.GetPageAsync(fixture.CompanyId, setup.UserId, new())).Items, x => x.Id == journal.Id);
        Assert.NotNull(await service.GetAsync(fixture.CompanyId, setup.UserId, journal.Id));
        var edit = (await service.GetAsync(fixture.CompanyId, setup.UserId, journal.Id))!;
        edit.Description = "Revised synthetic voucher";
        edit.Lines[1].Credit = 25m;
        Assert.True((await service.SaveAsync(edit, setup.UserId)).Succeeded);
        await using var revised = fixture.CreateDbContext();
        Assert.Equal(2, await revised.JournalLines.CountAsync(x => x.JournalId == journal.Id));
        Assert.Equal(25m, await revised.JournalLines.Where(x => x.JournalId == journal.Id).SumAsync(x => x.Credit));
        Assert.True(await revised.AuditEvents.AnyAsync(x => x.EntityId == journal.Id && x.Action == "DraftUpdated"));
    }

    [Fact]
    public async Task Fast_payment_creates_balancing_bank_line_and_rejects_locked_date()
    {
        var setup = await SetupAsync();
        var service = new VoucherDraftService(fixture, fixture.Clock);
        var model = Model(setup);
        model.Mode = VoucherEntryMode.FastPayment;
        model.SourceAccountId = setup.BankGlId;
        model.Lines = [new() { AccountId = setup.ExpenseId, Debit = 12.25m, Narration = "Expense" }];
        var result = await service.SaveAsync(model, setup.UserId);
        Assert.True(result.Succeeded, result.ErrorMessage);
        await using (var db = fixture.CreateDbContext())
        {
            var lines = await db.JournalLines.Where(x => x.JournalId == result.Id).OrderBy(x => x.LineNumber).ToListAsync();
            Assert.Equal(setup.BankGlId, lines[0].AccountId);
            Assert.Equal(12.25m, lines[0].Credit);
            Assert.Equal(12.25m, lines[1].Debit);
            var day = await db.LockDates.SingleAsync(x => x.Id == setup.DayId);
            day.IsLocked = true;
            await db.SaveChangesAsync();
        }
        model.Id = result.Id;
        var rejected = await service.SaveAsync(model, setup.UserId);
        Assert.False(rejected.Succeeded);
        Assert.Contains("locked", rejected.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        await using (var db = fixture.CreateDbContext())
        {
            db.LockDates.Add(new()
            {
                Id = Guid.NewGuid(),
                CompanyId = fixture.CompanyId,
                FiscalYearId = setup.YearId,
                Date = setup.Date.AddDays(1),
                CreatedAtUtc = fixture.Clock.UtcNow,
                UpdatedAtUtc = fixture.Clock.UtcNow
            });
            await db.SaveChangesAsync();
        }
        model.TransactionDate = setup.Date.AddDays(1);
        var moved = await service.SaveAsync(model, setup.UserId);
        Assert.False(moved.Succeeded);
        Assert.Contains("original voucher date", moved.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Voucher_commands_recheck_permission_and_currency_precision()
    {
        var setup = await SetupAsync();
        var service = new VoucherDraftService(fixture, fixture.Clock);
        var model = Model(setup);
        model.Lines = [new() { AccountId = setup.ExpenseId, Debit = 0.001m }, new() { AccountId = setup.BankGlId, Credit = 0.001m }];
        var precision = await service.SaveAsync(model, setup.UserId);
        Assert.False(precision.Succeeded);
        Assert.Contains("precision", precision.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        var unauthorized = await fixture.AddUserAsync("gl.vouchers:CanView");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(model, unauthorized.Id));
    }

    [Fact]
    public async Task Concurrent_draft_creation_assigns_distinct_sequential_numbers()
    {
        var setup = await SetupAsync();
        var service = new VoucherDraftService(fixture, fixture.Clock);
        VoucherDraftEditModel NewModel()
        {
            var model = Model(setup);
            model.Lines = [new() { AccountId = setup.ExpenseId, Debit = 1m },
                new() { AccountId = setup.BankGlId, Credit = 1m }];
            return model;
        }
        var results = await Task.WhenAll(service.SaveAsync(NewModel(), setup.UserId), service.SaveAsync(NewModel(), setup.UserId));
        Assert.All(results, x => Assert.True(x.Succeeded, x.ErrorMessage));
        Assert.Equal(2, results.Select(x => x.Number).Distinct().Count());
        Assert.Contains(results, x => x.Number!.EndsWith("000001", StringComparison.Ordinal));
        Assert.Contains(results, x => x.Number!.EndsWith("000002", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Local_admin_recovery_replaces_invalid_hash_and_revokes_sessions()
    {
        var admin = await fixture.AddUserAsync("identity.users:CanEdit");
        await using (var db = fixture.CreateDbContext())
        {
            var stored = await db.AppUsers.SingleAsync(x => x.Id == admin.Id);
            stored.UserName = "admin";
            stored.Role = "Administrator";
            await db.SaveChangesAsync();
        }
        var login = await fixture.Authentication.AuthenticateAsync(new("admin", fixture.Password));
        Assert.True(login.Succeeded);
        await using (var db = fixture.CreateDbContext())
        {
            var stored = await db.AppUsers.SingleAsync(x => x.Id == admin.Id);
            stored.PasswordHash = "temporarily-unhashed";
            stored.FailedLoginCount = 5;
            await db.SaveChangesAsync();
        }
        var freshPassword = "New-local-password-" + Guid.NewGuid().ToString("N");
        await new AdminPasswordRecovery(fixture, fixture.Clock).ResetAsync(fixture.CompanyId, freshPassword);
        await using var verify = fixture.CreateDbContext();
        var recovered = await verify.AppUsers.SingleAsync(x => x.Id == admin.Id);
        Assert.True(PasswordHasher.Verify(freshPassword, recovered.PasswordHash));
        Assert.Equal(0, recovered.FailedLoginCount);
        Assert.All(await verify.UserSessions.Where(x => x.AppUserId == admin.Id).ToListAsync(), x => Assert.NotNull(x.RevokedAtUtc));
        var audit = await verify.AuditEvents.SingleAsync(x => x.EntityId == admin.Id && x.Action == "AdminPasswordRecoveredLocally");
        Assert.True(await verify.OutboxMessages.AnyAsync(x => x.AuditEventId == audit.Id));
    }

    private async Task<Setup> SetupAsync()
    {
        var user = await fixture.AddUserAsync("gl.vouchers:CanView", "gl.vouchers:CanAdd", "gl.vouchers:CanEdit");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var date = DateOnly.FromDateTime(fixture.Clock.CurrentDateTime.DateTime);
        var year = new Erp_SetupFiscalYearEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            Code = "FY" + suffix,
            StartDate = date.AddDays(-10),
            EndDate = date.AddDays(10),
            Status = FiscalYearStatus.Open,
            CreatedAtUtc = fixture.Clock.UtcNow,
            UpdatedAtUtc = fixture.Clock.UtcNow
        };
        var day = new Erp_LockDateEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            FiscalYearId = year.Id,
            Date = date,
            CreatedAtUtc = fixture.Clock.UtcNow,
            UpdatedAtUtc = fixture.Clock.UtcNow
        };
        await using var db = fixture.CreateDbContext();
        var type = await db.VoucherTypes.SingleOrDefaultAsync(x => x.CompanyId == fixture.CompanyId && x.Code == "BPV")
            ?? new GL_VoucherTypeEntity
            {
                Id = Guid.NewGuid(),
                CompanyId = fixture.CompanyId,
                Code = "BPV",
                Description = "Bank Payment Voucher",
                RequiresBankAccount = true,
                IsActive = true,
                CreatedAtUtc = fixture.Clock.UtcNow,
                UpdatedAtUtc = fixture.Clock.UtcNow
            };
        var currency = new GL_CurrencyEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            Code = "T" + suffix,
            Name = "Test currency",
            Symbol = "T",
            DecimalPlaces = 2,
            IsBaseCurrency = true,
            IsActive = true,
            CreatedAtUtc = fixture.Clock.UtcNow,
            UpdatedAtUtc = fixture.Clock.UtcNow
        };
        GL_ChartOfAccountEntity Account(string number, string title) => new()
        {
            Id = Guid.NewGuid(),
            AccountNo = number,
            Title = title,
            AccountType = AccountType.Asset,
            IsActive = true,
            IsPostingAccount = true,
            CreatedAtUtc = fixture.Clock.UtcNow,
            UpdatedAtUtc = fixture.Clock.UtcNow
        };
        var expense = Account("7" + suffix, "Test expense");
        var bankGl = Account("8" + suffix, "Test bank");
        var bank = new GL_BankAccountEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            GlAccountId = bankGl.Id,
            Code = "B" + suffix,
            Name = "Test bank",
            BankName = "Test bank",
            AccountNumber = "Synthetic",
            IsActive = true,
            CreatedAtUtc = fixture.Clock.UtcNow,
            UpdatedAtUtc = fixture.Clock.UtcNow
        };
        db.FiscalYears.Add(year); db.LockDates.Add(day); if (db.Entry(type).State == EntityState.Detached) db.VoucherTypes.Add(type); db.Currencies.Add(currency);
        db.ChartOfAccounts.AddRange(expense, bankGl); db.BankAccounts.Add(bank);
        await db.SaveChangesAsync();
        return new(user.Id, type.Id, year.Id, currency.Id, day.Id, expense.Id, bankGl.Id, date);
    }

    private VoucherDraftEditModel Model(Setup setup) => new()
    {
        CompanyId = fixture.CompanyId,
        VoucherTypeId = setup.TypeId,
        FiscalYearId = setup.YearId,
        CurrencyId = setup.CurrencyId,
        TransactionDate = setup.Date,
        Description = "Synthetic voucher"
    };
    private sealed record Setup(Guid UserId, Guid TypeId, Guid YearId, Guid CurrencyId, Guid DayId,
        Guid ExpenseId, Guid BankGlId, DateOnly Date);
}
