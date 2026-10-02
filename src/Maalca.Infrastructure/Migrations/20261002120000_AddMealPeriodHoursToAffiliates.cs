using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Affiliate.MealPeriodHours (JSON, nullable): horarios por momento de comida del negocio.
    /// Sin backfill: null = la web usa los cortes por defecto, así ningún negocio cambia por esta migración.
    /// Escrita a mano (sin Designer): el atributo [Migration] la registra y el snapshot ya incluye la columna.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261002120000_AddMealPeriodHoursToAffiliates")]
    public partial class AddMealPeriodHoursToAffiliates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS: la columna pudo crearse a mano antes del deploy; la migracion no debe tumbar el arranque.
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" ADD COLUMN IF NOT EXISTS \"MealPeriodHours\" text NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" DROP COLUMN IF EXISTS \"MealPeriodHours\";");
        }
    }
}
