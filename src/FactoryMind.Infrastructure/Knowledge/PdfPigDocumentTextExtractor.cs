using FactoryMind.Application.Features.Knowledge;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace FactoryMind.Infrastructure.Knowledge;

public sealed class PdfPigDocumentTextExtractor : IDocumentTextExtractor {
    public Task<IReadOnlyList<DocumentPageText>> ExtractAsync(
        Stream content,
        CancellationToken cancellationToken) {
        try {
            using var document = PdfDocument.Open(content, new ParsingOptions {
                SkipMissingFonts = true
            });
            var pages = new List<DocumentPageText>(document.NumberOfPages);
            foreach (var page in document.GetPages()) {
                cancellationToken.ThrowIfCancellationRequested();
                pages.Add(new DocumentPageText(
                    page.Number,
                    ContentOrderTextExtractor.GetText(page)));
            }

            return Task.FromResult<IReadOnlyList<DocumentPageText>>(pages);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            throw new DocumentParsingException("PDF content could not be parsed.", exception);
        }
    }
}
