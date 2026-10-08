namespace OneZeroErp.Infrastructure.Persistence;

public sealed class UserSessionEntity
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public Guid CompanyId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

public sealed class RefreshTokenEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}

public sealed class PasswordResetEntity
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}

public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }
    public Guid AuditEventId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DispatchedAtUtc { get; set; }
    public int Attempts { get; set; }
}

// Durable local delivery receipt. Consumers use AuditEventId/Id to deduplicate
// before applying effects in the same database transaction.
public sealed class AuditDeliveryEntity
{
    public Guid Id { get; set; }
    public Guid AuditEventId { get; set; }
    public DateTimeOffset DeliveredAtUtc { get; set; }
}
