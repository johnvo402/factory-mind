using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.ExcelImports;
using FactoryMind.Application.Features.ExcelImports.ImportExcel;
using FactoryMind.Application.Features.ExcelImports.PreviewExcelImport;
using FactoryMind.Infrastructure.Excel;
using ClosedXML.Excel;

namespace FactoryMind.Tests;

public sealed class ExcelImportHandlerTests {
    [Fact]
    public async Task ClosedXml_reader_reads_the_first_worksheet_with_formatted_values() {
        await using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook()) {
            var worksheet = workbook.AddWorksheet("Machines");
            worksheet.Cell(1, 1).Value = "Code";
            worksheet.Cell(1, 2).Value = "Name";
            worksheet.Cell(2, 1).Value = "M-001";
            worksheet.Cell(2, 2).Value = "Injection";
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        var reader = new ClosedXmlWorkbookReader();

        var result = await reader.ReadAsync(stream, 10, CancellationToken.None);

        Assert.Equal(["Code", "Name"], result.Headers);
        Assert.Equal("M-001", Assert.Single(result.Rows)["Code"]);
    }

    [Fact]
    public async Task Preview_returns_bounded_rows_and_suggested_mapping() {
        var rows = Enumerable.Range(1, 12)
            .Select(index => Row(("Code", $"M-{index:000}"), ("Name", "Machine"), ("Status", "available")))
            .ToList();
        var reader = new FakeWorkbookReader(new(["Code", "Name", "Status"], rows, rows.Count));
        var handler = new PreviewExcelImportCommandHandler(reader);

        var result = await handler.Handle(
            new PreviewExcelImportCommand("machine", Stream.Null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(12, result.Value?.TotalRows);
        Assert.Equal(ExcelImportConstraints.PreviewRows, result.Value?.Rows.Count);
        Assert.Equal("Code", result.Value?.SuggestedMapping["code"]);
    }

    [Fact]
    public async Task Import_builds_and_persists_a_valid_machine_batch() {
        var reader = new FakeWorkbookReader(new(
            ["Code", "Name", "Status"],
            [Row(("Code", "m-001"), ("Name", "Injection"), ("Status", "AVAILABLE"))],
            1));
        var repository = new FakeImportRepository();
        var currentUser = new FakeCurrentUser();
        var handler = new ImportExcelCommandHandler(reader, repository, currentUser);

        var result = await handler.Handle(
            new ImportExcelCommand(
                "machine",
                Map(("code", "Code"), ("name", "Name"), ("status", "Status")),
                Stream.Null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value?.ImportedCount);
        var machine = Assert.Single(repository.Batch!.Machines);
        Assert.Equal(currentUser.CompanyId, machine.CompanyId);
        Assert.Equal("M-001", machine.Code);
        Assert.Equal("available", machine.Status);
        Assert.Equal(1, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Import_does_not_persist_when_any_row_is_invalid() {
        var reader = new FakeWorkbookReader(new(
            ["Code", "Name", "Status"],
            [
                Row(("Code", "M-001"), ("Name", "Injection"), ("Status", "available")),
                Row(("Code", "M-001"), ("Name", "Packing"), ("Status", "broken"))
            ],
            2));
        var repository = new FakeImportRepository();
        var handler = new ImportExcelCommandHandler(reader, repository, new FakeCurrentUser());

        var result = await handler.Handle(
            new ImportExcelCommand(
                "machine",
                Map(("code", "Code"), ("name", "Name"), ("status", "Status")),
                Stream.Null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value?.ImportedCount);
        Assert.Contains(result.Value!.Errors, error => error.Row == 3 && error.Field == "status");
        Assert.Contains(result.Value.Errors, error => error.Row == 3 && error.Field == "code");
        Assert.Null(repository.Batch);
        Assert.Equal(0, repository.SaveChangesCount);
    }

    [Fact]
    public async Task Inventory_import_resolves_material_code_inside_the_current_company() {
        var materialId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var reader = new FakeWorkbookReader(new(
            ["Material Code", "Warehouse Code", "Quantity"],
            [Row(("Material Code", "mat-pp"), ("Warehouse Code", "wh-raw"), ("Quantity", "1200.500"))],
            1));
        var repository = new FakeImportRepository {
            ReferenceData = new(
                new HashSet<string>(),
                new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) {
                    ["material:MAT-PP"] = materialId,
                    ["warehouse:WH-RAW"] = warehouseId
                })
        };
        var currentUser = new FakeCurrentUser();
        var handler = new ImportExcelCommandHandler(reader, repository, currentUser);

        var result = await handler.Handle(
            new ImportExcelCommand(
                "inventory",
                Map(
                    ("materialCode", "Material Code"),
                    ("warehouseCode", "Warehouse Code"),
                    ("quantity", "Quantity")),
                Stream.Null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var transaction = Assert.Single(repository.Batch!.InventoryTransactions);
        var balance = Assert.Single(repository.Batch.InventoryBalances);
        Assert.Equal(materialId, transaction.MaterialId);
        Assert.Equal(warehouseId, transaction.WarehouseId);
        Assert.Equal(currentUser.CompanyId, transaction.CompanyId);
        Assert.Equal(1200.500m, transaction.Quantity);
        Assert.Equal(1200.500m, balance.Quantity);
    }

    private static IReadOnlyDictionary<string, string> Row(params (string Key, string Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> Map(params (string Key, string Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "Manager";
    }

    private sealed class FakeWorkbookReader(
        ExcelWorkbookData workbook) : IExcelWorkbookReader {
        public Task<ExcelWorkbookData> ReadAsync(
            Stream content,
            int maximumRows,
            CancellationToken cancellationToken) => Task.FromResult(workbook);
    }

    private sealed class FakeImportRepository : IExcelImportRepository {
        public ExcelImportReferenceData ReferenceData { get; set; } = new(
            new HashSet<string>(),
            new Dictionary<string, Guid>());
        public ExcelImportBatch? Batch { get; private set; }
        public int SaveChangesCount { get; private set; }

        public Task<ExcelImportReferenceData> GetReferenceDataAsync(
            Guid companyId,
            string entityType,
            CancellationToken cancellationToken) => Task.FromResult(ReferenceData);

        public void Add(ExcelImportBatch batch) => Batch = batch;

        public Task SaveChangesAsync(CancellationToken cancellationToken) {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }
}
