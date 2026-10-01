using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSentAtToProposalsAndInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Proposals",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill: sin esto el primer barrido de los crons de recordatorios mandaría un
            // correo por CADA factura Pending/Overdue y CADA propuesta Sent sin firmar de todo el
            // historial. Se marca el historial existente como ya recordado; solo lo que se cree
            // o venza a partir de ahora entra en el barrido.
            migrationBuilder.Sql(
                "UPDATE \"Invoices\" SET \"ReminderSentAt\" = NOW() " +
                "WHERE \"ReminderSentAt\" IS NULL AND \"Status\" IN ('Pending', 'Overdue');");

            migrationBuilder.Sql(
                "UPDATE \"Proposals\" SET \"ReminderSentAt\" = NOW() " +
                "WHERE \"ReminderSentAt\" IS NULL AND \"Status\" = 'Sent';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Invoices");
        }
    }
}
