using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDealerVehicleListings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Make = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Model = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Trim = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Condition = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CompareAtPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Mileage = table.Column<int>(type: "integer", nullable: true),
                    MileageUnit = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Transmission = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FuelType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BodyType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Drivetrain = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ExteriorColor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    InteriorColor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Engine = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Doors = table.Column<int>(type: "integer", nullable: true),
                    Passengers = table.Column<int>(type: "integer", nullable: true),
                    Vin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    StockNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ImportOrigin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Featured = table.Column<bool>(type: "boolean", nullable: false),
                    IsPubliclyVisible = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsDemo = table.Column<bool>(type: "boolean", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Images = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DescriptionEn = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleListings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleListings_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleListings_AffiliateId_BodyType",
                table: "VehicleListings",
                columns: new[] { "AffiliateId", "BodyType" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleListings_AffiliateId_IsPubliclyVisible_Status",
                table: "VehicleListings",
                columns: new[] { "AffiliateId", "IsPubliclyVisible", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleListings_AffiliateId_Make_Model",
                table: "VehicleListings",
                columns: new[] { "AffiliateId", "Make", "Model" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleListings_AffiliateId_Price",
                table: "VehicleListings",
                columns: new[] { "AffiliateId", "Price" });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleListings_AffiliateId_Year",
                table: "VehicleListings",
                columns: new[] { "AffiliateId", "Year" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleListings");
        }
    }
}
