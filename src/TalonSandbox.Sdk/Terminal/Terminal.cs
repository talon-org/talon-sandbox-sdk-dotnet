using System.Net.WebSockets;

namespace TalonSandbox.Terminal;

/// <summary>
/// PTY terminal factory. Accessed via <see cref="Sandbox.Terminal"/>.
/// </summary>
public sealed class Terminal
{
    private readonly string _sandboxId;
    private readonly string _baseUrl;
    private readonly string? _authHeader;

    internal Terminal(string sandboxId, string baseUrl, string? authHeader)
    {
        _sandboxId = sandboxId;
        _baseUrl = baseUrl;
        _authHeader = authHeader;
    }

    /// <summary>Opens a new interactive PTY session over WebSocket.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected <see cref="PtySession"/> with the receive loop already started.</returns>
    public async Task<PtySession> OpenAsync(CancellationToken cancellationToken = default)
    {
        var wsUrl = _baseUrl
            .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');

        wsUrl += $"/v1/sandboxes/{_sandboxId}/pty";

        var ws = new ClientWebSocket();
        if (_authHeader is not null)
            ws.Options.SetRequestHeader("Authorization", _authHeader);

        await ws.ConnectAsync(new Uri(wsUrl), cancellationToken).ConfigureAwait(false);

        var session = new PtySession(ws);
        session.StartReceiving();
        return session;
    }
}
