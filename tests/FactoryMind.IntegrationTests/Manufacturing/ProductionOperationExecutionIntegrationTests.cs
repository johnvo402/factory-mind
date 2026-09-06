using System.Net;
using System.Net.Http.Json;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Boms;
using FactoryMind.Application.Features.Materials;
using FactoryMind.Application.Features.Machines;
using FactoryMind.Application.Features.Products;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.Routings;
using FactoryMind.Application.Features.Warehouses;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class ProductionOperationExecutionIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Release_requires_an_active_routing_and_leaves_no_partial_snapshot() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await PostAsync<ProductResponse>(
            Client, ApiRoutes.Products.Group, new ProductRequest("P-NO-ROUTE", "No Route Product"));
        var material = await PostAsync<MaterialResponse>(
            Client, ApiRoutes.Materials.Group, new MaterialRequest("M-NO-ROUTE", "Material", "kg"));
        var bom = await PostAsync<BomResponse>(Client, BomsRoute(product.Id), new BomRequest(
            1m, [new BomItemRequest(material.Id, 1m, null)]));
        using (var activateBom = await Client.PostAsync(ActivateBomRoute(product.Id, bom.Id), null)) {
            activateBom.EnsureSuccessStatusCode();
        }
        var order = await CreateOrderAsync(Client, "PO-NO-ROUTE", product.Id, 1m);

        using var release = await Client.PostAsync(ReleaseOrderRoute(order.Id), null);
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        var persisted = await GetOrderAsync(Client, order.Id);
        Assert.Equal(ProductionOrderStatuses.Planned, persisted.Status);
        Assert.Null(persisted.BillOfMaterialId);
        Assert.Null(persisted.RoutingId);
        Assert.Empty(persisted.Operations);
    }

    [Fact]
    public async Task Release_rejects_a_routing_whose_work_center_was_deactivated_after_activation() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "INACTIVE-RELEASE");
        using (var deactivate = await Client.PostAsync(
                   DeactivateWorkCenterRoute(scenario.Cutting.Id), null)) {
            deactivate.EnsureSuccessStatusCode();
        }
        var order = await CreateOrderAsync(Client, "PO-INACTIVE-RELEASE", scenario.Product.Id, 1m);

        using var release = await Client.PostAsync(ReleaseOrderRoute(order.Id), null);
        Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        var persisted = await GetOrderAsync(Client, order.Id);
        Assert.Equal(ProductionOrderStatuses.Planned, persisted.Status);
        Assert.Null(persisted.BillOfMaterialId);
        Assert.Null(persisted.RoutingId);
        Assert.Null(persisted.ReleasedAt);
        Assert.Empty(persisted.Operations);
    }

    [Fact]
    public async Task Legacy_released_order_starts_and_consumes_material_once_without_fabricating_routing() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "LEGACY-START", operationCount: 1);
        var order = await CreateOrderAsync(Client, "PO-LEGACY-START", scenario.Product.Id, 1m);
        await SetExecutionStateAsync(
            order.Id, scenario.Bom.Id, null, ProductionOrderStatuses.Released, startedAt: null);
        var request = new StartProductionOrderRequest([
            new ProductionMaterialAllocationRequest(scenario.Material.Id, scenario.Raw.Id, 1m)
        ]);

        var started = await PostAsync<ProductionOrderResponse>(Client, StartOrderRoute(order.Id), request);
        Assert.Equal(ProductionOrderStatuses.InProgress, started.Status);
        Assert.NotNull(started.StartedAt);
        Assert.Null(started.RoutingId);
        Assert.Empty(started.Operations);
        using (var repeatedStart = await Client.PostAsJsonAsync(StartOrderRoute(order.Id), request)) {
            Assert.Equal(HttpStatusCode.Conflict, repeatedStart.StatusCode);
        }

        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(99m, (await dbContext.InventoryBalances.SingleAsync(balance =>
            balance.MaterialId == scenario.Material.Id && balance.WarehouseId == scenario.Raw.Id)).Quantity);
        Assert.Single(await dbContext.InventoryTransactions.Where(transaction =>
            transaction.ReferenceId == order.Id &&
            transaction.Type == InventoryTransactionType.ProductionConsume).ToListAsync());
        Assert.Empty(await dbContext.ProductionOrderOperations.Where(operation =>
            operation.ProductionOrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task Legacy_in_progress_order_completes_and_outputs_once_without_fabricating_routing() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "LEGACY-COMPLETE", operationCount: 1);
        var order = await CreateOrderAsync(Client, "PO-LEGACY-COMPLETE", scenario.Product.Id, 2m);
        await SetExecutionStateAsync(
            order.Id, scenario.Bom.Id, null, ProductionOrderStatuses.InProgress, DateTime.UtcNow);
        var request = new CompleteProductionOrderRequest(scenario.Finished.Id);

        var completed = await PostAsync<ProductionOrderResponse>(Client, CompleteOrderRoute(order.Id), request);
        Assert.Equal(ProductionOrderStatuses.Completed, completed.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.Null(completed.RoutingId);
        Assert.Empty(completed.Operations);
        using (var repeatedComplete = await Client.PostAsJsonAsync(CompleteOrderRoute(order.Id), request)) {
            Assert.Equal(HttpStatusCode.Conflict, repeatedComplete.StatusCode);
        }

        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(2m, (await dbContext.ProductInventoryBalances.SingleAsync(balance =>
            balance.ProductId == scenario.Product.Id && balance.WarehouseId == scenario.Finished.Id)).Quantity);
        Assert.Single(await dbContext.ProductInventoryTransactions.Where(transaction =>
            transaction.ReferenceId == order.Id &&
            transaction.Type == ProductInventoryTransactionType.ProductionOutput).ToListAsync());
        Assert.Empty(await dbContext.ProductionOrderOperations.Where(operation =>
            operation.ProductionOrderId == order.Id).ToListAsync());
    }

    [Fact]
    public async Task Routed_orders_without_operation_snapshots_cannot_use_the_legacy_bypass() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "MALFORMED-ROUTED", operationCount: 1);
        var released = await CreateOrderAsync(Client, "PO-MALFORMED-START", scenario.Product.Id, 1m);
        await SetExecutionStateAsync(
            released.Id,
            scenario.Bom.Id,
            scenario.Routing.Id,
            ProductionOrderStatuses.Released,
            startedAt: null);
        using (var start = await Client.PostAsJsonAsync(StartOrderRoute(released.Id),
                   new StartProductionOrderRequest([
                       new ProductionMaterialAllocationRequest(scenario.Material.Id, scenario.Raw.Id, 1m)
                   ]))) {
            Assert.Equal(HttpStatusCode.Conflict, start.StatusCode);
        }

        var inProgress = await CreateOrderAsync(Client, "PO-MALFORMED-COMPLETE", scenario.Product.Id, 1m);
        await SetExecutionStateAsync(
            inProgress.Id,
            scenario.Bom.Id,
            scenario.Routing.Id,
            ProductionOrderStatuses.InProgress,
            DateTime.UtcNow);
        using var complete = await Client.PostAsJsonAsync(
            CompleteOrderRoute(inProgress.Id), new CompleteProductionOrderRequest(scenario.Finished.Id));
        Assert.Equal(HttpStatusCode.Conflict, complete.StatusCode);

        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Empty(await dbContext.InventoryTransactions.Where(transaction =>
            transaction.Type == InventoryTransactionType.ProductionConsume).ToListAsync());
        Assert.Empty(await dbContext.ProductInventoryTransactions.ToListAsync());
        Assert.Empty(await dbContext.ProductionOrderOperations.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_release_has_one_success_and_one_conflict_with_one_complete_snapshot() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "RELEASE-RACE");
        var order = await CreateOrderAsync(Client, "PO-RELEASE-RACE", scenario.Product.Id, 1m);

        var responses = await Task.WhenAll(
            Client.PostAsync(ReleaseOrderRoute(order.Id), null),
            Client.PostAsync(ReleaseOrderRoute(order.Id), null));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        var persisted = await GetOrderAsync(Client, order.Id);
        Assert.Equal(ProductionOrderStatuses.Released, persisted.Status);
        Assert.Equal(3, persisted.Operations.Count);
        Assert.Equal(3, persisted.Operations.Select(operation => operation.Sequence).Distinct().Count());
    }

    [Fact]
    public async Task Release_locks_routing_and_snapshots_operations_across_later_revisions() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "SNAPSHOT");
        var orderOne = await CreateOrderAsync(Client, "PO-SNAPSHOT-1", scenario.Product.Id, 5m);
        var releasedOne = await ReleaseAsync(Client, orderOne.Id);

        Assert.Equal(scenario.Bom.Id, releasedOne.BillOfMaterialId);
        Assert.Equal(scenario.Routing.Id, releasedOne.RoutingId);
        Assert.Equal(1, releasedOne.RoutingRevision);
        Assert.Equal(new[] { 10, 20, 30 }, releasedOne.Operations.Select(item => item.Sequence));
        Assert.Equal(new[] { "Cutting", "Assembly", "Packaging" },
            releasedOne.Operations.Select(item => item.Name));

        var revisionTwo = await CreateRoutingAsync(Client, scenario.Product.Id, [
            new RoutingOperationRequest(10, "Laser cutting", scenario.Cutting.Id, 3, 6, null),
            new RoutingOperationRequest(20, "Assembly v2", scenario.Assembly.Id, 1, 8, null)
        ]);
        await ActivateRoutingAsync(Client, scenario.Product.Id, revisionTwo.Id);

        var persistedOne = await GetOrderAsync(Client, orderOne.Id);
        Assert.Equal(scenario.Routing.Id, persistedOne.RoutingId);
        Assert.Equal(new[] { "Cutting", "Assembly", "Packaging" },
            persistedOne.Operations.Select(item => item.Name));
        var orderTwo = await CreateOrderAsync(Client, "PO-SNAPSHOT-2", scenario.Product.Id, 5m);
        var releasedTwo = await ReleaseAsync(Client, orderTwo.Id);
        Assert.Equal(revisionTwo.Id, releasedTwo.RoutingId);
        Assert.Equal(2, releasedTwo.RoutingRevision);
        Assert.Equal(new[] { "Laser cutting", "Assembly v2" },
            releasedTwo.Operations.Select(item => item.Name));
    }

    [Fact]
    public async Task Operations_enforce_sequence_and_order_completion_requires_all_operations() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "HAPPY");
        var order = await CreateOrderAsync(Client, "PO-HAPPY", scenario.Product.Id, 5m);
        var released = await ReleaseAsync(Client, order.Id);
        var started = await StartOrderAsync(Client, released, scenario, 5m);
        Assert.Equal(ProductionOrderStatuses.InProgress, started.Status);

        using (var startSecond = await Client.PostAsJsonAsync(
                   StartOperationRoute(order.Id, released.Operations[1].Id),
                   new StartProductionOrderOperationRequest(
                       MachineFor(scenario, released.Operations[1]).Id))) {
            Assert.Equal(HttpStatusCode.Conflict, startSecond.StatusCode);
        }
        using (var completeTooEarly = await Client.PostAsJsonAsync(
                   CompleteOrderRoute(order.Id), new CompleteProductionOrderRequest(scenario.Finished.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, completeTooEarly.StatusCode);
        }

        foreach (var operation in released.Operations) {
            var machine = MachineFor(scenario, operation);
            var running = await StartOperationAsync(Client, order.Id, operation.Id, machine.Id);
            Assert.Equal(ProductionOperationStatuses.InProgress, running.Status);
            Assert.NotNull(running.StartedAt);
            Assert.Equal(machine.Id, running.MachineId);
            Assert.Equal(machine.Code, running.MachineCode);
            Assert.Equal(machine.Name, running.MachineName);
            var completed = await CompleteOperationAsync(Client, order.Id, operation.Id);
            Assert.Equal(ProductionOperationStatuses.Completed, completed.Status);
            Assert.NotNull(completed.CompletedAt);
            Assert.Equal(machine.Id, completed.MachineId);
        }
        var completedOrder = await CompleteOrderAsync(Client, order.Id, scenario.Finished.Id);
        Assert.Equal(ProductionOrderStatuses.Completed, completedOrder.Status);

        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(95m, (await dbContext.InventoryBalances.SingleAsync()).Quantity);
        Assert.Single(await dbContext.InventoryTransactions.Where(transaction =>
            transaction.Type == InventoryTransactionType.ProductionConsume).ToListAsync());
        Assert.Equal(5m, (await dbContext.ProductInventoryBalances.SingleAsync()).Quantity);
        Assert.Single(await dbContext.ProductInventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_operation_transitions_have_exactly_one_success_and_terminal_states_are_frozen() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "CONCURRENT");
        var order = await CreateOrderAsync(Client, "PO-CONCURRENT", scenario.Product.Id, 5m);
        var released = await ReleaseAsync(Client, order.Id);
        await StartOrderAsync(Client, released, scenario, 5m);
        var operation = released.Operations[0];

        var starts = await Task.WhenAll(
            Client.PostAsJsonAsync(StartOperationRoute(order.Id, operation.Id),
                new StartProductionOrderOperationRequest(MachineFor(scenario, operation).Id)),
            Client.PostAsJsonAsync(StartOperationRoute(order.Id, operation.Id),
                new StartProductionOrderOperationRequest(MachineFor(scenario, operation).Id)));
        Assert.Single(starts, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(starts, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in starts) {
            response.Dispose();
        }

        var completes = await Task.WhenAll(
            Client.PostAsync(CompleteOperationRoute(order.Id, operation.Id), null),
            Client.PostAsync(CompleteOperationRoute(order.Id, operation.Id), null));
        Assert.Single(completes, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(completes, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in completes) {
            response.Dispose();
        }

        foreach (var remaining in released.Operations.Skip(1)) {
            await StartOperationAsync(
                Client, order.Id, remaining.Id, MachineFor(scenario, remaining).Id);
            await CompleteOperationAsync(Client, order.Id, remaining.Id);
        }
        await CompleteOrderAsync(Client, order.Id, scenario.Finished.Id);

        using var startAfterOrderComplete = await Client.PostAsJsonAsync(
            StartOperationRoute(order.Id, operation.Id),
            new StartProductionOrderOperationRequest(MachineFor(scenario, operation).Id));
        using var completeAfterOrderComplete = await Client.PostAsync(
            CompleteOperationRoute(order.Id, operation.Id), null);
        using var completeOrderAgain = await Client.PostAsJsonAsync(
            CompleteOrderRoute(order.Id), new CompleteProductionOrderRequest(scenario.Finished.Id));
        Assert.Equal(HttpStatusCode.Conflict, startAfterOrderComplete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, completeAfterOrderComplete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, completeOrderAgain.StatusCode);
    }

    [Fact]
    public async Task Last_operation_completion_race_never_outputs_before_the_operation_completes() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "RACE", operationCount: 1);
        var order = await CreateOrderAsync(Client, "PO-RACE", scenario.Product.Id, 5m);
        var released = await ReleaseAsync(Client, order.Id);
        await StartOrderAsync(Client, released, scenario, 5m);
        await StartOperationAsync(
            Client,
            order.Id,
            released.Operations[0].Id,
            MachineFor(scenario, released.Operations[0]).Id);

        var results = await Task.WhenAll(
            Client.PostAsync(CompleteOperationRoute(order.Id, released.Operations[0].Id), null),
            Client.PostAsJsonAsync(
                CompleteOrderRoute(order.Id),
                new CompleteProductionOrderRequest(scenario.Finished.Id)));
        Assert.Equal(HttpStatusCode.OK, results[0].StatusCode);
        Assert.Contains(results[1].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        foreach (var response in results) {
            response.Dispose();
        }
        if ((await GetOrderAsync(Client, order.Id)).Status == ProductionOrderStatuses.InProgress) {
            await CompleteOrderAsync(Client, order.Id, scenario.Finished.Id);
        }

        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(ProductionOperationStatuses.Completed,
            (await dbContext.ProductionOrderOperations.SingleAsync()).Status);
        Assert.Single(await dbContext.ProductInventoryTransactions.ToListAsync());
    }

    [Fact]
    public async Task Wrong_work_center_and_unavailable_machine_leave_operation_and_machine_unchanged() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "ELIGIBILITY", operationCount: 1);
        var order = await CreateOrderAsync(Client, "PO-ELIGIBILITY", scenario.Product.Id, 1m);
        var released = await ReleaseAsync(Client, order.Id);
        await StartOrderAsync(Client, released, scenario, 1m);
        var operation = released.Operations[0];
        var wrongMachine = scenario.Machines.Single(machine => machine.WorkCenterId == scenario.Assembly.Id);

        using (var wrongWorkCenter = await Client.PostAsJsonAsync(
                   StartOperationRoute(order.Id, operation.Id),
                   new StartProductionOrderOperationRequest(wrongMachine.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, wrongWorkCenter.StatusCode);
        }

        var requiredMachine = MachineFor(scenario, operation);
        await SetMachineStatusAsync(requiredMachine.Id, MachineStatuses.Maintenance);
        using (var maintenance = await Client.PostAsJsonAsync(
                   StartOperationRoute(order.Id, operation.Id),
                   new StartProductionOrderOperationRequest(requiredMachine.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, maintenance.StatusCode);
        }
        await SetMachineStatusAsync(requiredMachine.Id, MachineStatuses.Offline);
        using (var offline = await Client.PostAsJsonAsync(
                   StartOperationRoute(order.Id, operation.Id),
                   new StartProductionOrderOperationRequest(requiredMachine.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, offline.StatusCode);
        }
        await SetMachineStatusAsync(requiredMachine.Id, MachineStatuses.Running);
        using (var running = await Client.PostAsJsonAsync(
                   StartOperationRoute(order.Id, operation.Id),
                   new StartProductionOrderOperationRequest(requiredMachine.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, running.StatusCode);
        }

        var persisted = await GetOrderAsync(Client, order.Id);
        Assert.Equal(ProductionOperationStatuses.Pending, persisted.Operations[0].Status);
        Assert.Null(persisted.Operations[0].MachineId);
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(MachineStatuses.Available, (await dbContext.Machines.SingleAsync(
            machine => machine.Id == wrongMachine.Id)).Status);
        Assert.Equal(MachineStatuses.Running, (await dbContext.Machines.SingleAsync(
            machine => machine.Id == requiredMachine.Id)).Status);
    }

    [Fact]
    public async Task Concurrent_orders_claiming_the_same_machine_have_exactly_one_success() {
        var firstClient = CreateClient();
        var secondClient = CreateClient();
        await LoginAsync(firstClient, TestData.CompanyAAdminEmail);
        await LoginAsync(secondClient, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(firstClient, "MACHINE-RACE", operationCount: 1);
        var orderA = await CreateOrderAsync(firstClient, "PO-MACHINE-RACE-A", scenario.Product.Id, 1m);
        var orderB = await CreateOrderAsync(firstClient, "PO-MACHINE-RACE-B", scenario.Product.Id, 1m);
        var releasedA = await ReleaseAsync(firstClient, orderA.Id);
        var releasedB = await ReleaseAsync(firstClient, orderB.Id);
        await StartOrderAsync(firstClient, releasedA, scenario, 1m);
        await StartOrderAsync(firstClient, releasedB, scenario, 1m);
        var machine = MachineFor(scenario, releasedA.Operations[0]);

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync(
                StartOperationRoute(orderA.Id, releasedA.Operations[0].Id),
                new StartProductionOrderOperationRequest(machine.Id)),
            secondClient.PostAsJsonAsync(
                StartOperationRoute(orderB.Id, releasedB.Operations[0].Id),
                new StartProductionOrderOperationRequest(machine.Id)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var operations = await dbContext.ProductionOrderOperations
            .Where(operation => operation.ProductionOrderId == orderA.Id ||
                operation.ProductionOrderId == orderB.Id)
            .ToListAsync();
        Assert.Single(operations, operation =>
            operation.Status == ProductionOperationStatuses.InProgress &&
            operation.MachineId == machine.Id);
        Assert.Single(operations, operation =>
            operation.Status == ProductionOperationStatuses.Pending && operation.MachineId == null);
        Assert.Equal(MachineStatuses.Running,
            (await dbContext.Machines.SingleAsync(candidate => candidate.Id == machine.Id)).Status);
    }

    [Fact]
    public async Task Completed_operation_releases_machine_for_reuse_and_preserves_both_snapshots() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "REUSE", operationCount: 1);
        var orderA = await CreateOrderAsync(Client, "PO-REUSE-A", scenario.Product.Id, 1m);
        var orderB = await CreateOrderAsync(Client, "PO-REUSE-B", scenario.Product.Id, 1m);
        var releasedA = await ReleaseAsync(Client, orderA.Id);
        var releasedB = await ReleaseAsync(Client, orderB.Id);
        await StartOrderAsync(Client, releasedA, scenario, 1m);
        await StartOrderAsync(Client, releasedB, scenario, 1m);
        var machine = MachineFor(scenario, releasedA.Operations[0]);

        await StartOperationAsync(Client, orderA.Id, releasedA.Operations[0].Id, machine.Id);
        var completedA = await CompleteOperationAsync(Client, orderA.Id, releasedA.Operations[0].Id);
        var runningB = await StartOperationAsync(Client, orderB.Id, releasedB.Operations[0].Id, machine.Id);

        Assert.Equal(machine.Id, completedA.MachineId);
        Assert.Equal(machine.Code, completedA.MachineCode);
        Assert.Equal(machine.Name, completedA.MachineName);
        Assert.Equal(machine.Id, runningB.MachineId);
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(MachineStatuses.Running,
            (await dbContext.Machines.SingleAsync(candidate => candidate.Id == machine.Id)).Status);
    }

    [Fact]
    public async Task Legacy_in_progress_operation_without_machine_completes_without_modifying_machines() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "LEGACY-OP", operationCount: 1);
        var order = await CreateOrderAsync(Client, "PO-LEGACY-OP", scenario.Product.Id, 1m);
        var released = await ReleaseAsync(Client, order.Id);
        await StartOrderAsync(Client, released, scenario, 1m);
        using (var scope = ApiFactory.Services.CreateScope()) {
            var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
            await dbContext.ProductionOrderOperations
                .Where(operation => operation.Id == released.Operations[0].Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(operation => operation.Status, ProductionOperationStatuses.InProgress)
                    .SetProperty(operation => operation.StartedAt, DateTime.UtcNow));
        }

        var completed = await CompleteOperationAsync(Client, order.Id, released.Operations[0].Id);

        Assert.Equal(ProductionOperationStatuses.Completed, completed.Status);
        Assert.Null(completed.MachineId);
        Assert.Null(completed.MachineCode);
        Assert.Null(completed.MachineName);
        using var verificationScope = ApiFactory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.All(await verificationContext.Machines.ToListAsync(),
            machine => Assert.Equal(MachineStatuses.Available, machine.Status));
    }

    [Fact]
    public async Task Active_machine_rejects_admin_changes_then_completes_and_history_blocks_delete() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "ADMIN-GUARD", operationCount: 1);
        var order = await CreateOrderAsync(Client, "PO-ADMIN-GUARD", scenario.Product.Id, 1m);
        var released = await ReleaseAsync(Client, order.Id);
        await StartOrderAsync(Client, released, scenario, 1m);
        var machine = MachineFor(scenario, released.Operations[0]);
        await StartOperationAsync(Client, order.Id, released.Operations[0].Id, machine.Id);

        using (var changeStatus = await Client.PutAsJsonAsync(
                   MachineByIdRoute(machine.Id),
                   new MachineRequest(machine.Code, machine.Name, MachineStatuses.Maintenance, machine.WorkCenterId))) {
            Assert.Equal(HttpStatusCode.Conflict, changeStatus.StatusCode);
        }
        using (var changeWorkCenter = await Client.PutAsJsonAsync(
                   MachineByIdRoute(machine.Id),
                   new MachineRequest(machine.Code, machine.Name, MachineStatuses.Available, scenario.Assembly.Id))) {
            Assert.Equal(HttpStatusCode.Conflict, changeWorkCenter.StatusCode);
        }

        using (var deactivate = await Client.PostAsync(
                   DeactivateWorkCenterRoute(scenario.Cutting.Id), null)) {
            deactivate.EnsureSuccessStatusCode();
        }

        await CompleteOperationAsync(Client, order.Id, released.Operations[0].Id);
        using var delete = await Client.DeleteAsync(MachineByIdRoute(machine.Id));
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var persistedMachine = await dbContext.Machines.SingleAsync(candidate => candidate.Id == machine.Id);
        Assert.Equal(MachineStatuses.Available, persistedMachine.Status);
        Assert.Equal(machine.WorkCenterId, persistedMachine.WorkCenterId);
        Assert.Equal(machine.Id, (await dbContext.ProductionOrderOperations.SingleAsync(
            operation => operation.Id == released.Operations[0].Id)).MachineId);
    }

    [Fact]
    public async Task Complete_rejects_corrupted_assigned_machine_status_and_preserves_operation() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "COMPLETE-GUARD", operationCount: 1);
        var order = await CreateOrderAsync(Client, "PO-COMPLETE-GUARD", scenario.Product.Id, 1m);
        var released = await ReleaseAsync(Client, order.Id);
        await StartOrderAsync(Client, released, scenario, 1m);
        var operation = released.Operations[0];
        var machine = MachineFor(scenario, operation);
        await StartOperationAsync(Client, order.Id, operation.Id, machine.Id);
        await SetMachineStatusAsync(machine.Id, MachineStatuses.Maintenance);

        using var complete = await Client.PostAsync(
            CompleteOperationRoute(order.Id, operation.Id), null);

        Assert.Equal(HttpStatusCode.Conflict, complete.StatusCode);
        var persisted = await GetOrderAsync(Client, order.Id);
        Assert.Equal(ProductionOperationStatuses.InProgress, persisted.Operations[0].Status);
        Assert.Equal(machine.Id, persisted.Operations[0].MachineId);
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(MachineStatuses.Maintenance,
            (await dbContext.Machines.SingleAsync(candidate => candidate.Id == machine.Id)).Status);
    }

    [Fact]
    public async Task Deactivated_work_center_blocks_start_without_mutating_machine_or_snapshot() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var scenario = await CreateScenarioAsync(Client, "DEACTIVATED-START", operationCount: 1);
        var order = await CreateOrderAsync(Client, "PO-DEACTIVATED-START", scenario.Product.Id, 1m);
        var released = await ReleaseAsync(Client, order.Id);
        await StartOrderAsync(Client, released, scenario, 1m);
        using (var deactivate = await Client.PostAsync(
                   DeactivateWorkCenterRoute(scenario.Cutting.Id), null)) {
            deactivate.EnsureSuccessStatusCode();
        }
        var machine = MachineFor(scenario, released.Operations[0]);

        using var start = await Client.PostAsJsonAsync(
            StartOperationRoute(order.Id, released.Operations[0].Id),
            new StartProductionOrderOperationRequest(machine.Id));

        Assert.Equal(HttpStatusCode.Conflict, start.StatusCode);
        var persisted = await GetOrderAsync(Client, order.Id);
        Assert.Equal(ProductionOperationStatuses.Pending, persisted.Operations[0].Status);
        Assert.Equal(released.Operations[0].WorkCenterCode, persisted.Operations[0].WorkCenterCode);
        Assert.Null(persisted.Operations[0].MachineId);
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(MachineStatuses.Available,
            (await dbContext.Machines.SingleAsync(candidate => candidate.Id == machine.Id)).Status);
    }

    [Fact]
    public async Task Start_does_not_disclose_or_claim_another_tenants_machine() {
        var companyAClient = CreateClient();
        var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var scenarioA = await CreateScenarioAsync(companyAClient, "TENANT-MACHINE-A", operationCount: 1);
        var scenarioB = await CreateScenarioAsync(companyBClient, "TENANT-MACHINE-B", operationCount: 1);
        var order = await CreateOrderAsync(
            companyAClient, "PO-TENANT-MACHINE", scenarioA.Product.Id, 1m);
        var released = await ReleaseAsync(companyAClient, order.Id);
        await StartOrderAsync(companyAClient, released, scenarioA, 1m);
        var tenantBMachine = scenarioB.Machines.Single(
            machine => machine.WorkCenterId == scenarioB.Cutting.Id);

        using var start = await companyAClient.PostAsJsonAsync(
            StartOperationRoute(order.Id, released.Operations[0].Id),
            new StartProductionOrderOperationRequest(tenantBMachine.Id));

        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        Assert.Null((await GetOrderAsync(companyAClient, order.Id)).Operations[0].MachineId);
    }

    [Fact]
    public async Task Operation_routes_do_not_disclose_another_tenants_order() {
        var companyAClient = CreateClient();
        var companyBClient = CreateClient();
        await LoginAsync(companyAClient, TestData.CompanyAAdminEmail);
        await LoginAsync(companyBClient, TestData.CompanyBAdminEmail);
        var scenario = await CreateScenarioAsync(companyAClient, "TENANT", operationCount: 1);
        var order = await CreateOrderAsync(companyAClient, "PO-TENANT-OPS", scenario.Product.Id, 1m);
        var released = await ReleaseAsync(companyAClient, order.Id);

        using var list = await companyBClient.GetAsync(OperationsRoute(order.Id));
        using var start = await companyBClient.PostAsJsonAsync(
            StartOperationRoute(order.Id, released.Operations[0].Id),
            new StartProductionOrderOperationRequest(MachineFor(scenario, released.Operations[0]).Id));
        using var complete = await companyBClient.PostAsync(
            CompleteOperationRoute(order.Id, released.Operations[0].Id), null);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, start.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);
    }

    private static async Task<Scenario> CreateScenarioAsync(
        HttpClient client,
        string suffix,
        int operationCount = 3) {
        var product = await PostAsync<ProductResponse>(
            client, ApiRoutes.Products.Group, new ProductRequest($"P-{suffix}", $"Product {suffix}"));
        var material = await PostAsync<MaterialResponse>(
            client, ApiRoutes.Materials.Group, new MaterialRequest($"M-{suffix}", $"Material {suffix}", "kg"));
        var raw = await PostAsync<WarehouseResponse>(
            client, ApiRoutes.Warehouses.Group, new WarehouseCreateRequest($"RAW-{suffix}", "Raw", null));
        var finished = await PostAsync<WarehouseResponse>(
            client, ApiRoutes.Warehouses.Group, new WarehouseCreateRequest($"FG-{suffix}", "Finished", null));
        await PostAsync<object>(client, ApiRoutes.Inventories.Group + ApiRoutes.Inventories.Receive,
            new InventoryMovementRequest(raw.Id, material.Id, 100m, null, null, null));
        var bom = await PostAsync<BomResponse>(client, BomsRoute(product.Id), new BomRequest(
            1m, [new BomItemRequest(material.Id, 1m, null)]));
        using (var activateBom = await client.PostAsync(ActivateBomRoute(product.Id, bom.Id), null)) {
            activateBom.EnsureSuccessStatusCode();
        }
        var cutting = await PostAsync<WorkCenterResponse>(
            client, ApiRoutes.WorkCenters.Group, new WorkCenterCreateRequest($"CUT-{suffix}", "Cutting", null));
        var assembly = await PostAsync<WorkCenterResponse>(
            client, ApiRoutes.WorkCenters.Group, new WorkCenterCreateRequest($"ASM-{suffix}", "Assembly", null));
        var packaging = await PostAsync<WorkCenterResponse>(
            client, ApiRoutes.WorkCenters.Group, new WorkCenterCreateRequest($"PKG-{suffix}", "Packaging", null));
        var machines = new[] { cutting, assembly, packaging }
            .Select((workCenter, index) => PostAsync<MachineResponse>(
                client,
                ApiRoutes.Machines.Group,
                new MachineRequest(
                    $"MC-{suffix}-{index + 1}",
                    $"Machine {suffix} {index + 1}",
                    MachineStatuses.Available,
                    workCenter.Id)))
            .ToArray();
        var definitions = new[] {
            new RoutingOperationRequest(10, "Cutting", cutting.Id, 2, 5, null),
            new RoutingOperationRequest(20, "Assembly", assembly.Id, 1, 10, null),
            new RoutingOperationRequest(30, "Packaging", packaging.Id, 0, 3, null)
        }.Take(operationCount).ToList();
        var routing = await CreateRoutingAsync(client, product.Id, definitions);
        await ActivateRoutingAsync(client, product.Id, routing.Id);
        return new(
            product,
            material,
            raw,
            finished,
            bom,
            routing,
            cutting,
            assembly,
            await Task.WhenAll(machines));
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string route, object body) {
        using var response = await client.PostAsJsonAsync(route, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>())!.Data!;
    }

    private static async Task<RoutingResponse> CreateRoutingAsync(
        HttpClient client,
        Guid productId,
        IReadOnlyList<RoutingOperationRequest> operations) => await PostAsync<RoutingResponse>(
        client, RoutingsRoute(productId), new RoutingRequest(operations));

    private static async Task ActivateRoutingAsync(HttpClient client, Guid productId, Guid routingId) {
        using var response = await client.PostAsync(ActivateRoutingRoute(productId, routingId), null);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ProductionOrderResponse> CreateOrderAsync(
        HttpClient client,
        string number,
        Guid productId,
        decimal quantity) => await PostAsync<ProductionOrderResponse>(
        client, ApiRoutes.ProductionOrders.Group, new ProductionOrderRequest(number, productId, quantity));

    private static async Task<ProductionOrderResponse> ReleaseAsync(HttpClient client, Guid orderId) {
        using var response = await client.PostAsync(ReleaseOrderRoute(orderId), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
    }

    private static async Task<ProductionOrderResponse> StartOrderAsync(
        HttpClient client,
        ProductionOrderResponse order,
        Scenario scenario,
        decimal quantity) => await PostAsync<ProductionOrderResponse>(
        client,
        StartOrderRoute(order.Id),
        new StartProductionOrderRequest([
            new ProductionMaterialAllocationRequest(scenario.Material.Id, scenario.Raw.Id, quantity)
        ]));

    private static async Task<ProductionOrderOperationResponse> StartOperationAsync(
        HttpClient client,
        Guid orderId,
        Guid operationId,
        Guid machineId) {
        using var response = await client.PostAsJsonAsync(
            StartOperationRoute(orderId, operationId),
            new StartProductionOrderOperationRequest(machineId));
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<ApiResponse<ProductionOrderOperationResponse>>())!.Data!;
    }

    private static async Task<ProductionOrderOperationResponse> CompleteOperationAsync(
        HttpClient client,
        Guid orderId,
        Guid operationId) {
        using var response = await client.PostAsync(CompleteOperationRoute(orderId, operationId), null);
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<ApiResponse<ProductionOrderOperationResponse>>())!.Data!;
    }

    private static async Task<ProductionOrderResponse> CompleteOrderAsync(
        HttpClient client,
        Guid orderId,
        Guid warehouseId) => await PostAsync<ProductionOrderResponse>(
        client, CompleteOrderRoute(orderId), new CompleteProductionOrderRequest(warehouseId));

    private static async Task<ProductionOrderResponse> GetOrderAsync(HttpClient client, Guid orderId) {
        var response = await client.GetFromJsonAsync<ApiResponse<IReadOnlyList<ProductionOrderResponse>>>(
            ApiRoutes.ProductionOrders.Group);
        return response!.Data!.Single(order => order.Id == orderId);
    }

    private static MachineResponse MachineFor(
        Scenario scenario,
        ProductionOrderOperationResponse operation) => scenario.Machines.Single(
            machine => machine.WorkCenterId == operation.WorkCenterId);

    private async Task SetExecutionStateAsync(
        Guid orderId,
        Guid billOfMaterialId,
        Guid? routingId,
        string status,
        DateTime? startedAt) {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var changedAt = DateTime.UtcNow;
        var affected = await dbContext.ProductionOrders
            .Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(order => order.BillOfMaterialId, billOfMaterialId)
                .SetProperty(order => order.RoutingId, routingId)
                .SetProperty(order => order.Status, status)
                .SetProperty(order => order.ReleasedAt, changedAt)
                .SetProperty(order => order.StartedAt, startedAt)
                .SetProperty(order => order.UpdatedAt, changedAt));
        Assert.Equal(1, affected);
    }

    private async Task SetMachineStatusAsync(Guid machineId, string status) {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(1, await dbContext.Machines
            .Where(machine => machine.Id == machineId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(machine => machine.Status, status)));
    }

    private static string BomsRoute(Guid productId) => ApiRoutes.Products.Group + ApiRoutes.Products.Boms
        .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal);
    private static string ActivateBomRoute(Guid productId, Guid bomId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.ActivateBom.Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal)
            .Replace("{bomId:guid}", bomId.ToString(), StringComparison.Ordinal);
    private static string RoutingsRoute(Guid productId) => ApiRoutes.Products.Group + ApiRoutes.Products.Routings
        .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal);
    private static string ActivateRoutingRoute(Guid productId, Guid routingId) => ApiRoutes.Products.Group +
        ApiRoutes.Products.ActivateRouting
            .Replace("{productId:guid}", productId.ToString(), StringComparison.Ordinal)
            .Replace("{routingId:guid}", routingId.ToString(), StringComparison.Ordinal);
    private static string ReleaseOrderRoute(Guid orderId) => OrderRoute(ApiRoutes.ProductionOrders.Release, orderId);
    private static string StartOrderRoute(Guid orderId) => OrderRoute(ApiRoutes.ProductionOrders.Start, orderId);
    private static string CompleteOrderRoute(Guid orderId) => OrderRoute(ApiRoutes.ProductionOrders.Complete, orderId);
    private static string OperationsRoute(Guid orderId) => OrderRoute(ApiRoutes.ProductionOrders.Operations, orderId);
    private static string DeactivateWorkCenterRoute(Guid workCenterId) => ApiRoutes.WorkCenters.Group +
        ApiRoutes.WorkCenters.Deactivate.Replace(
            "{workCenterId:guid}", workCenterId.ToString(), StringComparison.Ordinal);
    private static string StartOperationRoute(Guid orderId, Guid operationId) =>
        OperationRoute(ApiRoutes.ProductionOrders.StartOperation, orderId, operationId);
    private static string CompleteOperationRoute(Guid orderId, Guid operationId) =>
        OperationRoute(ApiRoutes.ProductionOrders.CompleteOperation, orderId, operationId);
    private static string OrderRoute(string route, Guid orderId) => ApiRoutes.ProductionOrders.Group + route
        .Replace("{productionOrderId:guid}", orderId.ToString(), StringComparison.Ordinal);
    private static string OperationRoute(string route, Guid orderId, Guid operationId) => OrderRoute(route, orderId)
        .Replace("{operationId:guid}", operationId.ToString(), StringComparison.Ordinal);

    private sealed record Scenario(
        ProductResponse Product,
        MaterialResponse Material,
        WarehouseResponse Raw,
        WarehouseResponse Finished,
        BomResponse Bom,
        RoutingResponse Routing,
        WorkCenterResponse Cutting,
        WorkCenterResponse Assembly,
        IReadOnlyList<MachineResponse> Machines);
}
