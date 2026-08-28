using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Inventories;
using FactoryMind.Application.Features.Inventories.AdjustInventory;
using FactoryMind.Application.Features.Inventories.IssueInventory;
using FactoryMind.Application.Features.Inventories.ReceiveInventory;
using FactoryMind.Application.Features.Inventories.TransferInventory;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Domain.Manufacturing;

namespace FactoryMind.Tests;

public sealed class InventoryCommandHandlerTests {
    [Theory]
    [InlineData(InventoryTransactionType.Receipt, 4)]
    [InlineData(InventoryTransactionType.AdjustmentIncrease, 4)]
    [InlineData(InventoryTransactionType.TransferIn, 4)]
    [InlineData(InventoryTransactionType.ProductionOutput, 4)]
    [InlineData(InventoryTransactionType.Issue, -4)]
    [InlineData(InventoryTransactionType.AdjustmentDecrease, -4)]
    [InlineData(InventoryTransactionType.TransferOut, -4)]
    [InlineData(InventoryTransactionType.ProductionConsume, -4)]
    public void Transaction_type_resolves_one_signed_quantity(
        InventoryTransactionType type,
        decimal expected) {
        Assert.Equal(expected, type.ToSignedQuantity(4));
    }

    [Fact]
    public async Task Receive_creates_receipt_and_increases_balance() {
        var context = new InventoryTestContext();
        var handler = new ReceiveInventoryCommandHandler(
            context.Inventory, context.Warehouses, context.Materials, context.User);

        var result = await handler.Handle(new(
            context.Source.Id, context.Material.Id, 100m, "Delivery", "PurchaseReceipt", Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100m, context.Inventory.Balance(context.Source.Id, context.Material.Id));
        Assert.Equal(InventoryTransactionType.Receipt, Assert.Single(context.Inventory.Transactions).Type);
    }

    [Fact]
    public async Task Issue_decreases_balance() {
        var context = new InventoryTestContext();
        context.Inventory.SetBalance(context.Source.Id, context.Material.Id, 100m);
        var handler = new IssueInventoryCommandHandler(
            context.Inventory, context.Warehouses, context.Materials, context.User);

        var result = await handler.Handle(new(
            context.Source.Id, context.Material.Id, 25m, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(75m, context.Inventory.Balance(context.Source.Id, context.Material.Id));
        Assert.Equal(InventoryTransactionType.Issue, Assert.Single(context.Inventory.Transactions).Type);
    }

    [Fact]
    public async Task Insufficient_issue_changes_neither_ledger_nor_balance() {
        var context = new InventoryTestContext();
        context.Inventory.SetBalance(context.Source.Id, context.Material.Id, 10m);
        var handler = new IssueInventoryCommandHandler(
            context.Inventory, context.Warehouses, context.Materials, context.User);

        var result = await handler.Handle(new(
            context.Source.Id, context.Material.Id, 20m, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("inventories.insufficient_stock", result.Error?.Code);
        Assert.Equal(10m, context.Inventory.Balance(context.Source.Id, context.Material.Id));
        Assert.Empty(context.Inventory.Transactions);
    }

    [Theory]
    [InlineData(InventoryAdjustmentDirection.Increase, 5, 15, InventoryTransactionType.AdjustmentIncrease)]
    [InlineData(InventoryAdjustmentDirection.Decrease, 3, 7, InventoryTransactionType.AdjustmentDecrease)]
    public async Task Adjustment_uses_explicit_direction(
        InventoryAdjustmentDirection direction,
        decimal quantity,
        decimal expected,
        InventoryTransactionType expectedType) {
        var context = new InventoryTestContext();
        context.Inventory.SetBalance(context.Source.Id, context.Material.Id, 10m);
        var handler = new AdjustInventoryCommandHandler(
            context.Inventory, context.Warehouses, context.Materials, context.User);

        var result = await handler.Handle(new(
            context.Source.Id, context.Material.Id, direction, quantity, "Cycle count", null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, context.Inventory.Balance(context.Source.Id, context.Material.Id));
        Assert.Equal(expectedType, Assert.Single(context.Inventory.Transactions).Type);
    }

    [Fact]
    public async Task Transfer_updates_both_balances_and_shares_correlation() {
        var context = new InventoryTestContext();
        context.Inventory.SetBalance(context.Source.Id, context.Material.Id, 100m);
        var handler = new TransferInventoryCommandHandler(
            context.Inventory, context.Warehouses, context.Materials, context.User);

        var result = await handler.Handle(new(
            context.Source.Id, context.Destination.Id, context.Material.Id, 30m, "Replenish", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(70m, context.Inventory.Balance(context.Source.Id, context.Material.Id));
        Assert.Equal(30m, context.Inventory.Balance(context.Destination.Id, context.Material.Id));
        Assert.Equal(2, context.Inventory.Transactions.Count);
        Assert.Equal(
            context.Inventory.Transactions[0].ReferenceId,
            context.Inventory.Transactions[1].ReferenceId);
        Assert.NotNull(context.Inventory.Transactions[0].ReferenceId);
    }

    [Fact]
    public async Task Transfer_rejects_same_source_and_destination() {
        var context = new InventoryTestContext();
        var handler = new TransferInventoryCommandHandler(
            context.Inventory, context.Warehouses, context.Materials, context.User);

        var result = await handler.Handle(new(
            context.Source.Id, context.Source.Id, context.Material.Id, 1m, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("inventories.same_warehouse_transfer", result.Error?.Code);
        Assert.Empty(context.Inventory.Transactions);
    }

    [Fact]
    public async Task Receive_rejects_warehouse_from_another_company() {
        var context = new InventoryTestContext();
        var foreignWarehouse = new Warehouse {
            CompanyId = Guid.NewGuid(),
            Code = "WH-OTHER",
            Name = "Other"
        };
        context.Warehouses.Warehouses.Add(foreignWarehouse);
        var handler = new ReceiveInventoryCommandHandler(
            context.Inventory, context.Warehouses, context.Materials, context.User);

        var result = await handler.Handle(new(
            foreignWarehouse.Id, context.Material.Id, 10m, null, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("inventories.warehouse_not_found", result.Error?.Code);
        Assert.Empty(context.Inventory.Transactions);
    }

    private sealed class InventoryTestContext {
        public FakeCurrentUser User { get; } = new();
        public FakeInventoryRepository Inventory { get; } = new();
        public FakeWarehouseRepository Warehouses { get; } = new();
        public FakeMaterialRepository Materials { get; } = new();
        public Warehouse Source { get; }
        public Warehouse Destination { get; }
        public Material Material { get; }

        public InventoryTestContext() {
            Source = new Warehouse {
                CompanyId = User.CompanyId,
                Code = "WH-A",
                Name = "Source"
            };
            Destination = new Warehouse {
                CompanyId = User.CompanyId,
                Code = "WH-B",
                Name = "Destination"
            };
            Material = new Material {
                CompanyId = User.CompanyId,
                Code = "MAT-STEEL",
                Name = "Steel",
                Unit = "kg"
            };
            Warehouses.Warehouses.AddRange([Source, Destination]);
            Materials.Materials.Add(Material);
        }
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "Manager";
    }

    private sealed class FakeInventoryRepository : IInventoryRepository {
        private readonly Dictionary<(Guid WarehouseId, Guid MaterialId), decimal> _balances = [];
        public List<InventoryTransaction> Transactions { get; } = [];

        public decimal Balance(Guid warehouseId, Guid materialId) =>
            _balances.GetValueOrDefault((warehouseId, materialId));

        public void SetBalance(Guid warehouseId, Guid materialId, decimal quantity) =>
            _balances[(warehouseId, materialId)] = quantity;

        public Task<InventoryOperationResult> ApplyAsync(
            InventoryTransaction transaction,
            CancellationToken cancellationToken) {
            var key = (transaction.WarehouseId, transaction.MaterialId);
            var next = _balances.GetValueOrDefault(key) + transaction.SignedQuantity();
            if (next < 0) {
                return Task.FromResult(new InventoryOperationResult(
                    InventoryOperationStatus.InsufficientStock, []));
            }
            _balances[key] = next;
            Transactions.Add(transaction);
            return Task.FromResult(new InventoryOperationResult(
                InventoryOperationStatus.Success, [transaction]));
        }

        public async Task<InventoryOperationResult> TransferAsync(
            InventoryTransaction transferOut,
            InventoryTransaction transferIn,
            CancellationToken cancellationToken) {
            var sourceKey = (transferOut.WarehouseId, transferOut.MaterialId);
            if (_balances.GetValueOrDefault(sourceKey) < transferOut.Quantity) {
                return new(InventoryOperationStatus.InsufficientStock, []);
            }
            await ApplyAsync(transferOut, cancellationToken);
            await ApplyAsync(transferIn, cancellationToken);
            return new(InventoryOperationStatus.Success, [transferOut, transferIn]);
        }

        public Task<IReadOnlyList<InventoryBalance>> GetBalancesAsync(
            Guid companyId,
            Guid? warehouseId,
            Guid? materialId,
            string? search,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<InventoryBalance>>([]);

        public Task<(IReadOnlyList<InventoryTransaction> Items, int TotalCount)> GetTransactionsAsync(
            Guid companyId,
            Guid? warehouseId,
            Guid? materialId,
            InventoryTransactionType? type,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            CancellationToken cancellationToken) => Task.FromResult(
                ((IReadOnlyList<InventoryTransaction>)Transactions, Transactions.Count));
    }

    private sealed class FakeWarehouseRepository : IWarehouseRepository {
        public List<Warehouse> Warehouses { get; } = [];

        public Task<IReadOnlyList<Warehouse>> GetByCompanyAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Warehouse>>(
                Warehouses.Where(warehouse => warehouse.CompanyId == companyId).ToList());

        public Task<Warehouse?> GetByIdAsync(
            Guid warehouseId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(Warehouses.SingleOrDefault(warehouse =>
                warehouse.Id == warehouseId && warehouse.CompanyId == companyId));

        public Task<bool> CodeExistsAsync(
            Guid companyId,
            string code,
            Guid? excludedWarehouseId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Warehouse warehouse) => Warehouses.Add(warehouse);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeMaterialRepository : IMaterialRepository {
        public List<Material> Materials { get; } = [];

        public Task<IReadOnlyList<Material>> GetByCompanyAsync(
            Guid companyId,
            string? search,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Material>>(
                Materials.Where(material => material.CompanyId == companyId).ToList());

        public Task<Material?> GetByIdAsync(
            Guid materialId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(Materials.SingleOrDefault(material =>
                material.Id == materialId && material.CompanyId == companyId));

        public Task<bool> CodeExistsAsync(
            Guid companyId,
            string code,
            Guid? excludedMaterialId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> HasBomItemsAsync(
            Guid materialId,
            Guid companyId,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public void Add(Material material) => Materials.Add(material);
        public void Remove(Material material) => Materials.Remove(material);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
