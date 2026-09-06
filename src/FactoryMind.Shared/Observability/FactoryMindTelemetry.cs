using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FactoryMind.Shared.Observability;

public static class FactoryMindTelemetry {
    public const string ActivitySourceName = "FactoryMind";
    public const string MeterName = "FactoryMind";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> AiRequests = Meter.CreateCounter<long>(
        "factorymind.ai.requests",
        description: "AI provider requests by operation and outcome.");
    public static readonly Histogram<double> AiRequestDuration = Meter.CreateHistogram<double>(
        "factorymind.ai.request.duration",
        unit: "ms",
        description: "AI provider request duration.");
    public static readonly Counter<long> AiRetries = Meter.CreateCounter<long>(
        "factorymind.ai.retries",
        description: "AI provider transport retries.");
    public static readonly Counter<long> AiInputTokens = Meter.CreateCounter<long>(
        "factorymind.ai.input.tokens",
        description: "Provider-reported AI input tokens.");
    public static readonly Counter<long> AiOutputTokens = Meter.CreateCounter<long>(
        "factorymind.ai.output.tokens",
        description: "Provider-reported AI output tokens.");
    public static readonly Counter<long> AiTotalTokens = Meter.CreateCounter<long>(
        "factorymind.ai.total.tokens",
        description: "Provider-reported AI total tokens.");
    public static readonly Histogram<double> AiChatResponseHeadersDuration = Meter.CreateHistogram<double>(
        "factorymind.ai.chat.response_headers.duration",
        unit: "ms",
        description: "Time until Gemini streaming response headers are received.");
    public static readonly Histogram<double> AiChatTimeToFirstToken = Meter.CreateHistogram<double>(
        "factorymind.ai.chat.time_to_first_token",
        unit: "ms",
        description: "Time until the first generated chat chunk is received.");
    public static readonly Histogram<double> AiChatStreamDuration = Meter.CreateHistogram<double>(
        "factorymind.ai.chat.stream.duration",
        unit: "ms",
        description: "Total Gemini response-stream duration.");
    public static readonly Histogram<long> AiChatGeneratedChunks = Meter.CreateHistogram<long>(
        "factorymind.ai.chat.generated_chunks",
        description: "Generated text chunks per Gemini streaming request.");
    public static readonly Histogram<long> AiEmbeddingBatchSize = Meter.CreateHistogram<long>(
        "factorymind.ai.embedding.batch_size",
        description: "Inputs per embedding request.");

    public static readonly Counter<long> RagRequests = Meter.CreateCounter<long>(
        "factorymind.rag.requests",
        description: "Knowledge RAG requests by outcome.");
    public static readonly Histogram<double> RagDuration = Meter.CreateHistogram<double>(
        "factorymind.rag.duration",
        unit: "ms",
        description: "Knowledge RAG duration.");
    public static readonly Histogram<double> RagVectorDuration = Meter.CreateHistogram<double>(
        "factorymind.rag.vector.duration",
        unit: "ms",
        description: "Vector retrieval duration.");
    public static readonly Histogram<double> RagLexicalDuration = Meter.CreateHistogram<double>(
        "factorymind.rag.lexical.duration",
        unit: "ms",
        description: "Lexical retrieval duration.");
    public static readonly Histogram<double> RagRankDuration = Meter.CreateHistogram<double>(
        "factorymind.rag.rank.duration",
        unit: "ms",
        description: "Hybrid reranking duration.");
    public static readonly Histogram<long> RagVectorCandidates = Meter.CreateHistogram<long>(
        "factorymind.rag.vector.candidates",
        description: "Vector candidates per retrieval.");
    public static readonly Histogram<long> RagLexicalCandidates = Meter.CreateHistogram<long>(
        "factorymind.rag.lexical.candidates",
        description: "Lexical candidates per retrieval.");
    public static readonly Histogram<long> RagResults = Meter.CreateHistogram<long>(
        "factorymind.rag.results",
        description: "Final knowledge results per retrieval.");

    public static readonly Counter<long> BusinessRagRequests = Meter.CreateCounter<long>(
        "factorymind.rag.business.requests",
        description: "Business RAG requests by intent and outcome.");
    public static readonly Histogram<double> BusinessRagDuration = Meter.CreateHistogram<double>(
        "factorymind.rag.business.duration",
        unit: "ms",
        description: "Business RAG context duration.");
    public static readonly Histogram<long> BusinessRagScopeCount = Meter.CreateHistogram<long>(
        "factorymind.rag.business.scope_count",
        description: "Selected business scopes per request.");
    public static readonly Histogram<long> BusinessRagCandidates = Meter.CreateHistogram<long>(
        "factorymind.rag.business.candidates",
        description: "Business records returned by bounded retrieval.");
    public static readonly Histogram<long> BusinessRagEvidence = Meter.CreateHistogram<long>(
        "factorymind.rag.business.evidence",
        description: "Business evidence records included in context.");

    public static readonly Counter<long> ChatContextRequests = Meter.CreateCounter<long>(
        "factorymind.chat.context.requests",
        description: "Chat context builds by intent and outcome.");
    public static readonly Histogram<double> ChatContextDuration = Meter.CreateHistogram<double>(
        "factorymind.chat.context.duration",
        unit: "ms",
        description: "Chat context build duration.");
    public static readonly Histogram<long> ChatKnowledgeSources = Meter.CreateHistogram<long>(
        "factorymind.chat.knowledge_sources",
        description: "Knowledge sources supplied to chat.");
    public static readonly Histogram<long> ChatBusinessEvidence = Meter.CreateHistogram<long>(
        "factorymind.chat.business_evidence",
        description: "Business evidence records supplied to chat.");

    public static readonly Counter<long> DocumentsProcessed = Meter.CreateCounter<long>(
        "factorymind.documents.processed",
        description: "Document processing attempts by outcome.");
    public static readonly Histogram<double> DocumentProcessingDuration = Meter.CreateHistogram<double>(
        "factorymind.documents.processing.duration",
        unit: "ms",
        description: "Document processing duration.");
    public static readonly Histogram<long> DocumentChunksProduced = Meter.CreateHistogram<long>(
        "factorymind.documents.chunks",
        description: "Chunks produced per document.");
    public static readonly Histogram<long> DocumentEmbeddingBatches = Meter.CreateHistogram<long>(
        "factorymind.documents.embedding_batches",
        description: "Embedding batches per document.");

    public static TagList Tags(params (string Key, object? Value)[] values) {
        var tags = new TagList();
        foreach (var (key, value) in values) {
            tags.Add(key, value);
        }

        return tags;
    }
}
