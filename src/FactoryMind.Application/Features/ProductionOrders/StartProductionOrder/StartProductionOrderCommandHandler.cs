using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Shared.Contracts;
using Mediator;

namespace FactoryMind.Application.Features.ProductionOrders.StartProductionOrder;

public sealed class StartProductionOrderCommandHandler(
    IProductionExecutionRepository executionRepository,
    IMaterialRepository materialRepository,
    IWarehouseRepository warehouseRepository,
    MaterialRequirementCalculator calculator,
    ICurrentUser currentUser)
    : IRequestHandler<StartProductionOrderCommand, Result<ProductionOrderResponse>> {
    public async ValueTask<Result<ProductionOrderResponse>> Handle(
        StartProductionOrderCommand command,
        CancellationToken cancellationToken) {
        var order = await executionRepository.GetAsync(
            command.ProductionOrderId,
            currentUser.CompanyId,
            cancellationToken);
        if (order is null) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.NotFound);
        }
        if (order.Status != ProductionOrderStatuses.Released) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.InvalidTransition);
        }
        if (order.BillOfMaterial is null) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.LockedBomRequired);
        }
        if (order.RoutingId is null) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.LockedRoutingRequired);
        }
        if (command.Allocations.Count == 0) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.AllocationsRequired);
        }
        if (command.Allocations.Any(allocation => allocation.Quantity <= 0)) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.AllocationQuantityInvalid);
        }

        var requirements = calculator.Calculate(
            order.BillOfMaterial,
            order.Quantity,
            new Dictionary<Guid, decimal>());
        var requiredByMaterial = requirements.Materials.ToDictionary(
            material => material.MaterialId,
            material => material.RequiredQuantity);

        var materials = new Dictionary<Guid, Material>();
        foreach (var materialId in command.Allocations.Select(allocation => allocation.MaterialId).Distinct()) {
            var material = await materialRepository.GetByIdAsync(
                materialId,
                currentUser.CompanyId,
                cancellationToken);
            if (material is null) {
                return Result<ProductionOrderResponse>.Failure(InventoryErrors.MaterialNotFound);
            }
            if (!requiredByMaterial.ContainsKey(material.Id)) {
                return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.ExtraAllocationMaterial);
            }
            materials.Add(material.Id, material);
        }

        if (requiredByMaterial.Keys.Except(materials.Keys).Any()) {
            return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.MissingAllocationMaterial);
        }

        var normalizedAllocations = command.Allocations
            .GroupBy(allocation => new { allocation.MaterialId, allocation.WarehouseId })
            .Select(group => new ProductionMaterialAllocation(
                group.Key.MaterialId,
                group.Key.WarehouseId,
                MaterialRequirementCalculator.RoundQuantity(group.Sum(allocation => allocation.Quantity))))
            .OrderBy(allocation => allocation.MaterialId)
            .ThenBy(allocation => allocation.WarehouseId)
            .ToList();
        foreach (var requirement in requiredByMaterial) {
            var allocated = MaterialRequirementCalculator.RoundQuantity(normalizedAllocations
                .Where(allocation => allocation.MaterialId == requirement.Key)
                .Sum(allocation => allocation.Quantity));
            if (allocated != requirement.Value) {
                return Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.AllocationTotalMismatch);
            }
        }

        var warehouses = new Dictionary<Guid, Warehouse>();
        foreach (var warehouseId in normalizedAllocations.Select(allocation => allocation.WarehouseId).Distinct()) {
            var warehouse = await warehouseRepository.GetByIdAsync(
                warehouseId,
                currentUser.CompanyId,
                cancellationToken);
            if (warehouse is not { IsActive: true }) {
                return Result<ProductionOrderResponse>.Failure(InventoryErrors.WarehouseNotFound);
            }
            warehouses.Add(warehouse.Id, warehouse);
        }

        var startedAt = DateTime.UtcNow;
        var transactions = normalizedAllocations.Select(allocation => new InventoryTransaction {
            CompanyId = currentUser.CompanyId,
            WarehouseId = allocation.WarehouseId,
            Warehouse = warehouses[allocation.WarehouseId],
            MaterialId = allocation.MaterialId,
            Material = materials[allocation.MaterialId],
            Type = InventoryTransactionType.ProductionConsume,
            Quantity = allocation.Quantity,
            ReferenceType = "ProductionOrder",
            ReferenceId = order.Id,
            Note = "Production material consumption.",
            CreatedByUserId = currentUser.UserId,
            CreatedAt = startedAt
        }).ToList();

        var outcome = await executionRepository.TryStartAsync(
            order.Id,
            currentUser.CompanyId,
            transactions,
            startedAt,
            cancellationToken);
        return outcome.Status switch {
            ProductionExecutionStatus.Success => Result<ProductionOrderResponse>.Success(
                ProductionOrderResponse.From(outcome.Order!)),
            ProductionExecutionStatus.InsufficientStock =>
                Result<ProductionOrderResponse>.Failure(InventoryErrors.InsufficientStock),
            ProductionExecutionStatus.WarehouseUnavailable =>
                Result<ProductionOrderResponse>.Failure(InventoryErrors.WarehouseNotFound),
            ProductionExecutionStatus.MaterialUnavailable =>
                Result<ProductionOrderResponse>.Failure(InventoryErrors.MaterialNotFound),
            _ => Result<ProductionOrderResponse>.Failure(ProductionOrderErrors.InvalidTransition)
        };
    }
}
