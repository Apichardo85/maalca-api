using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityCausasImpact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Causas",
                table: "Affiliates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunityImpact",
                table: "Affiliates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Causas",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "CommunityImpact",
                table: "Affiliates");
        }
    }
}
