using FactoryMind.Application.Common.Authorization;
using FactoryMind.Application.Common.Behaviors;
using Mediator;

namespace FactoryMind.Tests;

public sealed class AuthorizationBehaviorTests {
    [Fact]
    public async Task Protected_request_requires_an_authenticated_user() {
        var behavior = new AuthorizationBehavior<ProtectedRequest, string>(
            new FakePolicyChecker(isAuthenticated: false, isAuthorized: false));

        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            async () => await behavior.Handle(
                new ProtectedRequest(),
                (_, _) => ValueTask.FromResult("handled"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Protected_request_requires_its_policy() {
        var checker = new FakePolicyChecker(isAuthenticated: true, isAuthorized: false);
        var behavior = new AuthorizationBehavior<ProtectedRequest, string>(checker);

        await Assert.ThrowsAsync<ForbiddenAccessException>(
            async () => await behavior.Handle(
                new ProtectedRequest(),
                (_, _) => ValueTask.FromResult("handled"),
                CancellationToken.None));

        Assert.Equal(AuthorizationPolicies.Admin, checker.RequestedPolicy);
    }

    [Fact]
    public async Task Authorized_request_reaches_its_handler() {
        var behavior = new AuthorizationBehavior<ProtectedRequest, string>(
            new FakePolicyChecker(isAuthenticated: true, isAuthorized: true));

        var response = await behavior.Handle(
            new ProtectedRequest(),
            (_, _) => ValueTask.FromResult("handled"),
            CancellationToken.None);

        Assert.Equal("handled", response);
    }

    [Fact]
    public async Task Public_request_bypasses_policy_checks() {
        var checker = new FakePolicyChecker(isAuthenticated: false, isAuthorized: false);
        var behavior = new AuthorizationBehavior<PublicRequest, string>(checker);

        var response = await behavior.Handle(
            new PublicRequest(),
            (_, _) => ValueTask.FromResult("handled"),
            CancellationToken.None);

        Assert.Equal("handled", response);
        Assert.Null(checker.RequestedPolicy);
    }

    private sealed record ProtectedRequest : IRequest<string>, IAuthorizedRequest {
        public string Policy => AuthorizationPolicies.Admin;
    }

    private sealed record PublicRequest : IRequest<string>;

    private sealed class FakePolicyChecker(bool isAuthenticated, bool isAuthorized) : IPolicyChecker {
        public bool IsAuthenticated { get; } = isAuthenticated;
        public string? RequestedPolicy { get; private set; }

        public Task<bool> IsAuthorizedAsync(string policy, CancellationToken cancellationToken) {
            RequestedPolicy = policy;
            return Task.FromResult(isAuthorized);
        }
    }
}
