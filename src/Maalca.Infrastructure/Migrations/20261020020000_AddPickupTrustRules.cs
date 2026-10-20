using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// "No vino" + reglas de confianza para pagar al recoger: Orders.NoShowAt, Customers.NoShows,
    /// Affiliates.PickupNewCustomerLimit (null = sin límite) y Affiliates.PickupNoShowRequiresCard
    /// (true para los negocios existentes: no cambia nada hasta que alguien marque un "No vino").
    /// Escrita a mano (sin Designer), mismo patrón que AddScreenWalls.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261020020000_AddPickupTrustRules")]
    public partial class AddPickupTrustRules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "NoShowAt", table: "Orders", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<int>(name: "NoShows", table: "Customers", type: "integer", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<decimal>(name: "PickupNewCustomerLimit", table: "Affiliates", type: "numeric(10,2)", precision: 10, scale: 2, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "PickupNoShowRequiresCard", table: "Affiliates", type: "boolean", nullable: false, defaultValue: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NoShowAt", table: "Orders");
            migrationBuilder.DropColumn(name: "NoShows", table: "Customers");
            migrationBuilder.DropColumn(name: "PickupNewCustomerLimit", table: "Affiliates");
            migrationBuilder.DropColumn(name: "PickupNoShowRequiresCard", table: "Affiliates");
        }
    }
}
