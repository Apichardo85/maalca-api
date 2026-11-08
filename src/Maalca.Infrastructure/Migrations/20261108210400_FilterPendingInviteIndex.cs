using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilterPendingInviteIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAffiliateMaps_SupabaseUserId_AffiliateId",
                table: "UserAffiliateMaps");

            migrationBuilder.CreateIndex(
                name: "IX_UserAffiliateMaps_SupabaseUserId_AffiliateId",
                table: "UserAffiliateMaps",
                columns: new[] { "SupabaseUserId", "AffiliateId" },
                unique: true,
                filter: "\"SupabaseUserId\" <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserAffiliateMaps_SupabaseUserId_AffiliateId",
                table: "UserAffiliateMaps");

            migrationBuilder.CreateIndex(
                name: "IX_UserAffiliateMaps_SupabaseUserId_AffiliateId",
                table: "UserAffiliateMaps",
                columns: new[] { "SupabaseUserId", "AffiliateId" },
                unique: true);
        }
    }
}
