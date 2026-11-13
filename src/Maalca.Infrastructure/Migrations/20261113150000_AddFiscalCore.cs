using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Facturación electrónica (e-CF, DGII): perfil fiscal por negocio, secuencias de e-NCF, comprobantes, líneas,
    /// historial de estados y bitácora del webhook del proveedor. Escrita a mano (mismo patrón que
    /// AddIntegrationConnections): no toca el snapshot. Idempotente.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261113150000_AddFiscalCore")]
    public partial class AddFiscalCore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""FiscalProfiles"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Rnc"" character varying(11) NOT NULL,
    ""LegalName"" character varying(200) NOT NULL,
    ""TradeName"" character varying(200) NULL,
    ""Address"" character varying(100) NOT NULL,
    ""Province"" character varying(80) NULL,
    ""Municipality"" character varying(80) NULL,
    ""Email"" character varying(80) NULL,
    ""Environment"" character varying(12) NOT NULL,
    ""Status"" character varying(20) NOT NULL,
    ""DefaultBillingIndicator"" integer NOT NULL DEFAULT 4,
    ""DefaultGoodService"" integer NOT NULL DEFAULT 1,
    ""ProviderCompanyId"" character varying(80) NULL,
    ""ProviderRegisteredAt"" timestamp with time zone NULL,
    ""ReceptionUrl"" text NULL,
    ""ApprovalUrl"" text NULL,
    ""AuthenticationUrl"" text NULL,
    ""CertificateIssuer"" character varying(200) NULL,
    ""CertificateStartsAt"" timestamp with time zone NULL,
    ""CertificateExpiresAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_FiscalProfiles"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_FiscalProfiles_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_FiscalProfiles_AffiliateId"" ON ""FiscalProfiles"" (""AffiliateId"");

CREATE TABLE IF NOT EXISTS ""FiscalSequences"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""DocType"" integer NOT NULL,
    ""NextNumber"" bigint NOT NULL,
    ""LastNumber"" bigint NOT NULL,
    ""ExpiresOn"" date NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_FiscalSequences"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_FiscalSequences_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_FiscalSequences_AffiliateId_DocType"" ON ""FiscalSequences"" (""AffiliateId"", ""DocType"");

CREATE TABLE IF NOT EXISTS ""ElectronicInvoices"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""OrderId"" uuid NULL,
    ""DocType"" integer NOT NULL,
    ""Encf"" character varying(13) NOT NULL,
    ""Status"" character varying(20) NOT NULL,
    ""LegalStatus"" character varying(40) NULL,
    ""ProviderId"" character varying(80) NULL,
    ""BuyerRnc"" character varying(11) NULL,
    ""BuyerName"" character varying(200) NULL,
    ""Currency"" character varying(3) NOT NULL,
    ""Subtotal"" numeric(18,2) NOT NULL,
    ""Tax"" numeric(18,2) NOT NULL,
    ""NonBillable"" numeric(18,2) NOT NULL,
    ""Total"" numeric(18,2) NOT NULL,
    ""StampDate"" date NOT NULL,
    ""PaymentMethod"" integer NOT NULL DEFAULT 1,
    ""XmlUrl"" text NULL,
    ""PdfUrl"" text NULL,
    ""SecurityCode"" character varying(40) NULL,
    ""StampUrl"" text NULL,
    ""ErrorCode"" character varying(40) NULL,
    ""ErrorMessage"" text NULL,
    ""Attempts"" integer NOT NULL DEFAULT 0,
    ""FinishedAt"" timestamp with time zone NULL,
    ""LastSyncAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_ElectronicInvoices"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ElectronicInvoices_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ElectronicInvoices_AffiliateId_Encf"" ON ""ElectronicInvoices"" (""AffiliateId"", ""Encf"");
CREATE INDEX IF NOT EXISTS ""IX_ElectronicInvoices_AffiliateId_CreatedAt"" ON ""ElectronicInvoices"" (""AffiliateId"", ""CreatedAt"");
CREATE INDEX IF NOT EXISTS ""IX_ElectronicInvoices_ProviderId"" ON ""ElectronicInvoices"" (""ProviderId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ElectronicInvoices_AffiliateId_OrderId_DocType"" ON ""ElectronicInvoices"" (""AffiliateId"", ""OrderId"", ""DocType"") WHERE ""OrderId"" IS NOT NULL AND ""Status"" <> 'rejected';

CREATE TABLE IF NOT EXISTS ""ElectronicInvoiceItems"" (
    ""Id"" uuid NOT NULL,
    ""InvoiceId"" uuid NOT NULL,
    ""LineNumber"" integer NOT NULL,
    ""Name"" character varying(200) NOT NULL,
    ""Quantity"" numeric(18,4) NOT NULL,
    ""UnitPrice"" numeric(18,4) NOT NULL,
    ""Amount"" numeric(18,2) NOT NULL,
    ""BillingIndicator"" integer NOT NULL,
    ""GoodService"" integer NOT NULL DEFAULT 1,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_ElectronicInvoiceItems"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ElectronicInvoiceItems_ElectronicInvoices_InvoiceId"" FOREIGN KEY (""InvoiceId"") REFERENCES ""ElectronicInvoices"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_ElectronicInvoiceItems_InvoiceId"" ON ""ElectronicInvoiceItems"" (""InvoiceId"");

CREATE TABLE IF NOT EXISTS ""ElectronicInvoiceEvents"" (
    ""Id"" uuid NOT NULL,
    ""InvoiceId"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Source"" character varying(12) NOT NULL,
    ""Status"" character varying(20) NOT NULL,
    ""LegalStatus"" character varying(40) NULL,
    ""Detail"" text NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_ElectronicInvoiceEvents"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ElectronicInvoiceEvents_ElectronicInvoices_InvoiceId"" FOREIGN KEY (""InvoiceId"") REFERENCES ""ElectronicInvoices"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_ElectronicInvoiceEvents_InvoiceId"" ON ""ElectronicInvoiceEvents"" (""InvoiceId"");

CREATE TABLE IF NOT EXISTS ""FiscalWebhookEvents"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Payload"" text NOT NULL,
    ""Handled"" boolean NOT NULL DEFAULT FALSE,
    ""Error"" text NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_FiscalWebhookEvents"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_FiscalWebhookEvents_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_FiscalWebhookEvents_AffiliateId_CreatedAt"" ON ""FiscalWebhookEvents"" (""AffiliateId"", ""CreatedAt"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""FiscalWebhookEvents"";
DROP TABLE IF EXISTS ""ElectronicInvoiceEvents"";
DROP TABLE IF EXISTS ""ElectronicInvoiceItems"";
DROP TABLE IF EXISTS ""ElectronicInvoices"";
DROP TABLE IF EXISTS ""FiscalSequences"";
DROP TABLE IF EXISTS ""FiscalProfiles"";
");
        }
    }
}
