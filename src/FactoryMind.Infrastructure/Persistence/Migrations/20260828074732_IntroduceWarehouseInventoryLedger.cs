using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class IntroduceWarehouseInventoryLedger : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_warehouses_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inventory_balances",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_inventory_balances", x => x.Id);
                    table.CheckConstraint("CK_inventory_balances_Quantity_nonnegative", "\"Quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_inventory_balances_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_balances_materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_balances_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_transactions",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    ReferenceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_inventory_transactions", x => x.Id);
                    table.CheckConstraint("CK_inventory_transactions_Quantity_positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_inventory_transactions_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_inventory_transactions_warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_CompanyId_WarehouseId",
                table: "inventory_balances",
                columns: new[] { "CompanyId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_CompanyId_WarehouseId_MaterialId",
                table: "inventory_balances",
                columns: new[] { "CompanyId", "WarehouseId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_MaterialId",
                table: "inventory_balances",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_balances_WarehouseId",
                table: "inventory_balances",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_CompanyId_CreatedAt",
                table: "inventory_transactions",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_CompanyId_ReferenceId",
                table: "inventory_transactions",
                columns: new[] { "CompanyId", "ReferenceId" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_CompanyId_WarehouseId_MaterialId_Cre~",
                table: "inventory_transactions",
                columns: new[] { "CompanyId", "WarehouseId", "MaterialId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_CreatedByUserId",
                table: "inventory_transactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_MaterialId",
                table: "inventory_transactions",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_inventory_transactions_WarehouseId",
                table: "inventory_transactions",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_CompanyId_Code",
                table: "warehouses",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_CompanyId_Name",
                table: "warehouses",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.Sql("""
                WITH legacy_warehouses AS (
                    SELECT
                        "CompanyId",
                        "Warehouse",
                        MIN("CreatedAt") AS "CreatedAt",
                        ROW_NUMBER() OVER (
                            PARTITION BY "CompanyId"
                            ORDER BY "Warehouse") AS warehouse_number
                    FROM inventories
                    GROUP BY "CompanyId", "Warehouse"
                )
                INSERT INTO warehouses
                    ("Id", "CompanyId", "Code", "Name", "Description", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT
                    md5("CompanyId"::text || ':warehouse:' || "Warehouse")::uuid,
                    "CompanyId",
                    'WH-LEGACY-' || LPAD(warehouse_number::text, 3, '0'),
                    "Warehouse",
                    'Migrated from the legacy inventory warehouse value.',
                    TRUE,
                    "CreatedAt",
                    NOW()
                FROM legacy_warehouses;

                INSERT INTO inventory_balances
                    ("Id", "CompanyId", "WarehouseId", "MaterialId", "Quantity", "UpdatedAt")
                SELECT
                    "Id",
                    "CompanyId",
                    md5("CompanyId"::text || ':warehouse:' || "Warehouse")::uuid,
                    "MaterialId",
                    "Quantity",
                    "UpdatedAt"
                FROM inventories;

                INSERT INTO inventory_transactions
                    ("Id", "CompanyId", "WarehouseId", "MaterialId", "Type", "Quantity",
                     "ReferenceType", "ReferenceId", "Note", "CreatedByUserId", "CreatedAt")
                SELECT
                    md5("Id"::text || ':opening-ledger')::uuid,
                    "CompanyId",
                    md5("CompanyId"::text || ':warehouse:' || "Warehouse")::uuid,
                    "MaterialId",
                    'AdjustmentIncrease',
                    "Quantity",
                    'LegacyInventoryMigration',
                    "Id",
                    'Opening balance migrated from the legacy inventory table.',
                    NULL,
                    "CreatedAt"
                FROM inventories
                WHERE "Quantity" > 0;
                """);

            migrationBuilder.DropTable(
                name: "inventories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "inventories",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Warehouse = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_inventories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_inventories_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_inventories_materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_inventories_CompanyId_MaterialId_Warehouse",
                table: "inventories",
                columns: new[] { "CompanyId", "MaterialId", "Warehouse" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_inventories_CompanyId_Warehouse",
                table: "inventories",
                columns: new[] { "CompanyId", "Warehouse" });

            migrationBuilder.CreateIndex(
                name: "IX_inventories_MaterialId",
                table: "inventories",
                column: "MaterialId");

            migrationBuilder.Sql("""
                INSERT INTO inventories
                    ("Id", "CompanyId", "MaterialId", "CreatedAt", "Quantity", "UpdatedAt", "Warehouse")
                SELECT
                    balance."Id",
                    balance."CompanyId",
                    balance."MaterialId",
                    balance."UpdatedAt",
                    balance."Quantity",
                    balance."UpdatedAt",
                    warehouse."Name"
                FROM inventory_balances AS balance
                INNER JOIN warehouses AS warehouse ON warehouse."Id" = balance."WarehouseId";
                """);

            migrationBuilder.DropTable(
                name: "inventory_balances");

            migrationBuilder.DropTable(
                name: "inventory_transactions");

            migrationBuilder.DropTable(
                name: "warehouses");
        }
    }
}
