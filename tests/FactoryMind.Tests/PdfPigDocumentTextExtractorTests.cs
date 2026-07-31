using FactoryMind.Infrastructure.Knowledge;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace FactoryMind.Tests;

public sealed class PdfPigDocumentTextExtractorTests {
    [Fact]
    public async Task Extractor_reads_text_in_page_order() {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var firstPage = builder.AddPage(PageSize.A4);
        firstPage.AddText("Factory safety manual", 12, new PdfPoint(25, 700), font);
        var secondPage = builder.AddPage(PageSize.A4);
        secondPage.AddText("Stop the machine before maintenance", 12, new PdfPoint(25, 700), font);
        await using var content = new MemoryStream(builder.Build());
        var extractor = new PdfPigDocumentTextExtractor();

        var pages = await extractor.ExtractAsync(content, CancellationToken.None);

        Assert.Equal(2, pages.Count);
        Assert.Contains("Factory safety manual", pages[0].Content);
        Assert.Contains("Stop the machine before maintenance", pages[1].Content);
    }
}
