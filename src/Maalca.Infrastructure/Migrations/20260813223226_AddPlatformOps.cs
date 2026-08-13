using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ImpersonationExpiresAt",
                table: "UserAffiliateMaps",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsImpersonation",
                table: "UserAffiliateMaps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdminImpersonationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminSupabaseUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AdminEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminImpersonationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformAdmins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupabaseUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAdmins", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminImpersonationLogs_AdminSupabaseUserId",
                table: "AdminImpersonationLogs",
                column: "AdminSupabaseUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminImpersonationLogs_AffiliateId",
                table: "AdminImpersonationLogs",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdmins_Email",
                table: "PlatformAdmins",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlatformAdmins_SupabaseUserId",
                table: "PlatformAdmins",
                column: "SupabaseUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminImpersonationLogs");

            migrationBuilder.DropTable(
                name: "PlatformAdmins");

            migrationBuilder.DropColumn(
                name: "ImpersonationExpiresAt",
                table: "UserAffiliateMaps");

            migrationBuilder.DropColumn(
                name: "IsImpersonation",
                table: "UserAffiliateMaps");
        }
    }
}
