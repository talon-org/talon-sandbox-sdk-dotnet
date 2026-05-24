using System.Text;
using System.Text.Json;
using TalonSandbox.Exceptions;
using TalonSandbox.Internal;
using TalonSandbox.Models;
using TalonSandbox.Process;

namespace TalonSandbox;

/// <summary>
/// Top-level entry point for the Talon Sandbox SDK.
///
/// <code>
/// await using var sb = await Sandbox.CreateAsync(new() {
///     Image = "node:20-bookworm",
///     Resources = new() { Cpu = 2, Memory = "4GiB" },
///     Network = "allowlist",
///     Timeout = "30m",
///     Ttl = "6h",
/// });
/// var result = await sb.RunAsync("npm install");
/// Console.WriteLine(result.Stdout);
/// </code>
/// </summary>
public sealed class Sandbox : IAsyncDisposable
{
    private readonly SandboxClient _client;
    private SandboxInfo _info;

    // ── Sub-resources ─────────────────────────────────────────────────────

    /// <summary>Filesystem operations inside this sandbox.</summary>
    public Fs Fs { get; }

    /// <summary>Environment variable management.</summary>
    public Env Env { get; }

    /// <summary>Headless browser session.</summary>
    public Browser Browser { get; }

    /// <summary>Interactive PTY terminal factory.</summary>
    public Terminal.Terminal Terminal { get; }

    // ── Constructor ───────────────────────────────────────────────────────

    private Sandbox(SandboxInfo info, SandboxClient client)
    {
        _info = info;
        _client = client;
        Fs = new Fs(info.Id, client);
        Env = new Env(info.Id, client);
        Browser = new Browser(info.Id, client);
        Terminal = new Terminal.Terminal(info.Id, client.BaseUrl, client.AuthorizationHeader);
    }

    // ── Properties ────────────────────────────────────────────────────────

    /// <summary>Sandbox ID (e.g. "sb_abc123").</summary>
    public string Id => _info.Id;

    /// <summary>Current state snapshot: "running" | "paused" | "stopped" | "killed".</summary>
    public string State => _info.State;

    /// <summary>Full sandbox metadata (last-known snapshot).</summary>
    public SandboxInfo Info => _info;

    // ── Static factories ──────────────────────────────────────────────────

    /// <summary>
    /// Creates a new sandbox and waits until it is running.
    /// </summary>
    /// <param name="options">Creation options.</param>
    /// <param name="client">Optional explicit client; if null uses env vars / global config.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task<Sandbox> CreateAsync(
        CreateOptions? options = null,
        SandboxClient? client = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CreateOptions();
        var ownedClient = client is null;
        client ??= new SandboxClient();

        try
        {
            var body = BuildCreateRequest(options);
            var json = JsonSerializer.Serialize(body, TalonJsonContext.Default.CreateSandboxRequest);
            var req = new HttpRequestMessage(HttpMethod.Post, "/v1/sandboxes?wait=running")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            var info = await client.SendAsync(req, TalonJsonContext.Default.SandboxInfo, cancellationToken)
                .ConfigureAwait(false);

            return new Sandbox(info, client);
        }
        catch
        {
            if (ownedClient) client.Dispose();
            throw;
        }
    }

    /// <summary>Attaches to an existing sandbox by ID.</summary>
    public static async Task<Sandbox> GetAsync(
        string sandboxId,
        SandboxClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var ownedClient = client is null;
        client ??= new SandboxClient();
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"/v1/sandboxes/{sandboxId}");
            var info = await client.SendAsync(req, TalonJsonContext.Default.SandboxInfo, cancellationToken)
                .ConfigureAwait(false);
            return new Sandbox(info, client);
        }
        catch
        {
            if (ownedClient) client.Dispose();
            throw;
        }
    }

    /// <summary>Lists sandboxes, optionally filtered by labels (client-side).</summary>
    public static async Task<IReadOnlyList<SandboxInfo>> ListAsync(
        ListOptions? options = null,
        SandboxClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var ownedClient = client is null;
        client ??= new SandboxClient();
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "/v1/sandboxes");
            var result = await client.SendAsync(req, TalonJsonContext.Default.SandboxListResponse, cancellationToken)
                .ConfigureAwait(false);

            var sandboxes = result.Sandboxes;
            if (options?.Labels is { Count: > 0 } labels)
            {
                sandboxes = sandboxes
                    .Where(s => s.Labels is not null &&
                                labels.All(kv => s.Labels.TryGetValue(kv.Key, out var v) && v == kv.Value))
                    .ToList();
            }
            return sandboxes;
        }
        finally
        {
            if (ownedClient) client.Dispose();
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>Freezes all processes inside the sandbox (SIGSTOP). Idempotent.</summary>
    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/sandboxes/{Id}/pause");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Resumes a paused sandbox. Idempotent.</summary>
    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/sandboxes/{Id}/resume");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Destroys the sandbox. Irreversible.</summary>
    public async Task KillAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/v1/sandboxes/{Id}");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }

    // ── Commands ──────────────────────────────────────────────────────────

    /// <summary>
    /// Runs a shell command synchronously and returns the result.
    /// Internally calls <c>POST /v1/sandboxes/{id}/exec</c>.
    /// </summary>
    /// <param name="command">Shell command string (wrapped in /bin/sh -c).</param>
    /// <param name="cwd">Working directory inside the sandbox.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ProcessResult> RunAsync(
        string command,
        string? cwd = null,
        CancellationToken cancellationToken = default)
    {
        var body = new RunRequest
        {
            Command = ["/bin/sh", "-c", command],
            Cwd = cwd,
        };
        var req = _client.JsonRequest(HttpMethod.Post, $"/v1/sandboxes/{Id}/exec",
            body, TalonJsonContext.Default.RunRequest);
        var result = await _client.SendAsync(req, TalonJsonContext.Default.ExecResult, cancellationToken)
            .ConfigureAwait(false);
        return new ProcessResult(result.Stdout, result.Stderr, result.ExitCode, result.DurationMs);
    }

    /// <summary>
    /// Starts a long-running process and returns a handle for streaming output.
    /// Internally calls <c>POST /v1/sandboxes/{id}/processes</c>.
    /// </summary>
    /// <param name="command">Shell command string (wrapped in /bin/sh -c).</param>
    /// <param name="cwd">Working directory inside the sandbox.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SandboxProcess> SpawnAsync(
        string command,
        string? cwd = null,
        CancellationToken cancellationToken = default)
    {
        var body = new RunRequest { Command = ["/bin/sh", "-c", command], Cwd = cwd };
        var req = _client.JsonRequest(HttpMethod.Post, $"/v1/sandboxes/{Id}/processes",
            body, TalonJsonContext.Default.RunRequest);
        var info = await _client.SendAsync(req, TalonJsonContext.Default.ProcessInfo, cancellationToken)
            .ConfigureAwait(false);
        var proc = new SandboxProcess(Id, info, _client);
        proc.StartPolling();
        return proc;
    }

    // ── Port exposure ─────────────────────────────────────────────────────

    /// <summary>
    /// Exposes a port from the sandbox and returns the preview URL.
    /// Requires server v1.1+ (Spec 50). On 404 throws <see cref="NotImplementedException"/>.
    /// </summary>
    /// <param name="port">Container port to expose (1-65535).</param>
    /// <param name="options">Signing / TTL / subdomain options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string> ExposeAsync(
        int port,
        ExposeOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var body = new ExposeRequest
        {
            Port = port,
            Sign = options?.Sign ?? false,
            Ttl = options?.Ttl,
            Subdomain = options?.Subdomain,
        };
        var req = _client.JsonRequest(HttpMethod.Post, $"/v1/sandboxes/{Id}/expose",
            body, TalonJsonContext.Default.ExposeRequest);

        try
        {
            var result = await _client.SendAsync(req, TalonJsonContext.Default.ExposedPort, cancellationToken)
                .ConfigureAwait(false);
            return result.Url;
        }
        catch (NotFoundException ex) when (IsEndpointMissing(ex))
        {
            // Server doesn't have /expose endpoint yet (pre-v1.1). Surface
            // as NotImplementedException so callers can degrade gracefully.
            // A 404 that names "sandbox" in the body falls through as a real
            // NotFoundException — that's a user-supplied bad sandbox id.
            throw new NotImplementedException(
                "ExposeAsync requires server v1.1+ (Spec 50). Upgrade your Talon Sandbox server.");
        }
    }

    /// <summary>
    /// Heuristic: a 404 whose body doesn't mention "sandbox" / "port" is
    /// almost certainly chi's default "404 page not found" for a missing
    /// route. When the body identifies the resource, the sandbox or port
    /// genuinely doesn't exist and we let the original NotFoundException
    /// propagate.
    /// </summary>
    private static bool IsEndpointMissing(NotFoundException ex)
    {
        var msg = ex.Message ?? string.Empty;
        if (msg.Contains("sandbox", StringComparison.OrdinalIgnoreCase)) return false;
        if (msg.Contains("port", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>Removes a port exposure.</summary>
    public async Task UnexposeAsync(int port, CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, $"/v1/sandboxes/{Id}/expose/{port}");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns all currently exposed ports (explicit + dynamic).</summary>
    public async Task<IReadOnlyList<ExposedPort>> ExposedAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"/v1/sandboxes/{Id}/expose");
        var result = await _client.SendAsync(req, TalonJsonContext.Default.ExposedPortListResponse, cancellationToken)
            .ConfigureAwait(false);
        return result.Ports;
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────

    /// <summary>Kills the sandbox on disposal. Use <c>await using var sb = …</c>.</summary>
    public async ValueTask DisposeAsync()
    {
        try { await KillAsync().ConfigureAwait(false); }
        catch { /* best-effort */ }
        await _client.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override string ToString() => $"Sandbox({Id}, {State})";

    // ── Private helpers ───────────────────────────────────────────────────

    private static CreateSandboxRequest BuildCreateRequest(CreateOptions opts)
    {
        var req = new CreateSandboxRequest
        {
            ImageId = opts.Image,
            NetworkPolicy = NormalizeNetwork(opts.Network),
            Env = opts.Env,
            Labels = opts.Labels,
        };

        if (opts.Timeout is not null)
            req.IdleTimeoutSeconds = DurationParser.ParseSeconds(opts.Timeout);

        if (opts.Ttl is not null)
            req.TtlSeconds = DurationParser.ParseSeconds(opts.Ttl);

        if (opts.Resources is { } r)
        {
            if (r.Cpu is { } cpu)
                req.CpuMillis = (long)(cpu * 1000);

            if (r.Memory is not null)
                req.MemoryBytes = SizeParser.Parse(r.Memory);

            if (r.Disk is not null)
                req.DiskBytes = SizeParser.Parse(r.Disk);
        }

        return req;
    }

    private static string? NormalizeNetwork(string? network) => network?.ToLowerInvariant() switch
    {
        "allowlist" or "restricted-egress" => "restricted-egress",
        "open" or "full-egress" => "full-egress",
        "sealed" or "offline" => "offline",
        null => null,
        var other => other,
    };
}
