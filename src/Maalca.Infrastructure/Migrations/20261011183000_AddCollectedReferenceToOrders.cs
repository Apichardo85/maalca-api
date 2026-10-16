using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Orders.CollectedReference — referencia manual y libre al cobro real cuando el negocio
    /// cobra en su propio POS (ej. Toast) en vez de que MaalCa procese el pago: permite anotar el
    /// número de ticket/orden de ese sistema para poder conciliar después. MaalCa nunca mueve el
    /// dinero por aquí, es solo una nota. Escrita a mano (sin Designer), mismo patrón que
    /// AddCollectedToOrders / AddAcknowledgedAtToOrders.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261011183000_AddCollectedReferenceToOrders")]
    public partial class AddCollectedReferenceToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CollectedReference",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CollectedReference", table: "Orders");
        }
    }
}
