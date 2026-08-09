using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateLanguageAndBoardTheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Affiliates",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "es");

            migrationBuilder.AddColumn<int>(
                name: "BoardTheme",
                table: "Affiliates",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoardTheme",
                table: "Affiliates");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Affiliates");
        }
    }
}
