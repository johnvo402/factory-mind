using FluentValidation;
using FactoryMind.Application.Features.Knowledge;

namespace FactoryMind.Api.Endpoints;

public sealed class UploadDocumentForm {
    public IFormFile File { get; init; } = null!;
    public string? Title { get; init; }
}

public sealed class UploadDocumentFormValidator : AbstractValidator<UploadDocumentForm> {
    public UploadDocumentFormValidator() {
        RuleFor(request => request.File)
            .NotNull().WithMessage("PDF file is required.");

        When(request => request.File is not null, () => {
            RuleFor(request => request.File.Length)
                .GreaterThan(0).WithMessage("PDF file must not be empty.")
                .LessThanOrEqualTo(DocumentUploadConstraints.MaximumFileSize)
                .WithMessage("PDF file must not exceed 100 MB.");
            RuleFor(request => request.File.FileName)
                .Must(fileName => Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Only PDF files are supported.")
                .MaximumLength(DocumentUploadConstraints.MaximumFileNameLength)
                .WithMessage("File name must not exceed 255 characters.");
            RuleFor(request => request.File.ContentType)
                .Equal(DocumentUploadConstraints.PdfContentType, StringComparer.OrdinalIgnoreCase)
                .WithMessage("File content type must be application/pdf.");
        });

        RuleFor(request => request.Title)
            .MaximumLength(DocumentUploadConstraints.MaximumTitleLength)
            .WithMessage("Document title must not exceed 200 characters.");
    }
}
