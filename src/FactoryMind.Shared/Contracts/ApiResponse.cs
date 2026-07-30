namespace FactoryMind.Shared.Contracts;

public sealed record ApiResponse<T>(bool Success, string Message, T? Data)
{
    public static ApiResponse<T> Ok(T data) => new(true, "OK", data);
    public static ApiResponse<T> Failure(string message) => new(false, message, default);
}
