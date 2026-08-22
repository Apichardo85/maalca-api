using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProposalAttachmentAndAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "Proposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentName",
                table: "Proposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedIp",
                table: "Proposals",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedUserAgent",
                table: "Proposals",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcceptedUserAgent",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "AcceptedIp",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "AttachmentName",
                table: "Proposals");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "Proposals");
        }
    }
}
