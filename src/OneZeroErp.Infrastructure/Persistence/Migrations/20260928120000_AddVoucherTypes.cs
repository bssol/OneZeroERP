using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations;

[Migration("20260928120000_AddVoucherTypes")]
public partial class AddVoucherTypes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Gl_Setup_VoucherTypes",
            schema: "gl",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                RequiresBankAccount = table.Column<bool>(type: "bit", nullable: false),
                RequiresCashAccount = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Gl_Setup_VoucherTypes", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Gl_Setup_VoucherTypes_CompanyId_Code",
            schema: "gl",
            table: "Gl_Setup_VoucherTypes",
            columns: new[] { "CompanyId", "Code" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Gl_Setup_VoucherTypes", schema: "gl");
}
