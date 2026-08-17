using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeBlockAndReminder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "Appointments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TimeBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffId = table.Column<Guid>(type: "uuid", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    EndTime = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeBlocks_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TimeBlocks_TeamMembers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "TeamMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeBlocks_AffiliateId_Date",
                table: "TimeBlocks",
                columns: new[] { "AffiliateId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_TimeBlocks_StaffId",
                table: "TimeBlocks",
                column: "StaffId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimeBlocks");

            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "Appointments");
        }
    }
}
