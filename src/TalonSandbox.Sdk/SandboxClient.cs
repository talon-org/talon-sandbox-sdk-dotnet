using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TalonSandbox.Exceptions;
using TalonSandbox.Internal;

namespace TalonSandbox;

// ── Options ───────────────────────────────────────────────────────────────────

/// <summary>Configuration options for <see cref="SandboxClient"/> when used with DI.</summary>
public sealed class SandboxClientOptions
{
    /// <summary>Base URL of the Talon Sandbox API server.</summary>
    public string Server { get; set; } = Configuration.ResolveServer();

    /// <summary>API key (Bearer token with <c>ask_…</c> prefix).</summary>
    public string? ApiKey { get; set; } = Configuration.ResolveApiKey();

    /// <summary>Default timeout for HTTP requests.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

// ── DI extensions ─────────────────────────────────────────────────────────────

/// <summary>Extension methods for registering the Talon Sandbox SDK with the DI container.</summary>
public static class TalonSandboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SandboxClient"/> with the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to configure <see cref="SandboxClientOptions"/>.</param>
    public static IServiceCollection AddTalonSandbox(
        this IServiceCollection services,
        Action<SandboxClientOptions>? configure = null)
    {
        var opts = new SandboxClientOptions();
        configure?.Invoke(opts);
        services.AddSingleton(Options.Create(opts));

        services.AddHttpClient(SandboxClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri(opts.Server.TrimEnd('/') + "/");
            client.Timeout = opts.Timeout;
        });

        services.AddTransient<SandboxClient>();
        return services;
    }
}

// ── SandboxClient ─────────────────────────────────────────────────────────────

/// <summary>
/// Low-level HTTP client for the Talon Sandbox API. Handles authentication,
/// error mapping, and JSON (de)serialization.
///
/// <para>For typical usage prefer the high-level <see cref="Sandbox"/> facade.</para>
///
/// <para>
/// <b>Standalone usage:</b>
/// <code>
/// using var client = new SandboxClient("https://api.example.com", apiKey: "ask_…");
/// </code>
/// </para>
///
/// <para>
/// <b>DI usage:</b>
/// <code>
/// services.AddTalonSandbox(o => o.Server = "https://api.example.com");
/// </code>
/// </para>
/// </summary>
public sealed class SandboxClient : IDisposable, IAsyncDisposable
{
    internal const string HttpClientName = "TalonSandbox";

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    // ── Constructors ──────────────────────────────────────────────────────

    /// <summary>
    /// Standalone constructor — reads env vars / global config when arguments are null.
    /// </summary>
    public SandboxClient(string? server = null, string? apiKey = null, TimeSpan? timeout = null)
    {
        _baseUrl = (server ?? Configuration.ResolveServer()).TrimEnd('/');
        _apiKey = apiKey ?? Configuration.ResolveApiKey();

        _http = new HttpClient(new HttpClientHandler { UseCookies = false })
        {
            BaseAddress = new Uri(_baseUrl + "/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };
        _ownsHttpClient = true;
    }

    /// <summary>DI constructor: injected via <see cref="TalonSandboxServiceCollectionExtensions.AddTalonSandbox"/>.</summary>
    public SandboxClient(IHttpClientFactory factory, IOptions<SandboxClientOptions> options)
    {
        var opts = options.Value;
        _baseUrl = opts.Server.TrimEnd('/');
        _apiKey = opts.ApiKey;
        _http = factory.CreateClient(HttpClientName);
        _ownsHttpClient = false;
    }

    // ── Internal properties ───────────────────────────────────────────────

    internal string BaseUrl => _baseUrl;

    internal string? AuthorizationHeader =>
        _apiKey is { } k ? $"Bearer {k}" : null;

    // ── Auth ──────────────────────────────────────────────────────────────

    private void ApplyAuth(HttpRequestMessage req)
    {
        if (AuthorizationHeader is { } v)
            req.Headers.Authorization = AuthenticationHeaderValue.Parse(v);
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────

    internal async Task<T> SendAsync<T>(
        HttpRequestMessage req,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        CancellationToken ct)
    {
        ApplyAuth(req);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        HttpHelper.ThrowIfError((int)resp.StatusCode, body);
        return JsonSerializer.Deserialize(body, typeInfo)
               ?? throw new SandboxException("Server returned empty response");
    }

    internal async Task SendNoContentAsync(HttpRequestMessage req, CancellationToken ct)
    {
        ApplyAuth(req);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (resp.StatusCode is not (HttpStatusCode.NoContent or HttpStatusCode.OK))
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            HttpHelper.ThrowIfError((int)resp.StatusCode, body);
        }
    }

    internal async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage req, CancellationToken ct)
    {
        ApplyAuth(req);
        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            // Capture status + body BEFORE Dispose. Reading StatusCode after
            // Dispose works today by accident (value-type prop on the message)
            // but any future proxy handler that touches the disposed Content
            // internally throws ObjectDisposedException, masking the real
            // server error from the user.
            var status = (int)resp.StatusCode;
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            resp.Dispose();
            HttpHelper.ThrowIfError(status, body);
        }
        return resp;
    }

    internal HttpRequestMessage JsonRequest<T>(
        HttpMethod method, string path, T payload,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(payload, typeInfo);
        return new HttpRequestMessage(method, path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    // ── Images ────────────────────────────────────────────────────────────

    /// <summary>
    /// 查询平台可用的 baseimage 列表（GET /v1/images）。
    /// 任何已认证成员均可调用，结果可用于创建 sandbox 时填写 Image 选项。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<Models.ImageInfo>> ListImagesAsync(
        CancellationToken cancellationToken = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/v1/images");
        var result = await SendAsync(req, Internal.TalonJsonContext.Default.ImageListResponse, cancellationToken)
            .ConfigureAwait(false);
        return result.Images;
    }

    // ── Dispose ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void Dispose() { if (_ownsHttpClient) _http.Dispose(); }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}
