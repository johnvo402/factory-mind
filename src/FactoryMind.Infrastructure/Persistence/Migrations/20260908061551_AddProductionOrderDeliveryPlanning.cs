using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddProductionOrderDeliveryPlanning : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "production_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "production_orders",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "normal");

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_CompanyId_Priority",
                table: "production_orders",
                columns: new[] { "CompanyId", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_CompanyId_Status_DueDate",
                table: "production_orders",
                columns: new[] { "CompanyId", "Status", "DueDate" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_production_orders_Priority_valid",
                table: "production_orders",
                sql: "\"Priority\" IN ('low', 'normal', 'high', 'urgent')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropIndex(
                name: "IX_production_orders_CompanyId_Priority",
                table: "production_orders");

            migrationBuilder.DropIndex(
                name: "IX_production_orders_CompanyId_Status_DueDate",
                table: "production_orders");

            migrationBuilder.DropCheckConstraint(
                name: "CK_production_orders_Priority_valid",
                table: "production_orders");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "production_orders");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "production_orders");
        }
    }
}
