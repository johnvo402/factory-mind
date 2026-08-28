using System.Globalization;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Domain.Manufacturing;
using Microsoft.EntityFrameworkCore;

namespace FactoryMind.Infrastructure.Persistence.Chat;

public sealed class EfBusinessContextRepository(
    FactoryMindDbContext dbContext) : IBusinessContextRepository {
    public async Task<IReadOnlyList<BusinessDataRecord>> RetrieveAsync(
        Guid companyId,
        BusinessDataScope scopes,
        string? machineStatus,
        string? productionOrderStatus,
        int limitPerScope,
        CancellationToken cancellationToken) {
        var records = new List<BusinessDataRecord>();

        if (scopes.HasFlag(BusinessDataScope.Machines)) {
            var machines = await dbContext.Machines
                .AsNoTracking()
                .Where(machine => machine.CompanyId == companyId)
                .Where(machine => machineStatus == null || machine.Status == machineStatus)
                .OrderBy(machine => machine.Code)
                .Take(limitPerScope)
                .Select(machine => new {
                    machine.Id,
                    machine.Code,
                    machine.Name,
                    machine.Status,
                    machine.UpdatedAt
                })
                .ToListAsync(cancellationToken);
            records.AddRange(machines.Select(machine => new BusinessDataRecord(
                machine.Id,
                "machine",
                $"{machine.Code} - {machine.Name}",
                $"Trạng thái: {MachineStatusLabel(machine.Status)}. "
                + $"Cập nhật: {FormatTimestamp(machine.UpdatedAt)}.")));
        }

        if (scopes.HasFlag(BusinessDataScope.Materials)) {
            var materials = await dbContext.Materials
                .AsNoTracking()
                .Where(material => material.CompanyId == companyId)
                .OrderBy(material => material.Code)
                .Take(limitPerScope)
                .Select(material => new {
                    material.Id,
                    material.Code,
                    material.Name,
                    material.Unit
                })
                .ToListAsync(cancellationToken);
            records.AddRange(materials.Select(material => new BusinessDataRecord(
                material.Id,
                "material",
                $"{material.Code} - {material.Name}",
                $"Đơn vị tính: {material.Unit}.")));
        }

        if (scopes.HasFlag(BusinessDataScope.Inventory)) {
            var inventories = await dbContext.InventoryBalances
                .AsNoTracking()
                .Where(balance => balance.CompanyId == companyId)
                .OrderByDescending(balance => balance.Quantity)
                .ThenBy(balance => balance.Material!.Code)
                .Take(limitPerScope)
                .Select(balance => new {
                    balance.Id,
                    balance.Material!.Code,
                    balance.Material.Name,
                    balance.Material.Unit,
                    WarehouseCode = balance.Warehouse!.Code,
                    WarehouseName = balance.Warehouse.Name,
                    balance.Quantity,
                    balance.UpdatedAt
                })
                .ToListAsync(cancellationToken);
            records.AddRange(inventories.Select(inventory => new BusinessDataRecord(
                inventory.Id,
                "inventory",
                $"{inventory.Code} - {inventory.Name}",
                $"Kho lưu trữ: {inventory.WarehouseCode} - {inventory.WarehouseName}. "
                + $"Số lượng hiện có: {Format(inventory.Quantity)} {inventory.Unit}. "
                + $"Cập nhật: {FormatTimestamp(inventory.UpdatedAt)}.")));
        }

        if (scopes.HasFlag(BusinessDataScope.Products)) {
            var products = await dbContext.Products
                .AsNoTracking()
                .Where(product => product.CompanyId == companyId)
                .OrderBy(product => product.Code)
                .Take(limitPerScope)
                .Select(product => new {
                    product.Id,
                    product.Code,
                    product.Name
                })
                .ToListAsync(cancellationToken);
            records.AddRange(products.Select(product => new BusinessDataRecord(
                product.Id,
                "product",
                $"{product.Code} - {product.Name}",
                "Sản phẩm đang có trong danh mục sản xuất.")));
        }

        if (scopes.HasFlag(BusinessDataScope.ProductionOrders)) {
            var orders = await dbContext.ProductionOrders
                .AsNoTracking()
                .Where(order => order.CompanyId == companyId)
                .Where(order => productionOrderStatus == null || order.Status == productionOrderStatus)
                .OrderByDescending(order => order.UpdatedAt)
                .Take(limitPerScope)
                .Select(order => new {
                    order.Id,
                    order.Number,
                    ProductCode = order.Product!.Code,
                    ProductName = order.Product.Name,
                    order.Quantity,
                    order.Status,
                    order.UpdatedAt
                })
                .ToListAsync(cancellationToken);
            records.AddRange(orders.Select(order => new BusinessDataRecord(
                order.Id,
                "production_order",
                $"{order.Number} - {order.ProductCode} {order.ProductName}",
                $"Số lượng: {Format(order.Quantity)}. "
                + $"Trạng thái: {ProductionOrderStatusLabel(order.Status)}. "
                + $"Cập nhật: {FormatTimestamp(order.UpdatedAt)}.")));
        }

        return records;
    }

    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTime value) =>
        value.ToUniversalTime().ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string MachineStatusLabel(string status) => status switch {
        MachineStatuses.Available => "Sẵn sàng",
        MachineStatuses.Running => "Đang vận hành",
        MachineStatuses.Maintenance => "Đang bảo trì",
        MachineStatuses.Offline => "Ngừng hoạt động",
        _ => status
    };

    private static string ProductionOrderStatusLabel(string status) => status switch {
        ProductionOrderStatuses.Planned => "Đã lên kế hoạch",
        ProductionOrderStatuses.InProgress => "Đang sản xuất",
        ProductionOrderStatuses.Completed => "Đã hoàn thành",
        ProductionOrderStatuses.Cancelled => "Đã hủy",
        _ => status
    };
}
