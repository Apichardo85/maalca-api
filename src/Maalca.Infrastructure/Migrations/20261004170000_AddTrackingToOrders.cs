using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Orders.TrackingToken (único, para /t/{token}) + EstimatedReadyAt (hora estimada que fija el personal).
    /// Escrita a mano (sin Designer): el atributo [Migration] la registra y el snapshot ya incluye las columnas.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261004170000_AddTrackingToOrders")]
    public partial class AddTrackingToOrders : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingToken",
                table: "Orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EstimatedReadyAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TrackingToken",
                table: "Orders",
                column: "TrackingToken",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Orders_TrackingToken", table: "Orders");
            migrationBuilder.DropColumn(name: "TrackingToken", table: "Orders");
            migrationBuilder.DropColumn(name: "EstimatedReadyAt", table: "Orders");
        }
    }
}
