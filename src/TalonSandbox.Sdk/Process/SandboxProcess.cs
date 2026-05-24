using TalonSandbox.Internal;
using TalonSandbox.Models;

namespace TalonSandbox.Process;

/// <summary>Event args for stdout chunks from a spawned process.</summary>
public sealed class LineReceivedEventArgs : EventArgs
{
    /// <summary>A chunk of output from the process (may span multiple lines).</summary>
    public string Line { get; }
    internal LineReceivedEventArgs(string line) => Line = line;
}

/// <summary>
/// Handle for a long-running process started by <see cref="Sandbox.SpawnAsync"/>.
/// Subscribe to <see cref="StdoutReceived"/> before calling <see cref="WaitAsync"/>.
/// </summary>
public sealed class SandboxProcess
{
    private readonly string _sandboxId;
    private readonly string _processId;
    private readonly SandboxClient _client;
    private Task? _pollTask;
    private readonly CancellationTokenSource _cts = new();
    private int _exited; // 0 / 1, atomic via Interlocked
    private int _exitCode;
    /// <summary>
    /// How many bytes of the log we've already surfaced as
    /// <see cref="StdoutReceived"/>. We track bytes (not lines) because the
    /// server's <c>?tail=N</c> window can rotate as new output arrives — a
    /// line-count approach silently loses output whenever the window shifts.
    /// </summary>
    private int _logByteOffset;

    /// <summary>Platform process record ID (e.g. "proc_abc123").</summary>
    public string Id => _processId;

    /// <summary>Fires for each chunk of output (polled from the log endpoint).</summary>
    public event EventHandler<LineReceivedEventArgs>? StdoutReceived;

    /// <summary>Fires when the process exits. The event arg is the exit code (-1 if unknown).</summary>
    public event EventHandler<int>? Exited;

    /// <summary>Fires when the background poll loop hits an unexpected error.</summary>
    public event EventHandler<Exception>? ErrorReceived;

    internal SandboxProcess(string sandboxId, ProcessInfo info, SandboxClient client)
    {
        _sandboxId = sandboxId;
        _processId = info.Id;
        _client = client;
    }

    internal void StartPolling()
    {
        _pollTask = Task.Run(PollLoopAsync);
    }

    private async Task PollLoopAsync()
    {
        // Tail window. Larger than the default 64KiB so a chatty process
        // doesn't lose bytes between ticks; the server caps at 32 MiB.
        const int tailBytes = 65536;

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await DrainLogsAsync(tailBytes, _cts.Token).ConfigureAwait(false);

                var listReq = new HttpRequestMessage(HttpMethod.Get,
                    $"/v1/sandboxes/{_sandboxId}/processes");
                var list = await _client.SendAsync(
                    listReq, TalonJsonContext.Default.ProcessListResponse, _cts.Token)
                    .ConfigureAwait(false);
                var proc = list.Processes.FirstOrDefault(p => p.Id == _processId);
                if (proc is null)
                {
                    // Disappeared from list — assume cleaned up.
                    await DrainLogsAsync(tailBytes, _cts.Token).ConfigureAwait(false);
                    MarkExited(-1);
                    return;
                }
                if (proc.Status is "exited" or "killed" or "stopped" or "failed")
                {
                    // Final drain so the user gets the last bytes the process
                    // emitted between the previous tick and the exit.
                    await DrainLogsAsync(tailBytes, _cts.Token).ConfigureAwait(false);
                    MarkExited(proc.ExitCode);
                    return;
                }

                await Task.Delay(500, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                // Surface the error so callers can see what's wrong rather
                // than silently burning connections on a bad auth or 500.
                try { ErrorReceived?.Invoke(this, ex); } catch { /* user handler */ }
                try
                {
                    await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task DrainLogsAsync(int tailBytes, CancellationToken ct)
    {
        var logReq = new HttpRequestMessage(HttpMethod.Get,
            $"/v1/sandboxes/{_sandboxId}/processes/{_processId}/logs?tail={tailBytes}");
        using var resp = await _client.SendRawAsync(logReq, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (text.Length > _logByteOffset)
        {
            var chunk = text.Substring(_logByteOffset);
            _logByteOffset = text.Length;
            if (chunk.Length > 0)
            {
                try { StdoutReceived?.Invoke(this, new LineReceivedEventArgs(chunk)); }
                catch { /* user handler — don't kill the poll loop */ }
            }
        }
        else if (text.Length < _logByteOffset)
        {
            // Server tail window rotated past our cursor. Reset to the
            // current end to avoid double-emitting old bytes.
            _logByteOffset = text.Length;
        }
    }

    private void MarkExited(int code)
    {
        if (Interlocked.Exchange(ref _exited, 1) != 0) return;
        _exitCode = code;
        try { Exited?.Invoke(this, code); } catch { /* user handler */ }
    }

    /// <summary>Waits until the process exits or cancellation is requested.</summary>
    public async Task<int> WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_pollTask is null) return _exitCode;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        try { await _pollTask.WaitAsync(linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        return _exitCode;
    }

    /// <summary>Kills the process. Idempotent.</summary>
    public async Task KillAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete,
            $"/v1/sandboxes/{_sandboxId}/processes/{_processId}");
        try
        {
            await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
        }
        catch (Exceptions.NotFoundException) { /* already gone */ }

        // Fire Exited even though the poll loop might race to do it too —
        // MarkExited is idempotent. We do this *before* cancelling so a
        // user awaiting WaitAsync() gets the event.
        MarkExited(-1);
        await _cts.CancelAsync().ConfigureAwait(false);
    }
}
