using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Orders.CollectedAt / CollectedMethod: registro del cobro en el local (pagar al recoger / al mesero).
    /// Escrita a mano (sin Designer): el atributo [Migration] la registra y el snapshot ya incluye las columnas.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261004150000_AddCollectedToOrders")]
    public partial class AddCollectedToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CollectedAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectedMethod",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CollectedAt", table: "Orders");
            migrationBuilder.DropColumn(name: "CollectedMethod", table: "Orders");
        }
    }
}
