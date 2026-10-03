using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAccountNatureFromChartOfAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nature",
                schema: "gl",
                table: "Gl_Setup_ChartOfAccounts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nature",
                schema: "gl",
                table: "Gl_Setup_ChartOfAccounts",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");
        }
    }
}
