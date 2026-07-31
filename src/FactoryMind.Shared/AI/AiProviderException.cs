namespace FactoryMind.Shared.AI;

public sealed class AiProviderException : Exception {
    public AiProviderException(string message, Exception? innerException = null)
        : base(message, innerException) {
    }
}
