namespace TalonSandbox.Exceptions;

/// <summary>Thrown on 401 (unauthenticated) or 403 (forbidden) responses.</summary>
public sealed class AuthException : SandboxException
{
    public AuthException(string message, int statusCode = 401)
        : base(message, statusCode) { }
}
