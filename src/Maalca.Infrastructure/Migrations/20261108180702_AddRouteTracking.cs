using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ArrivalLat",
                table: "RouteStops",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ArrivalLng",
                table: "RouteStops",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivedAt",
                table: "RouteStops",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartedAt",
                table: "RouteStops",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VehiclePositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Lat = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    Lng = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    AccuracyMeters = table.Column<double>(type: "double precision", nullable: true),
                    SpeedKmh = table.Column<double>(type: "double precision", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehiclePositions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositions_AffiliateId",
                table: "VehiclePositions",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_VehiclePositions_RouteId",
                table: "VehiclePositions",
                column: "RouteId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehiclePositions");

            migrationBuilder.DropColumn(
                name: "ArrivalLat",
                table: "RouteStops");

            migrationBuilder.DropColumn(
                name: "ArrivalLng",
                table: "RouteStops");

            migrationBuilder.DropColumn(
                name: "ArrivedAt",
                table: "RouteStops");

            migrationBuilder.DropColumn(
                name: "DepartedAt",
                table: "RouteStops");
        }
    }
}
