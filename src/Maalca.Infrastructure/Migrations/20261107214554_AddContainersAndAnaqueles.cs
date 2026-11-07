using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContainersAndAnaqueles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Anaqueles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    QrCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Anaqueles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Anaqueles_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Anaqueles_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContainerTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CapacityLiters = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    Deposit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContainerTypes_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContainerMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnaquelId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BottleId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegisteredBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContainerMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContainerMovements_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContainerMovements_Anaqueles_AnaquelId",
                        column: x => x.AnaquelId,
                        principalTable: "Anaqueles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerMovements_ContainerTypes_ContainerTypeId",
                        column: x => x.ContainerTypeId,
                        principalTable: "ContainerTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContainerMovements_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Anaqueles_AffiliateId_CustomerId",
                table: "Anaqueles",
                columns: new[] { "AffiliateId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Anaqueles_CustomerId",
                table: "Anaqueles",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Anaqueles_QrCode",
                table: "Anaqueles",
                column: "QrCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_AffiliateId_CustomerId_ContainerTypeId",
                table: "ContainerMovements",
                columns: new[] { "AffiliateId", "CustomerId", "ContainerTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_AffiliateId_IdempotencyKey",
                table: "ContainerMovements",
                columns: new[] { "AffiliateId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_AnaquelId",
                table: "ContainerMovements",
                column: "AnaquelId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_ContainerTypeId",
                table: "ContainerMovements",
                column: "ContainerTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerMovements_CustomerId",
                table: "ContainerMovements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ContainerTypes_AffiliateId_Name",
                table: "ContainerTypes",
                columns: new[] { "AffiliateId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContainerMovements");

            migrationBuilder.DropTable(
                name: "Anaqueles");

            migrationBuilder.DropTable(
                name: "ContainerTypes");
        }
    }
}
