using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FactoryMind.Infrastructure.Persistence.Migrations {
    /// <inheritdoc />
    public partial class AddHybridKnowledgeSearch : Migration {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                CREATE INDEX "IX_document_chunks_Content_fts"
                ON document_chunks
                USING GIN (to_tsvector('simple', COALESCE("Content", '')));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DROP INDEX "IX_document_chunks_Content_fts";
                """);
        }
    }
}
