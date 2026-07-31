using System.Globalization;
using FactoryMind.Application.Features.Chat;
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
                $"status={machine.Status}; updatedAt={machine.UpdatedAt:O}")));
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
                $"unit={material.Unit}")));
        }

        if (scopes.HasFlag(BusinessDataScope.Inventory)) {
            var inventories = await dbContext.Inventories
                .AsNoTracking()
                .Where(inventory => inventory.CompanyId == companyId)
                .OrderByDescending(inventory => inventory.Quantity)
                .ThenBy(inventory => inventory.Material!.Code)
                .Take(limitPerScope)
                .Select(inventory => new {
                    inventory.Id,
                    inventory.Material!.Code,
                    inventory.Material.Name,
                    inventory.Material.Unit,
                    inventory.Warehouse,
                    inventory.Quantity,
                    inventory.UpdatedAt
                })
                .ToListAsync(cancellationToken);
            records.AddRange(inventories.Select(inventory => new BusinessDataRecord(
                inventory.Id,
                "inventory",
                $"{inventory.Code} - {inventory.Name}",
                $"warehouse={inventory.Warehouse}; quantity={Format(inventory.Quantity)} "
                + $"{inventory.Unit}; updatedAt={inventory.UpdatedAt:O}")));
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
                "Product master data record.")));
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
                $"quantity={Format(order.Quantity)}; status={order.Status}; updatedAt={order.UpdatedAt:O}")));
        }

        return records;
    }

    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
