namespace TalonSandbox.Exceptions;

/// <summary>Thrown when the tenant has exceeded their sandbox quota (422).</summary>
public sealed class QuotaException : SandboxException
{
    public QuotaException(string message)
        : base(message, 422) { }
}
