using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OneZeroErp.Infrastructure.Persistence;

public sealed class ErpDesignTimeFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ErpDbContext>()
        .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=OneZeroErp_DesignOnly;Integrated Security=true;TrustServerCertificate=true")
        .Options);
}
