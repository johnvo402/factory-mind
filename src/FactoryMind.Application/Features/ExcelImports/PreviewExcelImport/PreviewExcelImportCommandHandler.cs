using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ExcelImports.PreviewExcelImport;

public sealed class PreviewExcelImportCommandHandler(
    IExcelWorkbookReader workbookReader) : IRequestHandler<PreviewExcelImportCommand, Result<ExcelPreviewResponse>> {
    public async ValueTask<Result<ExcelPreviewResponse>> Handle(
        PreviewExcelImportCommand command,
        CancellationToken cancellationToken) {
        var entityType = command.EntityType.Trim().ToLowerInvariant();
        var fields = ExcelImportDefinition.GetRequiredFields(entityType);
        if (fields is null) {
            return Result<ExcelPreviewResponse>.Failure(ExcelImportErrors.InvalidEntityType);
        }

        try {
            var workbook = await workbookReader.ReadAsync(
                command.Content,
                ExcelImportConstraints.MaximumRows,
                cancellationToken);
            var response = new ExcelPreviewResponse(
                workbook.Headers,
                workbook.Rows.Take(ExcelImportConstraints.PreviewRows).ToList(),
                workbook.TotalRows,
                fields,
                ExcelImportDefinition.SuggestMapping(workbook.Headers, fields));
            return Result<ExcelPreviewResponse>.Success(response);
        } catch (ExcelWorkbookException) {
            return Result<ExcelPreviewResponse>.Failure(ExcelImportErrors.InvalidWorkbook);
        }
    }
}
