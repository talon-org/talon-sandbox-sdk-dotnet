using TalonSandbox.Internal;
using TalonSandbox.Models;

namespace TalonSandbox;

/// <summary>
/// Environment variable access inside a sandbox. Accessed via <see cref="Sandbox.Env"/>.
/// </summary>
public sealed class Env
{
    private readonly string _sandboxId;
    private readonly SandboxClient _client;

    internal Env(string sandboxId, SandboxClient client)
    {
        _sandboxId = sandboxId;
        _client = client;
    }

    /// <summary>Gets the value of an environment variable, or null if not set.</summary>
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"/v1/sandboxes/{_sandboxId}/env/{Uri.EscapeDataString(key)}");
        var result = await _client.SendAsync(req, TalonJsonContext.Default.EnvGetResponse, cancellationToken)
            .ConfigureAwait(false);
        return result.Value;
    }

    /// <summary>Returns all environment variables as a dictionary.</summary>
    public async Task<Dictionary<string, string>> AllAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"/v1/sandboxes/{_sandboxId}/env");
        var result = await _client.SendAsync(req, TalonJsonContext.Default.EnvAllResponse, cancellationToken)
            .ConfigureAwait(false);
        return result.Env;
    }

    /// <summary>
    /// Sets a persistent environment variable for the sandbox.
    /// Only updates the stored value; running processes are not restarted and will
    /// not see the change until the next time they read the variable (e.g. on next
    /// process launch).
    /// </summary>
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var body = new EnvSetRequest { Value = value };
        var req = _client.JsonRequest(HttpMethod.Put,
            $"/v1/sandboxes/{_sandboxId}/env/{Uri.EscapeDataString(key)}",
            body, TalonJsonContext.Default.EnvSetRequest);
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a persistent environment variable from the sandbox.
    /// Running processes are not affected; the variable will be absent for processes
    /// started after this call.
    /// </summary>
    public async Task UnsetAsync(string key, CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete,
            $"/v1/sandboxes/{_sandboxId}/env/{Uri.EscapeDataString(key)}");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }
}
