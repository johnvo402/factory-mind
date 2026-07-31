using FactoryMind.Api.Endpoints;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;

namespace FactoryMind.Tests;

public sealed class UploadDocumentFormValidatorTests {
    [Fact]
    public async Task Upload_accepts_a_non_empty_PDF_form_file() {
        var validator = new UploadDocumentFormValidator();
        var form = CreateForm("manual.pdf", "application/pdf", [1, 2, 3]);

        var result = await validator.TestValidateAsync(form);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Upload_rejects_a_non_PDF_extension_and_content_type() {
        var validator = new UploadDocumentFormValidator();
        var form = CreateForm("manual.txt", "text/plain", [1]);

        var result = await validator.TestValidateAsync(form);

        result.ShouldHaveValidationErrorFor(request => request.File.FileName)
            .WithErrorMessage("Only PDF files are supported.");
        result.ShouldHaveValidationErrorFor(request => request.File.ContentType)
            .WithErrorMessage("File content type must be application/pdf.");
    }

    private static UploadDocumentForm CreateForm(string fileName, string contentType, byte[] content) {
        var stream = new MemoryStream(content);
        return new UploadDocumentForm {
            File = new FormFile(stream, 0, stream.Length, "file", fileName) {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            }
        };
    }
}
