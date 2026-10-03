using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using OneZeroErp.Application;
using OneZeroErp.Infrastructure.GeneralLedger;

namespace OneZeroErp.Infrastructure.Persistence;

public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query { get; }
    IQueryable<TEntity> QueryNonTracking { get; }
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<TEntity?> FirstOrDefaultNonTrackingAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<TEntity?> SingleOrDefaultNonTrackingAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<PagedResult<TEntity>> GetPageAsync(
        PagedQuery query,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? shapeQuery = null,
        CancellationToken cancellationToken = default);
    void Add(TEntity entity);
    void Remove(TEntity entity);
}

public interface IUnitOfWork
{
    IRepository<AppUserEntity> AppUsers { get; }
    IRepository<AppUserPermissionEntity> AppUserPermissions { get; }
    IRepository<Erp_SetupFiscalYearEntity> FiscalYears { get; }
    IRepository<Erp_LockDateEntity> LockDates { get; }
    IRepository<GL_ChartOfAccountEntity> ChartOfAccounts { get; }
    IRepository<GL_VoucherTypeEntity> VoucherTypes { get; }
    IRepository<GL_BankAccountEntity> BankAccounts { get; }
    IRepository<GL_CashAccountEntity> CashAccounts { get; }
    IRepository<GL_CurrencyEntity> Currencies { get; }
    IRepository<GL_ExchangeRateEntity> ExchangeRates { get; }
    IRepository<AuditEventEntity> AuditEvents { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class EfRepository<TEntity>(ErpDbContext db) : IRepository<TEntity> where TEntity : class
{
    public IQueryable<TEntity> Query => db.Set<TEntity>();
    public IQueryable<TEntity> QueryNonTracking => db.Set<TEntity>().AsNoTracking();

    public Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Query.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<TEntity?> FirstOrDefaultNonTrackingAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        QueryNonTracking.FirstOrDefaultAsync(predicate, cancellationToken);

    public Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Query.SingleOrDefaultAsync(predicate, cancellationToken);

    public Task<TEntity?> SingleOrDefaultNonTrackingAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        QueryNonTracking.SingleOrDefaultAsync(predicate, cancellationToken);

    public Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default) =>
        predicate is null
            ? QueryNonTracking.AnyAsync(cancellationToken)
            : QueryNonTracking.AnyAsync(predicate, cancellationToken);

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var query = predicate is null ? Query : Query.Where(predicate);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<TEntity>> GetPageAsync(
        PagedQuery query,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? shapeQuery = null,
        CancellationToken cancellationToken = default)
    {
        var safeQuery = new PagedQuery(query.SafePageNumber, query.SafePageSize);
        var source = shapeQuery?.Invoke(QueryNonTracking) ?? QueryNonTracking;
        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((safeQuery.SafePageNumber - 1) * safeQuery.SafePageSize)
            .Take(safeQuery.SafePageSize)
            .ToListAsync(cancellationToken);

        return new(items, safeQuery.SafePageNumber, safeQuery.SafePageSize, totalCount);
    }

    public void Add(TEntity entity) => db.Set<TEntity>().Add(entity);

    public void Remove(TEntity entity) => db.Set<TEntity>().Remove(entity);
}

public sealed class EfUnitOfWork(ErpDbContext db) : IUnitOfWork
{
    public IRepository<AppUserEntity> AppUsers { get; } = new EfRepository<AppUserEntity>(db);
    public IRepository<AppUserPermissionEntity> AppUserPermissions { get; } = new EfRepository<AppUserPermissionEntity>(db);
    public IRepository<Erp_SetupFiscalYearEntity> FiscalYears { get; } = new EfRepository<Erp_SetupFiscalYearEntity>(db);
    public IRepository<Erp_LockDateEntity> LockDates { get; } = new EfRepository<Erp_LockDateEntity>(db);
    public IRepository<GL_ChartOfAccountEntity> ChartOfAccounts { get; } = new EfRepository<GL_ChartOfAccountEntity>(db);
    public IRepository<GL_VoucherTypeEntity> VoucherTypes { get; } = new EfRepository<GL_VoucherTypeEntity>(db);
    public IRepository<GL_BankAccountEntity> BankAccounts { get; } = new EfRepository<GL_BankAccountEntity>(db);
    public IRepository<GL_CashAccountEntity> CashAccounts { get; } = new EfRepository<GL_CashAccountEntity>(db);
    public IRepository<GL_CurrencyEntity> Currencies { get; } = new EfRepository<GL_CurrencyEntity>(db);
    public IRepository<GL_ExchangeRateEntity> ExchangeRates { get; } = new EfRepository<GL_ExchangeRateEntity>(db);
    public IRepository<AuditEventEntity> AuditEvents { get; } = new EfRepository<AuditEventEntity>(db);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
