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

    /// <summary>
    /// 列出当前租户下的所有 sandbox，可按 labels 过滤。
    ///
    /// <para>
    /// 当 <see cref="ListOptions.Labels"/> 非空时：
    /// <list type="bullet">
    ///   <item>把每个 (key, value) 拼成 <c>label=key:value</c> query 参数发给服务端（AND 语义）；</item>
    ///   <item>同时在客户端对返回结果再次过滤，兼容老服务端未实现该参数的场景。</item>
    /// </list>
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyList<SandboxInfo>> ListAsync(
        ListOptions? options = null,
        SandboxClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var ownedClient = client is null;
        client ??= new SandboxClient();
        try
        {
            // 拼服务端 label 过滤参数：每个 (key, value) 生成一个 label=key:value query 参数。
            // value 可能含等号，故用冒号分隔而非等号；重复同名参数表示 AND 语义。
            var url = BuildListUrl(options?.Labels);
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            var result = await client.SendAsync(req, TalonJsonContext.Default.SandboxListResponse, cancellationToken)
                .ConfigureAwait(false);

            var sandboxes = result.Sandboxes;

            // 客户端兜底过滤：老服务端若忽略 label 参数则在此补齐；新服务端已过滤时此处无额外开销。
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

    /// <summary>
    /// 构造 list 请求 URL，当 labels 非空时追加多个 <c>label=key:value</c> 参数。
    /// key 和 value 均经 <see cref="Uri.EscapeDataString"/> 编码以保证安全；
    /// 两者之间用冒号 <c>:</c> 分隔（value 可含等号，冒号不歧义）。
    /// </summary>
    private static string BuildListUrl(Dictionary<string, string>? labels)
    {
        // 无 label 过滤时直接返回基础路径，不附加任何 query string。
        if (labels is not { Count: > 0 })
            return "/v1/sandboxes";

        var sb = new StringBuilder("/v1/sandboxes?");
        var first = true;
        foreach (var kv in labels)
        {
            if (!first) sb.Append('&');
            // 格式：label=<encoded-key>:<encoded-value>
            sb.Append("label=");
            sb.Append(Uri.EscapeDataString(kv.Key));
            sb.Append(':');
            sb.Append(Uri.EscapeDataString(kv.Value));
            first = false;
        }
        return sb.ToString();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// 将 stopped 状态的 sandbox 重新启动，切换到 running 状态。
    /// 与 <see cref="ResumeAsync"/> 不同——start 是从 stopped 切 running，
    /// resume 是从 paused（SIGSTOP）切 running。幂等，返回 204。
    /// </summary>
    public async Task StartSandboxAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/sandboxes/{Id}/start");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 将 running 状态的 sandbox 停止（running→stopped）。
    /// 进程被终止但 sandbox 元数据保留，可通过 <see cref="StartSandboxAsync"/> 重启。
    /// 与 <see cref="PauseAsync"/>（SIGSTOP 冻结）语义不同。幂等，返回 204。
    /// </summary>
    public async Task StopSandboxAsync(CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/sandboxes/{Id}/stop");
        await _client.SendNoContentAsync(req, cancellationToken).ConfigureAwait(false);
    }

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
    /// <param name="env">额外注入的环境变量，格式 "KEY=value"。null 时不发送该字段。</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ProcessResult> RunAsync(
        string command,
        string? cwd = null,
        IEnumerable<string>? env = null,
        CancellationToken cancellationToken = default)
    {
        var body = new RunRequest
        {
            Command = ["/bin/sh", "-c", command],
            Cwd = cwd,
            Env = env?.ToList(),
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
    /// <param name="env">额外注入的环境变量，格式 "KEY=value"。null 时不发送该字段。</param>
    /// <param name="exposePorts">进程声明对外暴露的端口列表，用于预览反代准入与 DNAT。null 时不发送该字段。</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SandboxProcess> SpawnAsync(
        string command,
        string? cwd = null,
        IEnumerable<string>? env = null,
        IEnumerable<int>? exposePorts = null,
        CancellationToken cancellationToken = default)
    {
        var body = new RunRequest
        {
            Command = ["/bin/sh", "-c", command],
            Cwd = cwd,
            Env = env?.ToList(),
            ExposePorts = exposePorts?.ToList(),
        };
        var req = _client.JsonRequest(HttpMethod.Post, $"/v1/sandboxes/{Id}/processes",
            body, TalonJsonContext.Default.RunRequest);
        var info = await _client.SendAsync(req, TalonJsonContext.Default.ProcessInfo, cancellationToken)
            .ConfigureAwait(false);
        var proc = new SandboxProcess(Id, info, _client);
        proc.StartPolling();
        return proc;
    }

    // ── Agent ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 在 sandbox 内同步执行高层 agent 任务（Spec 38）。
    /// 调用 POST /v1/sandboxes/{id}/agent/run，阻塞直到任务完成，最长 5 分钟。
    /// </summary>
    /// <param name="goal">自然语言任务描述，如 "打开 https://example.com 并截图"。</param>
    /// <param name="options">可选参数：max_steps / llm_model。</param>
    /// <param name="cancellationToken">取消令牌（建议设 5 分钟以上超时）。</param>
    public async Task<AgentRunResult> AgentRunAsync(
        string goal,
        AgentRunOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var body = new AgentRunRequest
        {
            Goal = goal,
            MaxSteps = options?.MaxSteps ?? 0,
            LlmModel = options?.LlmModel,
        };
        var req = _client.JsonRequest(HttpMethod.Post, $"/v1/sandboxes/{Id}/agent/run",
            body, TalonJsonContext.Default.AgentRunRequest);
        return await _client.SendAsync(req, TalonJsonContext.Default.AgentRunResult, cancellationToken)
            .ConfigureAwait(false);
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
            // 非空时覆盖 worker 全局白名单；null 则不发送字段，后端回退全局配置
            NetworkAllowedHosts = opts.NetworkAllowedHosts is { Count: > 0 } h ? h : null,
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
