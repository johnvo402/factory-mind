using System.Net;
using System.Net.Http.Json;
using FactoryMind.Api.Routing;
using FactoryMind.Application.Features.AiActions;
using FactoryMind.Application.Features.Chat;
using FactoryMind.Application.Features.Chat.CreateConversation;
using FactoryMind.Domain.Chat;
using FactoryMind.Domain.Identity;
using FactoryMind.Domain.Manufacturing;
using FactoryMind.Infrastructure.Persistence;
using FactoryMind.IntegrationTests.Infrastructure;
using FactoryMind.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FactoryMind.IntegrationTests.Chat;

[Collection(IntegrationTestCollection.Name)]
public sealed class AiReleaseActionIntegrationTests(PostgreSqlFixture fixture) : IntegrationTestBase(fixture) {
    [Fact]
    public async Task Repeated_identical_chat_request_reuses_pending_snapshot() {
        await SeedReleaseOrderAsync("PO-AI-DUP");
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var conversationId = await CreateConversationAsync(Client);
        await CreateProposalAsync(Client, conversationId, "Release PO-AI-DUP");
        await CreateProposalAsync(Client, conversationId, "Release PO-AI-DUP");

        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(1, await db.AiActionProposals.CountAsync());
        Assert.Equal(1, await db.AiActionEvents.CountAsync(actionEvent =>
            actionEvent.EventType == AiActionEventTypes.Proposed));
    }

    [Fact]
    public async Task Concurrent_confirmations_produce_one_release_and_one_success_event() {
        var seeded = await SeedReleaseOrderAsync("PO-AI-RACE");
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var conversationId = await CreateConversationAsync(Client);
        await CreateProposalAsync(Client, conversationId, "Release PO-AI-RACE");
        Guid proposalId;
        using (var scope = ApiFactory.Services.CreateScope()) {
            proposalId = await scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>()
                .AiActionProposals.AsNoTracking().Select(proposal => proposal.Id).SingleAsync();
        }

        var route = ActionRoute(proposalId, ApiRoutes.AiActions.Confirm);
        var responses = await Task.WhenAll(Client.PostAsync(route, null), Client.PostAsync(route, null));
        Assert.All(responses, response => response.EnsureSuccessStatusCode());
        foreach (var response in responses) response.Dispose();

        using var verifyScope = ApiFactory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Equal(ProductionOrderStatuses.Released, await db.ProductionOrders.AsNoTracking()
            .Where(order => order.Id == seeded.OrderId).Select(order => order.Status).SingleAsync());
        Assert.Single(await db.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.ProductionOrderId == seeded.OrderId).ToListAsync());
        Assert.Equal(1, await db.AiActionEvents.CountAsync(actionEvent =>
            actionEvent.ProposalId == proposalId
            && actionEvent.EventType == AiActionEventTypes.ExecutionSucceeded));
    }

    [Fact]
    public async Task Chat_only_proposes_then_exact_confirm_releases_once_without_start_or_inventory_mutation() {
        var seeded = await SeedReleaseOrderAsync("PO-AI-001");
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var conversationId = await CreateConversationAsync(Client);

        using var stream = await Client.PostAsJsonAsync(
            ConversationRoute(conversationId, ApiRoutes.Conversations.StreamMessage),
            new { content = "Release PO-AI-001" });
        stream.EnsureSuccessStatusCode();
        var sse = await stream.Content.ReadAsStringAsync();
        Assert.Contains("event: ai-action-proposal", sse);
        Assert.Contains("\"status\":\"pending\"", sse);

        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var proposal = await db.AiActionProposals.AsNoTracking().SingleAsync();
        Assert.Equal(AiActionProposalStatuses.Pending, proposal.Status);
        Assert.Equal(ProductionOrderStatuses.Planned, await db.ProductionOrders.AsNoTracking()
            .Where(order => order.Id == seeded.OrderId).Select(order => order.Status).SingleAsync());
        Assert.Empty(await db.ProductionOrderOperations.AsNoTracking().ToListAsync());

        var confirmRoute = ActionRoute(proposal.Id, ApiRoutes.AiActions.Confirm);
        using var confirmed = await Client.PostAsync(confirmRoute, null);
        confirmed.EnsureSuccessStatusCode();
        using var repeated = await Client.PostAsync(confirmRoute, null);
        repeated.EnsureSuccessStatusCode();

        db.ChangeTracker.Clear();
        var order = await db.ProductionOrders.AsNoTracking().SingleAsync(candidate => candidate.Id == seeded.OrderId);
        Assert.Equal(ProductionOrderStatuses.Released, order.Status);
        Assert.Equal(seeded.BomId, order.BillOfMaterialId);
        Assert.Equal(seeded.RoutingId, order.RoutingId);
        Assert.NotNull(order.ReleasedAt);
        Assert.Null(order.StartedAt);
        Assert.Single(await db.ProductionOrderOperations.AsNoTracking()
            .Where(operation => operation.ProductionOrderId == seeded.OrderId).ToListAsync());
        Assert.Empty(await db.InventoryTransactions.AsNoTracking().ToListAsync());
        Assert.Equal(MachineStatuses.Available, await db.Machines.AsNoTracking()
            .Where(machine => machine.Id == TestData.CompanyAMachineId)
            .Select(machine => machine.Status).SingleAsync());
        Assert.Equal(1, await db.AiActionEvents.CountAsync(actionEvent =>
            actionEvent.ProposalId == proposal.Id
            && actionEvent.EventType == AiActionEventTypes.ExecutionSucceeded));
    }

    [Fact]
    public async Task Unauthorized_and_cross_tenant_users_cannot_create_or_discover_proposal() {
        await SeedReleaseOrderAsync("PO-AI-002");
        await LoginAsync(Client, TestData.CompanyAUserEmail);
        var conversationId = await CreateConversationAsync(Client);
        using var stream = await Client.PostAsJsonAsync(
            ConversationRoute(conversationId, ApiRoutes.Conversations.StreamMessage),
            new { content = "Release PO-AI-002" });
        stream.EnsureSuccessStatusCode();

        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        Assert.Empty(await db.AiActionProposals.AsNoTracking().ToListAsync());
        Assert.Equal(ProductionOrderStatuses.Planned, await db.ProductionOrders.AsNoTracking()
            .Select(order => order.Status).SingleAsync());

        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var adminConversation = await CreateConversationAsync(Client);
        await CreateProposalAsync(Client, adminConversation, "Release PO-AI-002");
        var proposalId = await db.AiActionProposals.AsNoTracking().Select(proposal => proposal.Id).SingleAsync();

        using var foreign = CreateClient();
        await LoginAsync(foreign, TestData.CompanyBAdminEmail);
        using var response = await foreign.GetAsync(ActionRoute(proposalId, ApiRoutes.AiActions.ById));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Quantity_change_marks_confirmed_proposal_stale_and_cancel_is_idempotent() {
        var seeded = await SeedReleaseOrderAsync("PO-AI-003");
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var conversationId = await CreateConversationAsync(Client);
        await CreateProposalAsync(Client, conversationId, "Release PO-AI-003");

        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var proposalId = await db.AiActionProposals.AsNoTracking().Select(proposal => proposal.Id).SingleAsync();
        await db.ProductionOrders.Where(order => order.Id == seeded.OrderId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(order => order.Quantity, 101m));

        using var response = await Client.PostAsync(ActionRoute(proposalId, ApiRoutes.AiActions.Confirm), null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AiActionProposalStatuses.Stale, await db.AiActionProposals.AsNoTracking()
            .Select(proposal => proposal.Status).SingleAsync());
        Assert.Equal(ProductionOrderStatuses.Planned, await db.ProductionOrders.AsNoTracking()
            .Select(order => order.Status).SingleAsync());

        await CreateProposalAsync(Client, conversationId, "Release PO-AI-003");
        var cancellable = await db.AiActionProposals.AsNoTracking()
            .Where(proposal => proposal.Status == AiActionProposalStatuses.Pending)
            .Select(proposal => proposal.Id).SingleAsync();
        var cancelRoute = ActionRoute(cancellable, ApiRoutes.AiActions.Cancel);
        using var cancelled = await Client.PostAsync(cancelRoute, null);
        cancelled.EnsureSuccessStatusCode();
        using var repeated = await Client.PostAsync(cancelRoute, null);
        repeated.EnsureSuccessStatusCode();
        Assert.Equal(1, await db.AiActionEvents.CountAsync(actionEvent =>
            actionEvent.ProposalId == cancellable && actionEvent.EventType == AiActionEventTypes.Cancelled));
    }

    [Fact]
    public async Task Expired_proposal_cannot_release_and_is_audited_once() {
        var seeded = await SeedReleaseOrderAsync("PO-AI-EXPIRED");
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var conversationId = await CreateConversationAsync(Client);
        await CreateProposalAsync(Client, conversationId, "Release PO-AI-EXPIRED");

        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var proposalId = await db.AiActionProposals.AsNoTracking().Select(proposal => proposal.Id).SingleAsync();
        await db.AiActionProposals.Where(proposal => proposal.Id == proposalId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(proposal => proposal.CreatedAt, DateTime.UtcNow.AddMinutes(-2))
                .SetProperty(proposal => proposal.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));

        using var response = await Client.PostAsync(ActionRoute(proposalId, ApiRoutes.AiActions.Confirm), null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ProductionOrderStatuses.Planned, await db.ProductionOrders.AsNoTracking()
            .Where(order => order.Id == seeded.OrderId).Select(order => order.Status).SingleAsync());
        Assert.Equal(1, await db.AiActionEvents.CountAsync(actionEvent =>
            actionEvent.ProposalId == proposalId && actionEvent.EventType == AiActionEventTypes.Expired));
    }

    [Theory]
    [InlineData("bom")]
    [InlineData("routing")]
    public async Task Active_definition_change_makes_proposal_stale(string changedDefinition) {
        var seeded = await SeedReleaseOrderAsync($"PO-AI-{changedDefinition.ToUpperInvariant()}");
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var conversationId = await CreateConversationAsync(Client);
        await CreateProposalAsync(Client, conversationId, $"Release PO-AI-{changedDefinition.ToUpperInvariant()}");
        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var proposalId = await db.AiActionProposals.AsNoTracking().Select(proposal => proposal.Id).SingleAsync();
        if (changedDefinition == "bom") {
            await db.BillOfMaterials.Where(bom => bom.Id == seeded.BomId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(bom => bom.Status, BillOfMaterialStatuses.Archived));
        } else {
            await db.Routings.Where(routing => routing.Id == seeded.RoutingId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(routing => routing.Status, RoutingStatuses.Archived));
        }

        using var response = await Client.PostAsync(ActionRoute(proposalId, ApiRoutes.AiActions.Confirm), null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AiActionProposalStatuses.Stale, await db.AiActionProposals.AsNoTracking()
            .Select(proposal => proposal.Status).SingleAsync());
        Assert.Equal(ProductionOrderStatuses.Planned, await db.ProductionOrders.AsNoTracking()
            .Where(order => order.Id == seeded.OrderId).Select(order => order.Status).SingleAsync());
    }

    [Fact]
    public async Task Work_center_change_fails_confirmation_without_release() {
        var seeded = await SeedReleaseOrderAsync("PO-AI-WC");
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var conversationId = await CreateConversationAsync(Client);
        await CreateProposalAsync(Client, conversationId, "Release PO-AI-WC");
        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var proposalId = await db.AiActionProposals.AsNoTracking().Select(proposal => proposal.Id).SingleAsync();
        await db.WorkCenters.Where(workCenter => workCenter.Code == "AI-WC")
            .ExecuteUpdateAsync(setters => setters.SetProperty(workCenter => workCenter.IsActive, false));

        using var response = await Client.PostAsync(ActionRoute(proposalId, ApiRoutes.AiActions.Confirm), null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AiActionProposalStatuses.Failed, await db.AiActionProposals.AsNoTracking()
            .Select(proposal => proposal.Status).SingleAsync());
        Assert.Equal(ProductionOrderStatuses.Planned, await db.ProductionOrders.AsNoTracking()
            .Where(order => order.Id == seeded.OrderId).Select(order => order.Status).SingleAsync());
    }

    [Fact]
    public async Task Another_manager_in_same_tenant_cannot_confirm_creators_proposal() {
        await SeedReleaseOrderAsync("PO-AI-OWNER");
        await LoginAsync(Client, TestData.CompanyAAdminEmail);
        var conversationId = await CreateConversationAsync(Client);
        await CreateProposalAsync(Client, conversationId, "Release PO-AI-OWNER");
        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var proposalId = await db.AiActionProposals.AsNoTracking().Select(proposal => proposal.Id).SingleAsync();
        var hasher = scope.ServiceProvider.GetRequiredService<FactoryMind.Application.Features.Auth.ICredentialHasher>();
        db.Users.Add(new User {
            CompanyId = TestData.CompanyAId,
            Name = "Second Manager",
            Email = "manager-2@factorymind.test",
            Role = UserRoles.Manager,
            PasswordHash = hasher.HashPassword(TestData.Password)
        });
        await db.SaveChangesAsync();

        using var other = CreateClient();
        await LoginAsync(other, "manager-2@factorymind.test");
        using var response = await other.PostAsync(ActionRoute(proposalId, ApiRoutes.AiActions.Confirm), null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<ReleaseSeed> SeedReleaseOrderAsync(string number) {
        using var scope = ApiFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FactoryMindDbContext>();
        var product = new Product { CompanyId = TestData.CompanyAId, Code = "AI-PROD", Name = "AI Product" };
        var workCenter = new WorkCenter {
            CompanyId = TestData.CompanyAId,
            Code = "AI-WC",
            Name = "AI Work Center",
            IsActive = true
        };
        var bom = new BillOfMaterial {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Revision = 1,
            OutputQuantity = 1,
            Status = BillOfMaterialStatuses.Active
        };
        var routing = new Routing {
            CompanyId = TestData.CompanyAId,
            Product = product,
            Revision = 1,
            Status = RoutingStatuses.Active
        };
        routing.Operations.Add(new RoutingOperation {
            Sequence = 10,
            Name = "Prepare",
            WorkCenter = workCenter,
            SetupTimeMinutes = 1,
            RunTimeMinutes = 2
        });
        var order = new ProductionOrder {
            CompanyId = TestData.CompanyAId,
            Number = number,
            Product = product,
            Quantity = 100,
            Status = ProductionOrderStatuses.Planned
        };
        db.AddRange(product, workCenter, bom, routing, order);
        await db.SaveChangesAsync();
        return new(order.Id, bom.Id, routing.Id);
    }

    private static async Task<Guid> CreateConversationAsync(HttpClient client) {
        using var response = await client.PostAsJsonAsync(
            ApiRoutes.Conversations.Group, new CreateConversationCommand(null));
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<ConversationResponse>>();
        return envelope!.Data!.Id;
    }

    private static async Task CreateProposalAsync(HttpClient client, Guid conversationId, string content) {
        using var response = await client.PostAsJsonAsync(
            ConversationRoute(conversationId, ApiRoutes.Conversations.StreamMessage), new { content });
        response.EnsureSuccessStatusCode();
        await response.Content.ReadAsStringAsync();
    }

    private static string ConversationRoute(Guid conversationId, string template) =>
        ApiRoutes.Conversations.Group + template.Replace(
            "{conversationId:guid}", conversationId.ToString(), StringComparison.Ordinal);

    private static string ActionRoute(Guid proposalId, string template) =>
        ApiRoutes.AiActions.Group + template.Replace(
            "{proposalId:guid}", proposalId.ToString(), StringComparison.Ordinal);

    private sealed record ReleaseSeed(Guid OrderId, Guid BomId, Guid RoutingId);
}
