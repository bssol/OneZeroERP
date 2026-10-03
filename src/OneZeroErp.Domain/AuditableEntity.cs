namespace OneZeroErp.Domain;

public abstract class AuditableEntity
{
    protected AuditableEntity()
    {
    }

    protected AuditableEntity(Guid companyId, Guid createdBy, DateTimeOffset createdOn)
    {
        EnsureActor(createdBy);
        if (companyId == Guid.Empty) throw new DomainRuleException("A valid company is required.");
        if (createdOn == default) throw new DomainRuleException("A valid creation timestamp is required.");

        CompanyId = companyId;
        CreatedBy = createdBy;
        CreatedOn = createdOn.ToUniversalTime();
    }

    public Guid CompanyId { get; private set; }
    public DateTimeOffset CreatedOn { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedOn { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public DateTimeOffset? DeletedOn { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public bool IsDeleted => DeletedOn.HasValue;

    protected void MarkUpdated(Guid actorUserName, DateTimeOffset updatedOn)
    {
        EnsureActor(actorUserName);
        if (updatedOn == default) throw new DomainRuleException("A valid update timestamp is required.");
        UpdatedBy = actorUserName;
        UpdatedOn = updatedOn.ToUniversalTime();
    }

    protected void MarkDeleted(Guid actorUserName, DateTimeOffset deletedOn)
    {
        EnsureActor(actorUserName);
        if (deletedOn == default) throw new DomainRuleException("A valid deletion timestamp is required.");
        DeletedBy = actorUserName;
        DeletedOn = deletedOn.ToUniversalTime();
        MarkUpdated(actorUserName, deletedOn);
    }

    protected static void EnsureActor(Guid actorUserName)
    {
        if (actorUserName == Guid.Empty) throw new DomainRuleException("A valid acting user is required.");
    }
}