using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations;

[Migration("20261003120000_AddBankAndCashAccounts")]
public partial class AddBankAndCashAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Gl_Setup_BankAccounts",
            schema: "gl",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                BankName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                AccountNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Gl_Setup_BankAccounts", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Gl_Setup_CashAccounts",
            schema: "gl",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GlAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                Location = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Gl_Setup_CashAccounts", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Gl_Setup_BankAccounts_CompanyId_Code", schema: "gl", table: "Gl_Setup_BankAccounts", columns: new[] { "CompanyId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Gl_Setup_BankAccounts_CompanyId_GlAccountId", schema: "gl", table: "Gl_Setup_BankAccounts", columns: new[] { "CompanyId", "GlAccountId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Gl_Setup_CashAccounts_CompanyId_Code", schema: "gl", table: "Gl_Setup_CashAccounts", columns: new[] { "CompanyId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Gl_Setup_CashAccounts_CompanyId_GlAccountId", schema: "gl", table: "Gl_Setup_CashAccounts", columns: new[] { "CompanyId", "GlAccountId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Gl_Setup_BankAccounts", schema: "gl");
        migrationBuilder.DropTable(name: "Gl_Setup_CashAccounts", schema: "gl");
    }
}
