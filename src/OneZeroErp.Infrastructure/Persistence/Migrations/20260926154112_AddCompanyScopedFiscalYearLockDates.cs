using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyScopedFiscalYearLockDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "erp");

            migrationBuilder.RenameTable(name: "AppUsers", schema: "identity", newName: "Identity_AppUsers", newSchema: "identity");
            migrationBuilder.RenameTable(name: "AppUserPermissions", schema: "identity", newName: "Identity_AppUserPermissions", newSchema: "identity");
            migrationBuilder.RenameTable(name: "AuditEvents", schema: "audit", newName: "Audit_Events", newSchema: "audit");
            migrationBuilder.RenameTable(name: "FiscalYears", schema: "gl", newName: "Erp_FiscalYears", newSchema: "erp");
            migrationBuilder.RenameTable(name: "ChartOfAccounts", schema: "gl", newName: "Gl_Setup_ChartOfAccounts", newSchema: "gl");

            migrationBuilder.DropIndex(
                name: "IX_FiscalYears_Code",
                schema: "erp",
                table: "Erp_FiscalYears");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                schema: "erp",
                table: "Erp_FiscalYears",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Erp_LockDate",
                schema: "erp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    IsLocked = table.Column<bool>(type: "bit", nullable: false),
                    LockedByUserId = table.Column<Guid?>(type: "uniqueidentifier", nullable: true),
                    LockedAtUtc = table.Column<DateTimeOffset?>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Erp_LockDate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Erp_LockDate_Erp_FiscalYears_FiscalYearId",
                        column: x => x.FiscalYearId,
                        principalSchema: "erp",
                        principalTable: "Erp_FiscalYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Erp_LockDate_CompanyId_Date",
                schema: "erp",
                table: "Erp_LockDate",
                columns: new[] { "CompanyId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Erp_LockDate_CompanyId_FiscalYearId_Date",
                schema: "erp",
                table: "Erp_LockDate",
                columns: new[] { "CompanyId", "FiscalYearId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Erp_LockDate_FiscalYearId",
                schema: "erp",
                table: "Erp_LockDate",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Erp_FiscalYears_CompanyId_Code",
                schema: "erp",
                table: "Erp_FiscalYears",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Erp_LockDate_Erp_FiscalYears_FiscalYearId",
                schema: "erp",
                table: "Erp_LockDate");

            migrationBuilder.DropIndex(
                name: "IX_Erp_LockDate_CompanyId_Date",
                schema: "erp",
                table: "Erp_LockDate");

            migrationBuilder.DropIndex(
                name: "IX_Erp_LockDate_CompanyId_FiscalYearId_Date",
                schema: "erp",
                table: "Erp_LockDate");

            migrationBuilder.DropIndex(
                name: "IX_Erp_LockDate_FiscalYearId",
                schema: "erp",
                table: "Erp_LockDate");

            migrationBuilder.DropIndex(
                name: "IX_Erp_FiscalYears_CompanyId_Code",
                schema: "erp",
                table: "Erp_FiscalYears");

            migrationBuilder.DropTable(name: "Erp_LockDate", schema: "erp");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                schema: "erp",
                table: "Erp_FiscalYears");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalYears_Code",
                schema: "gl",
                table: "FiscalYears",
                column: "Code",
                unique: true);

            migrationBuilder.RenameTable(name: "Gl_Setup_ChartOfAccounts", schema: "gl", newName: "ChartOfAccounts", newSchema: "gl");
            migrationBuilder.RenameTable(name: "Erp_FiscalYears", schema: "erp", newName: "FiscalYears", newSchema: "gl");
            migrationBuilder.RenameTable(name: "Audit_Events", schema: "audit", newName: "AuditEvents", newSchema: "audit");
            migrationBuilder.RenameTable(name: "Identity_AppUserPermissions", schema: "identity", newName: "AppUserPermissions", newSchema: "identity");
            migrationBuilder.RenameTable(name: "Identity_AppUsers", schema: "identity", newName: "AppUsers", newSchema: "identity");
        }
    }
}
