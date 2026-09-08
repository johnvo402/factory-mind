using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddWorkCenterCapacityPlanning : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<int>(
                name: "ParallelCapacity",
                table: "work_centers",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "UTC");

            migrationBuilder.CreateTable(
                name: "work_center_days_off",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_work_center_days_off", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_center_days_off_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_center_days_off_work_centers_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "work_centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_center_shifts",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkCenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_work_center_shifts", x => x.Id);
                    table.CheckConstraint("CK_work_center_shifts_DayOfWeek_range", "\"DayOfWeek\" >= 0 AND \"DayOfWeek\" <= 6");
                    table.CheckConstraint("CK_work_center_shifts_Time_order", "\"StartTime\" < \"EndTime\"");
                    table.ForeignKey(
                        name: "FK_work_center_shifts_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_center_shifts_work_centers_WorkCenterId",
                        column: x => x.WorkCenterId,
                        principalTable: "work_centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_work_centers_ParallelCapacity_range",
                table: "work_centers",
                sql: "\"ParallelCapacity\" >= 1 AND \"ParallelCapacity\" <= 100");

            migrationBuilder.CreateIndex(
                name: "IX_work_center_days_off_CompanyId_WorkCenterId_Date",
                table: "work_center_days_off",
                columns: new[] { "CompanyId", "WorkCenterId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_center_days_off_WorkCenterId",
                table: "work_center_days_off",
                column: "WorkCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_work_center_shifts_CompanyId_WorkCenterId_DayOfWeek",
                table: "work_center_shifts",
                columns: new[] { "CompanyId", "WorkCenterId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_work_center_shifts_WorkCenterId",
                table: "work_center_shifts",
                column: "WorkCenterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "work_center_days_off");

            migrationBuilder.DropTable(
                name: "work_center_shifts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_work_centers_ParallelCapacity_range",
                table: "work_centers");

            migrationBuilder.DropColumn(
                name: "ParallelCapacity",
                table: "work_centers");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "companies");
        }
    }
}
