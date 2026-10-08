using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OneZeroErp.Infrastructure.Persistence;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260930120000_AddChartOfAccountPostingFlag")]
public partial class AddChartOfAccountPostingFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH(N'[gl].[Gl_Setup_ChartOfAccounts]', N'IsPostingAccount') IS NULL
            BEGIN
                ALTER TABLE [gl].[Gl_Setup_ChartOfAccounts]
                    ADD [IsPostingAccount] bit NOT NULL CONSTRAINT [DF_Gl_Setup_ChartOfAccounts_IsPostingAccount] DEFAULT (1);
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "IsPostingAccount", schema: "gl", table: "Gl_Setup_ChartOfAccounts");
}
