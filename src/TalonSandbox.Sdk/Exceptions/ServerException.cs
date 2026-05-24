namespace TalonSandbox.Exceptions;

/// <summary>Thrown on 5xx server errors.</summary>
public sealed class ServerException : SandboxException
{
    public ServerException(string message, int statusCode = 500)
        : base(message, statusCode) { }
}
