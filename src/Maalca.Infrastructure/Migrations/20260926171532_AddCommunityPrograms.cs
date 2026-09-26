using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityPrograms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Activities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CommunityPrograms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TitleEn = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DescriptionEn = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    GoalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    Schedule = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WeekDays = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VolunteersNeeded = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPrograms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPrograms_Affiliates_AffiliateId",
                        column: x => x.AffiliateId,
                        principalTable: "Affiliates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPrograms_AffiliateId_SortOrder",
                table: "CommunityPrograms",
                columns: new[] { "AffiliateId", "SortOrder" });

            // Copia los Services existentes de afiliados Community (BusinessType = 7) hacia la
            // tabla nueva -- "Programas" ya no lee/escribe Services (ver CatalogCrudService.cs),
            // asi que sin esto los programas cargados antes del rediseno (ej. la tutoria de
            // prueba de NTC) desaparecerian de la pagina publica. Price -> GoalAmount solo si
            // > 0 (el sentinel de "sin meta" en Service es 0, pero GoalAmount null es lo que
            // el frontend espera para no mostrar "Meta: $0.00" falso). Los Services originales
            // quedan intactos, sin usarse.
            migrationBuilder.Sql(@"
                INSERT INTO ""CommunityPrograms""
                    (""Id"", ""AffiliateId"", ""Title"", ""TitleEn"", ""Description"", ""DescriptionEn"",
                     ""ImageUrl"", ""GoalAmount"", ""IsActive"", ""SortOrder"", ""CreatedAt"", ""UpdatedAt"")
                SELECT
                    s.""Id"", s.""AffiliateId"", s.""Name"", s.""NameEn"", s.""Description"", s.""DescriptionEn"",
                    s.""ImageUrl"",
                    CASE WHEN s.""Price"" > 0 THEN s.""Price"" ELSE NULL END,
                    s.""IsActive"", s.""SortOrder"", s.""CreatedAt"", s.""UpdatedAt""
                FROM ""Services"" s
                INNER JOIN ""Affiliates"" a ON a.""Id"" = s.""AffiliateId""
                WHERE a.""BusinessType"" = 7;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityPrograms");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Activities");
        }
    }
}
