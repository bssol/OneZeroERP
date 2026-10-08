using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OneZeroErp.Infrastructure.Persistence;

internal sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSessionEntity>
{
    public void Configure(EntityTypeBuilder<UserSessionEntity> entity)
    {
        entity.ToTable("Identity_UserSessions", "identity");
        entity.HasKey(x => x.Id);
        entity.HasOne<AppUserEntity>().WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasIndex(x => new { x.AppUserId, x.ExpiresAtUtc });
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshTokenEntity>
{
    public void Configure(EntityTypeBuilder<RefreshTokenEntity> entity)
    {
        entity.ToTable("Identity_RefreshTokens", "identity");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        entity.HasIndex(x => x.TokenHash).IsUnique();
        entity.HasOne<UserSessionEntity>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PasswordResetConfiguration : IEntityTypeConfiguration<PasswordResetEntity>
{
    public void Configure(EntityTypeBuilder<PasswordResetEntity> entity)
    {
        entity.ToTable("Identity_PasswordResets", "identity");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        entity.HasIndex(x => x.TokenHash).IsUnique();
        entity.HasOne<AppUserEntity>().WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> entity)
    {
        entity.ToTable("Platform_Outbox", "platform");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.AuditEventId).IsUnique();
        entity.HasIndex(x => new { x.DispatchedAtUtc, x.CreatedAtUtc });
        entity.HasOne<AuditEventEntity>().WithMany().HasForeignKey(x => x.AuditEventId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AuditDeliveryConfiguration : IEntityTypeConfiguration<AuditDeliveryEntity>
{
    public void Configure(EntityTypeBuilder<AuditDeliveryEntity> entity)
    {
        entity.ToTable("Platform_AuditDeliveries", "platform");
        entity.HasKey(x => x.Id);
        entity.HasIndex(x => x.AuditEventId).IsUnique();
        entity.HasOne<AuditEventEntity>().WithMany().HasForeignKey(x => x.AuditEventId).OnDelete(DeleteBehavior.Restrict);
    }
}
