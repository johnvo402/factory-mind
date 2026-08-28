using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddVersionedBomAndMaterialRequirements : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "bill_of_materials",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    OutputQuantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_bill_of_materials", x => x.Id);
                    table.CheckConstraint("CK_bill_of_materials_OutputQuantity_positive", "\"OutputQuantity\" > 0");
                    table.ForeignKey(
                        name: "FK_bill_of_materials_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bill_of_materials_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "bom_items",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BillOfMaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ScrapPercentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_bom_items", x => x.Id);
                    table.CheckConstraint("CK_bom_items_Quantity_positive", "\"Quantity\" > 0");
                    table.CheckConstraint("CK_bom_items_ScrapPercentage_range", "\"ScrapPercentage\" IS NULL OR (\"ScrapPercentage\" >= 0 AND \"ScrapPercentage\" <= 100)");
                    table.ForeignKey(
                        name: "FK_bom_items_bill_of_materials_BillOfMaterialId",
                        column: x => x.BillOfMaterialId,
                        principalTable: "bill_of_materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_bom_items_materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bill_of_materials_CompanyId_ProductId",
                table: "bill_of_materials",
                columns: new[] { "CompanyId", "ProductId" },
                unique: true,
                filter: "\"Status\" = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_bill_of_materials_CompanyId_ProductId_Revision",
                table: "bill_of_materials",
                columns: new[] { "CompanyId", "ProductId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bill_of_materials_ProductId",
                table: "bill_of_materials",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_bom_items_BillOfMaterialId_MaterialId",
                table: "bom_items",
                columns: new[] { "BillOfMaterialId", "MaterialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bom_items_MaterialId",
                table: "bom_items",
                column: "MaterialId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "bom_items");

            migrationBuilder.DropTable(
                name: "bill_of_materials");
        }
    }
}
