using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OneZeroErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteBootstrapIdentityAndOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                schema: "identity",
                table: "Identity_AppUsers",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                schema: "identity",
                table: "Identity_AppUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntilUtc",
                schema: "identity",
                table: "Identity_AppUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                schema: "audit",
                table: "Audit_Events",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Identity_PasswordResets",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_PasswordResets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Identity_PasswordResets_Identity_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalSchema: "identity",
                        principalTable: "Identity_AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Identity_UserSessions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_UserSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Identity_UserSessions_Identity_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalSchema: "identity",
                        principalTable: "Identity_AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Platform_AuditDeliveries",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platform_AuditDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Platform_AuditDeliveries_Audit_Events_AuditEventId",
                        column: x => x.AuditEventId,
                        principalSchema: "audit",
                        principalTable: "Audit_Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Platform_Outbox",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuditEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Platform_Outbox", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Platform_Outbox_Audit_Events_AuditEventId",
                        column: x => x.AuditEventId,
                        principalSchema: "audit",
                        principalTable: "Audit_Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Identity_RefreshTokens",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Identity_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Identity_RefreshTokens_Identity_UserSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "identity",
                        principalTable: "Identity_UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Identity_PasswordResets_AppUserId",
                schema: "identity",
                table: "Identity_PasswordResets",
                column: "AppUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_PasswordResets_TokenHash",
                schema: "identity",
                table: "Identity_PasswordResets",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Identity_RefreshTokens_SessionId",
                schema: "identity",
                table: "Identity_RefreshTokens",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Identity_RefreshTokens_TokenHash",
                schema: "identity",
                table: "Identity_RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Identity_UserSessions_AppUserId_ExpiresAtUtc",
                schema: "identity",
                table: "Identity_UserSessions",
                columns: new[] { "AppUserId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Platform_AuditDeliveries_AuditEventId",
                schema: "platform",
                table: "Platform_AuditDeliveries",
                column: "AuditEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Platform_Outbox_AuditEventId",
                schema: "platform",
                table: "Platform_Outbox",
                column: "AuditEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Platform_Outbox_DispatchedAtUtc_CreatedAtUtc",
                schema: "platform",
                table: "Platform_Outbox",
                columns: new[] { "DispatchedAtUtc", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Identity_PasswordResets",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Identity_RefreshTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Platform_AuditDeliveries",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Platform_Outbox",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "Identity_UserSessions",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                schema: "identity",
                table: "Identity_AppUsers");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                schema: "identity",
                table: "Identity_AppUsers");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                schema: "identity",
                table: "Identity_AppUsers");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                schema: "audit",
                table: "Audit_Events");
        }
    }
}
