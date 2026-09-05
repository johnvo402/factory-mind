using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddRoutingWorkCentersAndProductionOperations : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<Guid>(
                name: "RoutingId",
                table: "production_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "routings",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_routings", x => x.Id);
                    table.CheckConstraint("CK_routings_Revision_positive", "\"Revision\" > 0");
                    table.CheckConstraint("CK_routings_Status_valid", "\"Status\" IN ('draft', 'active', 'archived')");
                    table.ForeignKey(
                        name: "FK_routings_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_routings_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_centers",
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
                    table.PrimaryKey("PK_work_centers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_centers_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "routing_operations",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SetupTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    RunTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_routing_operations", x => x.Id);
                    table.CheckConstraint("CK_routing_operations_RunTimeMinutes_nonnegative", "\"RunTimeMinutes\" >= 0");
                    table.CheckConstraint("CK_routing_operations_Sequence_positive", "\"Sequence\" > 0");
                    table.CheckConstraint("CK_routing_operations_SetupTimeMinutes_nonnegative", "\"SetupTimeMinutes\" >= 0");
                    table.ForeignKey(
                        name: "FK_routing_operations_routings_RoutingId",
                        column: x => x.RoutingId,
                        principalTable: "routings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_routing_operations_work_centers_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "work_centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "production_order_operations",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutingOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkCenterCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WorkCenterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SetupTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    RunTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_production_order_operations", x => x.Id);
                    table.CheckConstraint("CK_production_order_operations_RunTimeMinutes_nonnegative", "\"RunTimeMinutes\" >= 0");
                    table.CheckConstraint("CK_production_order_operations_Sequence_positive", "\"Sequence\" > 0");
                    table.CheckConstraint("CK_production_order_operations_SetupTimeMinutes_nonnegative", "\"SetupTimeMinutes\" >= 0");
                    table.CheckConstraint("CK_production_order_operations_Status_valid", "\"Status\" IN ('pending', 'in_progress', 'completed')");
                    table.ForeignKey(
                        name: "FK_production_order_operations_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_order_operations_production_orders_ProductionOrd~",
                        column: x => x.ProductionOrderId,
                        principalTable: "production_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_order_operations_routing_operations_RoutingOpera~",
                        column: x => x.RoutingOperationId,
                        principalTable: "routing_operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_production_order_operations_work_centers_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "work_centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_RoutingId",
                table: "production_orders",
                column: "RoutingId");

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_CompanyId",
                table: "production_order_operations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_one_in_progress",
                table: "production_order_operations",
                column: "ProductionOrderId",
                unique: true,
                filter: "\"Status\" = 'in_progress'");

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_ProductionOrderId",
                table: "production_order_operations",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_ProductionOrderId_Sequence",
                table: "production_order_operations",
                columns: new[] { "ProductionOrderId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_ProductionOrderId_Status",
                table: "production_order_operations",
                columns: new[] { "ProductionOrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_RoutingOperationId",
                table: "production_order_operations",
                column: "RoutingOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_Status",
                table: "production_order_operations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_WorkCenterId",
                table: "production_order_operations",
                column: "WorkCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_routing_operations_RoutingId",
                table: "routing_operations",
                column: "RoutingId");

            migrationBuilder.CreateIndex(
                name: "IX_routing_operations_RoutingId_Sequence",
                table: "routing_operations",
                columns: new[] { "RoutingId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_routing_operations_WorkCenterId",
                table: "routing_operations",
                column: "WorkCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_routings_CompanyId_ProductId",
                table: "routings",
                columns: new[] { "CompanyId", "ProductId" },
                unique: true,
                filter: "\"Status\" = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_routings_CompanyId_ProductId_Revision",
                table: "routings",
                columns: new[] { "CompanyId", "ProductId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_routings_ProductId",
                table: "routings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_routings_Status",
                table: "routings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_work_centers_CompanyId_Code",
                table: "work_centers",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_centers_CompanyId_Name",
                table: "work_centers",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_production_orders_routings_RoutingId",
                table: "production_orders",
                column: "RoutingId",
                principalTable: "routings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropForeignKey(
                name: "FK_production_orders_routings_RoutingId",
                table: "production_orders");

            migrationBuilder.DropTable(
                name: "production_order_operations");

            migrationBuilder.DropTable(
                name: "routing_operations");

            migrationBuilder.DropTable(
                name: "routings");

            migrationBuilder.DropTable(
                name: "work_centers");

            migrationBuilder.DropIndex(
                name: "IX_production_orders_RoutingId",
                table: "production_orders");

            migrationBuilder.DropColumn(
                name: "RoutingId",
                table: "production_orders");
        }
    }
}
