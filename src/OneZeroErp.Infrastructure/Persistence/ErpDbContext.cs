using Microsoft.EntityFrameworkCore;
using OneZeroErp.Infrastructure.GeneralLedger;

namespace OneZeroErp.Infrastructure.Persistence;

public sealed class ErpDbContext(DbContextOptions<ErpDbContext> options) : DbContext(options)
{
    public DbSet<AppUserEntity> AppUsers => Set<AppUserEntity>();
    public DbSet<AppUserPermissionEntity> AppUserPermissions => Set<AppUserPermissionEntity>();
    public DbSet<Erp_SetupFiscalYearEntity> FiscalYears => Set<Erp_SetupFiscalYearEntity>();
    public DbSet<Erp_LockDateEntity> LockDates => Set<Erp_LockDateEntity>();
    public DbSet<GL_ChartOfAccountEntity> ChartOfAccounts => Set<GL_ChartOfAccountEntity>();
    public DbSet<GL_VoucherTypeEntity> VoucherTypes => Set<GL_VoucherTypeEntity>();
    public DbSet<GL_BankAccountEntity> BankAccounts => Set<GL_BankAccountEntity>();
    public DbSet<GL_CashAccountEntity> CashAccounts => Set<GL_CashAccountEntity>();
    public DbSet<GL_CurrencyEntity> Currencies => Set<GL_CurrencyEntity>();
    public DbSet<GL_ExchangeRateEntity> ExchangeRates => Set<GL_ExchangeRateEntity>();
    public DbSet<GL_TaxConfigurationEntity> TaxConfigurations => Set<GL_TaxConfigurationEntity>();
    public DbSet<GL_JournalEntity> Journals => Set<GL_JournalEntity>();
    public DbSet<GL_JournalLineEntity> JournalLines => Set<GL_JournalLineEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<UserSessionEntity> UserSessions => Set<UserSessionEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<PasswordResetEntity> PasswordResets => Set<PasswordResetEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();
    public DbSet<AuditDeliveryEntity> AuditDeliveries => Set<AuditDeliveryEntity>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareAuditOutbox();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareAuditOutbox();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareAuditOutbox()
    {
        var entries = ChangeTracker.Entries<AuditEventEntity>().ToArray();
        if (entries.Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are append-only.");
        foreach (var entry in entries.Where(x => x.State == EntityState.Added))
        {
            if (!OutboxMessages.Local.Any(x => x.AuditEventId == entry.Entity.Id))
                OutboxMessages.Add(new() { Id = entry.Entity.Id, AuditEventId = entry.Entity.Id, CreatedAtUtc = entry.Entity.OccurredAtUtc });
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.ApplyConfiguration(new UserSessionConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new PasswordResetConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AuditDeliveryConfiguration());

        modelBuilder.Entity<AppUserEntity>(entity =>
        {
            entity.ToTable("Identity_AppUsers", "identity");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(100).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasMany(x => x.Permissions).WithOne(x => x.AppUser).HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUserPermissionEntity>(entity =>
        {
            entity.ToTable("Identity_AppUserPermissions", "identity");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Permission).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => new { x.AppUserId, x.Permission }).IsUnique();
        });

        modelBuilder.Entity<Erp_SetupFiscalYearEntity>(entity =>
        {
            entity.ToTable("Erp_FiscalYears", "erp");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyId).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(x => x.StartDate).HasColumnType("date");
            entity.Property(x => x.EndDate).HasColumnType("date");
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Erp_LockDateEntity>(entity =>
        {
            entity.ToTable("Erp_LockDate", "erp");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyId).IsRequired();
            entity.Property(x => x.FiscalYearId).IsRequired();
            entity.Property(x => x.Date).HasColumnType("date").IsRequired();
            entity.Property(x => x.IsLocked).IsRequired();
            entity.Property(x => x.LockedAtUtc);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.FiscalYearId, x.Date }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.Date });
            entity.HasOne<Erp_SetupFiscalYearEntity>()
                            .WithMany()
                            .HasForeignKey(x => x.FiscalYearId)
                            .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GL_ChartOfAccountEntity>(entity =>
        {
            entity.ToTable("Gl_Setup_ChartOfAccounts", "gl");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AccountNo).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => x.AccountNo).IsUnique();
            entity.Property(x => x.ParentAccountNo).HasMaxLength(30);
            entity.Property(x => x.ParentAccountTitle).HasMaxLength(160);
            entity.Property(x => x.AccountLevel).IsRequired();
            entity.Property(x => x.AccountType).HasConversion<string>().HasMaxLength(32).IsRequired();

            entity.Property(x => x.Title).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.IsPostingAccount).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => x.ParentAccountNo);
        });
        modelBuilder.Entity<GL_VoucherTypeEntity>(entity =>
        {
            entity.ToTable("Gl_Setup_VoucherTypes", "gl");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyId).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200).IsRequired();
            entity.Property(x => x.RequiresBankAccount).IsRequired();
            entity.Property(x => x.RequiresCashAccount).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });
        modelBuilder.Entity<GL_BankAccountEntity>(entity =>
        {
            entity.ToTable("Gl_Setup_BankAccounts", "gl");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyId).IsRequired();
            entity.Property(x => x.GlAccountId).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.BankName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.AccountNumber).HasMaxLength(80).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.GlAccountId }).IsUnique();
        });
        modelBuilder.Entity<GL_CashAccountEntity>(entity =>
        {
            entity.ToTable("Gl_Setup_CashAccounts", "gl");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyId).IsRequired();
            entity.Property(x => x.GlAccountId).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(160).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.GlAccountId }).IsUnique();
        });
        modelBuilder.Entity<GL_CurrencyEntity>(entity =>
        {
            entity.ToTable("Gl_Setup_Currencies", "gl");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyId).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Symbol).HasMaxLength(8).IsRequired();
            entity.Property(x => x.DecimalPlaces).IsRequired();
            entity.Property(x => x.IsBaseCurrency).IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });
        modelBuilder.Entity<GL_ExchangeRateEntity>(entity =>
        {
            entity.ToTable("Gl_Setup_ExchangeRates", "gl");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyId).IsRequired();
            entity.Property(x => x.CurrencyId).IsRequired();
            entity.Property(x => x.EffectiveDate).HasColumnType("date").IsRequired();
            entity.Property(x => x.RateToBase).HasColumnType("decimal(19,8)").IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.CurrencyId, x.EffectiveDate }).IsUnique();
            entity.HasOne<GL_CurrencyEntity>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<GL_TaxConfigurationEntity>(entity =>
        {
            entity.ToTable("Gl_Setup_TaxConfigurations", "gl");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CompanyId).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.TaxType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.RatePercent).HasColumnType("decimal(9,4)").IsRequired();
            entity.Property(x => x.EffectiveFrom).HasColumnType("date").IsRequired();
            entity.Property(x => x.IsActive).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.Property(x => x.UpdatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });
        modelBuilder.Entity<GL_JournalEntity>(entity =>
        {
            entity.ToTable("Gl_Journals", "gl");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasMaxLength(80).IsRequired();
            entity.Property(x => x.TransactionDate).HasColumnType("date");
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Number }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.VoucherTypeId, x.FiscalYearId, x.SequenceNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.TransactionDate });
            entity.HasOne<GL_VoucherTypeEntity>().WithMany().HasForeignKey(x => x.VoucherTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Erp_SetupFiscalYearEntity>().WithMany().HasForeignKey(x => x.FiscalYearId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GL_CurrencyEntity>().WithMany().HasForeignKey(x => x.CurrencyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(x => x.Lines).WithOne(x => x.Journal).HasForeignKey(x => x.JournalId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<GL_JournalLineEntity>(entity =>
        {
            entity.ToTable("Gl_JournalLines", "gl", table => table.HasCheckConstraint("CK_JournalLine_OneSide", "([Debit] > 0 AND [Credit] = 0) OR ([Credit] > 0 AND [Debit] = 0)"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Narration).HasMaxLength(500);
            entity.Property(x => x.Debit).HasColumnType("decimal(19,4)");
            entity.Property(x => x.Credit).HasColumnType("decimal(19,4)");
            entity.HasIndex(x => new { x.JournalId, x.LineNumber }).IsUnique();
            entity.HasOne<GL_ChartOfAccountEntity>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AuditEventEntity>(entity =>
        {
            entity.ToTable("Audit_Events", "audit");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(80).IsRequired();
            entity.Property(x => x.EntityType).HasMaxLength(120).IsRequired();
            entity.Property(x => x.BeforeSummary).HasMaxLength(4000);
            entity.Property(x => x.AfterSummary).HasMaxLength(4000);
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAtUtc });
        });
    }
}
