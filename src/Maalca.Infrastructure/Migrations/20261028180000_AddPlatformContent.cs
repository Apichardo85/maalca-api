using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// PlatformContent: overrides del copy del sitio de MaalCa, editables desde /ops/sitio sin
    /// desplegar. Key = clave de traducción de la web. Sin filas = la web usa sus textos por
    /// defecto, así que esta tabla arranca vacía y no cambia nada hasta que MaalCa edite algo.
    /// Solo crea la tabla; no toca datos existentes. Escrita a mano (sin Designer), mismo patrón
    /// que AddPlatformAddOnsAndScreensOnly.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261028180000_AddPlatformContent")]
    public partial class AddPlatformContent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Es = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    En = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformContents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlatformContents_Key",
                table: "PlatformContents",
                column: "Key",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlatformContents");
        }
    }
}
