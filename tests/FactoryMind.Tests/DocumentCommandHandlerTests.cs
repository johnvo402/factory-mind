using System.Text;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Knowledge;
using FactoryMind.Application.Features.Knowledge.GetDocuments;
using FactoryMind.Application.Features.Knowledge.ProcessDocument;
using FactoryMind.Application.Features.Knowledge.QueueDocumentProcessing;
using FactoryMind.Application.Features.Knowledge.UploadDocument;
using FactoryMind.Domain.Knowledge;

namespace FactoryMind.Tests;

public sealed class DocumentCommandHandlerTests {
    [Fact]
    public async Task Upload_stores_a_valid_PDF_and_tenant_metadata() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorage();
        var queue = new FakeDocumentProcessingQueue();
        var handler = new UploadDocumentCommandHandler(repository, storage, queue, currentUser);
        await using var content = PdfStream("Factory manual");

        var result = await handler.Handle(
            new UploadDocumentCommand(
                "  Injection Manual  ",
                "manual.pdf",
                "application/pdf",
                content.Length,
                content),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var document = Assert.Single(repository.AddedDocuments);
        Assert.Equal(currentUser.CompanyId, document.CompanyId);
        Assert.Equal(currentUser.UserId, document.UploadedByUserId);
        Assert.Equal("Injection Manual", document.Title);
        Assert.StartsWith($"companies/{currentUser.CompanyId:N}/documents/", document.Path);
        Assert.Equal(document.Path, storage.ObjectKey);
        Assert.Equal(document.Id, queue.DocumentId);
        Assert.Equal(currentUser.CompanyId, queue.CompanyId);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(storage.Content));
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Upload_rejects_a_file_without_a_PDF_signature() {
        var repository = new FakeDocumentRepository();
        var storage = new FakeFileStorage();
        var handler = new UploadDocumentCommandHandler(
            repository,
            storage,
            new FakeDocumentProcessingQueue(),
            new FakeCurrentUser());
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("not a PDF"));

        var result = await handler.Handle(
            new UploadDocumentCommand(null, "fake.pdf", "application/pdf", content.Length, content),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("knowledge.invalid_pdf", result.Error?.Code);
        Assert.Empty(repository.AddedDocuments);
        Assert.Null(storage.ObjectKey);
    }

    [Fact]
    public async Task List_documents_uses_the_current_company_scope() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeDocumentRepository();
        repository.Documents.Add(new KnowledgeDocument {
            CompanyId = currentUser.CompanyId,
            Title = "SOP",
            FileName = "sop.pdf",
            Path = "object-key"
        });
        var handler = new GetDocumentsQueryHandler(repository, currentUser);

        var result = await handler.Handle(new GetDocumentsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(currentUser.CompanyId, repository.RequestedCompanyId);
        Assert.Single(result.Value!);
    }

    [Fact]
    public async Task Process_document_replaces_chunks_and_marks_document_ready() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeDocumentRepository();
        var document = new KnowledgeDocument {
            CompanyId = currentUser.CompanyId,
            Title = "Manual",
            FileName = "manual.pdf",
            Path = "manual-object"
        };
        document.Chunks.Add(new DocumentChunk {
            CompanyId = currentUser.CompanyId,
            Sequence = 0,
            PageNumber = 1,
            Content = "Old content"
        });
        repository.Documents.Add(document);
        var extractor = new FakeDocumentTextExtractor([
            new DocumentPageText(1, "First page instructions"),
            new DocumentPageText(2, "Second page safety notes")
        ]);
        var handler = new ProcessDocumentCommandHandler(
            repository,
            new FakeFileStorage(),
            extractor,
            new DocumentChunker());

        var result = await handler.Handle(
            new ProcessDocumentCommand(document.Id, currentUser.CompanyId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatuses.Ready, document.Status);
        Assert.Equal(2, document.PageCount);
        Assert.Equal(2, document.ChunkCount);
        Assert.Equal([1, 2], document.Chunks.Select(chunk => chunk.PageNumber));
        Assert.DoesNotContain(document.Chunks, chunk => chunk.Content == "Old content");
        Assert.All(document.Chunks, chunk => Assert.Equal(currentUser.CompanyId, chunk.CompanyId));
        Assert.NotNull(document.ProcessedAt);
    }

    [Fact]
    public async Task Process_document_marks_an_image_only_PDF_as_failed() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeDocumentRepository();
        var document = new KnowledgeDocument {
            CompanyId = currentUser.CompanyId,
            Title = "Scanned manual",
            FileName = "scan.pdf",
            Path = "scan-object"
        };
        repository.Documents.Add(document);
        var handler = new ProcessDocumentCommandHandler(
            repository,
            new FakeFileStorage(),
            new FakeDocumentTextExtractor([new DocumentPageText(1, "  ")]),
            new DocumentChunker());

        var result = await handler.Handle(
            new ProcessDocumentCommand(document.Id, currentUser.CompanyId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatuses.Failed, document.Status);
        Assert.Contains("OCR", document.ProcessingError);
        Assert.Empty(document.Chunks);
    }

    [Fact]
    public async Task Queue_processing_uses_the_current_company_scope() {
        var currentUser = new FakeCurrentUser();
        var repository = new FakeDocumentRepository();
        var queue = new FakeDocumentProcessingQueue();
        var document = new KnowledgeDocument {
            CompanyId = currentUser.CompanyId,
            Title = "SOP",
            FileName = "sop.pdf",
            Path = "sop-object"
        };
        repository.Documents.Add(document);
        var handler = new QueueDocumentProcessingCommandHandler(repository, queue, currentUser);

        var result = await handler.Handle(
            new QueueDocumentProcessingCommand(document.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(currentUser.CompanyId, repository.RequestedCompanyId);
        Assert.Equal(document.Id, queue.DocumentId);
    }

    [Fact]
    public void Chunker_preserves_pages_and_creates_overlapping_chunks() {
        var words = Enumerable.Range(0, 400).Select(index => $"word{index}");
        var firstPage = string.Join(' ', words);
        var chunker = new DocumentChunker();

        var chunks = chunker.Chunk([
            new DocumentPageText(1, firstPage),
            new DocumentPageText(2, "Final safety instruction")
        ]);

        Assert.True(chunks.Count >= 4);
        Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(chunk => chunk.Sequence));
        Assert.Equal(2, chunks[^1].PageNumber);
        Assert.Contains("Final safety instruction", chunks[^1].Content);
        Assert.True(chunks[0].Content.Length <= DocumentChunker.TargetLength);
        Assert.Contains(chunks[1].Content.Split(' ')[0], chunks[0].Content);
    }

    private static MemoryStream PdfStream(string content) =>
        new(Encoding.UTF8.GetBytes($"%PDF-1.7\n{content}"));

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "User";
    }

    private sealed class FakeDocumentRepository : IDocumentRepository {
        public List<KnowledgeDocument> Documents { get; } = [];
        public List<KnowledgeDocument> AddedDocuments { get; } = [];
        public Guid? RequestedCompanyId { get; private set; }
        public int SaveChangesCount { get; private set; }

        public Task<IReadOnlyList<KnowledgeDocument>> GetByCompanyAsync(
            Guid companyId,
            CancellationToken cancellationToken) {
            RequestedCompanyId = companyId;
            return Task.FromResult<IReadOnlyList<KnowledgeDocument>>(Documents);
        }

        public void Add(KnowledgeDocument document) => AddedDocuments.Add(document);

        public Task<KnowledgeDocument?> GetByIdAsync(
            Guid documentId,
            Guid companyId,
            CancellationToken cancellationToken) {
            RequestedCompanyId = companyId;
            return Task.FromResult(Documents.SingleOrDefault(document =>
                document.Id == documentId && document.CompanyId == companyId));
        }

        public Task<KnowledgeDocument?> GetForProcessingAsync(
            Guid documentId,
            Guid companyId,
            CancellationToken cancellationToken) {
            return GetByIdAsync(documentId, companyId, cancellationToken);
        }

        public async Task MarkProcessingAsync(
            Guid documentId,
            Guid companyId,
            CancellationToken cancellationToken) {
            var document = await GetByIdAsync(documentId, companyId, cancellationToken);
            if (document is not null) {
                document.Status = DocumentStatuses.Processing;
                document.ProcessingError = null;
                document.ProcessedAt = null;
            }
        }

        public Task CompleteProcessingAsync(
            KnowledgeDocument document,
            IReadOnlyList<DocumentChunk> chunks,
            int pageCount,
            DateTime processedAt,
            CancellationToken cancellationToken) {
            document.Chunks.Clear();
            foreach (var chunk in chunks) {
                document.Chunks.Add(chunk);
            }

            document.PageCount = pageCount;
            document.ChunkCount = chunks.Count;
            document.Status = DocumentStatuses.Ready;
            document.ProcessingError = null;
            document.ProcessedAt = processedAt;
            return Task.CompletedTask;
        }

        public async Task MarkProcessingFailedAsync(
            Guid documentId,
            Guid companyId,
            string processingError,
            DateTime processedAt,
            CancellationToken cancellationToken) {
            var document = await GetByIdAsync(documentId, companyId, cancellationToken);
            if (document is not null) {
                document.Status = DocumentStatuses.Failed;
                document.ProcessingError = processingError;
                document.ProcessedAt = processedAt;
            }
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFileStorage : IFileStorage {
        public string? ObjectKey { get; private set; }
        public byte[] Content { get; private set; } = [];

        public async Task UploadAsync(
            string objectKey,
            Stream content,
            long length,
            string contentType,
            CancellationToken cancellationToken) {
            ObjectKey = objectKey;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Content = buffer.ToArray();
        }

        public Task<Stream> DownloadAsync(string objectKey, CancellationToken cancellationToken) {
            Stream stream = new MemoryStream(Content, writable: false);
            return Task.FromResult(stream);
        }
    }

    private sealed class FakeDocumentProcessingQueue : IDocumentProcessingQueue {
        public Guid? DocumentId { get; private set; }
        public Guid? CompanyId { get; private set; }

        public void Enqueue(Guid documentId, Guid companyId) {
            DocumentId = documentId;
            CompanyId = companyId;
        }
    }

    private sealed class FakeDocumentTextExtractor(
        IReadOnlyList<DocumentPageText> pages) : IDocumentTextExtractor {
        public Task<IReadOnlyList<DocumentPageText>> ExtractAsync(
            Stream content,
            CancellationToken cancellationToken) {
            return Task.FromResult(pages);
        }
    }
}
