namespace TalonSandbox.Exceptions;

/// <summary>Base exception for all Talon Sandbox SDK errors.</summary>
public class SandboxException : Exception
{
    /// <summary>HTTP status code if the error originated from an API response, otherwise 0.</summary>
    public int StatusCode { get; }

    /// <summary>Server-side request ID for correlation with audit logs, if available.</summary>
    public string? RequestId { get; init; }

    public SandboxException(string message, int statusCode = 0)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public SandboxException(string message, Exception inner, int statusCode = 0)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
