using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryUnitAndRestrictRecipeDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "InventoryItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "unidad");

            // Borrar un ingrediente usado en la receta de un plato dejaba de descontar stock sin
            // avisar (la fila de ProductIngredient desaparecía en cascada). Ahora el borrado falla
            // explícito (ver InventoryService.DeleteInventoryItemAsync) hasta que se quite de la
            // receta primero.
            migrationBuilder.DropForeignKey(
                name: "FK_ProductIngredients_InventoryItems_InventoryItemId",
                table: "ProductIngredients");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductIngredients_InventoryItems_InventoryItemId",
                table: "ProductIngredients",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductIngredients_InventoryItems_InventoryItemId",
                table: "ProductIngredients");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductIngredients_InventoryItems_InventoryItemId",
                table: "ProductIngredients",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "InventoryItems");
        }
    }
}
