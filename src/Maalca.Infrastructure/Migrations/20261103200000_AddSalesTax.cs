using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Impuesto de venta configurable: Affiliates.SalesTaxRate (%, null = sin configurar) y SalesTaxOnShipping;
    /// PriceLists.TaxExempt (reventa); Orders.TaxRate (la tasa con la que se calculó). Nadie cambia de
    /// comportamiento hasta que el dueño fija una tasa. Escrita a mano, mismo patrón que AddStoreCustomers.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261103200000_AddSalesTax")]
    public partial class AddSalesTax : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Affiliates"" ADD COLUMN IF NOT EXISTS ""SalesTaxRate"" numeric(6,3) NULL;
ALTER TABLE ""Affiliates"" ADD COLUMN IF NOT EXISTS ""SalesTaxOnShipping"" boolean NOT NULL DEFAULT TRUE;
ALTER TABLE ""PriceLists"" ADD COLUMN IF NOT EXISTS ""TaxExempt"" boolean NOT NULL DEFAULT FALSE;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""TaxRate"" numeric(6,3) NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Orders"" DROP COLUMN IF EXISTS ""TaxRate"";
ALTER TABLE ""PriceLists"" DROP COLUMN IF EXISTS ""TaxExempt"";
ALTER TABLE ""Affiliates"" DROP COLUMN IF EXISTS ""SalesTaxOnShipping"", DROP COLUMN IF EXISTS ""SalesTaxRate"";
");
        }
    }
}
