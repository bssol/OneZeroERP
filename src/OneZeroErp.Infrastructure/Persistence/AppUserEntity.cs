namespace OneZeroErp.Infrastructure.Persistence;

public sealed class AppUserEntity
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<AppUserPermissionEntity> Permissions { get; set; } = new List<AppUserPermissionEntity>();
}

public sealed class AppUserPermissionEntity
{
    public Guid Id { get; set; }
    public Guid AppUserId { get; set; }
    public string Permission { get; set; } = string.Empty;
    public AppUserEntity AppUser { get; set; } = null!;
}