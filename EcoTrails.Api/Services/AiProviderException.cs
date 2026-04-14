namespace EcoTrails.Api.Services;

public sealed class AiProviderException : Exception
{
    public AiProviderException(int statusCode, string clientMessage, string? logMessage = null, Exception? innerException = null)
        : base(logMessage ?? clientMessage, innerException)
    {
        StatusCode = statusCode;
        ClientMessage = clientMessage;
    }

    public int StatusCode { get; }

    public string ClientMessage { get; }
}
