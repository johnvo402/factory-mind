using FactoryMind.Application.Common.Identity;
using FactoryMind.Application.Features.Dashboard;
using FactoryMind.Application.Features.Dashboard.GetDashboardSummary;

namespace FactoryMind.Tests;

public sealed class DashboardQueryHandlerTests {
    [Fact]
    public async Task Summary_uses_the_current_company_scope() {
        var currentUser = new FakeCurrentUser();
        var expected = new DashboardSummary(3, 5, 2, 4, 1);
        var repository = new FakeDashboardRepository(expected);
        var handler = new GetDashboardSummaryQueryHandler(repository, currentUser);

        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
        Assert.Equal(currentUser.CompanyId, repository.CompanyId);
    }

    private sealed class FakeCurrentUser : ICurrentUser {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid CompanyId { get; } = Guid.NewGuid();
        public string Role => "User";
    }

    private sealed class FakeDashboardRepository(
        DashboardSummary summary) : IDashboardRepository {
        public Guid? CompanyId { get; private set; }

        public Task<DashboardSummary> GetSummaryAsync(
            Guid companyId,
            CancellationToken cancellationToken) {
            CompanyId = companyId;
            return Task.FromResult(summary);
        }
    }
}
