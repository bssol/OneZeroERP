using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations;

[Migration("20260930120000_AddChartOfAccountPostingFlag")]
public partial class AddChartOfAccountPostingFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPostingAccount",
            schema: "gl",
            table: "Gl_Setup_ChartOfAccounts",
            type: "bit",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "IsPostingAccount", schema: "gl", table: "Gl_Setup_ChartOfAccounts");
}
