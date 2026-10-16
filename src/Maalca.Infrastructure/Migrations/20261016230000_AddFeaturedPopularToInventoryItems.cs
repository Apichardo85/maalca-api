using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// InventoryItems.Featured / Popular: destacados e insignia "Popular" para la tienda Retail
    /// (antes solo existían en Product). Default false. Escrita a mano (sin Designer).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261016230000_AddFeaturedPopularToInventoryItems")]
    public partial class AddFeaturedPopularToInventoryItems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "Featured", table: "InventoryItems", type: "boolean", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<bool>(name: "Popular", table: "InventoryItems", type: "boolean", nullable: false, defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Popular", table: "InventoryItems");
            migrationBuilder.DropColumn(name: "Featured", table: "InventoryItems");
        }
    }
}
