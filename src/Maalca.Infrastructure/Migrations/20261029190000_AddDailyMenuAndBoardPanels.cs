using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Menú del día + flyers verticales del board:
    ///  - Affiliates.DailyMenu (JSON, nullable): rotación semanal del menú del día.
    ///  - Affiliates.BoardOrientation (int, default 0 = Landscape) y Affiliates.BoardPanels (JSON, nullable).
    ///  - Screens.Orientation / Screens.Panels (nullable = hereda del negocio).
    /// Sin backfill: nadie cambia de aspecto hasta elegir la plantilla "Flyers verticales" (BoardLayout = 7).
    /// Escrita a mano (sin Designer), mismo patrón que AddMealPeriodHoursToAffiliates; IF NOT EXISTS
    /// para que el arranque no falle si alguna columna ya se creó a mano.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261029190000_AddDailyMenuAndBoardPanels")]
    public partial class AddDailyMenuAndBoardPanels : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" ADD COLUMN IF NOT EXISTS \"DailyMenu\" text NULL;");
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" ADD COLUMN IF NOT EXISTS \"BoardOrientation\" integer NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" ADD COLUMN IF NOT EXISTS \"BoardPanels\" text NULL;");
            migrationBuilder.Sql("ALTER TABLE \"Screens\" ADD COLUMN IF NOT EXISTS \"Orientation\" integer NULL;");
            migrationBuilder.Sql("ALTER TABLE \"Screens\" ADD COLUMN IF NOT EXISTS \"Panels\" text NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Screens\" DROP COLUMN IF EXISTS \"Panels\";");
            migrationBuilder.Sql("ALTER TABLE \"Screens\" DROP COLUMN IF EXISTS \"Orientation\";");
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" DROP COLUMN IF EXISTS \"BoardPanels\";");
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" DROP COLUMN IF EXISTS \"BoardOrientation\";");
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" DROP COLUMN IF EXISTS \"DailyMenu\";");
        }
    }
}
