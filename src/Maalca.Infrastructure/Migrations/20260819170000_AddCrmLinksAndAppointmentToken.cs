using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmLinksAndAppointmentToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tarea #246 — token público para "gestiona tu cita" en citas que ya existen. Postgres
            // 13+ trae gen_random_uuid() en core (sin extensión) — se usa como default SQL solo
            // para rellenar las filas existentes; las citas nuevas siempre mandan su propio valor
            // desde Appointment.Token = Guid.NewGuid() (ver entidad).
            migrationBuilder.AddColumn<Guid>(
                name: "Token",
                table: "Appointments",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Token",
                table: "Appointments",
                column: "Token",
                unique: true);

            // Tarea #244 — vínculo opcional a Customer, resuelto/creado por teléfono en el
            // service layer (no hay dato que backfillear acá: las filas existentes se quedan
            // en NULL y el próximo cambio de estado o edición las vincula si trae teléfono).
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "QueueEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_CustomerId",
                table: "QueueEntries",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_QueueEntries_Customers_CustomerId",
                table: "QueueEntries",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "TableReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TableReservations_CustomerId",
                table: "TableReservations",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_TableReservations_Customers_CustomerId",
                table: "TableReservations",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "Proposals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_CustomerId",
                table: "Proposals",
                column: "CustomerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Proposals_Customers_CustomerId",
                table: "Proposals",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proposals_Customers_CustomerId",
                table: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_CustomerId",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Proposals");

            migrationBuilder.DropForeignKey(
                name: "FK_TableReservations_Customers_CustomerId",
                table: "TableReservations");

            migrationBuilder.DropIndex(
                name: "IX_TableReservations_CustomerId",
                table: "TableReservations");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "TableReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_QueueEntries_Customers_CustomerId",
                table: "QueueEntries");

            migrationBuilder.DropIndex(
                name: "IX_QueueEntries_CustomerId",
                table: "QueueEntries");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "QueueEntries");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_Token",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "Appointments");
        }
    }
}
