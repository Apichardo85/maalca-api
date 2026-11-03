using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Affiliates.SalesTaxState (2 letras, nullable): estado donde el negocio cobra impuesto; envíos a otro estado
    /// van sin impuesto. Escrita a mano, mismo patrón que AddSalesTax.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261103210000_AddSalesTaxState")]
    public partial class AddSalesTaxState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Affiliates"" ADD COLUMN IF NOT EXISTS ""SalesTaxState"" character varying(2) NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Affiliates"" DROP COLUMN IF EXISTS ""SalesTaxState"";");
        }
    }
}
