using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OneZeroErp.Application.Time;
using OneZeroErp.IdentityAccess;
using OneZeroErp.Infrastructure.Identity;
using OneZeroErp.Infrastructure.Persistence;

namespace OneZeroErp.IntegrationTests;

public sealed class SqlFixture : IAsyncLifetime, IDbContextFactory<ErpDbContext>
{
    public string DatabaseName { get; } = "OneZeroErp_Test_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; }
    public Guid CompanyId { get; } = Guid.NewGuid();
    public string Password { get; } = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
    public IConfiguration Configuration { get; }
    public TestClock Clock { get; } = new();

    public SqlFixture()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ONEZERO_TEST_SQL")
            ?? "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;TrustServerCertificate=true");
        builder.InitialCatalog = DatabaseName;
        ConnectionString = builder.ConnectionString;
        Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
            ["ConnectionStrings:Default"] = ConnectionString,
            ["Company:DefaultCompanyId"] = CompanyId.ToString(),
            ["Outbox:Enabled"] = "false"
        }).Build();
    }

    public ErpDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ErpDbContext>().UseSqlServer(ConnectionString).Options);
    public Task<ErpDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    public SqlSessionAuthenticationService Authentication => new(this, new JwtTokenService(Configuration, Clock), new ClaimPermissionService(), Clock);

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task<AppUserEntity> AddUserAsync(params string[] permissions)
    {
        var id = Guid.NewGuid();
        var user = new AppUserEntity
        {
            Id = id,
            CompanyId = CompanyId,
            UserName = "test-" + id.ToString("N"),
            DisplayName = "Synthetic test user",
            PasswordHash = PasswordHasher.Hash(Password),
            Role = "Test",
            IsActive = true,
            CreatedAtUtc = Clock.UtcNow,
            UpdatedAtUtc = Clock.UtcNow,
            Permissions = permissions.Select(permission => new AppUserPermissionEntity { Id = Guid.NewGuid(), AppUserId = id, Permission = permission }).ToList()
        };
        await using var db = CreateDbContext();
        db.AppUsers.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task DisposeAsync()
    {
        // Only the unique database allocated by this fixture can be removed.
        if (!DatabaseName.StartsWith("OneZeroErp_Test_", StringComparison.Ordinal) || !Guid.TryParseExact(DatabaseName[16..], "N", out _))
            throw new InvalidOperationException("Refusing to remove a database not created by this fixture.");
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }
}

public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CurrentDateTime => UtcNow;
    public DateOnly CurrentDate => DateOnly.FromDateTime(CurrentDateTime.DateTime);
    public TimeOnly CurrentTime => TimeOnly.FromDateTime(CurrentDateTime.DateTime);
}
