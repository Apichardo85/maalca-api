using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModifierGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Arreglo 2026-11-23 (arranque en frío del CI): esta migración se generó con un snapshot que no conocía
            // columnas y tablas creadas antes por migraciones escritas a mano (AddNameEn, AddTeamManagement,
            // AddProposalAttachmentAndAuditFields, AddInvoiceVoidAndCorrectionFields, AddInventory*, AddAuditLogEntries,
            // AddProductIngredients). En producción y staging ya está aplicada y este cambio no las toca; en una base
            // vacía las volvía a crear y reventaba ("column HourlyRate already exists"). Lo duplicado queda idempotente.
            migrationBuilder.Sql(@"
ALTER TABLE ""TeamMembers"" ADD COLUMN IF NOT EXISTS ""HourlyRate"" numeric(18,2) NULL;
ALTER TABLE ""TeamMembers"" ADD COLUMN IF NOT EXISTS ""PinCode"" character varying(6) NULL;
ALTER TABLE ""Services"" ADD COLUMN IF NOT EXISTS ""NameEn"" text NULL;
ALTER TABLE ""Proposals"" ADD COLUMN IF NOT EXISTS ""AcceptedIp"" text NULL;
ALTER TABLE ""Proposals"" ADD COLUMN IF NOT EXISTS ""AcceptedUserAgent"" text NULL;
ALTER TABLE ""Proposals"" ADD COLUMN IF NOT EXISTS ""AttachmentName"" text NULL;
ALTER TABLE ""Proposals"" ADD COLUMN IF NOT EXISTS ""AttachmentUrl"" text NULL;
ALTER TABLE ""Products"" ADD COLUMN IF NOT EXISTS ""NameEn"" text NULL;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""ReplacedByInvoiceId"" uuid NULL;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""ReplacesInvoiceId"" uuid NULL;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""VoidReason"" text NULL;
ALTER TABLE ""Invoices"" ADD COLUMN IF NOT EXISTS ""VoidedAt"" timestamp with time zone NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""Barcode"" text NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""InternalCode"" text NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""NameEn"" text NULL;
ALTER TABLE ""InventoryItems"" ADD COLUMN IF NOT EXISTS ""Unit"" character varying(20) NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS ""AuditLogEntries"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NULL,
    ""ActorId"" text NULL,
    ""ActorName"" text NULL,
    ""Action"" text NOT NULL,
    ""EntityType"" text NOT NULL,
    ""EntityId"" uuid NULL,
    ""Description"" text NOT NULL,
    ""MetadataJson"" text NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_AuditLogEntries"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_AuditLogEntries_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"")
);

CREATE TABLE IF NOT EXISTS ""ProductIngredients"" (
    ""Id"" uuid NOT NULL,
    ""ProductId"" uuid NOT NULL,
    ""InventoryItemId"" uuid NOT NULL,
    ""Quantity"" numeric(18,3) NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_ProductIngredients"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ProductIngredients_InventoryItems_InventoryItemId"" FOREIGN KEY (""InventoryItemId"") REFERENCES ""InventoryItems"" (""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_ProductIngredients_Products_ProductId"" FOREIGN KEY (""ProductId"") REFERENCES ""Products"" (""Id"") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ""StaffTasks"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""TeamMemberId"" uuid NULL,
    ""Title"" text NOT NULL,
    ""Description"" text NULL,
    ""Status"" text NOT NULL,
    ""DueDate"" timestamp with time zone NULL,
    ""CompletedAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_StaffTasks"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_StaffTasks_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_StaffTasks_TeamMembers_TeamMemberId"" FOREIGN KEY (""TeamMemberId"") REFERENCES ""TeamMembers"" (""Id"") ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS ""TimeEntries"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""TeamMemberId"" uuid NOT NULL,
    ""ClockIn"" timestamp with time zone NOT NULL,
    ""ClockOut"" timestamp with time zone NULL,
    ""Source"" text NOT NULL,
    ""Notes"" text NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_TimeEntries"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_TimeEntries_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_TimeEntries_TeamMembers_TeamMemberId"" FOREIGN KEY (""TeamMemberId"") REFERENCES ""TeamMembers"" (""Id"") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ""IX_AuditLogEntries_AffiliateId"" ON ""AuditLogEntries"" (""AffiliateId"");
CREATE INDEX IF NOT EXISTS ""IX_ProductIngredients_InventoryItemId"" ON ""ProductIngredients"" (""InventoryItemId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProductIngredients_ProductId_InventoryItemId"" ON ""ProductIngredients"" (""ProductId"", ""InventoryItemId"");
CREATE INDEX IF NOT EXISTS ""IX_StaffTasks_AffiliateId"" ON ""StaffTasks"" (""AffiliateId"");
CREATE INDEX IF NOT EXISTS ""IX_StaffTasks_TeamMemberId"" ON ""StaffTasks"" (""TeamMemberId"");
CREATE INDEX IF NOT EXISTS ""IX_TimeEntries_AffiliateId"" ON ""TimeEntries"" (""AffiliateId"");
CREATE INDEX IF NOT EXISTS ""IX_TimeEntries_TeamMemberId"" ON ""TimeEntries"" (""TeamMemberId"");
");









            migrationBuilder.AlterColumn<string>(
                name: "StripePaymentIntentId",
                table: "Invoices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StripeCheckoutSessionId",
                table: "Invoices",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);










            migrationBuilder.CreateTable(
                name: "ModifierGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NameEn = table.Column<string>(type: "text", nullable: true),
                    MinSelect = table.Column<int>(type: "integer", nullable: false),
                    MaxSelect = table.Column<int>(type: "integer", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifierGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierGroups_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });




            migrationBuilder.CreateTable(
                name: "ModifierOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    NameEn = table.Column<string>(type: "text", nullable: true),
                    PriceDelta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModifierOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModifierOptions_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductModifierGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModifierGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductModifierGroups_ModifierGroups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalTable: "ModifierGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductModifierGroups_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });


            migrationBuilder.CreateIndex(
                name: "IX_ModifierGroups_AffiliateId",
                table: "ModifierGroups",
                column: "AffiliateId");

            migrationBuilder.CreateIndex(
                name: "IX_ModifierOptions_ModifierGroupId",
                table: "ModifierOptions",
                column: "ModifierGroupId");



            migrationBuilder.CreateIndex(
                name: "IX_ProductModifierGroups_ModifierGroupId",
                table: "ProductModifierGroups",
                column: "ModifierGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductModifierGroups_ProductId_ModifierGroupId",
                table: "ProductModifierGroups",
                columns: new[] { "ProductId", "ModifierGroupId" },
                unique: true);



}

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogEntries");

            migrationBuilder.DropTable(
                name: "ModifierOptions");

            migrationBuilder.DropTable(
                name: "ProductIngredients");

            migrationBuilder.DropTable(
                name: "ProductModifierGroups");

            migrationBuilder.DropTable(
                name: "StaffTasks");

            migrationBuilder.DropTable(
                name: "TimeEntries");

            migrationBuilder.DropTable(
                name: "ModifierGroups");

            migrationBuilder.DropColumn(
                name: "HourlyRate",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "PinCode",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "AcceptedIp",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "AcceptedUserAgent",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "AttachmentName",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ReplacedByInvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ReplacesInvoiceId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "InternalCode",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "InventoryItems");

            migrationBuilder.AlterColumn<string>(
                name: "StripePaymentIntentId",
                table: "Invoices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StripeCheckoutSessionId",
                table: "Invoices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
