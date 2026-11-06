using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// CarrierAccounts (cuenta del negocio con United Bridge: API key cifrada, servicio, caja y origen) +
    /// Orders.LabelUrl / Orders.LabelCost (etiqueta comprada desde MaalCa y su costo real). Escrita a mano, mismo patrón
    /// que AddCommerceFoundations.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261105200000_AddCarrierAccounts")]
    public partial class AddCarrierAccounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""CarrierAccounts"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Provider"" character varying(30) NOT NULL,
    ""ApiKeyEnc"" text NULL,
    ""ApiKeyLast4"" character varying(8) NULL,
    ""Service"" character varying(40) NOT NULL,
    ""Package"" character varying(40) NOT NULL,
    ""LengthIn"" numeric(10,2) NOT NULL DEFAULT 12,
    ""WidthIn"" numeric(10,2) NOT NULL DEFAULT 9,
    ""HeightIn"" numeric(10,2) NOT NULL DEFAULT 6,
    ""BoxWeightOz"" numeric(10,2) NOT NULL DEFAULT 0,
    ""FromName"" text NULL,
    ""FromPhone"" text NULL,
    ""FromAddress1"" text NULL,
    ""FromAddress2"" text NULL,
    ""FromCity"" text NULL,
    ""FromState"" text NULL,
    ""FromZip"" text NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_CarrierAccounts"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_CarrierAccounts_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_CarrierAccounts_AffiliateId_Provider"" ON ""CarrierAccounts"" (""AffiliateId"", ""Provider"");
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""LabelUrl"" text NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""LabelCost"" numeric(18,2) NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Orders"" DROP COLUMN IF EXISTS ""LabelCost"";
ALTER TABLE ""Orders"" DROP COLUMN IF EXISTS ""LabelUrl"";
DROP TABLE IF EXISTS ""CarrierAccounts"";
");
        }
    }
}
