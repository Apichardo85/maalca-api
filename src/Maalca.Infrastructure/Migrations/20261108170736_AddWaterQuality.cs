using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaterQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WaterMaintenanceLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PerformedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterMaintenanceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterMaintenanceLogs_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WaterQualityTests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LabName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TestType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sample = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParametersJson = table.Column<string>(type: "text", nullable: true),
                    CertificateUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterQualityTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WaterQualityTests_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WaterMaintenanceLogs_AffiliateId_LogDate",
                table: "WaterMaintenanceLogs",
                columns: new[] { "AffiliateId", "LogDate" });

            migrationBuilder.CreateIndex(
                name: "IX_WaterQualityTests_AffiliateId_TestDate",
                table: "WaterQualityTests",
                columns: new[] { "AffiliateId", "TestDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WaterMaintenanceLogs");

            migrationBuilder.DropTable(
                name: "WaterQualityTests");
        }
    }
}
