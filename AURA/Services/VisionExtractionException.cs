namespace AURA.Services;

public sealed class VisionExtractionException : Exception
{
    public VisionExtractionException(string code, string userMessage, Exception? innerException = null,
        TimeSpan? retryAfter = null, string? provider = null, string? requestId = null)
        : base(userMessage, innerException)
    {
        Code = code;
        UserMessage = userMessage;
        RetryAfter = retryAfter;
        Provider = provider;
        RequestId = requestId;
    }

    public string Code { get; }
    public string UserMessage { get; }
    public TimeSpan? RetryAfter { get; }
    public string? Provider { get; }
    public string? RequestId { get; }
}
