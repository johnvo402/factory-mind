using FactoryMind.Application.Features.ExcelImports;
using FluentValidation;

namespace FactoryMind.Api.Endpoints;

public sealed class PreviewExcelImportForm {
    public string EntityType { get; init; } = string.Empty;
    public IFormFile File { get; init; } = null!;
}

public sealed class ImportExcelForm {
    public string EntityType { get; init; } = string.Empty;
    public string Mapping { get; init; } = string.Empty;
    public IFormFile File { get; init; } = null!;
}

public sealed class PreviewExcelImportFormValidator : AbstractValidator<PreviewExcelImportForm> {
    public PreviewExcelImportFormValidator() {
        RuleFor(form => form.EntityType)
            .Must(entityType => entityType is not null
                && ExcelImportEntityTypes.All.Contains(entityType.Trim().ToLowerInvariant()))
            .WithMessage("Excel import entity type is invalid.");
        AddFileRules(this);
    }

    private static void AddFileRules(AbstractValidator<PreviewExcelImportForm> validator) {
        validator.RuleFor(form => form.File).NotNull().WithMessage("Excel file is required.");
        validator.When(form => form.File is not null, () => {
            validator.RuleFor(form => form.File.Length)
                .GreaterThan(0).WithMessage("Excel file must not be empty.")
                .LessThanOrEqualTo(ExcelImportConstraints.MaximumFileSize)
                .WithMessage("Excel file must not exceed 10 MB.");
            validator.RuleFor(form => form.File.FileName)
                .Must(fileName => Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Only .xlsx files are supported.");
        });
    }
}

public sealed class ImportExcelFormValidator : AbstractValidator<ImportExcelForm> {
    public ImportExcelFormValidator() {
        RuleFor(form => form.EntityType)
            .Must(entityType => entityType is not null
                && ExcelImportEntityTypes.All.Contains(entityType.Trim().ToLowerInvariant()))
            .WithMessage("Excel import entity type is invalid.");
        RuleFor(form => form.Mapping)
            .NotEmpty().WithMessage("Excel column mapping is required.");
        RuleFor(form => form.File).NotNull().WithMessage("Excel file is required.");
        When(form => form.File is not null, () => {
            RuleFor(form => form.File.Length)
                .GreaterThan(0).WithMessage("Excel file must not be empty.")
                .LessThanOrEqualTo(ExcelImportConstraints.MaximumFileSize)
                .WithMessage("Excel file must not exceed 10 MB.");
            RuleFor(form => form.File.FileName)
                .Must(fileName => Path.GetExtension(fileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                .WithMessage("Only .xlsx files are supported.");
        });
    }
}
