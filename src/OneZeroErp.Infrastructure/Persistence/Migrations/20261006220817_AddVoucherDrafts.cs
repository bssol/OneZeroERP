using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Gl_Journals",
                schema: "gl",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoucherTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FiscalYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SequenceNumber = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gl_Journals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Gl_JournalLines",
                schema: "gl",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Narration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Debit = table.Column<decimal>(type: "decimal(19,4)", nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(19,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gl_JournalLines", x => x.Id);
                    table.CheckConstraint("CK_JournalLine_OneSide", "([Debit] > 0 AND [Credit] = 0) OR ([Credit] > 0 AND [Debit] = 0)");
                    table.ForeignKey(
                        name: "FK_Gl_JournalLines_Gl_Journals_JournalId",
                        column: x => x.JournalId,
                        principalSchema: "gl",
                        principalTable: "Gl_Journals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gl_JournalLines_JournalId_LineNumber",
                schema: "gl",
                table: "Gl_JournalLines",
                columns: new[] { "JournalId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gl_Journals_CompanyId_Number",
                schema: "gl",
                table: "Gl_Journals",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gl_Journals_CompanyId_TransactionDate",
                schema: "gl",
                table: "Gl_Journals",
                columns: new[] { "CompanyId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Gl_Journals_CompanyId_VoucherTypeId_FiscalYearId_SequenceNumber",
                schema: "gl",
                table: "Gl_Journals",
                columns: new[] { "CompanyId", "VoucherTypeId", "FiscalYearId", "SequenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gl_JournalLines",
                schema: "gl");

            migrationBuilder.DropTable(
                name: "Gl_Journals",
                schema: "gl");
        }
    }
}
