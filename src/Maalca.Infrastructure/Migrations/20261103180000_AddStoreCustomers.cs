using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Cuentas de clientes dentro de una tienda (miembros/mayoristas):
    ///  - Tabla StoreCustomers (una fila por persona y tienda; única por AffiliateId + SupabaseUserId).
    ///  - Affiliates: StoreSignupMode (default "approval"), StoreDefaultPriceListId, StoreSignupNote.
    ///  - Orders.StoreCustomerId (para "Mis pedidos").
    /// Escrita a mano (sin Designer), mismo patrón que AddCommerceFoundations; IF NOT EXISTS.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261103180000_AddStoreCustomers")]
    public partial class AddStoreCustomers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Affiliates"" ADD COLUMN IF NOT EXISTS ""StoreSignupMode"" character varying(20) NOT NULL DEFAULT 'approval';
ALTER TABLE ""Affiliates"" ADD COLUMN IF NOT EXISTS ""StoreDefaultPriceListId"" uuid NULL;
ALTER TABLE ""Affiliates"" ADD COLUMN IF NOT EXISTS ""StoreSignupNote"" character varying(500) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""StoreCustomerId"" uuid NULL;
CREATE INDEX IF NOT EXISTS ""IX_Orders_StoreCustomerId"" ON ""Orders"" (""StoreCustomerId"");

CREATE TABLE IF NOT EXISTS ""StoreCustomers"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""SupabaseUserId"" character varying(64) NOT NULL,
    ""Email"" character varying(200) NOT NULL,
    ""Name"" character varying(120) NOT NULL,
    ""Phone"" character varying(30) NULL,
    ""BusinessName"" character varying(160) NULL,
    ""TaxId"" character varying(40) NULL,
    ""Message"" character varying(500) NULL,
    ""Status"" character varying(20) NOT NULL DEFAULT 'pending',
    ""PriceListId"" uuid NULL,
    ""Notes"" character varying(1000) NULL,
    ""ApprovedAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_StoreCustomers"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_StoreCustomers_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_StoreCustomers_PriceLists_PriceListId"" FOREIGN KEY (""PriceListId"") REFERENCES ""PriceLists"" (""Id"") ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_StoreCustomers_AffiliateId_SupabaseUserId"" ON ""StoreCustomers"" (""AffiliateId"", ""SupabaseUserId"");
CREATE INDEX IF NOT EXISTS ""IX_StoreCustomers_AffiliateId_Status"" ON ""StoreCustomers"" (""AffiliateId"", ""Status"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""StoreCustomers"";
DROP INDEX IF EXISTS ""IX_Orders_StoreCustomerId"";
ALTER TABLE ""Orders"" DROP COLUMN IF EXISTS ""StoreCustomerId"";
ALTER TABLE ""Affiliates"" DROP COLUMN IF EXISTS ""StoreSignupNote"", DROP COLUMN IF EXISTS ""StoreDefaultPriceListId"", DROP COLUMN IF EXISTS ""StoreSignupMode"";
");
        }
    }
}
