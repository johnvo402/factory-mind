using FactoryMind.Application.Common.Identity;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Application.Features.Inventories;

internal static class InventoryTransactionFactory {
    public static InventoryTransaction Create(
        ICurrentUser currentUser,
        Warehouse warehouse,
        Material material,
        InventoryTransactionType type,
        decimal quantity,
        string? note,
        string? referenceType,
        Guid? referenceId) => new() {
            CompanyId = currentUser.CompanyId,
            WarehouseId = warehouse.Id,
            Warehouse = warehouse,
            MaterialId = material.Id,
            Material = material,
            Type = type,
            Quantity = quantity,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ReferenceType = string.IsNullOrWhiteSpace(referenceType) ? null : referenceType.Trim(),
            ReferenceId = referenceId,
            CreatedByUserId = currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };
}
