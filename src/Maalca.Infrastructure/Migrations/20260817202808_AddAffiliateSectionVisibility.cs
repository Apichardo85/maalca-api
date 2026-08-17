using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAffiliateSectionVisibility : Migration
    {
        /// <inheritdoc />
        // 2026-08-17: esta migración se generó (`dotnet ef migrations add`) contra una base
        // local desactualizada, así que EF diffeó de más y metió por accidente operaciones que
        // YA estaban cubiertas por 20260817150000_AddTimeBlockAndReminder (columna
        // ReminderSentAt + tabla TimeBlocks) y 20260817180000_AddProposals (tabla Proposals).
        // En producción eso hacía crashear el contenedor al arrancar ("column already exists")
        // porque Migrate() corre esta migración después de esas dos, que ya crearon todo eso.
        // Se recorta a lo único que de verdad es nuevo acá: SectionVisibility.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SectionVisibility",
                table: "Affiliates",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SectionVisibility",
                table: "Affiliates");
        }
    }
}
