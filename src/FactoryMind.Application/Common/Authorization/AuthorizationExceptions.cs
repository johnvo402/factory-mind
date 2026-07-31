namespace FactoryMind.Application.Common.Authorization;

public sealed class AuthenticationRequiredException : Exception {
    public AuthenticationRequiredException() : base("Authentication is required.") { }
}

public sealed class ForbiddenAccessException : Exception {
    public ForbiddenAccessException() : base("You do not have permission to perform this action.") { }
}
