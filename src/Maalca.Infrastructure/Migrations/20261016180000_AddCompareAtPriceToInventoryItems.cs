using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// InventoryItems.CompareAtPrice: precio "antes" (tachado) opcional para ofertas en la tienda
    /// pública. Nullable, sin default: los items existentes quedan sin oferta. Escrita a mano
    /// (sin Designer), mismo patrón que AddBoardLayout.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261016180000_AddCompareAtPriceToInventoryItems")]
    public partial class AddCompareAtPriceToInventoryItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CompareAtPrice",
                table: "InventoryItems",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CompareAtPrice", table: "InventoryItems");
        }
    }
}
