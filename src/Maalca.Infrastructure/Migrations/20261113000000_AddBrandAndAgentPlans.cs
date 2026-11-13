using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Fase 2 del orquestador de agentes: perfil de marca por negocio y plan de agentes por negocio
    /// (cuota de publicaciones, redes, add-on del asistente y su tope de tokens). Solo tablas nuevas.
    /// Escrita a mano, mismo patrón que AddAgentCore.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261113000000_AddBrandAndAgentPlans")]
    public partial class AddBrandAndAgentPlans : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""BrandProfiles"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Voice"" text NULL,
    ""Audience"" text NULL,
    ""Offer"" text NULL,
    ""Dos"" text NULL,
    ""Donts"" text NULL,
    ""SpecialDates"" text NULL,
    ""Hashtags"" text NULL,
    ""Language"" character varying(10) NOT NULL DEFAULT 'es',
    ""Notes"" text NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" character varying(255) NULL,
    CONSTRAINT ""PK_BrandProfiles"" PRIMARY KEY (""Id"")
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_BrandProfiles_AffiliateId"" ON ""BrandProfiles"" (""AffiliateId"");

CREATE TABLE IF NOT EXISTS ""AgentAffiliatePlans"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""PostsPerMonth"" integer NOT NULL DEFAULT 0,
    ""Networks"" character varying(200) NOT NULL DEFAULT '',
    ""AssistantEnabled"" boolean NOT NULL DEFAULT FALSE,
    ""MonthlyTokenCap"" bigint NOT NULL DEFAULT 0,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" character varying(255) NULL,
    CONSTRAINT ""PK_AgentAffiliatePlans"" PRIMARY KEY (""Id"")
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AgentAffiliatePlans_AffiliateId"" ON ""AgentAffiliatePlans"" (""AffiliateId"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""AgentAffiliatePlans"";
DROP TABLE IF EXISTS ""BrandProfiles"";
");
        }
    }
}
