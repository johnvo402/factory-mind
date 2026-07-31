using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Knowledge;

public sealed class EfDocumentRepository(FactoryMindDbContext dbContext) : IDocumentRepository {
    public async Task<IReadOnlyList<KnowledgeDocument>> GetByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken) {
        return await dbContext.Documents
            .AsNoTracking()
            .Where(document => document.CompanyId == companyId)
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(KnowledgeDocument document) => dbContext.Documents.Add(document);

    public Task<KnowledgeDocument?> GetByIdAsync(
        Guid documentId,
        Guid companyId,
        CancellationToken cancellationToken) {
        return dbContext.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document => document.Id == documentId && document.CompanyId == companyId,
                cancellationToken);
    }

    public Task<KnowledgeDocument?> GetForProcessingAsync(
        Guid documentId,
        Guid companyId,
        CancellationToken cancellationToken) {
        return dbContext.Documents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document => document.Id == documentId && document.CompanyId == companyId,
                cancellationToken);
    }

    public async Task MarkProcessingAsync(
        Guid documentId,
        Guid companyId,
        CancellationToken cancellationToken) {
        await dbContext.Documents
            .Where(document => document.Id == documentId && document.CompanyId == companyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(document => document.Status, DocumentStatuses.Processing)
                .SetProperty(document => document.ProcessingError, (string?)null)
                .SetProperty(document => document.ProcessedAt, (DateTime?)null),
                cancellationToken);
    }

    public async Task CompleteProcessingAsync(
        KnowledgeDocument document,
        IReadOnlyList<DocumentChunk> chunks,
        int pageCount,
        DateTime processedAt,
        CancellationToken cancellationToken) {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.DocumentChunks
            .Where(chunk => chunk.DocumentId == document.Id && chunk.CompanyId == document.CompanyId)
            .ExecuteDeleteAsync(cancellationToken);
        dbContext.DocumentChunks.AddRange(chunks);
        await dbContext.SaveChangesAsync(cancellationToken);

        var updated = await dbContext.Documents
            .Where(candidate => candidate.Id == document.Id && candidate.CompanyId == document.CompanyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(candidate => candidate.PageCount, pageCount)
                .SetProperty(candidate => candidate.ChunkCount, chunks.Count)
                .SetProperty(candidate => candidate.Status, DocumentStatuses.Ready)
                .SetProperty(candidate => candidate.ProcessingError, (string?)null)
                .SetProperty(candidate => candidate.ProcessedAt, processedAt),
                cancellationToken);
        if (updated != 1) {
            throw new InvalidOperationException("Document disappeared while processing completed.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task MarkProcessingFailedAsync(
        Guid documentId,
        Guid companyId,
        string processingError,
        DateTime processedAt,
        CancellationToken cancellationToken) {
        await dbContext.Documents
            .Where(document => document.Id == documentId && document.CompanyId == companyId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(document => document.Status, DocumentStatuses.Failed)
                .SetProperty(document => document.ProcessingError, processingError)
                .SetProperty(document => document.ProcessedAt, processedAt),
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
