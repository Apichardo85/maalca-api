using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Módulos por plan (ModuleAccess): tabla ModulePlanRules (plan mínimo editable desde
    /// /ops/modulos; sin filas = defaults de ModuleCatalog) y Affiliates.ModulosCortesia.
    ///
    /// Datos, para no quitarle nada a nadie el día del deploy:
    ///  a) Quien tenía Cocina prendida explícitamente recibe también Estación (antes era el mismo token).
    ///  b) "billing" sale de ModulosActivos (ya no es módulo: Facturación siempre se ve).
    ///  c) Protección: los planes Growth (Entrepreneur) y los Starter con el trial ya vencido quedan
    ///     con cortesía "*" = conservan todo lo que tienen prendido, igual que antes de esta regla.
    ///     Los Starter en trial no cambian hoy (el trial cuenta como Professional) y al vencer ven
    ///     los candados; Professional no pierde nada.
    /// Solo agrega y marca; no borra datos. Escrita a mano (sin Designer), mismo patrón que
    /// AddScreenDevices.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261027180000_ModulePlanRulesAndCortesia")]
    public partial class ModulePlanRulesAndCortesia : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModulePlanRules",
                columns: table => new
                {
                    Token = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MinTier = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulePlanRules", x => x.Token);
                });

            migrationBuilder.AddColumn<string>(name: "ModulosCortesia", table: "Affiliates", type: "character varying(200)", maxLength: 200, nullable: true);

            // a) Cocina → también Estación.
            migrationBuilder.Sql(@"
UPDATE ""Affiliates""
SET ""ModulosActivos"" = ""ModulosActivos"" || ',station'
WHERE ""ModulosActivos"" IS NOT NULL
  AND (',' || ""ModulosActivos"" || ',') LIKE '%,kitchen,%'
  AND (',' || ""ModulosActivos"" || ',') NOT LIKE '%,station,%';");

            // b) Facturación deja de ser módulo.
            migrationBuilder.Sql(@"
UPDATE ""Affiliates""
SET ""ModulosActivos"" = btrim(regexp_replace(',' || ""ModulosActivos"" || ',', ',billing,', ',', 'g'), ',')
WHERE ""ModulosActivos"" IS NOT NULL
  AND (',' || ""ModulosActivos"" || ',') LIKE '%,billing,%';");

            // c) Protección de clientes existentes (Plan: 0 Free · 1 Entrepreneur · 2 Enterprise).
            migrationBuilder.Sql(@"
UPDATE ""Affiliates""
SET ""ModulosCortesia"" = '*'
WHERE ""ModulosCortesia"" IS NULL
  AND (""Plan"" = 1
       OR (""Plan"" = 0 AND COALESCE(""TrialOverrideEndsAt"", ""CreatedAt"" + interval '30 days') < now()));");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ModulosCortesia", table: "Affiliates");
            migrationBuilder.DropTable(name: "ModulePlanRules");
        }
    }
}
