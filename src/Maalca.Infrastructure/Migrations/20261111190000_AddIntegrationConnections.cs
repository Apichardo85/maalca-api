using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// IntegrationConnections: capa común de conexiones con sistemas externos (Clover primero; luego Toast, redes).
    /// Credenciales cifradas con SecretBox. Escrita a mano, mismo patrón que AddCarrierAccounts.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261111190000_AddIntegrationConnections")]
    public partial class AddIntegrationConnections : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""IntegrationConnections"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Provider"" character varying(30) NOT NULL,
    ""Kind"" character varying(20) NOT NULL,
    ""CredentialsEnc"" text NULL,
    ""CredentialsLast4"" character varying(8) NULL,
    ""ExternalId"" character varying(80) NULL,
    ""ExternalName"" character varying(200) NULL,
    ""Status"" character varying(20) NOT NULL,
    ""LastError"" text NULL,
    ""LastSyncAt"" timestamp with time zone NULL,
    ""ExpiresAt"" timestamp with time zone NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_IntegrationConnections"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_IntegrationConnections_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_IntegrationConnections_AffiliateId_Provider"" ON ""IntegrationConnections"" (""AffiliateId"", ""Provider"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""IntegrationConnections"";");
        }
    }
}
