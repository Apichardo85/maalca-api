using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateStripeConnectFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StripeConnectAccountId",
                table: "Affiliates",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StripeConnectChargesEnabled",
                table: "Affiliates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StripeConnectPayoutsEnabled",
                table: "Affiliates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StripeConnectDetailsSubmitted",
                table: "Affiliates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StripeConnectUpdatedAt",
                table: "Affiliates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Affiliates_StripeConnectAccountId",
                table: "Affiliates",
                column: "StripeConnectAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Affiliates_StripeConnectAccountId",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "StripeConnectAccountId",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "StripeConnectChargesEnabled",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "StripeConnectPayoutsEnabled",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "StripeConnectDetailsSubmitted",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "StripeConnectUpdatedAt",
                table: "Affiliates");
        }
    }
}
