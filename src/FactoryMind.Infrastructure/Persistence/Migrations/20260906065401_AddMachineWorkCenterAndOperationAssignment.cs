using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddMachineWorkCenterAndOperationAssignment : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "MachineCode",
                table: "production_order_operations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MachineId",
                table: "production_order_operations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MachineName",
                table: "production_order_operations",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkCenterId",
                table: "machines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_MachineId",
                table: "production_order_operations",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_production_order_operations_one_in_progress_per_machine",
                table: "production_order_operations",
                column: "MachineId",
                unique: true,
                filter: "\"Status\" = 'in_progress' AND \"MachineId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_production_order_operations_Machine_snapshot_consistent",
                table: "production_order_operations",
                sql: "(\"MachineId\" IS NULL AND \"MachineCode\" IS NULL AND \"MachineName\" IS NULL) OR (\"MachineId\" IS NOT NULL AND \"MachineCode\" IS NOT NULL AND \"MachineName\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_machines_WorkCenterId",
                table: "machines",
                column: "WorkCenterId");

            migrationBuilder.AddForeignKey(
                name: "FK_machines_work_centers_WorkCenterId",
                table: "machines",
                column: "WorkCenterId",
                principalTable: "work_centers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_production_order_operations_machines_MachineId",
                table: "production_order_operations",
                column: "MachineId",
                principalTable: "machines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropForeignKey(
                name: "FK_machines_work_centers_WorkCenterId",
                table: "machines");

            migrationBuilder.DropForeignKey(
                name: "FK_production_order_operations_machines_MachineId",
                table: "production_order_operations");

            migrationBuilder.DropIndex(
                name: "IX_production_order_operations_MachineId",
                table: "production_order_operations");

            migrationBuilder.DropIndex(
                name: "IX_production_order_operations_one_in_progress_per_machine",
                table: "production_order_operations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_production_order_operations_Machine_snapshot_consistent",
                table: "production_order_operations");

            migrationBuilder.DropIndex(
                name: "IX_machines_WorkCenterId",
                table: "machines");

            migrationBuilder.DropColumn(
                name: "MachineCode",
                table: "production_order_operations");

            migrationBuilder.DropColumn(
                name: "MachineId",
                table: "production_order_operations");

            migrationBuilder.DropColumn(
                name: "MachineName",
                table: "production_order_operations");

            migrationBuilder.DropColumn(
                name: "WorkCenterId",
                table: "machines");
        }
    }
}
