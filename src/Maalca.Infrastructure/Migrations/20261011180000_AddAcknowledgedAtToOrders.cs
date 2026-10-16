using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Orders.AcknowledgedAt — Estación de pedidos (Fase 3): cuándo el personal reconoció un
    /// pedido nuevo. Escrita a mano (sin Designer), mismo patrón que AddTrackingToOrders.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261011180000_AddAcknowledgedAtToOrders")]
    public partial class AddAcknowledgedAtToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AcknowledgedAt", table: "Orders");
        }
    }
}
