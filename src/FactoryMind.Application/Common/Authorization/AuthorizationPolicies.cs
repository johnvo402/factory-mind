namespace FactoryMind.Application.Common.Authorization;

public static class AuthorizationPolicies {
    public const string Authenticated = "Authenticated";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
}

public interface IAuthorizedRequest {
    string Policy { get; }
}

public interface IPolicyChecker {
    bool IsAuthenticated { get; }
    Task<bool> IsAuthorizedAsync(string policy, CancellationToken cancellationToken);
}
