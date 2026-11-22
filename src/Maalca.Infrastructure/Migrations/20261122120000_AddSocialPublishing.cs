using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Fase 4 del estudio de contenido: publicación en redes. Solo AGREGA columnas a ContentItems (expandir):
    /// cuándo y dónde quedó publicado el post, y el último error con su contador de intentos.
    /// Las credenciales de la página van en IntegrationConnections (Provider = "facebook", Kind = "social"),
    /// que ya existe. Escrita a mano, mismo patrón que AddContentStudio.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261122120000_AddSocialPublishing")]
    public partial class AddSocialPublishing : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""ContentItems"" ADD COLUMN IF NOT EXISTS ""PublishedAt"" timestamp with time zone NULL;
ALTER TABLE ""ContentItems"" ADD COLUMN IF NOT EXISTS ""ExternalPostId"" character varying(120) NULL;
ALTER TABLE ""ContentItems"" ADD COLUMN IF NOT EXISTS ""ExternalUrl"" character varying(500) NULL;
ALTER TABLE ""ContentItems"" ADD COLUMN IF NOT EXISTS ""PublishError"" text NULL;
ALTER TABLE ""ContentItems"" ADD COLUMN IF NOT EXISTS ""PublishAttempts"" integer NOT NULL DEFAULT 0;
ALTER TABLE ""ContentItems"" ADD COLUMN IF NOT EXISTS ""LastPublishAttemptAt"" timestamp with time zone NULL;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""ContentItems"" DROP COLUMN IF EXISTS ""LastPublishAttemptAt"";
ALTER TABLE ""ContentItems"" DROP COLUMN IF EXISTS ""PublishAttempts"";
ALTER TABLE ""ContentItems"" DROP COLUMN IF EXISTS ""PublishError"";
ALTER TABLE ""ContentItems"" DROP COLUMN IF EXISTS ""ExternalUrl"";
ALTER TABLE ""ContentItems"" DROP COLUMN IF EXISTS ""ExternalPostId"";
ALTER TABLE ""ContentItems"" DROP COLUMN IF EXISTS ""PublishedAt"";
");
        }
    }
}
