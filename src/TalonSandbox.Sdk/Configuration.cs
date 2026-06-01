namespace TalonSandbox;

/// <summary>
/// SDK 全局配置，在应用启动时设置一次；未显式传入 <see cref="SandboxClient"/> 的调用均回落到这里。
///
/// <para>
/// <b>托管 SaaS 用户（推荐）：</b>只需配置 API key，无需指定 server——
/// 默认指向官方托管端点 <c>https://api.sandbox.talon.net.cn</c>。
/// </para>
/// <para>
/// <b>自部署用户：</b>通过 <c>TALON_SANDBOX_SERVER</c> 环境变量或
/// <see cref="Configure"/> 的 <c>server</c> 参数覆盖端点。
/// </para>
/// </summary>
public static class Configuration
{
    private static string? _server;
    private static string? _apiKey;

    /// <summary>
    /// 配置所有 SDK 调用使用的默认 server 和 API key。
    /// </summary>
    /// <param name="server">
    /// Talon Sandbox API 的 Base URL。
    /// 托管 SaaS 用户通常无需传此参数，留 null 即使用官方端点。
    /// </param>
    /// <param name="apiKey">API key（Bearer token，<c>ask_…</c> 前缀）。</param>
    public static void Configure(string? server = null, string? apiKey = null)
    {
        _server = server;
        _apiKey = apiKey;
    }

    /// <summary>
    /// 服务端点解析优先级：
    /// <list type="number">
    ///   <item><description>显式调用 <see cref="Configure"/> 传入的 server</description></item>
    ///   <item><description>环境变量 <c>TALON_SANDBOX_SERVER</c>（自部署时设置）</description></item>
    ///   <item><description>官方托管端点 <c>https://api.sandbox.talon.net.cn</c></description></item>
    /// </list>
    /// </summary>
    internal static string ResolveServer() =>
        _server
        ?? Environment.GetEnvironmentVariable("TALON_SANDBOX_SERVER")
        ?? "https://api.sandbox.talon.net.cn";

    internal static string? ResolveApiKey() =>
        _apiKey
        ?? Environment.GetEnvironmentVariable("TALON_SANDBOX_API_KEY");
}
