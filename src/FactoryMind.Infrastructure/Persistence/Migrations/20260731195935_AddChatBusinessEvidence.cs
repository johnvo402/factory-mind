using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddChatBusinessEvidence : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "message_business_evidence",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNumber = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Detail = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_message_business_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_message_business_evidence_messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_message_business_evidence_MessageId_ReferenceNumber",
                table: "message_business_evidence",
                columns: new[] { "MessageId", "ReferenceNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "message_business_evidence");
        }
    }
}
