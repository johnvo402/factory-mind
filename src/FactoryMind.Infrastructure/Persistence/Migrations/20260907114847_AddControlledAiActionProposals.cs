using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddControlledAiActionProposals : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "ai_action_proposals",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceUserMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetDisplay = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProductionOrderNumberSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductionOrderStatusSnapshot = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProductIdSnapshot = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCodeSnapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProductNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    QuantitySnapshot = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    BillOfMaterialIdSnapshot = table.Column<Guid>(type: "uuid", nullable: false),
                    BillOfMaterialRevisionSnapshot = table.Column<int>(type: "integer", nullable: false),
                    RoutingIdSnapshot = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutingRevisionSnapshot = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ai_action_proposals", x => x.Id);
                    table.CheckConstraint("CK_ai_action_proposals_ActionType_valid", "\"ActionType\" = 'release_production_order'");
                    table.CheckConstraint("CK_ai_action_proposals_Expiry_after_creation", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.CheckConstraint("CK_ai_action_proposals_Quantity_positive", "\"QuantitySnapshot\" > 0");
                    table.CheckConstraint("CK_ai_action_proposals_Status_valid", "\"Status\" IN ('pending','confirmed','succeeded','failed','cancelled','expired','stale')");
                    table.ForeignKey(
                        name: "FK_ai_action_proposals_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_action_proposals_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_action_proposals_messages_SourceUserMessageId",
                        column: x => x.SourceUserMessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_action_proposals_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ai_action_events",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_ai_action_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ai_action_events_ai_action_proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "ai_action_proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_events_CompanyId_ProposalId_CreatedAt",
                table: "ai_action_events",
                columns: new[] { "CompanyId", "ProposalId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_events_ProposalId",
                table: "ai_action_events",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_proposals_CompanyId_CreatedByUserId_Status",
                table: "ai_action_proposals",
                columns: new[] { "CompanyId", "CreatedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_proposals_ConversationId_CreatedAt",
                table: "ai_action_proposals",
                columns: new[] { "ConversationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_proposals_CreatedByUserId",
                table: "ai_action_proposals",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_proposals_ExpiresAt",
                table: "ai_action_proposals",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_ai_action_proposals_SourceUserMessageId",
                table: "ai_action_proposals",
                column: "SourceUserMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "ai_action_events");

            migrationBuilder.DropTable(
                name: "ai_action_proposals");
        }
    }
}
