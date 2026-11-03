using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Affiliates.StoreLayout ("boutique" | "marketplace", plantilla pública de la tienda) e
    /// InventoryItems.VolumeTiers (JSON con el precio por cantidad). Escrita a mano, mismo patrón que AddSalesTax.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261103230000_AddStoreLayoutAndVolumeTiers")]
    public partial class AddStoreLayoutAndVolumeTiers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Affiliates"" ADD COLUMN IF NOT EXISTS ""StoreLayout"" character varying(20) NOT NULL DEFAULT 'boutique';
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""VolumeTiers"" text NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Affiliates"" DROP COLUMN IF EXISTS ""StoreLayout"";
ALTER TABLE ""InventoryItems"" DROP COLUMN IF EXISTS ""VolumeTiers"";
");
        }
    }
}
