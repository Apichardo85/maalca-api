using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Works.CoverUrlEn: portada de la edición en inglés. Solo AGREGA una columna que admite null (expandir).
    /// Escrita a mano e idempotente.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261127170000_AddWorkCoverUrlEn")]
    public partial class AddWorkCoverUrlEn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Works"" ADD COLUMN IF NOT EXISTS ""CoverUrlEn"" character varying(500) NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Works"" DROP COLUMN IF EXISTS ""CoverUrlEn"";");
        }
    }
}
