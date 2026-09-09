using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FactoryMind.Api.Endpoints;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.ProductionOrders;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.WorkCenters;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Manufacturing;

[Collection(IntegrationTestCollection.Name)]
public sealed class CapacityPlanningIntegrationTests(PostgreSqlFixture fixture)
    : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Calendar_replace_validates_atomically_and_is_tenant_isolated() {
        var clientA = CreateClient();
        var clientB = CreateClient();
        await LoginAsync(clientA, TestData.CompanyAAdminEmail);
        await LoginAsync(clientB, TestData.CompanyBAdminEmail);
        var center = await CreateCenterAsync(clientA, $"WC-CAL-{Guid.NewGuid():N}"[..18]);
        var route = CalendarRoute(center.Id);
        var valid = new ReplaceWorkCenterCalendarRequest(2, [
            new WorkCenterShiftRequest(2, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new WorkCenterShiftRequest(2, new TimeOnly(13, 0), new TimeOnly(17, 0))
        ], [new WorkCenterDayOffRequest(new DateOnly(2026, 9, 15))]);

        using (var savedResponse = await clientA.PutAsJsonAsync(route, valid)) {
            savedResponse.EnsureSuccessStatusCode();
            var saved = (await savedResponse.Content.ReadFromJsonAsync<ApiResponse<WorkCenterCalendarResponse>>())!.Data!;
            Assert.Equal(2, saved.ParallelCapacity);
            Assert.Equal(2, saved.Shifts.Count);
            Assert.Single(saved.DaysOff);
        }
        using (var foreign = await clientB.GetAsync(route)) {
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        }
        using (var overlap = await clientA.PutAsJsonAsync(route,
                   new ReplaceWorkCenterCalendarRequest(3, [
                       new WorkCenterShiftRequest(2, new TimeOnly(8, 0), new TimeOnly(12, 0)),
                       new WorkCenterShiftRequest(2, new TimeOnly(11, 0), new TimeOnly(14, 0))
                   ], []))) {
            Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        }

        var unchanged = (await clientA.GetFromJsonAsync<ApiResponse<WorkCenterCalendarResponse>>(route))!.Data!;
        Assert.Equal(2, unchanged.ParallelCapacity);
        Assert.Equal(2, unchanged.Shifts.Count);
    }

    [Fact]
    public async Task Planned_uses_active_routing_provisionally_and_released_uses_locked_snapshot() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var center = await SeedPlanningDataAsync(suffix);
        await PutCalendarAsync(Client, center.Id, 2);

        var response = await Client.GetFromJsonAsync<ApiResponse<SchedulePreviewResponse>>(
            ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.SchedulePreview + "?horizonDays=14");
        var preview = response!.Data!;
        var planned = preview.Orders.Single(order => order.Number == $"PO-PLAN-{suffix}");
        var released = preview.Orders.Single(order => order.Number == $"PO-LOCK-{suffix}");

        Assert.True(planned.IsProvisional);
        Assert.Equal(PlanningSources.ActiveRouting, planned.PlanningSource);
        Assert.Equal(90, planned.Operations.Single().StandardDurationMinutes);
        Assert.False(released.IsProvisional);
        Assert.Equal(PlanningSources.LockedSnapshot, released.PlanningSource);
        Assert.Equal(30, released.Operations.Single().StandardDurationMinutes);
        Assert.All(preview.Orders, order => Assert.DoesNotContain("TENANT-B", order.Number));

        using var scope = ApiFactory.Services.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>();
        var scheduleEvidence = await tools.ExecuteAsync(TestData.CompanyAId,
            Call("get_production_order_schedule_preview", $$"""{"number":"PO-PLAN-{{suffix}}","horizonDays":14}"""),
            CancellationToken.None);
        var capacityEvidence = await tools.ExecuteAsync(TestData.CompanyAId,
            Call("get_work_center_capacity_preview", $$"""{"code":"WC-{{suffix}}","horizonDays":14}"""),
            CancellationToken.None);
        Assert.Contains("not a guarantee", Assert.Single(scheduleEvidence.Records).Detail);
        Assert.Contains("planned capacity load", Assert.Single(capacityEvidence.Records).Detail);
    }

    [Fact]
    public async Task Filtered_api_and_ai_order_preview_keep_canonical_competing_workload() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (center, targetOrder) = await SeedCanonicalOrderContentionAsync(suffix);
        await PutCalendarAsync(Client, center.Id, 1);
        var previewRoute = ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.SchedulePreview;

        var canonical = (await Client.GetFromJsonAsync<ApiResponse<SchedulePreviewResponse>>(
            previewRoute + "?horizonDays=14"))!.Data!;
        var canonicalTarget = canonical.Orders.Single(order => order.Id == targetOrder.Id);
        var filtered = (await Client.GetFromJsonAsync<ApiResponse<SchedulePreviewResponse>>(
            previewRoute + $"?horizonDays=14&orderId={targetOrder.Id}"))!.Data!;
        var priorityFiltered = (await Client.GetFromJsonAsync<ApiResponse<SchedulePreviewResponse>>(
            previewRoute + $"?horizonDays=14&priority={ProductionOrderPriorities.Normal}"))!.Data!;

        Assert.Equal(canonicalTarget.ProjectedCompletion, filtered.Orders.Single().ProjectedCompletion);
        Assert.Equal(canonicalTarget.ProjectedCompletion,
            priorityFiltered.Orders.Single(order => order.Id == targetOrder.Id).ProjectedCompletion);
        Assert.Equal(1, filtered.Summary.OrdersConsidered);
        Assert.Equal(canonical.WorkCenters.Single(item => item.Id == center.Id).ScheduledMinutes,
            filtered.WorkCenters.Single(item => item.Id == center.Id).ScheduledMinutes);

        using var scope = ApiFactory.Services.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>();
        var evidence = await tools.ExecuteAsync(TestData.CompanyAId,
            Call("get_production_order_schedule_preview",
                $$"""{"number":"{{targetOrder.Number}}","horizonDays":14}"""),
            CancellationToken.None);
        var expectedCompletion = canonicalTarget.ProjectedCompletion!.Value
            .ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains($"projected completion {expectedCompletion}", Assert.Single(evidence.Records).Detail);
    }

    [Fact]
    public async Task Work_center_view_and_ai_capacity_keep_indirect_upstream_contention() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var (cut, paint, targetOrder) = await SeedIndirectContentionAsync(suffix);
        WorkCenterShiftRequest[] shortTuesdayShift = [
            new(2, new TimeOnly(12, 0), new TimeOnly(14, 0))
        ];
        await PutCalendarAsync(Client, cut.Id, 1, shortTuesdayShift);
        await PutCalendarAsync(Client, paint.Id, 1, shortTuesdayShift);
        var previewRoute = ApiRoutes.ProductionOrders.Group + ApiRoutes.ProductionOrders.SchedulePreview;

        var canonical = (await Client.GetFromJsonAsync<ApiResponse<SchedulePreviewResponse>>(
            previewRoute + "?horizonDays=1"))!.Data!;
        var canonicalTarget = canonical.Orders.Single(order => order.Id == targetOrder.Id);
        var filtered = (await Client.GetFromJsonAsync<ApiResponse<SchedulePreviewResponse>>(
            previewRoute + $"?horizonDays=1&workCenterId={paint.Id}"))!.Data!;

        Assert.Equal(canonicalTarget.ProjectedStart, filtered.Orders.Single().ProjectedStart);
        Assert.Null(filtered.Orders.Single().ProjectedCompletion);
        Assert.Single(filtered.WorkCenters);
        Assert.Equal(paint.Id, filtered.WorkCenters.Single().Id);
        Assert.Contains(filtered.Unscheduled, item => item.OrderId == targetOrder.Id
            && item.WorkCenterId == paint.Id
            && item.Reason == ScheduleUnscheduledReasons.HorizonExceeded);

        using var scope = ApiFactory.Services.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IManufacturingToolRegistry>();
        var evidence = await tools.ExecuteAsync(TestData.CompanyAId,
            Call("get_work_center_capacity_preview", $$"""{"code":"{{paint.Code}}","horizonDays":1}"""),
            CancellationToken.None);
        var detail = Assert.Single(evidence.Records).Detail;
        Assert.Contains("scheduled minutes 0", detail);
        Assert.Contains("unscheduled demand minutes 60", detail);
    }

    [Fact]
    public async Task Invalid_timezone_is_controlled_and_migration_has_planning_schema() {
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        using var invalid = await Client.PutAsJsonAsync(
            ApiRoutes.Settings.Group + ApiRoutes.Settings.Company,
            new UpdateCompanySettingsRequest("FactoryMind Company A", "Mars/Olympus"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var columns = await db.Database.SqlQueryRaw<string>("""
            SELECT "column_name" AS "Value" FROM information_schema.columns
            WHERE (table_name = 'companies' AND column_name = 'TimeZoneId')
               OR (table_name = 'work_centers' AND column_name = 'ParallelCapacity')
            ORDER BY "column_name"
            """).ToListAsync();
        Assert.Equal(["ParallelCapacity", "TimeZoneId"], columns);
        Assert.True(await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS "Value" FROM information_schema.tables
            WHERE table_name IN ('work_center_shifts', 'work_center_days_off')
            """).SingleAsync() == 2);
    }

    private async Task<WorkCenter> SeedPlanningDataAsync(string suffix) {
        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var center = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = $"WC-{suffix}",
            Name = "Planning Center",
            ParallelCapacity = 1
        };
        var product = new Product { CompanyId = TestData.CompanyAId, Code = $"P-{suffix}", Name = "Planning Product" };
        var active = new Routing {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Revision = 2,
            Status = RoutingStatuses.Active
        };
        active.Operations.Add(new RoutingOperation {
            Sequence = 10,
            Name = "Active operation",
            WorkCenter = center,
            SetupTimeMinutes = 30,
            RunTimeMinutes = 60
        });
        var lockedRouting = new Routing {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Revision = 1,
            Status = RoutingStatuses.Archived
        };
        lockedRouting.Operations.Add(new RoutingOperation {
            Sequence = 10,
            Name = "Old locked operation",
            WorkCenter = center,
            SetupTimeMinutes = 10,
            RunTimeMinutes = 20
        });
        var planned = new ProductionOrder {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Number = $"PO-PLAN-{suffix}",
            Quantity = 1,
            Status = ProductionOrderStatuses.Planned,
            Priority = ProductionOrderPriorities.Normal
        };
        var released = new ProductionOrder {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Routing = lockedRouting,
            Number = $"PO-LOCK-{suffix}",
            Quantity = 1,
            Status = ProductionOrderStatuses.Released,
            Priority = ProductionOrderPriorities.High,
            ReleasedAt = new DateTime(2026, 9, 8, 11, 0, 0, DateTimeKind.Utc)
        };
        released.Operations.Add(new ProductionOrderOperation {
            CompanyId = TestData.CompanyAId,
            Sequence = 10,
            Name = "Old locked operation",
            WorkCenter = center,
            WorkCenterCode = center.Code,
            WorkCenterName = center.Name,
            SetupTimeMinutes = 10,
            RunTimeMinutes = 20,
            RoutingOperation = lockedRouting.Operations.Single()
        });
        db.AddRange(center, product, active, lockedRouting, planned, released);
        await db.SaveChangesAsync();
        return center;
    }

    private static async Task<WorkCenterResponse> CreateCenterAsync(HttpClient client, string code) {
        using var response = await client.PostAsJsonAsync(ApiRoutes.WorkCenters.Group,
            new WorkCenterCreateRequest(code, code, null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<WorkCenterResponse>>())!.Data!;
    }

    private async Task<(WorkCenter Center, ProductionOrder Target)> SeedCanonicalOrderContentionAsync(string suffix) {
        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var center = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = $"WC-CAN-{suffix}",
            Name = "Canonical Center",
            ParallelCapacity = 1
        };
        var product = new Product {
            CompanyId = TestData.CompanyAId,
            Code = $"P-CAN-{suffix}",
            Name = "Canonical Product"
        };
        var routing = new Routing {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Revision = 1,
            Status = RoutingStatuses.Active
        };
        routing.Operations.Add(new RoutingOperation {
            Sequence = 10,
            Name = "Canonical cut",
            WorkCenter = center,
            SetupTimeMinutes = 0,
            RunTimeMinutes = 60
        });
        var competitor = new ProductionOrder {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Number = $"PO-A-{suffix}",
            Quantity = 1,
            Status = ProductionOrderStatuses.Planned,
            Priority = ProductionOrderPriorities.Urgent
        };
        var target = new ProductionOrder {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Number = $"PO-B-{suffix}",
            Quantity = 1,
            Status = ProductionOrderStatuses.Planned,
            Priority = ProductionOrderPriorities.Normal
        };
        db.AddRange(center, product, routing, competitor, target);
        await db.SaveChangesAsync();
        return (center, target);
    }

    private async Task<(WorkCenter Cut, WorkCenter Paint, ProductionOrder Target)> SeedIndirectContentionAsync(
        string suffix) {
        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var cut = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = $"CUT-{suffix}",
            Name = "Cut",
            ParallelCapacity = 1
        };
        var paint = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = $"PAINT-{suffix}",
            Name = "Paint",
            ParallelCapacity = 1
        };
        var targetProduct = new Product {
            CompanyId = TestData.CompanyAId,
            Code = $"P-TARGET-{suffix}",
            Name = "Target Product"
        };
        var competitorProduct = new Product {
            CompanyId = TestData.CompanyAId,
            Code = $"P-COMP-{suffix}",
            Name = "Competing Product"
        };
        var targetRouting = new Routing {
            CompanyId = TestData.CompanyAId,
            Product = targetProduct,
            Revision = 1,
            Status = RoutingStatuses.Active
        };
        targetRouting.Operations.Add(new RoutingOperation {
            Sequence = 10,
            Name = "Target cut",
            WorkCenter = cut,
            RunTimeMinutes = 60
        });
        targetRouting.Operations.Add(new RoutingOperation {
            Sequence = 20,
            Name = "Target paint",
            WorkCenter = paint,
            RunTimeMinutes = 60
        });
        var competitorRouting = new Routing {
            CompanyId = TestData.CompanyAId,
            Product = competitorProduct,
            Revision = 1,
            Status = RoutingStatuses.Active
        };
        competitorRouting.Operations.Add(new RoutingOperation {
            Sequence = 10,
            Name = "Competing cut",
            WorkCenter = cut,
            RunTimeMinutes = 60
        });
        var target = new ProductionOrder {
            CompanyId = TestData.CompanyAId,
            Product = targetProduct,
            Number = $"PO-TARGET-{suffix}",
            Quantity = 1,
            Status = ProductionOrderStatuses.Planned,
            Priority = ProductionOrderPriorities.Normal
        };
        var competitor = new ProductionOrder {
            CompanyId = TestData.CompanyAId,
            Product = competitorProduct,
            Number = $"PO-COMP-{suffix}",
            Quantity = 1,
            Status = ProductionOrderStatuses.Planned,
            Priority = ProductionOrderPriorities.Urgent
        };
        db.AddRange(cut, paint, targetProduct, competitorProduct, targetRouting, competitorRouting, target, competitor);
        await db.SaveChangesAsync();
        return (cut, paint, target);
    }

    private static async Task PutCalendarAsync(
        HttpClient client,
        Guid centerId,
        int capacity,
        IReadOnlyList<WorkCenterShiftRequest>? shifts = null) {
        using var response = await client.PutAsJsonAsync(CalendarRoute(centerId),
            new ReplaceWorkCenterCalendarRequest(capacity, shifts ?? [
                new WorkCenterShiftRequest(2, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                new WorkCenterShiftRequest(3, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                new WorkCenterShiftRequest(4, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                new WorkCenterShiftRequest(5, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                new WorkCenterShiftRequest(1, new TimeOnly(8, 0), new TimeOnly(17, 0))
            ], []));
        response.EnsureSuccessStatusCode();
    }

    private static string CalendarRoute(Guid centerId) => ApiRoutes.WorkCenters.Group
        + ApiRoutes.WorkCenters.Calendar.Replace("{workCenterId:guid}", centerId.ToString(), StringComparison.Ordinal);

    private static AiToolCall Call(string name, string json) {
        using var document = JsonDocument.Parse(json);
        return new AiToolCall(name, document.RootElement.Clone());
    }
}
