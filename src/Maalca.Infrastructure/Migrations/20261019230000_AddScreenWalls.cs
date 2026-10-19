using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Muros de pantallas sincronizadas: tabla ScreenWalls + Screens.WallId/WallCol/WallRow.
    /// Todo aditivo y nullable — las pantallas existentes quedan sueltas (sin muro), igual que hoy.
    /// Borrar un muro deja sus TVs como pantallas sueltas (FK SetNull), no las borra.
    /// Escrita a mano (sin Designer), mismo patrón que AddCompareAtPriceToProducts.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261019230000_AddScreenWalls")]
    public partial class AddScreenWalls : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScreenWalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Cols = table.Column<int>(type: "integer", nullable: false),
                    Rows = table.Column<int>(type: "integer", nullable: false),
                    BezelPx = table.Column<int>(type: "integer", nullable: false),
                    Calibrate = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenWalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreenWalls_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScreenWalls_AffiliateId",
                table: "ScreenWalls",
                column: "AffiliateId");

            migrationBuilder.AddColumn<Guid>(name: "WallId", table: "Screens", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<int>(name: "WallCol", table: "Screens", type: "integer", nullable: true);
            migrationBuilder.AddColumn<int>(name: "WallRow", table: "Screens", type: "integer", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Screens_WallId",
                table: "Screens",
                column: "WallId");

            migrationBuilder.AddForeignKey(
                name: "FK_Screens_ScreenWalls_WallId",
                table: "Screens",
                column: "WallId",
                principalTable: "ScreenWalls",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Screens_ScreenWalls_WallId", table: "Screens");
            migrationBuilder.DropIndex(name: "IX_Screens_WallId", table: "Screens");
            migrationBuilder.DropColumn(name: "WallId", table: "Screens");
            migrationBuilder.DropColumn(name: "WallCol", table: "Screens");
            migrationBuilder.DropColumn(name: "WallRow", table: "Screens");
            migrationBuilder.DropTable(name: "ScreenWalls");
        }
    }
}
