using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Products.CompareAtPrice: precio "antes" (tachado) opcional para ofertas en el menú público
    /// de Restaurant. Nullable, sin default: los platos existentes quedan sin oferta. Escrita a
    /// mano (sin Designer), mismo patrón que AddCompareAtPriceToInventoryItems.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261017150000_AddCompareAtPriceToProducts")]
    public partial class AddCompareAtPriceToProducts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CompareAtPrice",
                table: "Products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CompareAtPrice", table: "Products");
        }
    }
}
