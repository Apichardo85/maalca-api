using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Parada genérica (desacople de rutas, pieza 4): RouteStops.Kind ("delivery" por defecto, "pickup"),
    /// RouteStops.SubjectType/SubjectId (a qué pertenece la parada) y RouteStopLines (lo planificado por
    /// parada). Solo agrega: las rutas existentes quedan en "delivery" y se comportan igual.
    /// Escrita a mano, mismo patrón que AddIntegrationConnections.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261125160000_AddRouteStopKindAndPlannedLines")]
    public partial class AddRouteStopKindAndPlannedLines : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""RouteStops"" ADD COLUMN IF NOT EXISTS ""Kind"" character varying(20) NOT NULL DEFAULT 'delivery';");
            migrationBuilder.Sql(@"ALTER TABLE ""RouteStops"" ADD COLUMN IF NOT EXISTS ""SubjectType"" character varying(40) NULL;");
            migrationBuilder.Sql(@"ALTER TABLE ""RouteStops"" ADD COLUMN IF NOT EXISTS ""SubjectId"" uuid NULL;");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_RouteStops_AffiliateId_SubjectType_SubjectId"" ON ""RouteStops"" (""AffiliateId"", ""SubjectType"", ""SubjectId"");");
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""RouteStopLines"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""RouteStopId"" uuid NOT NULL,
    ""Description"" character varying(200) NOT NULL,
    ""Quantity"" numeric(18,2) NOT NULL DEFAULT 1,
    ""Notes"" character varying(500) NULL,
    ""SortOrder"" integer NOT NULL DEFAULT 0,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_RouteStopLines"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_RouteStopLines_RouteStops_RouteStopId"" FOREIGN KEY (""RouteStopId"") REFERENCES ""RouteStops"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_RouteStopLines_RouteStopId_SortOrder"" ON ""RouteStopLines"" (""RouteStopId"", ""SortOrder"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""RouteStopLines"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_RouteStops_AffiliateId_SubjectType_SubjectId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""RouteStops"" DROP COLUMN IF EXISTS ""SubjectId"";");
            migrationBuilder.Sql(@"ALTER TABLE ""RouteStops"" DROP COLUMN IF EXISTS ""SubjectType"";");
            migrationBuilder.Sql(@"ALTER TABLE ""RouteStops"" DROP COLUMN IF EXISTS ""Kind"";");
        }
    }
}
