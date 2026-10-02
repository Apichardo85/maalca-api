using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Affiliate.CategoryTranslations (JSON, nullable): traducción es/en de los nombres de categoría del catálogo.
    /// Sin backfill: null = la web muestra el nombre guardado. Escrita a mano (sin Designer) e idempotente.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261002160000_AddCategoryTranslationsToAffiliates")]
    public partial class AddCategoryTranslationsToAffiliates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" ADD COLUMN IF NOT EXISTS \"CategoryTranslations\" text NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Affiliates\" DROP COLUMN IF EXISTS \"CategoryTranslations\";");
        }
    }
}
