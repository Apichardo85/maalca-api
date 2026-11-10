using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerVehicleLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleLeads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleListingId = table.Column<Guid>(type: "uuid", nullable: true),
                    VehicleSummary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    DownPayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TermMonths = table.Column<int>(type: "integer", nullable: true),
                    MonthlyEstimate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    TradeMake = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    TradeModel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TradeYear = table.Column<int>(type: "integer", nullable: true),
                    TradeMileage = table.Column<int>(type: "integer", nullable: true),
                    TradeMileageUnit = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    TradeCondition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TradeImages = table.Column<string>(type: "text", nullable: true),
                    PreferredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InternalNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleLeads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleLeads_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VehicleLeads_VehicleListings_VehicleListingId",
                        column: x => x.VehicleListingId,
                        principalTable: "VehicleListings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLeads_AffiliateId_Kind_VehicleListingId",
                table: "VehicleLeads",
                columns: new[] { "AffiliateId", "Kind", "VehicleListingId" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLeads_AffiliateId_Status_CreatedAt",
                table: "VehicleLeads",
                columns: new[] { "AffiliateId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLeads_VehicleListingId",
                table: "VehicleLeads",
                column: "VehicleListingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleLeads");
        }
    }
}
