using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Knowledge;

public sealed class EfKnowledgeSearchRepository(
    FactoryMindDbContext dbContext) : IKnowledgeSearchRepository {
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        Guid companyId,
        string embeddingModel,
        float[] queryEmbedding,
        int limit,
        CancellationToken cancellationToken) {
        var vector = new Vector(queryEmbedding);
        var matches = await (
            from embedding in dbContext.DocumentEmbeddings.AsNoTracking()
            join chunk in dbContext.DocumentChunks.AsNoTracking()
                on embedding.DocumentChunkId equals chunk.Id
            join document in dbContext.Documents.AsNoTracking()
                on chunk.DocumentId equals document.Id
            where embedding.CompanyId == companyId
                && chunk.CompanyId == companyId
                && document.CompanyId == companyId
                && document.Status == DocumentStatuses.Ready
                && embedding.Model == embeddingModel
            select new {
                DocumentId = document.Id,
                DocumentTitle = document.Title,
                document.FileName,
                ChunkId = chunk.Id,
                chunk.PageNumber,
                chunk.Content,
                Distance = embedding.Embedding.CosineDistance(vector)
            })
            .OrderBy(match => match.Distance)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return matches
            .Select(match => new KnowledgeSearchResult(
                match.DocumentId,
                match.DocumentTitle,
                match.FileName,
                match.ChunkId,
                match.PageNumber,
                match.Content,
                Math.Round(1d - match.Distance, 6)))
            .ToList();
    }
}
