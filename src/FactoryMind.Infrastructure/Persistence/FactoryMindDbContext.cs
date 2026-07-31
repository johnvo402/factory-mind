using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Chat;
using FactoryMind.Domain.Identity;
using FactoryMind.Domain.Knowledge;
using FactoryMind.Domain.Manufacturing;
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
    public DbSet<ChatCitation> MessageCitations => Set<ChatCitation>();
    public DbSet<ChatBusinessEvidence> MessageBusinessEvidence => Set<ChatBusinessEvidence>();
    public DbSet<KnowledgeDocument> Documents => Set<KnowledgeDocument>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<DocumentEmbeddingRecord> DocumentEmbeddings => Set<DocumentEmbeddingRecord>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();

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

        modelBuilder.Entity<ChatCitation>(entity => {
            entity.ToTable("message_citations");
            entity.HasIndex(citation => new { citation.MessageId, citation.ReferenceNumber }).IsUnique();
            entity.Property(citation => citation.DocumentTitle).HasMaxLength(200).IsRequired();
            entity.Property(citation => citation.FileName).HasMaxLength(255).IsRequired();
            entity.Property(citation => citation.Excerpt).HasMaxLength(500).IsRequired();
            entity.HasOne(citation => citation.Message)
                .WithMany(message => message.Citations)
                .HasForeignKey(citation => citation.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatBusinessEvidence>(entity => {
            entity.ToTable("message_business_evidence");
            entity.HasIndex(evidence => new { evidence.MessageId, evidence.ReferenceNumber }).IsUnique();
            entity.Property(evidence => evidence.EntityType).HasMaxLength(50).IsRequired();
            entity.Property(evidence => evidence.Title).HasMaxLength(250).IsRequired();
            entity.Property(evidence => evidence.Detail).HasMaxLength(600).IsRequired();
            entity.HasOne(evidence => evidence.Message)
                .WithMany(message => message.BusinessEvidence)
                .HasForeignKey(evidence => evidence.MessageId)
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

        modelBuilder.Entity<Machine>(entity => {
            entity.ToTable("machines");
            entity.HasIndex(machine => new { machine.CompanyId, machine.Code }).IsUnique();
            entity.HasIndex(machine => new { machine.CompanyId, machine.Name });
            entity.Property(machine => machine.Code).HasMaxLength(MachineConstraints.MaximumCodeLength).IsRequired();
            entity.Property(machine => machine.Name).HasMaxLength(MachineConstraints.MaximumNameLength).IsRequired();
            entity.Property(machine => machine.Status).HasMaxLength(30).IsRequired();
            entity.HasOne(machine => machine.Company)
                .WithMany(company => company.Machines)
                .HasForeignKey(machine => machine.CompanyId);
        });

        modelBuilder.Entity<Material>(entity => {
            entity.ToTable("materials");
            entity.HasIndex(material => new { material.CompanyId, material.Code }).IsUnique();
            entity.HasIndex(material => new { material.CompanyId, material.Name });
            entity.Property(material => material.Code).HasMaxLength(MaterialConstraints.MaximumCodeLength).IsRequired();
            entity.Property(material => material.Name).HasMaxLength(MaterialConstraints.MaximumNameLength).IsRequired();
            entity.Property(material => material.Unit).HasMaxLength(MaterialConstraints.MaximumUnitLength).IsRequired();
            entity.HasOne(material => material.Company)
                .WithMany(company => company.Materials)
                .HasForeignKey(material => material.CompanyId);
        });

        modelBuilder.Entity<Product>(entity => {
            entity.ToTable("products");
            entity.HasIndex(product => new { product.CompanyId, product.Code }).IsUnique();
            entity.HasIndex(product => new { product.CompanyId, product.Name });
            entity.Property(product => product.Code).HasMaxLength(ProductConstraints.MaximumCodeLength).IsRequired();
            entity.Property(product => product.Name).HasMaxLength(ProductConstraints.MaximumNameLength).IsRequired();
            entity.HasOne(product => product.Company)
                .WithMany(company => company.Products)
                .HasForeignKey(product => product.CompanyId);
        });

        modelBuilder.Entity<Inventory>(entity => {
            entity.ToTable("inventories");
            entity.HasIndex(inventory => new {
                inventory.CompanyId,
                inventory.MaterialId,
                inventory.Warehouse
            }).IsUnique();
            entity.HasIndex(inventory => new { inventory.CompanyId, inventory.Warehouse });
            entity.Property(inventory => inventory.Warehouse)
                .HasMaxLength(InventoryConstraints.MaximumWarehouseLength)
                .IsRequired();
            entity.Property(inventory => inventory.Quantity)
                .HasPrecision(InventoryConstraints.QuantityPrecision, InventoryConstraints.QuantityScale);
            entity.HasOne(inventory => inventory.Company)
                .WithMany(company => company.Inventories)
                .HasForeignKey(inventory => inventory.CompanyId);
            entity.HasOne(inventory => inventory.Material)
                .WithMany()
                .HasForeignKey(inventory => inventory.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductionOrder>(entity => {
            entity.ToTable("production_orders");
            entity.HasIndex(order => new { order.CompanyId, order.Number }).IsUnique();
            entity.HasIndex(order => new { order.CompanyId, order.Status, order.UpdatedAt });
            entity.Property(order => order.Number)
                .HasMaxLength(ProductionOrderConstraints.MaximumNumberLength)
                .IsRequired();
            entity.Property(order => order.Status)
                .HasMaxLength(ProductionOrderConstraints.MaximumStatusLength)
                .IsRequired();
            entity.Property(order => order.Quantity)
                .HasPrecision(ProductionOrderConstraints.QuantityPrecision, ProductionOrderConstraints.QuantityScale);
            entity.HasOne(order => order.Company)
                .WithMany(company => company.ProductionOrders)
                .HasForeignKey(order => order.CompanyId);
            entity.HasOne(order => order.Product)
                .WithMany()
                .HasForeignKey(order => order.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
