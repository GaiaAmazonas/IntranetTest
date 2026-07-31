using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace Gaia.Modules.Inventory.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "brands",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "import_issues",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<string>(type: "text", nullable: false),
                    SourceRow = table.Column<int>(type: "integer", nullable: false),
                    Severity = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_import_issues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "movements",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Actor = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_movements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ClassName = table.Column<string>(type: "text", nullable: false),
                    Subcategory = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ControlLevel = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "items",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCode = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    SerialNumber = table.Column<string>(type: "text", nullable: true),
                    IntakeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Condition = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CostCenter = table.Column<string>(type: "text", nullable: true),
                    Funder = table.Column<string>(type: "text", nullable: true),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_items_brands_BrandId",
                        column: x => x.BrandId,
                        principalSchema: "inventory",
                        principalTable: "brands",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_items_products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "inventory",
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThirdPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ThirdPartyDocument = table.Column<string>(type: "text", nullable: true),
                    CustodianName = table.Column<string>(type: "text", nullable: false),
                    OrganizationalUnitCode = table.Column<string>(type: "text", nullable: true),
                    ActNumber = table.Column<string>(type: "text", nullable: true),
                    AssignedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReturnedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assignments_items_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalSchema: "inventory",
                        principalTable: "items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_InventoryItemId_IsActive",
                schema: "inventory",
                table: "assignments",
                columns: new[] { "InventoryItemId", "IsActive" },
                unique: true,
                filter: "\"IsActive\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_brands_Name",
                schema: "inventory",
                table: "brands",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_issues_BatchId",
                schema: "inventory",
                table: "import_issues",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_items_AssetCode",
                schema: "inventory",
                table: "items",
                column: "AssetCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_items_BrandId",
                schema: "inventory",
                table: "items",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_items_ProductId",
                schema: "inventory",
                table: "items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_items_SerialNumber",
                schema: "inventory",
                table: "items",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_movements_InventoryItemId_OccurredAtUtc",
                schema: "inventory",
                table: "movements",
                columns: new[] { "InventoryItemId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_products_Code",
                schema: "inventory",
                table: "products",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignments",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "import_issues",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "items",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "brands",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "products",
                schema: "inventory");
        }
    }
}
