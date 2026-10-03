using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChartOfAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartOfAccounts",
                schema: "gl",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ParentAccountNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ParentAccountTitle = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    AccountLevel = table.Column<int>(type: "int", nullable: false),
                    AccountType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Nature = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartOfAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_AccountNo",
                schema: "gl",
                table: "ChartOfAccounts",
                column: "AccountNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChartOfAccounts_ParentAccountNo",
                schema: "gl",
                table: "ChartOfAccounts",
                column: "ParentAccountNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartOfAccounts",
                schema: "gl");
        }
    }
}
