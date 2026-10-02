using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Paleta de 3 colores por negocio: Affiliate.AccentColor (nuevo). SecondaryColor ya existía.
    /// Columna nullable sin backfill: null = la plantilla pública deriva el color del primario,
    /// así ningún negocio existente cambia de aspecto por esta migración.
    /// Escrita a mano (sin Designer): el atributo [Migration] la registra y el snapshot ya incluye la columna.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261002013000_AddAccentColorToAffiliates")]
    public partial class AddAccentColorToAffiliates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                table: "Affiliates",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccentColor",
                table: "Affiliates");
        }
    }
}
