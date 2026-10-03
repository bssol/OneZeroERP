using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations;

[Migration("20260930130000_RepairChartOfAccountPostingColumn")]
public partial class RepairChartOfAccountPostingColumn : Migration
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

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This repair is intentionally forward-only; removing the column would destroy account classification data.
    }
}
