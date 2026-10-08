using OneZeroErp.Application;
using OneZeroErp.Domain;
using OneZeroErp.IdentityAccess;

namespace OneZeroErp.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_and_application_do_not_depend_on_infrastructure_ui_or_database_provider()
    {
        foreach (var assembly in new[] { typeof(AuditableEntity).Assembly, typeof(IAuthenticationService).Assembly })
        {
            var references = assembly.GetReferencedAssemblies().Select(x => x.Name ?? "");
            Assert.DoesNotContain(references, name => name.Contains("EntityFramework", StringComparison.Ordinal)
                || name.Contains("SqlClient", StringComparison.Ordinal) || name.Contains("Infrastructure", StringComparison.Ordinal)
                || name.Contains("AspNetCore", StringComparison.Ordinal) || name.EndsWith(".Web", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Identity_module_does_not_reference_gl_infrastructure_or_ui()
    {
        Assert.DoesNotContain(typeof(ClaimPermissionService).Assembly.GetReferencedAssemblies(),
            reference => reference.Name is "OneZeroErp.Infrastructure" or "OneZeroErp.Web" or "OneZeroErp.SharedUi");
    }

    [Theory]
    [InlineData("v1$-1$invalid$invalid")]
    [InlineData("v1$2147483647$invalid$invalid")]
    [InlineData("v1$600000$AA==$AA==")]
    public void Corrupt_password_hashes_fail_closed_without_excessive_work(string hash) => Assert.False(PasswordHasher.Verify("password", hash));
}
