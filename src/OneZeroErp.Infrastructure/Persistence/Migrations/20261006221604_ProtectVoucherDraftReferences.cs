using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProtectVoucherDraftReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Number",
                schema: "gl",
                table: "Gl_Journals",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.CreateIndex(
                name: "IX_Gl_Journals_CurrencyId",
                schema: "gl",
                table: "Gl_Journals",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Gl_Journals_FiscalYearId",
                schema: "gl",
                table: "Gl_Journals",
                column: "FiscalYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Gl_Journals_VoucherTypeId",
                schema: "gl",
                table: "Gl_Journals",
                column: "VoucherTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Gl_JournalLines_AccountId",
                schema: "gl",
                table: "Gl_JournalLines",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Gl_JournalLines_Gl_Setup_ChartOfAccounts_AccountId",
                schema: "gl",
                table: "Gl_JournalLines",
                column: "AccountId",
                principalSchema: "gl",
                principalTable: "Gl_Setup_ChartOfAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Gl_Journals_Erp_FiscalYears_FiscalYearId",
                schema: "gl",
                table: "Gl_Journals",
                column: "FiscalYearId",
                principalSchema: "erp",
                principalTable: "Erp_FiscalYears",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Gl_Journals_Gl_Setup_Currencies_CurrencyId",
                schema: "gl",
                table: "Gl_Journals",
                column: "CurrencyId",
                principalSchema: "gl",
                principalTable: "Gl_Setup_Currencies",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Gl_Journals_Gl_Setup_VoucherTypes_VoucherTypeId",
                schema: "gl",
                table: "Gl_Journals",
                column: "VoucherTypeId",
                principalSchema: "gl",
                principalTable: "Gl_Setup_VoucherTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gl_JournalLines_Gl_Setup_ChartOfAccounts_AccountId",
                schema: "gl",
                table: "Gl_JournalLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Gl_Journals_Erp_FiscalYears_FiscalYearId",
                schema: "gl",
                table: "Gl_Journals");

            migrationBuilder.DropForeignKey(
                name: "FK_Gl_Journals_Gl_Setup_Currencies_CurrencyId",
                schema: "gl",
                table: "Gl_Journals");

            migrationBuilder.DropForeignKey(
                name: "FK_Gl_Journals_Gl_Setup_VoucherTypes_VoucherTypeId",
                schema: "gl",
                table: "Gl_Journals");

            migrationBuilder.DropIndex(
                name: "IX_Gl_Journals_CurrencyId",
                schema: "gl",
                table: "Gl_Journals");

            migrationBuilder.DropIndex(
                name: "IX_Gl_Journals_FiscalYearId",
                schema: "gl",
                table: "Gl_Journals");

            migrationBuilder.DropIndex(
                name: "IX_Gl_Journals_VoucherTypeId",
                schema: "gl",
                table: "Gl_Journals");

            migrationBuilder.DropIndex(
                name: "IX_Gl_JournalLines_AccountId",
                schema: "gl",
                table: "Gl_JournalLines");

            migrationBuilder.AlterColumn<string>(
                name: "Number",
                schema: "gl",
                table: "Gl_Journals",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80);
        }
    }
}
