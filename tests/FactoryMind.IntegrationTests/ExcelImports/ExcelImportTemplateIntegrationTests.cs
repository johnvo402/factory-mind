using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.ExcelImports;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;

namespace FactoryMind.IntegrationTests.ExcelImports;

[Collection(IntegrationTestCollection.Name)]
public sealed class ExcelImportTemplateIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    public static TheoryData<string> SupportedEntityTypes => new() {
        ExcelImportEntityTypes.Machine,
        ExcelImportEntityTypes.Material,
        ExcelImportEntityTypes.Product,
        ExcelImportEntityTypes.Inventory,
        ExcelImportEntityTypes.ProductionOrder
    };

    [Theory]
    [MemberData(nameof(SupportedEntityTypes))]
    public async Task Manager_can_download_a_valid_template(string entityType) {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);

        using var response = await Client.GetAsync(TemplateRoute(entityType));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ExcelImportConstraints.ContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(
            $"factorymind-{entityType.Replace('_', '-')}-import-template.xlsx",
            response.Content.Headers.ContentDisposition?.FileNameStar);
        var content = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(content);
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        Assert.Equal("Data", workbook.Worksheet(1).Name);
        Assert.Equal("Hướng dẫn", workbook.Worksheet(2).Name);
        Assert.Equal(
            ExcelImportDefinition.GetRequiredFields(entityType),
            workbook.Worksheet(1).Row(1).CellsUsed().Select(cell => cell.GetString()));
    }

    [Theory]
    [MemberData(nameof(SupportedEntityTypes))]
    public async Task Downloaded_template_round_trips_through_preview_and_import(string entityType) {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var values = await CreateValidRowAsync(entityType);
        using var templateResponse = await Client.GetAsync(TemplateRoute(entityType));
        templateResponse.EnsureSuccessStatusCode();
        var template = await templateResponse.Content.ReadAsByteArrayAsync();
        var workbook = FillDataRow(template, values);

        using var previewContent = WorkbookForm(entityType, workbook);
        using var previewResponse = await Client.PostAsync(
            ApiRoutes.ExcelImports.Group + ApiRoutes.ExcelImports.Preview,
            previewContent);
        previewResponse.EnsureSuccessStatusCode();
        var previewEnvelope = await previewResponse.Content
            .ReadFromJsonAsync<ApiResponse<ExcelPreviewResponse>>();
        var requiredFields = ExcelImportDefinition.GetRequiredFields(entityType)!;
        Assert.Equal(requiredFields, previewEnvelope?.Data?.RequiredFields);
        Assert.All(requiredFields, field =>
            Assert.Equal(field, previewEnvelope?.Data?.SuggestedMapping[field]));

        using var importContent = WorkbookForm(entityType, workbook);
        importContent.Add(
            new StringContent(JsonSerializer.Serialize(requiredFields.ToDictionary(field => field))),
            "mapping");
        using var importResponse = await Client.PostAsync(
            ApiRoutes.ExcelImports.Group + ApiRoutes.ExcelImports.Import,
            importContent);
        importResponse.EnsureSuccessStatusCode();
        var importEnvelope = await importResponse.Content
            .ReadFromJsonAsync<ApiResponse<ExcelImportResponse>>();
        Assert.Equal(1, importEnvelope?.Data?.ImportedCount);
        Assert.Empty(importEnvelope?.Data?.Errors ?? []);

        if (entityType == ExcelImportEntityTypes.ProductionOrder) {
            using var ordersResponse = await Client.GetAsync(ApiRoutes.ProductionOrders.Group);
            var body = await ordersResponse.Content.ReadAsStringAsync();
            Assert.Contains(values["number"], body, StringComparison.Ordinal);
            Assert.Contains("\"status\":\"planned\"", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Unsupported_template_type_returns_a_controlled_validation_error() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);

        using var response = await Client.GetAsync(TemplateRoute("work_center"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Template_download_requires_authentication() {
        using var response = await Client.GetAsync(TemplateRoute(ExcelImportEntityTypes.Machine));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Non_manager_cannot_download_a_template() {
        await LoginAsync(Client, TestData.CompanyAUserEmail);

        using var response = await Client.GetAsync(TemplateRoute(ExcelImportEntityTypes.Machine));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string TemplateRoute(string entityType) =>
        ApiRoutes.ExcelImports.Group
        + ApiRoutes.ExcelImports.Template.Replace(
            "{entityType}",
            entityType,
            StringComparison.Ordinal);

    private async Task<IReadOnlyDictionary<string, string>> CreateValidRowAsync(string entityType) {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        switch (entityType) {
            case ExcelImportEntityTypes.Machine:
                return new Dictionary<string, string> {
                    ["code"] = $"CNC-{suffix}",
                    ["name"] = "Máy CNC Smoke",
                    ["status"] = "available"
                };
            case ExcelImportEntityTypes.Material:
                return new Dictionary<string, string> {
                    ["code"] = $"STEEL-{suffix}",
                    ["name"] = "Thép tấm Smoke",
                    ["unit"] = "kg"
                };
            case ExcelImportEntityTypes.Product:
                return new Dictionary<string, string> {
                    ["code"] = $"TABLE-{suffix}",
                    ["name"] = "Bàn làm việc Smoke"
                };
            case ExcelImportEntityTypes.Inventory:
                var materialCode = $"MAT-{suffix}";
                var warehouseCode = $"WH-{suffix}";
                await PostPrerequisiteAsync(
                    ApiRoutes.Materials.Group,
                    new { code = materialCode, name = "Vật tư Smoke", unit = "kg" });
                await PostPrerequisiteAsync(
                    ApiRoutes.Warehouses.Group,
                    new { code = warehouseCode, name = "Kho Smoke", description = "Round trip" });
                return new Dictionary<string, string> {
                    ["materialCode"] = materialCode,
                    ["warehouseCode"] = warehouseCode,
                    ["quantity"] = "125.500"
                };
            case ExcelImportEntityTypes.ProductionOrder:
                var productCode = $"PRODUCT-{suffix}";
                await PostPrerequisiteAsync(
                    ApiRoutes.Products.Group,
                    new { code = productCode, name = "Sản phẩm Smoke" });
                return new Dictionary<string, string> {
                    ["number"] = $"PO-{suffix}",
                    ["productCode"] = productCode,
                    ["quantity"] = "100"
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(entityType));
        }
    }

    private async Task PostPrerequisiteAsync(string route, object request) {
        using var response = await Client.PostAsJsonAsync(route, request);
        response.EnsureSuccessStatusCode();
    }

    private static byte[] FillDataRow(
        byte[] template,
        IReadOnlyDictionary<string, string> values) {
        using var input = new MemoryStream(template);
        using var workbook = new XLWorkbook(input);
        var worksheet = workbook.Worksheet("Data");
        var fields = worksheet.Row(1).CellsUsed().Select(cell => cell.GetString()).ToList();
        for (var index = 0; index < fields.Count; index++) {
            worksheet.Cell(2, index + 1).Value = values[fields[index]];
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    private static MultipartFormDataContent WorkbookForm(string entityType, byte[] workbook) {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(entityType), "entityType");
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue(ExcelImportConstraints.ContentType);
        content.Add(file, "file", $"{entityType}.xlsx");
        return content;
    }
}
