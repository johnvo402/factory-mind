using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Chat;
using FactoryMind.Domain.Identity;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Infrastructure.Persistence.Knowledge;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence;

public sealed class FactoryMindDbContext(DbContextOptions<FactoryMindDbContext> options) : DbContext(options) {
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<KnowledgeDocument> Documents => Set<KnowledgeDocument>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<DocumentEmbeddingRecord> DocumentEmbeddings => Set<DocumentEmbeddingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.Entity<Company>(entity => {
            entity.ToTable("companies");
            entity.Property(company => company.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<User>(entity => {
            entity.ToTable("users");
            entity.HasIndex(user => new { user.CompanyId, user.Email }).IsUnique();
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.Property(user => user.PasswordHash).IsRequired();
            entity.HasOne(user => user.Company)
                .WithMany(company => company.Users)
                .HasForeignKey(user => user.CompanyId);
        });

        modelBuilder.Entity<RefreshToken>(entity => {
            entity.ToTable("refresh_tokens");
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId);
        });

        modelBuilder.Entity<Conversation>(entity => {
            entity.ToTable("conversations");
            entity.HasIndex(conversation => new {
                conversation.CompanyId,
                conversation.UserId,
                conversation.UpdatedAt
            });
            entity.Property(conversation => conversation.Title).HasMaxLength(120).IsRequired();
            entity.HasOne(conversation => conversation.Company)
                .WithMany(company => company.Conversations)
                .HasForeignKey(conversation => conversation.CompanyId);
            entity.HasOne(conversation => conversation.User)
                .WithMany(user => user.Conversations)
                .HasForeignKey(conversation => conversation.UserId);
        });

        modelBuilder.Entity<ChatMessage>(entity => {
            entity.ToTable("messages");
            entity.HasIndex(message => new { message.ConversationId, message.CreatedAt });
            entity.Property(message => message.Role).HasMaxLength(20).IsRequired();
            entity.Property(message => message.Content).IsRequired();
            entity.HasOne(message => message.Conversation)
                .WithMany(conversation => conversation.Messages)
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<KnowledgeDocument>(entity => {
            entity.ToTable("documents");
            entity.HasIndex(document => new { document.CompanyId, document.CreatedAt });
            entity.Property(document => document.Title).HasMaxLength(200).IsRequired();
            entity.Property(document => document.FileName).HasMaxLength(255).IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(document => document.Path).HasMaxLength(600).IsRequired();
            entity.Property(document => document.Status).HasMaxLength(30).IsRequired();
            entity.Property(document => document.ProcessingError).HasMaxLength(500);
            entity.HasOne(document => document.Company)
                .WithMany(company => company.Documents)
                .HasForeignKey(document => document.CompanyId);
            entity.HasOne(document => document.UploadedByUser)
                .WithMany(user => user.UploadedDocuments)
                .HasForeignKey(document => document.UploadedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentChunk>(entity => {
            entity.ToTable("document_chunks");
            entity.HasIndex(chunk => new { chunk.DocumentId, chunk.Sequence }).IsUnique();
            entity.HasIndex(chunk => new { chunk.CompanyId, chunk.DocumentId });
            entity.Property(chunk => chunk.Content).IsRequired();
            entity.HasOne(chunk => chunk.Document)
                .WithMany(document => document.Chunks)
                .HasForeignKey(chunk => chunk.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentEmbeddingRecord>(entity => {
            entity.ToTable("document_embeddings");
            entity.HasIndex(embedding => embedding.DocumentChunkId).IsUnique();
            entity.HasIndex(embedding => new { embedding.CompanyId, embedding.DocumentChunkId });
            entity.Property(embedding => embedding.Model).HasMaxLength(200).IsRequired();
            entity.Property(embedding => embedding.Embedding)
                .HasColumnType($"vector({DocumentEmbeddingConstraints.Dimensions})")
                .IsRequired();
            entity.HasOne<DocumentChunk>()
                .WithOne()
                .HasForeignKey<DocumentEmbeddingRecord>(embedding => embedding.DocumentChunkId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
