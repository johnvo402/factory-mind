using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Application.Features.ExcelImports;

public sealed record ExcelImportTemplateFile(byte[] Content, string FileName);

public sealed record ExcelImportTemplateField(
    string Field,
    string Description,
    string Example,
    string? AllowedValues = null,
    string? Notes = null);

public sealed record ExcelImportTemplateDescription(
    string EntityLabel,
    IReadOnlyList<ExcelImportTemplateField> Fields,
    IReadOnlyList<string> Notes);

public interface IExcelImportTemplateGenerator {
    ExcelImportTemplateFile Generate(string entityType);
}

public static class ExcelImportTemplateDefinition {
    private const string UniqueCompanyCode = "Mã phải duy nhất trong công ty.";
    private const string PositiveQuantity = "Phải lớn hơn 0, có tối đa 18 chữ số và tối đa 3 chữ số thập phân.";

    private static readonly IReadOnlyDictionary<string, ExcelImportTemplateDescription> Descriptions =
        new Dictionary<string, ExcelImportTemplateDescription>(StringComparer.Ordinal) {
            [ExcelImportEntityTypes.Machine] = new(
                "Máy móc",
                [
                    new("code", "Mã máy duy nhất", "CNC-001", Notes: UniqueCompanyCode),
                    new("name", "Tên máy", "Máy CNC số 1"),
                    new(
                        "status",
                        "Trạng thái quản trị của máy",
                        MachineStatuses.Available,
                        string.Join(", ", [
                            MachineStatuses.Available,
                            MachineStatuses.Maintenance,
                            MachineStatuses.Offline
                        ]),
                        "Chỉ chấp nhận ba giá trị này; running do hệ thống quản lý.")
                ],
                [UniqueCompanyCode]),
            [ExcelImportEntityTypes.Material] = new(
                "Nguyên liệu",
                [
                    new("code", "Mã nguyên liệu duy nhất", "STEEL-01", Notes: UniqueCompanyCode),
                    new("name", "Tên nguyên liệu", "Thép tấm"),
                    new("unit", "Đơn vị tính dạng văn bản", "kg", "Ví dụ: kg, m, m2, pcs, L",
                        "Hệ thống nhận giá trị văn bản; danh sách ví dụ không phải enum giới hạn.")
                ],
                [UniqueCompanyCode]),
            [ExcelImportEntityTypes.Product] = new(
                "Sản phẩm",
                [
                    new("code", "Mã sản phẩm duy nhất", "TABLE-01", Notes: UniqueCompanyCode),
                    new("name", "Tên sản phẩm", "Bàn làm việc")
                ],
                [UniqueCompanyCode, "BOM và Routing được quản lý riêng, không thuộc file import này."]),
            [ExcelImportEntityTypes.Inventory] = new(
                "Tồn kho nguyên liệu đầu kỳ",
                [
                    new("materialCode", "Mã nguyên liệu đã tồn tại trong công ty", "STEEL-01",
                        Notes: "Nguyên liệu phải tồn tại trong cùng công ty."),
                    new("warehouseCode", "Mã kho đang hoạt động", "WH-RM-01",
                        Notes: "Kho phải tồn tại trong cùng công ty và đang hoạt động."),
                    new("quantity", "Số lượng tồn đầu kỳ", "125.500", Notes: PositiveQuantity)
                ],
                [
                    "Import tồn kho hiện tại tạo Opening Balance và giao dịch Receipt; đây không phải thao tác điều chỉnh tùy ý.",
                    "Mỗi cặp materialCode + warehouseCode chỉ được import khi chưa có số dư tồn kho và chỉ xuất hiện một lần trong workbook."
                ]),
            [ExcelImportEntityTypes.ProductionOrder] = new(
                "Lệnh sản xuất",
                [
                    new("number", "Số lệnh sản xuất duy nhất", "PO-2026-001",
                        Notes: "Số lệnh phải duy nhất trong công ty."),
                    new("productCode", "Mã Product đã tồn tại trong công ty", "TABLE-01",
                        Notes: "Product phải tồn tại trong cùng công ty."),
                    new("quantity", "Số lượng cần sản xuất", "100", Notes: PositiveQuantity)
                ],
                [
                    "Lệnh sản xuất được import với trạng thái Planned.",
                    "Không có cột status; Release, Start, thực thi công đoạn và Complete được thực hiện riêng trong hệ thống."
                ])
        };

    public static ExcelImportTemplateDescription? Get(string entityType) =>
        Descriptions.GetValueOrDefault(entityType);
}
