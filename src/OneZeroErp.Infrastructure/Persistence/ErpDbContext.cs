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
	public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.HasDefaultSchema("dbo");

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
