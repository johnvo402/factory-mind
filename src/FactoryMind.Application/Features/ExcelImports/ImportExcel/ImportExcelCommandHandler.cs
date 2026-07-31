using System.Globalization;
using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ExcelImports.ImportExcel;

public sealed class ImportExcelCommandHandler(
    IExcelWorkbookReader workbookReader,
    IExcelImportRepository repository,
    ICurrentUser currentUser) : IRequestHandler<ImportExcelCommand, Result<ExcelImportResponse>> {
    public async ValueTask<Result<ExcelImportResponse>> Handle(
        ImportExcelCommand command,
        CancellationToken cancellationToken) {
        var entityType = command.EntityType.Trim().ToLowerInvariant();
        var fields = ExcelImportDefinition.GetRequiredFields(entityType);
        if (fields is null) {
            return Result<ExcelImportResponse>.Failure(ExcelImportErrors.InvalidEntityType);
        }

        ExcelWorkbookData workbook;
        try {
            workbook = await workbookReader.ReadAsync(
                command.Content,
                ExcelImportConstraints.MaximumRows,
                cancellationToken);
        } catch (ExcelWorkbookException) {
            return Result<ExcelImportResponse>.Failure(ExcelImportErrors.InvalidWorkbook);
        }

        if (!HasValidMapping(command.Mapping, fields, workbook.Headers)) {
            return Result<ExcelImportResponse>.Failure(ExcelImportErrors.InvalidMapping);
        }

        var referenceData = await repository.GetReferenceDataAsync(
            currentUser.CompanyId,
            entityType,
            cancellationToken);
        var plan = BuildPlan(entityType, workbook.Rows, command.Mapping, referenceData);
        if (plan.Errors.Count > 0) {
            return Result<ExcelImportResponse>.Success(new(0, plan.Errors));
        }

        repository.Add(plan.Batch);
        await repository.SaveChangesAsync(cancellationToken);
        return Result<ExcelImportResponse>.Success(new(plan.Batch.Count, []));
    }

    private ExcelImportPlan BuildPlan(
        string entityType,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        IReadOnlyDictionary<string, string> mapping,
        ExcelImportReferenceData referenceData) {
        var machines = new List<Machine>();
        var materials = new List<Material>();
        var products = new List<Product>();
        var inventories = new List<Inventory>();
        var orders = new List<ProductionOrder>();
        var errors = new List<ExcelRowError>();
        var keys = new HashSet<string>(referenceData.ExistingKeys, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        for (var index = 0; index < rows.Count; index++) {
            var rowNumber = index + 2;
            var row = rows[index];
            switch (entityType) {
                case ExcelImportEntityTypes.Machine:
                    AddMachine(row, mapping, rowNumber, keys, machines, errors, now);
                    break;
                case ExcelImportEntityTypes.Material:
                    AddMaterial(row, mapping, rowNumber, keys, materials, errors, now);
                    break;
                case ExcelImportEntityTypes.Product:
                    AddProduct(row, mapping, rowNumber, keys, products, errors, now);
                    break;
                case ExcelImportEntityTypes.Inventory:
                    AddInventory(row, mapping, rowNumber, keys, referenceData.RelatedIds, inventories, errors, now);
                    break;
                case ExcelImportEntityTypes.ProductionOrder:
                    AddProductionOrder(row, mapping, rowNumber, keys, referenceData.RelatedIds, orders, errors, now);
                    break;
            }
        }

        return new ExcelImportPlan(
            new ExcelImportBatch(machines, materials, products, inventories, orders),
            errors);
    }

    private void AddMachine(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mapping,
        int rowNumber,
        HashSet<string> keys,
        List<Machine> entities,
        List<ExcelRowError> errors,
        DateTime now) {
        var start = errors.Count;
        var code = Required(row, mapping, "code", rowNumber, MachineConstraints.MaximumCodeLength, errors)
            .ToUpperInvariant();
        var name = Required(row, mapping, "name", rowNumber, MachineConstraints.MaximumNameLength, errors);
        var status = Required(row, mapping, "status", rowNumber, 30, errors).ToLowerInvariant();
        if (status.Length > 0 && !MachineStatuses.All.Contains(status)) {
            errors.Add(new(rowNumber, "status", "Machine status is invalid."));
        }
        AddDuplicateError(code, "code", rowNumber, keys, errors);
        if (errors.Count == start) {
            entities.Add(new Machine {
                CompanyId = currentUser.CompanyId,
                Code = code,
                Name = name,
                Status = status,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private void AddMaterial(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mapping,
        int rowNumber,
        HashSet<string> keys,
        List<Material> entities,
        List<ExcelRowError> errors,
        DateTime now) {
        var start = errors.Count;
        var code = Required(row, mapping, "code", rowNumber, MaterialConstraints.MaximumCodeLength, errors)
            .ToUpperInvariant();
        var name = Required(row, mapping, "name", rowNumber, MaterialConstraints.MaximumNameLength, errors);
        var unit = Required(row, mapping, "unit", rowNumber, MaterialConstraints.MaximumUnitLength, errors);
        AddDuplicateError(code, "code", rowNumber, keys, errors);
        if (errors.Count == start) {
            entities.Add(new Material {
                CompanyId = currentUser.CompanyId,
                Code = code,
                Name = name,
                Unit = unit,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private void AddProduct(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mapping,
        int rowNumber,
        HashSet<string> keys,
        List<Product> entities,
        List<ExcelRowError> errors,
        DateTime now) {
        var start = errors.Count;
        var code = Required(row, mapping, "code", rowNumber, ProductConstraints.MaximumCodeLength, errors)
            .ToUpperInvariant();
        var name = Required(row, mapping, "name", rowNumber, ProductConstraints.MaximumNameLength, errors);
        AddDuplicateError(code, "code", rowNumber, keys, errors);
        if (errors.Count == start) {
            entities.Add(new Product {
                CompanyId = currentUser.CompanyId,
                Code = code,
                Name = name,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private void AddInventory(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mapping,
        int rowNumber,
        HashSet<string> keys,
        IReadOnlyDictionary<string, Guid> relatedIds,
        List<Inventory> entities,
        List<ExcelRowError> errors,
        DateTime now) {
        var start = errors.Count;
        var materialCode = Required(row, mapping, "materialCode", rowNumber, MaterialConstraints.MaximumCodeLength, errors)
            .ToUpperInvariant();
        var warehouse = Required(row, mapping, "warehouse", rowNumber, InventoryConstraints.MaximumWarehouseLength, errors);
        var quantity = Decimal(row, mapping, "quantity", rowNumber, allowZero: true, errors);
        if (!relatedIds.TryGetValue(materialCode, out var materialId)) {
            errors.Add(new(rowNumber, "materialCode", "Material code was not found in this company."));
        }
        AddDuplicateError($"{materialId:N}|{warehouse}", "warehouse", rowNumber, keys, errors);
        if (errors.Count == start) {
            entities.Add(new Inventory {
                CompanyId = currentUser.CompanyId,
                MaterialId = materialId,
                Warehouse = warehouse,
                Quantity = quantity,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private void AddProductionOrder(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mapping,
        int rowNumber,
        HashSet<string> keys,
        IReadOnlyDictionary<string, Guid> relatedIds,
        List<ProductionOrder> entities,
        List<ExcelRowError> errors,
        DateTime now) {
        var start = errors.Count;
        var number = Required(row, mapping, "number", rowNumber, ProductionOrderConstraints.MaximumNumberLength, errors)
            .ToUpperInvariant();
        var productCode = Required(row, mapping, "productCode", rowNumber, ProductConstraints.MaximumCodeLength, errors)
            .ToUpperInvariant();
        var quantity = Decimal(row, mapping, "quantity", rowNumber, allowZero: false, errors);
        var status = Required(row, mapping, "status", rowNumber, ProductionOrderConstraints.MaximumStatusLength, errors)
            .ToLowerInvariant();
        if (!relatedIds.TryGetValue(productCode, out var productId)) {
            errors.Add(new(rowNumber, "productCode", "Product code was not found in this company."));
        }
        if (status.Length > 0 && !ProductionOrderStatuses.All.Contains(status)) {
            errors.Add(new(rowNumber, "status", "Production order status is invalid."));
        }
        AddDuplicateError(number, "number", rowNumber, keys, errors);
        if (errors.Count == start) {
            entities.Add(new ProductionOrder {
                CompanyId = currentUser.CompanyId,
                Number = number,
                ProductId = productId,
                Quantity = quantity,
                Status = status,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    private static string Required(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mapping,
        string field,
        int rowNumber,
        int maximumLength,
        List<ExcelRowError> errors) {
        var value = row.GetValueOrDefault(mapping[field], string.Empty).Trim();
        if (value.Length == 0) {
            errors.Add(new(rowNumber, field, $"{field} is required."));
        } else if (value.Length > maximumLength) {
            errors.Add(new(rowNumber, field, $"{field} must not exceed {maximumLength} characters."));
        }
        return value;
    }

    private static decimal Decimal(
        IReadOnlyDictionary<string, string> row,
        IReadOnlyDictionary<string, string> mapping,
        string field,
        int rowNumber,
        bool allowZero,
        List<ExcelRowError> errors) {
        var value = row.GetValueOrDefault(mapping[field], string.Empty).Trim();
        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            || (allowZero ? number < 0 : number <= 0)
            || !HasValidPrecision(value)) {
            errors.Add(new(rowNumber, field, allowZero
                ? "Quantity must be a non-negative number with at most 3 decimal places."
                : "Quantity must be a positive number with at most 3 decimal places."));
            return 0;
        }
        return number;
    }

    private static bool HasValidPrecision(string value) {
        var digits = value.Count(char.IsDigit);
        var separator = value.LastIndexOf('.');
        var scale = separator < 0 ? 0 : value.Length - separator - 1;
        return digits <= 18 && scale <= 3;
    }

    private static void AddDuplicateError(
        string key,
        string field,
        int rowNumber,
        HashSet<string> keys,
        List<ExcelRowError> errors) {
        if (key.Length > 0 && !keys.Add(key)) {
            errors.Add(new(rowNumber, field, "The business key already exists or is duplicated in this workbook."));
        }
    }

    private static bool HasValidMapping(
        IReadOnlyDictionary<string, string> mapping,
        IReadOnlyList<string> fields,
        IReadOnlyList<string> headers) => fields.All(field =>
        mapping.TryGetValue(field, out var header)
        && headers.Contains(header, StringComparer.OrdinalIgnoreCase));

    private sealed record ExcelImportPlan(
        ExcelImportBatch Batch,
        IReadOnlyList<ExcelRowError> Errors);
}
