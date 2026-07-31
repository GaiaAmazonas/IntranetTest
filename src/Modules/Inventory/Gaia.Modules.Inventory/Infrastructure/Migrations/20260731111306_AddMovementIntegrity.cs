using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gaia.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMovementIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_movements_items_InventoryItemId",
                schema: "inventory",
                table: "movements",
                column: "InventoryItemId",
                principalSchema: "inventory",
                principalTable: "items",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_movements_items_InventoryItemId",
                schema: "inventory",
                table: "movements");
        }
    }
}
