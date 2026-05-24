namespace TalonSandbox.Process;

/// <summary>Result of a completed <see cref="Sandbox.RunAsync"/> call.</summary>
public sealed record ProcessResult(
    string Stdout,
    string Stderr,
    int ExitCode,
    double DurationMs);
