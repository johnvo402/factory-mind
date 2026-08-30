using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddProductionExecutionLifecycle : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<Guid>(
                name: "BillOfMaterialId",
                table: "production_orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "production_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                table: "production_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "production_orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "inventory_transactions",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "inventory_balances",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.CreateIndex(
                name: "IX_production_orders_BillOfMaterialId",
                table: "production_orders",
                column: "BillOfMaterialId");

            migrationBuilder.AddForeignKey(
                name: "FK_production_orders_bill_of_materials_BillOfMaterialId",
                table: "production_orders",
                column: "BillOfMaterialId",
                principalTable: "bill_of_materials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropForeignKey(
                name: "FK_production_orders_bill_of_materials_BillOfMaterialId",
                table: "production_orders");

            migrationBuilder.DropIndex(
                name: "IX_production_orders_BillOfMaterialId",
                table: "production_orders");

            migrationBuilder.DropColumn(
                name: "BillOfMaterialId",
                table: "production_orders");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "production_orders");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "production_orders");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "production_orders");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "inventory_transactions",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "inventory_balances",
                type: "numeric(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,6)",
                oldPrecision: 18,
                oldScale: 6);
        }
    }
}
