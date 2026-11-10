using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "DeliveryRoutes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RouteTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: true),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    DaysMask = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteTemplates_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RouteTemplates_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RouteTemplateStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteTemplateStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RouteTemplateStops_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RouteTemplateStops_RouteTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "RouteTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryRoutes_TemplateId_RouteDate",
                table: "DeliveryRoutes",
                columns: new[] { "TemplateId", "RouteDate" },
                unique: true,
                filter: "\"TemplateId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTemplates_AffiliateId",
                table: "RouteTemplates",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTemplates_VehicleId",
                table: "RouteTemplates",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTemplateStops_CustomerId",
                table: "RouteTemplateStops",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_RouteTemplateStops_TemplateId_SortOrder",
                table: "RouteTemplateStops",
                columns: new[] { "TemplateId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RouteTemplateStops");

            migrationBuilder.DropTable(
                name: "RouteTemplates");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryRoutes_TemplateId_RouteDate",
                table: "DeliveryRoutes");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "DeliveryRoutes");
        }
    }
}
