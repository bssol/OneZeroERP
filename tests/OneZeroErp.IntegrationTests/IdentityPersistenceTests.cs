using Microsoft.EntityFrameworkCore;
using OneZeroErp.Application;
using OneZeroErp.Application.Identity;

namespace OneZeroErp.IntegrationTests;

public sealed class IdentityPersistenceTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    [Fact]
    public async Task Refresh_rotation_detects_reuse_and_revokes_the_entire_session()
    {
        var user = await fixture.AddUserAsync();
        var service = fixture.Authentication;
        var login = await service.AuthenticateAsync(new(user.UserName, fixture.Password));
        Assert.True(login.Succeeded);
        var refreshed = await service.RefreshAsync(login.RefreshToken!);
        Assert.True(refreshed.Succeeded);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.False((await service.RefreshAsync(login.RefreshToken!)).Succeeded);
        Assert.False((await service.RefreshAsync(refreshed.RefreshToken!)).Succeeded);
        Assert.Null(await service.ValidateSessionAsync(login.Principal!));
        await using var db = fixture.CreateDbContext();
        Assert.DoesNotContain(await db.RefreshTokens.Select(x => x.TokenHash).ToListAsync(), hash => hash == login.RefreshToken || hash == refreshed.RefreshToken);
        Assert.True(await db.AuditEvents.AnyAsync(x => x.EntityId == user.Id && x.Action == "RefreshTokenReuseDetected"));
    }

    [Fact]
    public async Task Concurrent_refresh_allows_one_rotation_then_revokes_on_reuse()
    {
        var user = await fixture.AddUserAsync();
        var login = await fixture.Authentication.AuthenticateAsync(new(user.UserName, fixture.Password));
        var results = await Task.WhenAll(fixture.Authentication.RefreshAsync(login.RefreshToken!), fixture.Authentication.RefreshAsync(login.RefreshToken!));
        Assert.Single(results, x => x.Succeeded);
        Assert.Null(await fixture.Authentication.ValidateSessionAsync(login.Principal!));
    }

    [Fact]
    public async Task Failed_passwords_lock_account_and_inactive_users_cannot_authenticate()
    {
        var user = await fixture.AddUserAsync();
        var service = fixture.Authentication;
        for (var i = 0; i < 5; i++) Assert.False((await service.AuthenticateAsync(new(user.UserName, "wrong"))).Succeeded);
        Assert.False((await service.AuthenticateAsync(new(user.UserName, fixture.Password))).Succeeded);
        var initialTime = fixture.Clock.UtcNow;
        fixture.Clock.UtcNow = initialTime.AddMinutes(16);
        try { Assert.True((await service.AuthenticateAsync(new(user.UserName, fixture.Password))).Succeeded); }
        finally { fixture.Clock.UtcNow = initialTime; }
        await using var db = fixture.CreateDbContext();
        var stored = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        stored.IsActive = false;
        await db.SaveChangesAsync();
        Assert.False((await service.AuthenticateAsync(new(user.UserName, fixture.Password))).Succeeded);
    }

    [Fact]
    public async Task Password_reset_requires_current_permission_is_single_use_and_revokes_sessions()
    {
        var admin = await fixture.AddUserAsync("identity.users:CanEdit");
        var user = await fixture.AddUserAsync();
        var service = fixture.Authentication;
        var adminLogin = await service.AuthenticateAsync(new(admin.UserName, fixture.Password));
        var login = await service.AuthenticateAsync(new(user.UserName, fixture.Password));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.IssuePasswordResetAsync(login.Principal!, admin.Id));
        var grant = await service.IssuePasswordResetAsync(adminLogin.Principal!, user.Id);
        Assert.NotNull(grant);
        var password = "replacement-" + Guid.NewGuid();
        Assert.True((await service.ResetPasswordAsync(new(grant.Token, password))).Succeeded);
        Assert.False((await service.ResetPasswordAsync(new(grant.Token, password))).Succeeded);
        Assert.Null(await service.ValidateSessionAsync(login.Principal!));
        Assert.False((await service.AuthenticateAsync(new(user.UserName, fixture.Password))).Succeeded);
        Assert.True((await service.AuthenticateAsync(new(user.UserName, password))).Succeeded);
    }

    [Fact]
    public async Task Session_validation_refreshes_permissions_and_rejects_company_changes_and_logout()
    {
        var user = await fixture.AddUserAsync("platform.diagnostics:CanView");
        var service = fixture.Authentication;
        var login = await service.AuthenticateAsync(new(user.UserName, fixture.Password));
        await using var db = fixture.CreateDbContext();
        db.AppUserPermissions.RemoveRange(await db.AppUserPermissions.Where(x => x.AppUserId == user.Id).ToListAsync());
        await db.SaveChangesAsync();
        var current = await service.ValidateSessionAsync(login.Principal!);
        Assert.NotNull(current);
        Assert.DoesNotContain(current.Claims, x => x.Type == "onezero:permission");
        await service.RevokeAsync(current);
        Assert.Null(await service.ValidateSessionAsync(login.Principal!));
        var second = await service.AuthenticateAsync(new(user.UserName, fixture.Password));
        var stored = await db.AppUsers.SingleAsync(x => x.Id == user.Id);
        stored.CompanyId = Guid.NewGuid();
        await db.SaveChangesAsync();
        Assert.Null(await service.ValidateSessionAsync(second.Principal!));
        Assert.False((await service.RefreshAsync(second.RefreshToken!)).Succeeded);
    }

    [Fact]
    public async Task Expired_sessions_and_reset_grants_fail_closed()
    {
        var user = await fixture.AddUserAsync("identity.users:CanEdit");
        var service = fixture.Authentication;
        var login = await service.AuthenticateAsync(new(user.UserName, fixture.Password));
        var grant = await service.IssuePasswordResetAsync(login.Principal!, user.Id);
        var initialTime = fixture.Clock.UtcNow;
        fixture.Clock.UtcNow = initialTime.AddHours(9);
        try
        {
            Assert.Null(await service.ValidateSessionAsync(login.Principal!));
            Assert.False((await service.RefreshAsync(login.RefreshToken!)).Succeeded);
            Assert.False((await service.ResetPasswordAsync(new(grant!.Token, fixture.Password))).Succeeded);
        }
        finally { fixture.Clock.UtcNow = initialTime; }
    }
}
