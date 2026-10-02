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
            migrationBuilder.AddColumn<string>(
                name: "MealPeriodHours",
                table: "Affiliates",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MealPeriodHours",
                table: "Affiliates");
        }
    }
}
