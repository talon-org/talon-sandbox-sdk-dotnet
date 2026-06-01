using System.Text.Json.Serialization;

namespace TalonSandbox.Models;

/// <summary>
/// 沙箱内活跃的无头浏览器会话。对应后端 dto.BrowserDTO（Spec 34）。
/// 客户端通常只需 <see cref="CdpUrl"/> 即可连接 CDP；其余字段供排障使用。
/// </summary>
public sealed class BrowserSession
{
    /// <summary>Chrome DevTools Protocol WebSocket URL（经 sandbox-api /preview 反代，含鉴权）。</summary>
    [JsonPropertyName("cdp_ws_url")]
    public string CdpUrl { get; set; } = string.Empty;

    /// <summary>该浏览器进程在 processes 表的 ID（如 "proc_abc123"）。</summary>
    [JsonPropertyName("process_id")]
    public string? ProcessId { get; set; }

    /// <summary>容器内 CDP 监听端口，固定 9222。</summary>
    [JsonPropertyName("cdp_port")]
    public int CdpPort { get; set; }

    /// <summary>CDP WebSocket 路径，如 "/devtools/browser/abc-def"。</summary>
    [JsonPropertyName("cdp_path")]
    public string? CdpPath { get; set; }

    /// <summary>
    /// 宿主机侧 DNAT 端口（仅 runc adapter 场景下非零，供排障使用）。
    /// 日常调用使用 <see cref="CdpUrl"/> 即可。
    /// </summary>
    [JsonPropertyName("host_port")]
    public int HostPort { get; set; }

    /// <summary>
    /// 浏览器 profile 目录（旧字段，新版后端响应不再包含）。
    /// 保留以维持向后兼容。
    /// </summary>
    [JsonPropertyName("profile_dir")]
    public string? ProfileDir { get; set; }
}
