using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations;

[Migration("20261003130000_AddCurrenciesAndExchangeRates")]
public partial class AddCurrenciesAndExchangeRates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Gl_Setup_Currencies",
            schema: "gl",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                Symbol = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                DecimalPlaces = table.Column<int>(type: "int", nullable: false),
                IsBaseCurrency = table.Column<bool>(type: "bit", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Gl_Setup_Currencies", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Gl_Setup_ExchangeRates",
            schema: "gl",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CurrencyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                RateToBase = table.Column<decimal>(type: "decimal(19,8)", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Gl_Setup_ExchangeRates", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Gl_Setup_Currencies_CompanyId_Code", schema: "gl", table: "Gl_Setup_Currencies", columns: new[] { "CompanyId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Gl_Setup_ExchangeRates_CompanyId_CurrencyId_EffectiveDate", schema: "gl", table: "Gl_Setup_ExchangeRates", columns: new[] { "CompanyId", "CurrencyId", "EffectiveDate" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Gl_Setup_ExchangeRates_CurrencyId", schema: "gl", table: "Gl_Setup_ExchangeRates", column: "CurrencyId");
        migrationBuilder.AddForeignKey(name: "FK_Gl_Setup_ExchangeRates_Gl_Setup_Currencies_CurrencyId", schema: "gl", table: "Gl_Setup_ExchangeRates", column: "CurrencyId", principalSchema: "gl", principalTable: "Gl_Setup_Currencies", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Gl_Setup_ExchangeRates_Gl_Setup_Currencies_CurrencyId", schema: "gl", table: "Gl_Setup_ExchangeRates");
        migrationBuilder.DropTable(name: "Gl_Setup_ExchangeRates", schema: "gl");
        migrationBuilder.DropTable(name: "Gl_Setup_Currencies", schema: "gl");
    }
}
