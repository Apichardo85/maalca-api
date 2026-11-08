using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContractAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Contracts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedByEmail",
                table: "Contracts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedByName",
                table: "Contracts",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedByStoreCustomerId",
                table: "Contracts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedDocumentHash",
                table: "Contracts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AcceptedByEmail",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AcceptedByName",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AcceptedByStoreCustomerId",
                table: "Contracts");

            migrationBuilder.DropColumn(
                name: "AcceptedDocumentHash",
                table: "Contracts");
        }
    }
}
