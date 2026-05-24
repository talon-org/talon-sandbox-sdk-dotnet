namespace TalonSandbox;

/// <summary>
/// Global SDK configuration. Set once at startup; individual <see cref="SandboxClient"/>
/// instances fall back to these values when not explicitly configured.
/// </summary>
public static class Configuration
{
    private static string? _server;
    private static string? _apiKey;

    /// <summary>
    /// Configures the default server URL and API key used by all SDK calls
    /// that do not provide an explicit <see cref="SandboxClient"/>.
    /// </summary>
    /// <param name="server">Base URL of the Talon Sandbox API.</param>
    /// <param name="apiKey">API key (Bearer token with <c>ask_…</c> prefix).</param>
    public static void Configure(string server, string? apiKey = null)
    {
        _server = server;
        _apiKey = apiKey;
    }

    internal static string ResolveServer() =>
        _server
        ?? Environment.GetEnvironmentVariable("TALON_SANDBOX_SERVER")
        ?? "http://localhost:18080";

    internal static string? ResolveApiKey() =>
        _apiKey
        ?? Environment.GetEnvironmentVariable("TALON_SANDBOX_API_KEY");
}
