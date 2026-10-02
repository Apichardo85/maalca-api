using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// CommunitySignups (inscripciones públicas de Comunidad: voluntarios y eventos) y
    /// Activities.Capacity (cupo opcional del evento). Tabla/columna nuevas, sin backfill. Escrita a
    /// mano (sin Designer) e idempotente: el atributo [Migration] la registra.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261002200000_AddCommunitySignups")]
    public partial class AddCommunitySignups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Activities"" ADD COLUMN IF NOT EXISTS ""Capacity"" integer NULL;

CREATE TABLE IF NOT EXISTS ""CommunitySignups"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Kind"" character varying(20) NOT NULL,
    ""CausaId"" uuid NULL,
    ""ActivityId"" uuid NULL,
    ""TargetTitle"" character varying(200) NOT NULL,
    ""Name"" character varying(100) NOT NULL,
    ""Phone"" character varying(30) NULL,
    ""Email"" character varying(200) NULL,
    ""PartySize"" integer NOT NULL,
    ""Notes"" character varying(500) NULL,
    ""Language"" character varying(5) NOT NULL,
    ""Status"" character varying(20) NOT NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_CommunitySignups"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_CommunitySignups_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_CommunitySignups_AffiliateId_Status_CreatedAt"" ON ""CommunitySignups"" (""AffiliateId"", ""Status"", ""CreatedAt"");
CREATE INDEX IF NOT EXISTS ""IX_CommunitySignups_ActivityId"" ON ""CommunitySignups"" (""ActivityId"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"CommunitySignups\"; ALTER TABLE \"Activities\" DROP COLUMN IF EXISTS \"Capacity\";");
        }
    }
}
