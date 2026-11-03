using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Bases de tienda (Retail) para venta a miembros/mayoristas y envíos:
    ///  - InventoryItems: marca, peso/medidas, hazmat, advertencia de asfixia, variantes y reglas de cantidad.
    ///  - Orders: lista de precios aplicada, envío (método, costo, dirección) y despacho manual (paquetería + rastreo).
    ///  - Tablas nuevas: PriceLists, PriceListItems, ShippingMethods.
    /// Todo nullable / con default: ningún negocio ni pedido existente cambia de comportamiento.
    /// Escrita a mano (sin Designer), mismo patrón que AddDailyMenuAndBoardPanels; IF NOT EXISTS para que el
    /// arranque no falle si algo ya se creó a mano.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261103050000_AddCommerceFoundations")]
    public partial class AddCommerceFoundations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""Brand"" character varying(120) NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""WeightOz"" numeric(10,2) NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""LengthIn"" numeric(10,2) NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""WidthIn"" numeric(10,2) NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""HeightIn"" numeric(10,2) NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""Hazmat"" character varying(30) NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""ChokingWarning"" character varying(30) NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""VariantGroup"" character varying(80) NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""VariantLabel"" character varying(60) NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""MinOrderQty"" integer NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""OrderQtyMultiple"" integer NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""MaxPerOrder"" integer NULL;

ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""PriceListId"" uuid NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""PriceListName"" character varying(120) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""FulfillmentType"" character varying(20) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShippingMethodId"" uuid NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShippingMethodName"" character varying(120) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShippingCost"" numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShipToName"" character varying(120) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShipToLine1"" character varying(200) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShipToLine2"" character varying(200) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShipToCity"" character varying(100) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShipToState"" character varying(50) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShipToZip"" character varying(20) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShippingCarrier"" character varying(30) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""TrackingNumber"" character varying(80) NULL;
ALTER TABLE ""Orders"" ADD COLUMN IF NOT EXISTS ""ShippedAt"" timestamp with time zone NULL;

CREATE TABLE IF NOT EXISTS ""PriceLists"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Name"" character varying(120) NOT NULL,
    ""Description"" character varying(500) NULL,
    ""DiscountPercent"" numeric(5,2) NOT NULL DEFAULT 0,
    ""MinOrderAmount"" numeric(18,2) NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""SortOrder"" integer NOT NULL DEFAULT 0,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_PriceLists"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_PriceLists_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_PriceLists_AffiliateId"" ON ""PriceLists"" (""AffiliateId"");

CREATE TABLE IF NOT EXISTS ""PriceListItems"" (
    ""Id"" uuid NOT NULL,
    ""PriceListId"" uuid NOT NULL,
    ""ItemId"" uuid NOT NULL,
    ""Price"" numeric(18,2) NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_PriceListItems"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_PriceListItems_PriceLists_PriceListId"" FOREIGN KEY (""PriceListId"") REFERENCES ""PriceLists"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PriceListItems_PriceListId_ItemId"" ON ""PriceListItems"" (""PriceListId"", ""ItemId"");

CREATE TABLE IF NOT EXISTS ""ShippingMethods"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Name"" character varying(120) NOT NULL,
    ""NameEn"" character varying(120) NULL,
    ""Price"" numeric(18,2) NOT NULL DEFAULT 0,
    ""FreeOverAmount"" numeric(18,2) NULL,
    ""MinDays"" integer NULL,
    ""MaxDays"" integer NULL,
    ""AllowsHazmat"" boolean NOT NULL DEFAULT FALSE,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""SortOrder"" integer NOT NULL DEFAULT 0,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_ShippingMethods"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ShippingMethods_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_ShippingMethods_AffiliateId"" ON ""ShippingMethods"" (""AffiliateId"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""ShippingMethods"";
DROP TABLE IF EXISTS ""PriceListItems"";
DROP TABLE IF EXISTS ""PriceLists"";
ALTER TABLE ""Orders"" DROP COLUMN IF EXISTS ""ShippedAt"", DROP COLUMN IF EXISTS ""TrackingNumber"", DROP COLUMN IF EXISTS ""ShippingCarrier"",
  DROP COLUMN IF EXISTS ""ShipToZip"", DROP COLUMN IF EXISTS ""ShipToState"", DROP COLUMN IF EXISTS ""ShipToCity"",
  DROP COLUMN IF EXISTS ""ShipToLine2"", DROP COLUMN IF EXISTS ""ShipToLine1"", DROP COLUMN IF EXISTS ""ShipToName"",
  DROP COLUMN IF EXISTS ""ShippingCost"", DROP COLUMN IF EXISTS ""ShippingMethodName"", DROP COLUMN IF EXISTS ""ShippingMethodId"",
  DROP COLUMN IF EXISTS ""FulfillmentType"", DROP COLUMN IF EXISTS ""PriceListName"", DROP COLUMN IF EXISTS ""PriceListId"";
ALTER TABLE ""InventoryItems"" DROP COLUMN IF EXISTS ""MaxPerOrder"", DROP COLUMN IF EXISTS ""OrderQtyMultiple"", DROP COLUMN IF EXISTS ""MinOrderQty"",
  DROP COLUMN IF EXISTS ""VariantLabel"", DROP COLUMN IF EXISTS ""VariantGroup"", DROP COLUMN IF EXISTS ""ChokingWarning"",
  DROP COLUMN IF EXISTS ""Hazmat"", DROP COLUMN IF EXISTS ""HeightIn"", DROP COLUMN IF EXISTS ""WidthIn"",
  DROP COLUMN IF EXISTS ""LengthIn"", DROP COLUMN IF EXISTS ""WeightOz"", DROP COLUMN IF EXISTS ""Brand"";
");
        }
    }
}
