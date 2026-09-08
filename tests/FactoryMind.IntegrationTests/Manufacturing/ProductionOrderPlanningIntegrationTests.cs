using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Dashboard;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class ProductionOrderPlanningIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    private const string OrdersRoute = ApiRoutes.ProductionOrders.Group;
    private static readonly DateTime TodayEnd = EndOfDay(8);

    [Fact]
    public async Task Create_and_planned_update_persist_planning_fields_but_released_update_remains_blocked() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var product = await AddProductAsync(TestData.CompanyAId, "PLAN-PRODUCT");

        var defaultResponse = await Client.PostAsJsonAsync(
            OrdersRoute,
            new ProductionOrderRequest("PO-DEFAULT", product.Id, 2m));
        defaultResponse.EnsureSuccessStatusCode();
        var defaultOrder = (await defaultResponse.Content
            .ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
        Assert.Equal(ProductionOrderPriorities.Normal, defaultOrder.Priority);
        Assert.Null(defaultOrder.DueDate);
        Assert.Equal(ProductionOrderDeliveryStatuses.NoDueDate, defaultOrder.DeliveryStatus);

        using var lowResponse = await Client.PostAsJsonAsync(
            OrdersRoute,
            new ProductionOrderRequest(
                "PO-LOW",
                product.Id,
                1m,
                EndOfDay(12),
                ProductionOrderPriorities.Low));
        lowResponse.EnsureSuccessStatusCode();
        var lowOrder = (await lowResponse.Content
            .ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
        Assert.Equal(ProductionOrderPriorities.Low, lowOrder.Priority);
        var persistedLow = (await GetPageAsync(
            $"{OrdersRoute}?search=PO-LOW&page=1&pageSize=10")).Items.Single();
        Assert.Equal(new DateTime(2026, 9, 12, 23, 59, 59, 999, DateTimeKind.Utc), persistedLow.DueDate);

        var createResponse = await Client.PostAsJsonAsync(
            OrdersRoute,
            new ProductionOrderRequest(
                "PO-URGENT",
                product.Id,
                3m,
                EndOfDay(7),
                ProductionOrderPriorities.Urgent));
        createResponse.EnsureSuccessStatusCode();
        var created = (await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
        Assert.Equal(ProductionOrderDeliveryStatuses.Overdue, created.DeliveryStatus);
        Assert.Equal(-1, created.DaysUntilDue);

        var updateResponse = await Client.PutAsJsonAsync(
            $"{OrdersRoute}/{created.Id}",
            new ProductionOrderRequest(
                "PO-URGENT",
                product.Id,
                4m,
                EndOfDay(9),
                ProductionOrderPriorities.High));
        updateResponse.EnsureSuccessStatusCode();
        var updated = (await updateResponse.Content
            .ReadFromJsonAsync<ApiResponse<ProductionOrderResponse>>())!.Data!;
        Assert.Equal(ProductionOrderPriorities.High, updated.Priority);
        Assert.Equal(ProductionOrderDeliveryStatuses.DueSoon, updated.DeliveryStatus);
        Assert.Equal(1, updated.DaysUntilDue);

        await SetStatusAsync(created.Id, ProductionOrderStatuses.Released);
        using var blocked = await Client.PutAsJsonAsync(
            $"{OrdersRoute}/{created.Id}",
            new ProductionOrderRequest("PO-CHANGED", product.Id, 5m, TodayEnd, ProductionOrderPriorities.Low));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        using var invalid = await Client.PostAsJsonAsync(
            OrdersRoute,
            new ProductionOrderRequest("PO-INVALID", product.Id, 1m, null, "critical"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Planning_filters_sort_and_paginate_before_returning_stable_tenant_results() {
        await SeedPlanningOrdersAsync();
        await LoginAsync(Client, TestData.CompanyAAdminEmail);

        var first = await GetPageAsync(
            $"{OrdersRoute}/planning?deliveryStatus=overdue&page=1&pageSize=2&sortBy=dueDate&sortDirection=asc");
        var second = await GetPageAsync(
            $"{OrdersRoute}/planning?deliveryStatus=overdue&page=2&pageSize=2&sortBy=dueDate&sortDirection=asc");
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Empty(first.Items.Select(order => order.Id).Intersect(second.Items.Select(order => order.Id)));
        Assert.All(first.Items.Concat(second.Items), order => Assert.True(order.IsOverdue));
        Assert.DoesNotContain(first.Items.Concat(second.Items), order => order.Number == "PO-B-OVERDUE");

        var riskOrder = await GetPageAsync(
            $"{OrdersRoute}/planning?page=1&pageSize=100&sortBy=deliveryRisk&sortDirection=asc");
        Assert.All(riskOrder.Items.Take(3), order => Assert.Equal(
            ProductionOrderDeliveryStatuses.Overdue,
            order.DeliveryStatus));
        Assert.Equal(ProductionOrderDeliveryStatuses.DueSoon, riskOrder.Items[3].DeliveryStatus);
        var reversedRiskOrder = await GetPageAsync(
            $"{OrdersRoute}/planning?page=1&pageSize=100&sortBy=deliveryRisk&sortDirection=desc");
        Assert.DoesNotContain(
            reversedRiskOrder.Items.Take(3),
            order => order.DeliveryStatus == ProductionOrderDeliveryStatuses.Overdue);

        var priority = await GetPageAsync(
            $"{OrdersRoute}?page=1&pageSize=100&sortBy=priority&sortDirection=desc");
        var priorityWeights = priority.Items.Select(order => order.Priority switch {
            ProductionOrderPriorities.Urgent => 4,
            ProductionOrderPriorities.High => 3,
            ProductionOrderPriorities.Normal => 2,
            _ => 1
        }).ToList();
        Assert.Equal(priorityWeights.OrderByDescending(weight => weight), priorityWeights);

        using var invalidPage = await Client.GetAsync($"{OrdersRoute}?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
    }

    [Fact]
    public async Task Dashboard_and_AI_tools_use_server_calculated_tenant_scoped_delivery_facts() {
        await SeedPlanningOrdersAsync();
        await LoginAsync(Client, TestData.CompanyAAdminEmail);

        var dashboard = await Client.GetFromJsonAsync<ApiResponse<DashboardSummary>>(
            ApiRoutes.Dashboard.Group + ApiRoutes.Dashboard.Summary);
        Assert.Equal(3, dashboard!.Data!.OverdueOrders);
        Assert.Equal(1, dashboard.Data.DueSoonOrders);
        Assert.Equal(2, dashboard.Data.UrgentActiveOrders);
        Assert.Equal(1, dashboard.Data.CompletedLateOrders);
        Assert.Equal(1, dashboard.Data.OrdersWithoutDueDate);

        using var scope = ApiFactory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>();
        var companyA = await registry.ExecuteAsync(
            TestData.CompanyAId,
            Call("list_production_orders", """{"deliveryStatus":"overdue","limit":20}"""),
            CancellationToken.None);
        var companyB = await registry.ExecuteAsync(
            TestData.CompanyBId,
            Call("get_production_order_status", """{"number":"PO-SAME"}"""),
            CancellationToken.None);
        Assert.Equal(3, companyA.Records.Count);
        Assert.All(companyA.Records, record => Assert.Contains("delivery status overdue", record.Detail));
        var tenantRecord = Assert.Single(companyB.Records);
        Assert.Contains("priority low", tenantRecord.Detail);
        Assert.Contains("delivery status on_track", tenantRecord.Detail);
    }

    [Fact]
    public async Task Migration_adds_nullable_due_date_normal_priority_constraint_and_planning_indexes() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var columns = await dbContext.Database.SqlQueryRaw<ColumnInfo>("""
            SELECT "column_name" AS "ColumnName", "is_nullable" AS "IsNullable", "column_default" AS "ColumnDefault"
            FROM information_schema.columns
            WHERE table_name = 'production_orders' AND column_name IN ('DueDate', 'Priority')
            ORDER BY column_name
            """).ToListAsync();
        Assert.Equal("YES", columns.Single(column => column.ColumnName == "DueDate").IsNullable);
        Assert.Contains("normal", columns.Single(column => column.ColumnName == "Priority").ColumnDefault);

        var indexes = await dbContext.Database.SqlQueryRaw<string>("""
            SELECT indexname AS "Value"
            FROM pg_indexes
            WHERE tablename = 'production_orders'
            """).ToListAsync();
        Assert.Contains("IX_production_orders_CompanyId_Status_DueDate", indexes);
        Assert.Contains("IX_production_orders_CompanyId_Priority", indexes);
    }

    private async Task<Product> AddProductAsync(Guid companyId, string code) {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var product = new Product { CompanyId = companyId, Code = code, Name = code };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        return product;
    }

    private async Task SeedPlanningOrdersAsync() {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var productA = new Product { CompanyId = TestData.CompanyAId, Code = "PLAN-A", Name = "Plan A" };
        var productB = new Product { CompanyId = TestData.CompanyBId, Code = "PLAN-B", Name = "Plan B" };
        dbContext.Products.AddRange(productA, productB);
        dbContext.ProductionOrders.AddRange(
            Order(TestData.CompanyAId, productA, "PO-SAME", ProductionOrderStatuses.Planned, ProductionOrderPriorities.Urgent, EndOfDay(5)),
            Order(TestData.CompanyAId, productA, "PO-A-OVERDUE-2", ProductionOrderStatuses.Released, ProductionOrderPriorities.High, EndOfDay(6)),
            Order(TestData.CompanyAId, productA, "PO-A-OVERDUE-3", ProductionOrderStatuses.InProgress, ProductionOrderPriorities.Normal, EndOfDay(7)),
            Order(TestData.CompanyAId, productA, "PO-A-DUE-SOON", ProductionOrderStatuses.Planned, ProductionOrderPriorities.Urgent, EndOfDay(9)),
            Order(TestData.CompanyAId, productA, "PO-A-NO-DUE", ProductionOrderStatuses.Released, ProductionOrderPriorities.Low, null),
            Order(TestData.CompanyAId, productA, "PO-A-LATE", ProductionOrderStatuses.Completed, ProductionOrderPriorities.Normal, EndOfDay(6), EndOfDay(7)),
            Order(TestData.CompanyAId, productA, "PO-A-CANCELLED", ProductionOrderStatuses.Cancelled, ProductionOrderPriorities.Urgent, EndOfDay(5)),
            Order(TestData.CompanyBId, productB, "PO-SAME", ProductionOrderStatuses.Planned, ProductionOrderPriorities.Low, EndOfDay(20)),
            Order(TestData.CompanyBId, productB, "PO-B-OVERDUE", ProductionOrderStatuses.Planned, ProductionOrderPriorities.Urgent, EndOfDay(4)));
        await dbContext.SaveChangesAsync();
    }

    private async Task SetStatusAsync(Guid orderId, string status) {
        using var scope = ApiFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        await dbContext.ProductionOrders.Where(order => order.Id == orderId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.Status, status));
    }

    private async Task<ProductionOrderPageResponse> GetPageAsync(string route) =>
        (await Client.GetFromJsonAsync<ApiResponse<ProductionOrderPageResponse>>(route))!.Data!;

    private static ProductionOrder Order(
        Guid companyId,
        Product product,
        string number,
        string status,
        string priority,
        DateTime? dueDate,
        DateTime? completedAt = null) => new() {
            CompanyId = companyId,
            Product = product,
            ProductId = product.Id,
            Number = number,
            Quantity = 1m,
            Status = status,
            Priority = priority,
            DueDate = dueDate,
            CompletedAt = completedAt,
            CreatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    private static DateTime EndOfDay(int day) =>
        new DateTime(2026, 9, day, 23, 59, 59, 999, DateTimeKind.Utc).AddTicks(9_999);

    private static AiToolCall Call(string name, string json) {
        using var document = JsonDocument.Parse(json);
        return new AiToolCall(name, document.RootElement.Clone());
    }

    private sealed record ColumnInfo(string ColumnName, string IsNullable, string? ColumnDefault);
}
