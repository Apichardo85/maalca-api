using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Works.Revision: versión de la ficha para que un guardado con datos viejos no pise uno nuevo (409).
    /// Solo AGREGA una columna con default 0 (expandir). Escrita a mano e idempotente (IF NOT EXISTS).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261127060000_AddWorkRevision")]
    public partial class AddWorkRevision : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Works"" ADD COLUMN IF NOT EXISTS ""Revision"" integer NOT NULL DEFAULT 0;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Works"" DROP COLUMN IF EXISTS ""Revision"";");
        }
    }
}
