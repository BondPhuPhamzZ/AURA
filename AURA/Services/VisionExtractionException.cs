namespace AURA.Services;

public sealed class VisionExtractionException : Exception
{
    public VisionExtractionException(string code, string userMessage, Exception? innerException = null)
        : base(userMessage, innerException)
    {
        Code = code;
        UserMessage = userMessage;
    }

    public string Code { get; }
    public string UserMessage { get; }
}
