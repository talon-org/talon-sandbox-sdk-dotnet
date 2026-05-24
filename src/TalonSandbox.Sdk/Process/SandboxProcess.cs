using TalonSandbox.Internal;
using TalonSandbox.Models;

namespace TalonSandbox.Process;

/// <summary>Event args for stdout lines from a spawned process.</summary>
public sealed class LineReceivedEventArgs : EventArgs
{
    /// <summary>A line of output from the process.</summary>
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

    /// <summary>Platform process record ID (e.g. "proc_abc123").</summary>
    public string Id => _processId;

    /// <summary>Fires for each line of output (polled from the log endpoint).</summary>
    public event EventHandler<LineReceivedEventArgs>? StdoutReceived;

    /// <summary>Fires when the process exits. The event arg is the exit code (0 if unknown).</summary>
    public event EventHandler<int>? Exited;

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
        var seen = 0;
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var logReq = new HttpRequestMessage(HttpMethod.Get,
                    $"/v1/sandboxes/{_sandboxId}/processes/{_processId}/logs?tail=65536");
                using var resp = await _client.SendRawAsync(logReq, _cts.Token).ConfigureAwait(false);
                var text = await resp.Content.ReadAsStringAsync(_cts.Token).ConfigureAwait(false);
                var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(seen))
                    StdoutReceived?.Invoke(this, new LineReceivedEventArgs(line));
                seen = lines.Length;

                // Check if process is done
                var listReq = new HttpRequestMessage(HttpMethod.Get,
                    $"/v1/sandboxes/{_sandboxId}/processes");
                var list = await _client.SendAsync(
                    listReq, TalonJsonContext.Default.ProcessListResponse, _cts.Token)
                    .ConfigureAwait(false);
                var proc = list.Processes.FirstOrDefault(p => p.Id == _processId);
                if (proc is null || proc.Status is "exited" or "killed" or "stopped")
                {
                    Exited?.Invoke(this, 0);
                    return;
                }

                await Task.Delay(500, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch { await Task.Delay(1000).ConfigureAwait(false); }
        }
    }

    /// <summary>Waits until the process exits or cancellation is requested.</summary>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (_pollTask is null) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        try { await _pollTask.WaitAsync(linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    /// <summary>Kills the process.</summary>
    public async Task KillAsync(CancellationToken cancellationToken = default)
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        var req = new HttpRequestMessage(HttpMethod.Delete,
            $"/v1/sandboxes/{_sandboxId}/processes/{_processId}");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }
}
