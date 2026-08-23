using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualBookingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ZoomLink",
                table: "Affiliates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Modality",
                table: "Services",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsVirtual",
                table: "Appointments",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVirtual",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Modality",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ZoomLink",
                table: "Affiliates");
        }
    }
}
