using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddFinishedGoodsInventoryAndProductionCompletion : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "production_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_inventory_balances",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_product_inventory_balances", x => x.Id);
                    table.CheckConstraint("CK_product_inventory_balances_Quantity_nonnegative", "\"Quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_product_inventory_balances_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_inventory_balances_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_inventory_balances_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_inventory_transactions",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_product_inventory_transactions", x => x.Id);
                    table.CheckConstraint("CK_product_inventory_transactions_Quantity_positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_product_inventory_transactions_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_inventory_transactions_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_inventory_transactions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_inventory_transactions_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_balances_CompanyId_WarehouseId",
                table: "product_inventory_balances",
                columns: new[] { "CompanyId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_balances_CompanyId_WarehouseId_ProductId",
                table: "product_inventory_balances",
                columns: new[] { "CompanyId", "WarehouseId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_balances_ProductId",
                table: "product_inventory_balances",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_balances_WarehouseId",
                table: "product_inventory_balances",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_transactions_company_warehouse_product_created",
                table: "product_inventory_transactions",
                columns: new[] { "CompanyId", "WarehouseId", "ProductId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_transactions_CompanyId_CreatedAt",
                table: "product_inventory_transactions",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_transactions_CompanyId_ReferenceId",
                table: "product_inventory_transactions",
                columns: new[] { "CompanyId", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_transactions_CreatedByUserId",
                table: "product_inventory_transactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_transactions_ProductId",
                table: "product_inventory_transactions",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_inventory_transactions_WarehouseId",
                table: "product_inventory_transactions",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "product_inventory_balances");

            migrationBuilder.DropTable(
                name: "product_inventory_transactions");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "production_orders");
        }
    }
}
