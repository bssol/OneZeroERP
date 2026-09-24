using System.Security.Claims;
using OneZeroErp.Application;
using OneZeroErp.IdentityAccess;

namespace OneZeroErp.UnitTests;

public sealed class PermissionServiceTests
{
    [Fact]
    public async Task Resource_permissions_are_scoped_and_wildcard_permissions_apply()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimPermissionService.PermissionClaimType, "gl.fiscal-years:CanView,gl.fiscal-years:CanEdit"),
            new Claim(ClaimPermissionService.PermissionClaimType, "*:CanExport")
        }, "test");
        var principal = new ClaimsPrincipal(identity);
        var service = new ClaimPermissionService();

        var fiscalYearPermissions = await service.GetPermissionsAsync(principal, "gl.fiscal-years");
        var reportPermissions = await service.GetPermissionsAsync(principal, "reports");

        Assert.True(fiscalYearPermissions.Allows(PermissionAction.CanView));
        Assert.True(fiscalYearPermissions.Allows(PermissionAction.CanEdit));
        Assert.False(fiscalYearPermissions.Allows(PermissionAction.CanDelete));
        Assert.True(reportPermissions.Allows(PermissionAction.CanExport));
    }
}
