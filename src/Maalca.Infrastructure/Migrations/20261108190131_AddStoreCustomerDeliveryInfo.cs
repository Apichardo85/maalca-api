using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreCustomerDeliveryInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "StoreCustomers",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryFrequency",
                table: "StoreCustomers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GpsLat",
                table: "StoreCustomers",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GpsLng",
                table: "StoreCustomers",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WantsCredit",
                table: "StoreCustomers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "StoreCustomers");

            migrationBuilder.DropColumn(
                name: "DeliveryFrequency",
                table: "StoreCustomers");

            migrationBuilder.DropColumn(
                name: "GpsLat",
                table: "StoreCustomers");

            migrationBuilder.DropColumn(
                name: "GpsLng",
                table: "StoreCustomers");

            migrationBuilder.DropColumn(
                name: "WantsCredit",
                table: "StoreCustomers");
        }
    }
}
