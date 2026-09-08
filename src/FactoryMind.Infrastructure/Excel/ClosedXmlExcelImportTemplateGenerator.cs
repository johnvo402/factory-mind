using ClosedXML.Excel;
using FactoryMind.Application.Features.ExcelImports;
using System.Globalization;

namespace FactoryMind.Infrastructure.Excel;

public sealed class ClosedXmlExcelImportTemplateGenerator : IExcelImportTemplateGenerator {
    public ExcelImportTemplateFile Generate(string entityType) {
        var fields = ExcelImportDefinition.GetRequiredFields(entityType)
            ?? throw new ArgumentException("Unsupported Excel import entity type.", nameof(entityType));
        var description = ExcelImportTemplateDefinition.Get(entityType)
            ?? throw new InvalidOperationException("Excel import template guidance is missing.");

        using var workbook = new XLWorkbook();
        AddDataWorksheet(workbook, fields);
        AddInstructionsWorksheet(workbook, description, fields);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ExcelImportTemplateFile(
            stream.ToArray(),
            $"factorymind-{entityType.Replace('_', '-')}-import-template.xlsx");
    }

    private static void AddDataWorksheet(XLWorkbook workbook, IReadOnlyList<string> fields) {
        var worksheet = workbook.AddWorksheet("Data");
        for (var index = 0; index < fields.Count; index++) {
            worksheet.Cell(1, index + 1).Value = fields[index];
        }

        var header = worksheet.Range(1, 1, 1, fields.Count);
        header.Style.Font.Bold = true;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#25634F");
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        header.SetAutoFilter();
        worksheet.SheetView.FreezeRows(1);
        worksheet.Row(1).Height = 24;

        for (var index = 0; index < fields.Count; index++) {
            var field = fields[index];
            worksheet.Column(index + 1).Width = field is "name" ? 30 : 22;
            if (field == "quantity") {
                worksheet.Column(index + 1).Style.NumberFormat.Format = "0.###";
            }
        }
    }

    private static void AddInstructionsWorksheet(
        XLWorkbook workbook,
        ExcelImportTemplateDescription description,
        IReadOnlyList<string> requiredFields) {
        var worksheet = workbook.AddWorksheet("Hướng dẫn");
        worksheet.Cell("A1").Value = "HƯỚNG DẪN IMPORT FACTORYMIND";
        worksheet.Range("A1:E1").Merge();
        worksheet.Cell("A1").Style.Font.Bold = true;
        worksheet.Cell("A1").Style.Font.FontSize = 16;
        worksheet.Cell("A1").Style.Font.FontColor = XLColor.White;
        worksheet.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromHtml("#25634F");
        worksheet.Cell("A3").Value = "Loại dữ liệu";
        worksheet.Cell("B3").Value = description.EntityLabel;
        worksheet.Cell("A4").Value = "Các cột bắt buộc";
        worksheet.Cell("B4").Value = string.Join(" | ", requiredFields);
        worksheet.Range("B3:E3").Merge();
        worksheet.Range("B4:E4").Merge();
        worksheet.Range("A3:A4").Style.Font.Bold = true;

        const int tableHeaderRow = 6;
        var tableHeaders = new[] { "Cột", "Ý nghĩa", "Giá trị hợp lệ", "Ví dụ", "Lưu ý" };
        for (var index = 0; index < tableHeaders.Length; index++) {
            worksheet.Cell(tableHeaderRow, index + 1).Value = tableHeaders[index];
        }
        var tableHeader = worksheet.Range(tableHeaderRow, 1, tableHeaderRow, tableHeaders.Length);
        tableHeader.Style.Font.Bold = true;
        tableHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCEFE8");

        for (var index = 0; index < description.Fields.Count; index++) {
            var field = description.Fields[index];
            var row = tableHeaderRow + index + 1;
            worksheet.Cell(row, 1).Value = field.Field;
            worksheet.Cell(row, 2).Value = field.Description;
            worksheet.Cell(row, 3).Value = field.AllowedValues ?? "Theo mô tả và quy tắc import";
            worksheet.Cell(row, 4).Value = field.Example;
            worksheet.Cell(row, 5).Value = field.Notes ?? string.Empty;
        }

        var notesRow = tableHeaderRow + description.Fields.Count + 2;
        worksheet.Cell(notesRow, 1).Value = "Lưu ý chung";
        worksheet.Cell(notesRow, 1).Style.Font.Bold = true;
        var notes = new List<string> {
            "Chỉ hỗ trợ tệp .xlsx.",
            $"Kích thước tệp tối đa {ExcelImportConstraints.MaximumFileSize / 1024 / 1024} MB.",
            $"Tối đa {ExcelImportConstraints.MaximumRows.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} dòng dữ liệu.",
            "Không đổi tên các cột nếu muốn hệ thống tự nhận diện mapping.",
            "Dòng đầu tiên của sheet Data là header.",
            "Một dòng tương ứng với một bản ghi.",
            "Không xóa các cột bắt buộc."
        };
        notes.AddRange(description.Notes);
        for (var index = 0; index < notes.Count; index++) {
            worksheet.Cell(notesRow + index + 1, 1).Value = $"• {notes[index]}";
            worksheet.Range(notesRow + index + 1, 1, notesRow + index + 1, 5).Merge();
        }

        worksheet.Column(1).Width = 22;
        worksheet.Column(2).Width = 38;
        worksheet.Column(3).Width = 34;
        worksheet.Column(4).Width = 24;
        worksheet.Column(5).Width = 52;
        worksheet.RangeUsed()!.Style.Alignment.WrapText = true;
        worksheet.RangeUsed()!.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        worksheet.SheetView.FreezeRows(tableHeaderRow);
    }
}
