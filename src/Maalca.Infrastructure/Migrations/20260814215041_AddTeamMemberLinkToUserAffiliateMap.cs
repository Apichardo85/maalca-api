using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamMemberLinkToUserAffiliateMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TeamMemberId",
                table: "UserAffiliateMaps",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserAffiliateMaps_TeamMemberId",
                table: "UserAffiliateMaps",
                column: "TeamMemberId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAffiliateMaps_TeamMembers_TeamMemberId",
                table: "UserAffiliateMaps",
                column: "TeamMemberId",
                principalTable: "TeamMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserAffiliateMaps_TeamMembers_TeamMemberId",
                table: "UserAffiliateMaps");

            migrationBuilder.DropIndex(
                name: "IX_UserAffiliateMaps_TeamMemberId",
                table: "UserAffiliateMaps");

            migrationBuilder.DropColumn(
                name: "TeamMemberId",
                table: "UserAffiliateMaps");
        }
    }
}
