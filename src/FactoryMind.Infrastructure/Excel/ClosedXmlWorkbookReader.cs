using ClosedXML.Excel;
using FactoryMind.Application.Features.ExcelImports;

namespace FactoryMind.Infrastructure.Excel;

public sealed class ClosedXmlWorkbookReader : IExcelWorkbookReader {
    public Task<ExcelWorkbookData> ReadAsync(
        Stream content,
        int maximumRows,
        CancellationToken cancellationToken) {
        try {
            cancellationToken.ThrowIfCancellationRequested();
            using var workbook = new XLWorkbook(content);
            var worksheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new ExcelWorkbookException("The workbook does not contain a worksheet.");
            var usedRange = worksheet.RangeUsed();
            if (usedRange is null) {
                throw new ExcelWorkbookException("The worksheet is empty.");
            }

            var columnCount = usedRange.ColumnCount();
            var totalRows = usedRange.RowCount() - 1;
            if (columnCount is < 1 or > ExcelImportConstraints.MaximumColumns
                || totalRows is < 1
                || totalRows > maximumRows) {
                throw new ExcelWorkbookException("The worksheet exceeds the allowed limits.");
            }

            var headers = Enumerable.Range(1, columnCount)
                .Select(column => usedRange.Cell(1, column).GetFormattedString().Trim())
                .ToList();
            if (headers.Any(string.IsNullOrWhiteSpace)
                || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Count) {
                throw new ExcelWorkbookException("Headers must be non-empty and unique.");
            }

            var rows = new List<IReadOnlyDictionary<string, string>>(totalRows);
            for (var rowIndex = 2; rowIndex <= usedRange.RowCount(); rowIndex++) {
                cancellationToken.ThrowIfCancellationRequested();
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var columnIndex = 1; columnIndex <= columnCount; columnIndex++) {
                    row[headers[columnIndex - 1]] = usedRange
                        .Cell(rowIndex, columnIndex)
                        .GetFormattedString()
                        .Trim();
                }
                rows.Add(row);
            }

            return Task.FromResult(new ExcelWorkbookData(headers, rows, totalRows));
        } catch (ExcelWorkbookException) {
            throw;
        } catch (Exception exception) when (exception is not OperationCanceledException) {
            throw new ExcelWorkbookException("The workbook could not be read.", exception);
        }
    }
}
