using System.Net.WebSockets;
using TalonSandbox.Exceptions;

namespace TalonSandbox.Terminal;

/// <summary>Event args carrying raw PTY output bytes.</summary>
public sealed class DataReceivedEventArgs : EventArgs
{
    /// <summary>Raw bytes from the PTY stdout/stderr.</summary>
    public ReadOnlyMemory<byte> Data { get; }
    internal DataReceivedEventArgs(ReadOnlyMemory<byte> data) => Data = data;
}

/// <summary>Event args carrying a PTY receive-loop error.</summary>
public sealed class PtyErrorEventArgs : EventArgs
{
    /// <summary>The exception that caused the error.</summary>
    public Exception Exception { get; }
    internal PtyErrorEventArgs(Exception ex) => Exception = ex;
}

/// <summary>
/// An open PTY session over WebSocket. Use <c>await using</c> to ensure cleanup.
///
/// <code>
/// await using var pty = await sb.Terminal.OpenAsync();
/// pty.DataReceived += (_, e) => Console.Write(Encoding.UTF8.GetString(e.Data.Span));
/// await pty.WriteAsync("ls\n");
/// await pty.ResizeAsync(40, 120);
/// </code>
/// </summary>
public sealed class PtySession : IAsyncDisposable
{
    private readonly ClientWebSocket _ws;
    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveTask;
    private volatile bool _closed;

    /// <summary>Fires on each chunk of PTY output. Handlers run on the threadpool receive task.</summary>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>Fires when the PTY closes normally or by server close frame.</summary>
    public event EventHandler? Closed;

    /// <summary>Fires when an unexpected error terminates the receive loop.</summary>
    public event EventHandler<PtyErrorEventArgs>? ErrorReceived;

    internal PtySession(ClientWebSocket ws) => _ws = ws;

    internal void StartReceiving()
    {
        _receiveTask = Task.Run(ReceiveLoopAsync);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[16 * 1024];
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var result = await _ws.ReceiveAsync(buffer, _cts.Token).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _closed = true;
                    Closed?.Invoke(this, EventArgs.Empty);
                    return;
                }

                var total = result.Count;
                while (!result.EndOfMessage)
                {
                    if (total >= buffer.Length)
                        Array.Resize(ref buffer, buffer.Length * 2);
                    result = await _ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer, total, buffer.Length - total),
                        _cts.Token).ConfigureAwait(false);
                    total += result.Count;
                }

                var data = new byte[total];
                Buffer.BlockCopy(buffer, 0, data, 0, total);
                DataReceived?.Invoke(this, new DataReceivedEventArgs(data));
            }
        }
        catch (OperationCanceledException)
        {
            _closed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
        catch (WebSocketException ex) when (
            ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely
            || _ws.State is WebSocketState.Aborted or WebSocketState.Closed)
        {
            _closed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _closed = true;
            ErrorReceived?.Invoke(this, new PtyErrorEventArgs(ex));
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Writes raw bytes to the PTY stdin.</summary>
    /// <exception cref="SandboxException">Thrown if the session is already closed.</exception>
    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        if (_closed) throw new SandboxException("PTY session is closed.");
        await _ws.SendAsync(data, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes a UTF-8 string to the PTY stdin.</summary>
    public Task WriteAsync(string text, CancellationToken cancellationToken = default)
        => WriteAsync(System.Text.Encoding.UTF8.GetBytes(text), cancellationToken);

    /// <summary>
    /// Sends a terminal resize notification.
    /// Wire format: 0x01 opcode + uint16 cols LE + uint16 rows LE.
    /// </summary>
    public async Task ResizeAsync(int rows, int cols, CancellationToken cancellationToken = default)
    {
        var buf = new byte[5];
        buf[0] = 0x01; // resize opcode
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(1), (ushort)cols);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(3), (ushort)rows);
        await WriteAsync(buf, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Closes the WebSocket gracefully. Idempotent.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_closed) return;
        _closed = true;
        await _cts.CancelAsync().ConfigureAwait(false);
        try
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", cancellationToken)
                    .ConfigureAwait(false);
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }

        if (_receiveTask is not null)
        {
            try { await _receiveTask.ConfigureAwait(false); }
            catch { }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _cts.Dispose();
        _ws.Dispose();
    }
}
