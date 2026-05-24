namespace TalonSandbox.Exceptions;

/// <summary>Thrown on 429 Too Many Requests.</summary>
public sealed class RateLimitException : SandboxException
{
    public RateLimitException(string message)
        : base(message, 429) { }
}
