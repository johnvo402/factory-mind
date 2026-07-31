using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentProcessingAndChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChunkCount",
                table: "documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PageCount",
                table: "documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                table: "documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_chunks_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_CompanyId_DocumentId",
                table: "document_chunks",
                columns: new[] { "CompanyId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_DocumentId_Sequence",
                table: "document_chunks",
                columns: new[] { "DocumentId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropColumn(
                name: "ChunkCount",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "PageCount",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "ProcessingError",
                table: "documents");
        }
    }
}
