using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Orders.CustomerId (nullable, sin FK): enlaza el pedido con el cliente para el historial.
    /// Escrita a mano (sin Designer): el atributo [Migration] la registra y el snapshot ya incluye la columna.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261002040000_AddCustomerIdToOrders")]
    public partial class AddCustomerIdToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                column: "CustomerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Orders");
        }
    }
}
