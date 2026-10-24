using System;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Monitoreo de pantallas: tabla ScreenDevices (una fila por TV que reporta señal de vida).
    /// Escrita a mano (sin Designer), mismo patrón que AddScreenWalls / AddBillingScreens.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261024040000_AddScreenDevices")]
    public partial class AddScreenDevices : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScreenDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScreenId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Visible = table.Column<bool>(type: "boolean", nullable: false),
                    ShowingKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ShowingLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Viewport = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AppVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PendingCommand = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CommandAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AlertSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreenDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreenDevices_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_ScreenDevices_AffiliateId_DeviceId", table: "ScreenDevices", columns: new[] { "AffiliateId", "DeviceId" });
            migrationBuilder.CreateIndex(name: "IX_ScreenDevices_LastSeenAt", table: "ScreenDevices", column: "LastSeenAt");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ScreenDevices");
        }
    }
}
