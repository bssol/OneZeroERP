namespace OneZeroErp.Domain.ErpSetups;

public sealed class Erp_DayLock : AuditableEntity
{
    private Erp_DayLock()
    {
    }

    private Erp_DayLock(Guid id, Guid companyId, Guid createdBy, DateTimeOffset createdOn, DateOnly date)
        : base(companyId, createdBy, createdOn)
    {
        Id = id;
        Date = date;
    }

    public Guid Id { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsLocked { get; private set; }
    public Guid? LockedByUserId { get; private set; }
    public DateTimeOffset? LockedAtUtc { get; private set; }

    public static Erp_DayLock Create(Guid companyId, Guid createdBy, DateTimeOffset createdOn, DateOnly date, Guid? id = null) => new(id ?? Guid.NewGuid(), companyId, createdBy, createdOn, date);

    public void Lock(Guid actorUserName, DateTimeOffset lockedAtUtc)
    {
        EnsureActor(actorUserName);
        IsLocked = true;
        LockedByUserId = actorUserName;
        LockedAtUtc = lockedAtUtc.ToUniversalTime();
        MarkUpdated(actorUserName, lockedAtUtc);
    }

    public void Unlock(Guid actorUserName, DateTimeOffset updatedOn)
    {
        EnsureActor(actorUserName);
        IsLocked = false;
        LockedByUserId = null;
        LockedAtUtc = null;
        MarkUpdated(actorUserName, updatedOn);
    }
}