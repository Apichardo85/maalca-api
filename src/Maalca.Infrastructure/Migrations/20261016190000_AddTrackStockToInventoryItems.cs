using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// InventoryItems.TrackStock: interruptor opt-in para mostrar Agotado/Quedan N en la tienda
    /// pública y rechazar pedidos que superen el stock. Default false (los items existentes no
    /// cambian de comportamiento). Escrita a mano (sin Designer), mismo patrón que AddBoardLayout.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261016190000_AddTrackStockToInventoryItems")]
    public partial class AddTrackStockToInventoryItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TrackStock",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TrackStock", table: "InventoryItems");
        }
    }
}
