using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Plantilla visual del Menu Board: Affiliates.BoardLayout (default 0 = Classic, igual que hoy)
    /// y Screens.Layout (null = hereda del negocio). Escrita a mano (sin Designer), mismo patrón
    /// que AddCollectedReferenceToOrders.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261013150000_AddBoardLayout")]
    public partial class AddBoardLayout : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BoardLayout",
                table: "Affiliates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Layout",
                table: "Screens",
                type: "integer",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Layout", table: "Screens");
            migrationBuilder.DropColumn(name: "BoardLayout", table: "Affiliates");
        }
    }
}
