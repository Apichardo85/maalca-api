using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Pedido desde la mesa: Orders.TableNumber (nullable, sin backfill — null = pedido normal).
    /// Escrita a mano (sin Designer): el atributo [Migration] la registra y el snapshot ya incluye la columna.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261002030000_AddTableNumberToOrders")]
    public partial class AddTableNumberToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TableNumber",
                table: "Orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TableNumber",
                table: "Orders");
        }
    }
}
