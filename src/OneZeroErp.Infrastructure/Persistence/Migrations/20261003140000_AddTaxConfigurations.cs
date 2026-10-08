using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneZeroErp.Infrastructure.Persistence;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20261003140000_AddTaxConfigurations")]
public partial class AddTaxConfigurations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Gl_Setup_TaxConfigurations",
            schema: "gl",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                TaxType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                RatePercent = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Gl_Setup_TaxConfigurations", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Gl_Setup_TaxConfigurations_CompanyId_Code", schema: "gl", table: "Gl_Setup_TaxConfigurations", columns: new[] { "CompanyId", "Code" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "Gl_Setup_TaxConfigurations", schema: "gl");
}
