using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Orders.ScheduledFor (date, nullable): pedido programado para la próxima apertura cuando el
    /// negocio estaba cerrado. Escrita a mano (sin Designer): el atributo [Migration] la registra
    /// y el snapshot ya incluye la columna.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261002100000_AddScheduledForToOrders")]
    public partial class AddScheduledForToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduledFor",
                table: "Orders",
                type: "date",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduledFor",
                table: "Orders");
        }
    }
}
