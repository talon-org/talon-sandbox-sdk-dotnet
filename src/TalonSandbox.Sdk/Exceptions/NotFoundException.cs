namespace TalonSandbox.Exceptions;

/// <summary>Thrown when the sandbox, process, or file is not found (404).</summary>
public sealed class NotFoundException : SandboxException
{
    public NotFoundException(string message)
        : base(message, 404) { }
}
