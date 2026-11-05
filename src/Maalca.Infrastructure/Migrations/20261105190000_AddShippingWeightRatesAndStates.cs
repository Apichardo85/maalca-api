using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// ShippingMethods.WeightRates ("1:6.50; 3:9.99; 10:18" = hasta N lb : precio) y ShippingMethods.States
    /// ("NJ,NY,PA"): tarifa por peso del pedido y zona por estado, ambos opcionales. Escrita a mano, mismo patrón
    /// que AddSalesTaxState.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261105190000_AddShippingWeightRatesAndStates")]
    public partial class AddShippingWeightRatesAndStates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""ShippingMethods"" ADD COLUMN IF NOT EXISTS ""WeightRates"" text NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""ShippingMethods"" ADD COLUMN IF NOT EXISTS ""States"" text NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""ShippingMethods"" DROP COLUMN IF EXISTS ""States"";");
            migrationBuilder.Sql(@"ALTER TABLE ""ShippingMethods"" DROP COLUMN IF EXISTS ""WeightRates"";");
        }
    }
}
