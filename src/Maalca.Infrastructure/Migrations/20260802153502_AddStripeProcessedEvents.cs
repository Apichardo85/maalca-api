using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeProcessedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StripeProcessedEvents",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StripeProcessedEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Affiliates_StripeCustomerId",
                table: "Affiliates",
                column: "StripeCustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StripeProcessedEvents");

            migrationBuilder.DropIndex(
                name: "IX_Affiliates_StripeCustomerId",
                table: "Affiliates");
        }
    }
}
