using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Facturación completa: Affiliates.ExtraScreens (pantallas pagadas aparte), BillingInterval
    /// ("month" | "year") y SetupFeePaidAt (el setup se cobra una sola vez). Escrita a mano (sin
    /// Designer), mismo patrón que AddScreenWalls.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261020150000_AddBillingScreens")]
    public partial class AddBillingScreens : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ExtraScreens", table: "Affiliates", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<string>(name: "BillingInterval", table: "Affiliates", type: "character varying(8)", maxLength: 8, nullable: true);
            migrationBuilder.AddColumn<DateTime>(name: "SetupFeePaidAt", table: "Affiliates", type: "timestamp with time zone", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ExtraScreens", table: "Affiliates");
            migrationBuilder.DropColumn(name: "BillingInterval", table: "Affiliates");
            migrationBuilder.DropColumn(name: "SetupFeePaidAt", table: "Affiliates");
        }
    }
}
