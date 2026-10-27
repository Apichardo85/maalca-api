using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Extras de MaalCa en tabla (PlatformAddOns, editables desde /ops/precios) con los precios
    /// aprobados el 2026-10-27, y Affiliates.ScreensOnly (modo "Solo pantallas": sin setup, sin
    /// catálogo demo). Solo agrega: no toca datos existentes. Escrita a mano (sin Designer), mismo
    /// patrón que AddScreenDevices.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261027170000_AddPlatformAddOnsAndScreensOnly")]
    public partial class AddPlatformAddOnsAndScreensOnly : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(name: "ScreensOnly", table: "Affiliates", type: "boolean", nullable: false, defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlatformAddOns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    NameEs = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NameEn = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DescEs = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    DescEn = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    VolumePrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    VolumeFromQty = table.Column<int>(type: "integer", nullable: true),
                    Unit = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformAddOns", x => x.Id);
                });

            migrationBuilder.CreateIndex(name: "IX_PlatformAddOns_Key", table: "PlatformAddOns", column: "Key", unique: true);

            var now = new DateTime(2026, 10, 27, 17, 0, 0, DateTimeKind.Utc);
            var columns = new[] { "Id", "Key", "NameEs", "NameEn", "DescEs", "DescEn", "Price", "VolumePrice", "VolumeFromQty", "Unit", "SortOrder", "Active", "CreatedAt" };

            // Unit: 0 OneTime · 1 Monthly · 2 PerLocationMonthly · 3 Block5h (ver AddOnUnit).
            migrationBuilder.InsertData(table: "PlatformAddOns", columns: columns, values: new object[]
            {
                new Guid("a1d0c0de-0000-4000-8000-000000000001"), "screen-kit",
                "Kit de pantalla", "Screen kit",
                "Dispositivo de streaming, montaje en pared, configuración e instalación. La TV la pone el negocio.",
                "Streaming device, wall mount, setup and installation. The business provides the TV.",
                249m, null, null, 0, 1, true, now
            });
            migrationBuilder.InsertData(table: "PlatformAddOns", columns: columns, values: new object[]
            {
                new Guid("a1d0c0de-0000-4000-8000-000000000002"), "screen-full-service",
                "Servicio completo de pantallas", "Full screen service",
                "Cargamos y mantenemos tu contenido (menú, promos y comerciales), vigilamos cada TV y reemplazamos el dispositivo en 48 h. El diseño se genera solo desde tu catálogo.",
                "We load and keep your content up to date (menu, promos and ads), watch every TV and replace the device within 48 h. The design is generated from your catalog.",
                39m, null, null, 2, 2, true, now
            });
            migrationBuilder.InsertData(table: "PlatformAddOns", columns: columns, values: new object[]
            {
                new Guid("a1d0c0de-0000-4000-8000-000000000003"), "extra-screen",
                "Pantalla adicional", "Extra screen",
                "Más pantallas de TV para tu menú o anuncios, además de las incluidas en tu plan.",
                "More TV screens for your menu or ads, on top of those included in your plan.",
                15m, 10m, 2, 1, 3, true, now
            });
            migrationBuilder.InsertData(table: "PlatformAddOns", columns: columns, values: new object[]
            {
                new Guid("a1d0c0de-0000-4000-8000-000000000004"), "social-posts",
                "Publicaciones en redes sociales", "Social media posts",
                "8 publicaciones + 1 reel al mes (Instagram + Facebook), con diseño y copy.",
                "8 posts + 1 reel a month (Instagram + Facebook), with design and copy.",
                249m, null, null, 1, 4, true, now
            });
            migrationBuilder.InsertData(table: "PlatformAddOns", columns: columns, values: new object[]
            {
                new Guid("a1d0c0de-0000-4000-8000-000000000005"), "support-247",
                "Soporte 24/7", "24/7 support",
                "Respuesta en menos de 1h, cualquier día, incluidos fines de semana y feriados.",
                "Response in under 1h, any day, including weekends and holidays.",
                200m, null, null, 1, 5, true, now
            });
            migrationBuilder.InsertData(table: "PlatformAddOns", columns: columns, values: new object[]
            {
                new Guid("a1d0c0de-0000-4000-8000-000000000006"), "consulting-block",
                "Bloque de consultoría", "Consulting block",
                "5 horas para integraciones, auditorías o estrategia. No expiran.",
                "5 hours for integrations, audits or strategy. Never expires.",
                400m, null, null, 3, 6, true, now
            });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlatformAddOns");
            migrationBuilder.DropColumn(name: "ScreensOnly", table: "Affiliates");
        }
    }
}
