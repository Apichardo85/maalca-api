using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAffiliateMapAndSaasFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Affiliates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BusinessType",
                table: "Affiliates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Affiliates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Affiliates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Affiliates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Plan",
                table: "Affiliates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanStartedAt",
                table: "Affiliates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlanStatus",
                table: "Affiliates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "Affiliates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Affiliates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Affiliates",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Affiliates",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Affiliates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsApp",
                table: "Affiliates",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserAffiliateMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupabaseUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAffiliateMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAffiliateMaps_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Affiliates_Slug",
                table: "Affiliates",
                column: "Slug",
                unique: true,
                filter: "\"Slug\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserAffiliateMaps_AffiliateId",
                table: "UserAffiliateMaps",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAffiliateMaps_SupabaseUserId",
                table: "UserAffiliateMaps",
                column: "SupabaseUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAffiliateMaps_SupabaseUserId_AffiliateId",
                table: "UserAffiliateMaps",
                columns: new[] { "SupabaseUserId", "AffiliateId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserAffiliateMaps");

            migrationBuilder.DropIndex(
                name: "IX_Affiliates_Slug",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "Plan",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "PlanStartedAt",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "PlanStatus",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "Published",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "WhatsApp",
                table: "Affiliates");
        }
    }
}
