using TalonSandbox.Internal;
using TalonSandbox.Models;

namespace TalonSandbox;

/// <summary>
/// Headless browser session management. Accessed via <see cref="Sandbox.Browser"/>.
/// </summary>
public sealed class Browser
{
    private readonly string _sandboxId;
    private readonly SandboxClient _client;

    internal Browser(string sandboxId, SandboxClient client)
    {
        _sandboxId = sandboxId;
        _client = client;
    }

    /// <summary>Launches a headless Chromium browser inside the sandbox.</summary>
    public async Task<BrowserSession> StartAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post,
            $"/v1/sandboxes/{_sandboxId}/browser");
        return await _client.SendAsync(req, TalonJsonContext.Default.BrowserSession, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Returns the current browser session, or throws NotFoundException if none is active.</summary>
    public async Task<BrowserSession> GetAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"/v1/sandboxes/{_sandboxId}/browser");
        return await _client.SendAsync(req, TalonJsonContext.Default.BrowserSession, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Stops the browser session.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete,
            $"/v1/sandboxes/{_sandboxId}/browser");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }
}
