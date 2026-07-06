using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateProcessStepsAndFaq : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Faq",
                table: "Affiliates",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessSteps",
                table: "Affiliates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Faq",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "ProcessSteps",
                table: "Affiliates");
        }
    }
}
